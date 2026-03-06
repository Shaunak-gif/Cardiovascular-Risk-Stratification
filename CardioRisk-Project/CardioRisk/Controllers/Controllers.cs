// =============================================
// CardioRisk / Controllers / HomeController.cs
// =============================================

using System.Web.Mvc;
using System.Data.SqlClient;
using CardioRisk.DAL;
using CardioRisk.Models;

namespace CardioRisk.Controllers
{
    public class HomeController : Controller
    {
        public ActionResult Index()
        {
            return View();
        }

        public ActionResult Dashboard()
        {
            var dt = DatabaseHelper.ExecuteStoredProcedure("sp_GetDashboardStats");
            var model = new DashboardViewModel();

            if (dt.Rows.Count > 0)
            {
                var row = dt.Rows[0];
                model.TotalPatients    = System.Convert.ToInt32(row["TotalPatients"]);
                model.LowRiskCount     = System.Convert.ToInt32(row["LowRiskCount"]);
                model.MediumRiskCount  = System.Convert.ToInt32(row["MediumRiskCount"]);
                model.HighRiskCount    = System.Convert.ToInt32(row["HighRiskCount"]);
                model.CriticalRiskCount = System.Convert.ToInt32(row["CriticalRiskCount"]);
            }

            // Recent 10 assessments
            var recentDt = DatabaseHelper.ExecuteQuery(@"
                SELECT TOP 10
                    p.PatientID,
                    p.FirstName + ' ' + p.LastName AS PatientName,
                    rp.PredictedRiskClass,
                    rp.ConfidenceScore,
                    cp.AssessmentDate
                FROM dbo.RiskPredictions rp
                INNER JOIN dbo.ClinicalParameters cp ON cp.ParameterID = rp.ParameterID
                INNER JOIN dbo.Patients p ON p.PatientID = rp.PatientID
                ORDER BY cp.AssessmentDate DESC");

            model.RecentAssessments = new System.Collections.Generic.List<PatientRiskSummary>();
            foreach (System.Data.DataRow row in recentDt.Rows)
            {
                model.RecentAssessments.Add(new PatientRiskSummary
                {
                    PatientID      = System.Convert.ToInt32(row["PatientID"]),
                    PatientName    = row["PatientName"].ToString(),
                    RiskClass      = row["PredictedRiskClass"].ToString(),
                    ConfidenceScore = System.Convert.ToDouble(row["ConfidenceScore"]),
                    AssessmentDate = System.Convert.ToDateTime(row["AssessmentDate"])
                });
            }

            return View(model);
        }
    }
}


// =============================================
// CardioRisk / Controllers / PatientController.cs
// =============================================

using System;
using System.Web.Mvc;
using CardioRisk.DAL;
using CardioRisk.Models;

namespace CardioRisk.Controllers
{
    public class PatientController : Controller
    {
        private readonly PatientRepository _patientRepo = new PatientRepository();
        private readonly ClinicalRepository _clinicalRepo = new ClinicalRepository();

        // GET: Patient list
        public ActionResult Index(string search = "")
        {
            var patients = string.IsNullOrWhiteSpace(search)
                ? _patientRepo.GetAllPatients()
                : _patientRepo.SearchPatients(search);

            ViewBag.Search = search;
            return View(patients);
        }

        // GET: Register form
        [HttpGet]
        public ActionResult Register()
        {
            return View(new PatientModel());
        }

        // POST: Register new patient
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Register(PatientModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            try
            {
                int newID = _patientRepo.AddPatient(model);
                TempData["Success"] = $"Patient registered successfully. ID: {newID}";
                return RedirectToAction("Profile", new { id = newID });
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Error saving patient: " + ex.Message);
                return View(model);
            }
        }

        // GET: Patient profile
        public ActionResult Profile(int id)
        {
            var patient = _patientRepo.GetPatientByID(id);
            if (patient == null)
                return HttpNotFound();

            var history = _clinicalRepo.GetPatientHistory(id);
            ViewBag.History = history;
            return View(patient);
        }

        // GET: Patient assessment history
        public ActionResult History(int id)
        {
            var patient = _patientRepo.GetPatientByID(id);
            if (patient == null) return HttpNotFound();

            var history = _clinicalRepo.GetPatientHistory(id);
            ViewBag.Patient = patient;
            return View(history);
        }
    }
}


// =============================================
// CardioRisk / Controllers / PredictionController.cs
// =============================================

using System;
using System.Collections.Generic;
using System.Web.Mvc;
using CardioRisk.DAL;
using CardioRisk.ML;
using CardioRisk.Models;

namespace CardioRisk.Controllers
{
    public class PredictionController : Controller
    {
        private readonly PatientRepository  _patientRepo  = new PatientRepository();
        private readonly ClinicalRepository _clinicalRepo = new ClinicalRepository();

        // GET: Assessment input form
        [HttpGet]
        public ActionResult Assess(int id)
        {
            var patient = _patientRepo.GetPatientByID(id);
            if (patient == null) return HttpNotFound();

            var model = new ClinicalParametersModel
            {
                PatientID = id,
                Age       = patient.Age
            };

            ViewBag.Patient = patient;
            return View(model);
        }

        // POST: Run Naive Bayes prediction
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Assess(ClinicalParametersModel model)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.Patient = _patientRepo.GetPatientByID(model.PatientID);
                return View(model);
            }

            // 1. Save clinical parameters
            int paramID = _clinicalRepo.SaveClinicalParameters(model);

            // 2. Load training data and train the classifier
            var trainingData   = _clinicalRepo.GetTrainingData();
            var classifier     = new NaiveBayesClassifier();
            var trainingSamples = ConvertToSamples(trainingData);
            classifier.Train(trainingSamples);

            // 3. Build prediction sample from input
            var sample = new ClinicalSample
            {
                Age               = model.Age,
                SystolicBP        = model.SystolicBP,
                DiastolicBP       = model.DiastolicBP,
                CholesterolTotal  = model.CholesterolTotal,
                BMI               = model.BMI,
                BloodGlucose      = model.BloodGlucose,
                SmokingStatus     = FeatureEncoder.EncodeSmokingStatus(model.SmokingStatus),
                HasDiabetes       = model.HasDiabetes ? 1 : 0,
                FamilyHistory     = model.FamilyHistory ? 1 : 0,
                ExerciseFrequency = FeatureEncoder.EncodeExerciseFrequency(model.ExerciseFrequency)
            };

            // 4. Predict
            var probabilities = classifier.PredictProbabilities(sample);
            var predictedClass = classifier.Predict(sample);
            double confidence  = probabilities[predictedClass];

            // 5. Save prediction
            var prediction = new RiskPredictionModel
            {
                ParameterID       = paramID,
                PatientID         = model.PatientID,
                PredictedRiskClass = FeatureEncoder.RiskClassToString(predictedClass),
                ProbLow           = probabilities[RiskClass.Low],
                ProbMedium        = probabilities[RiskClass.Medium],
                ProbHigh          = probabilities[RiskClass.High],
                ProbCritical      = probabilities[RiskClass.Critical],
                ConfidenceScore   = confidence
            };

            int predictionID = _clinicalRepo.SavePrediction(prediction);

            return RedirectToAction("Result", new { id = predictionID });
        }

        // GET: Risk result page
        public ActionResult Result(int id)
        {
            // Load prediction + patient + parameters
            var dt = DatabaseHelper.ExecuteQuery(@"
                SELECT rp.*, cp.*, p.FirstName, p.LastName, p.DateOfBirth, p.Gender, p.Email
                FROM dbo.RiskPredictions rp
                INNER JOIN dbo.ClinicalParameters cp ON cp.ParameterID = rp.ParameterID
                INNER JOIN dbo.Patients p ON p.PatientID = rp.PatientID
                WHERE rp.PredictionID = @PredictionID",
                new[] { new System.Data.SqlClient.SqlParameter("@PredictionID", id) });

            if (dt.Rows.Count == 0) return HttpNotFound();

            var row = dt.Rows[0];
            var viewModel = new RiskResultViewModel
            {
                Patient = new PatientModel
                {
                    PatientID   = Convert.ToInt32(row["PatientID"]),
                    FirstName   = row["FirstName"].ToString(),
                    LastName    = row["LastName"].ToString(),
                    DateOfBirth = Convert.ToDateTime(row["DateOfBirth"]),
                    Gender      = row["Gender"].ToString(),
                    Email       = row["Email"] == DBNull.Value ? "" : row["Email"].ToString()
                },
                Parameters = new ClinicalParametersModel
                {
                    SystolicBP       = Convert.ToDouble(row["SystolicBP"]),
                    DiastolicBP      = Convert.ToDouble(row["DiastolicBP"]),
                    CholesterolTotal = Convert.ToDouble(row["CholesterolTotal"]),
                    BMI              = Convert.ToDouble(row["BMI"]),
                    BloodGlucose     = Convert.ToDouble(row["BloodGlucose"]),
                    SmokingStatus    = row["SmokingStatus"].ToString(),
                    HasDiabetes      = Convert.ToBoolean(row["HasDiabetes"]),
                    FamilyHistory    = Convert.ToBoolean(row["FamilyHistory"]),
                    ExerciseFrequency = row["ExerciseFrequency"].ToString()
                },
                Prediction = new RiskPredictionModel
                {
                    PredictionID       = id,
                    PredictedRiskClass = row["PredictedRiskClass"].ToString(),
                    ProbLow            = Convert.ToDouble(row["ProbLow"]),
                    ProbMedium         = Convert.ToDouble(row["ProbMedium"]),
                    ProbHigh           = Convert.ToDouble(row["ProbHigh"]),
                    ProbCritical       = Convert.ToDouble(row["ProbCritical"]),
                    ConfidenceScore    = Convert.ToDouble(row["ConfidenceScore"])
                },
                RiskFactors     = BuildRiskFactors(row),
                Recommendations = BuildRecommendations(row["PredictedRiskClass"].ToString(), row)
            };

            return View(viewModel);
        }

        // ── Build risk factor list
        private List<string> BuildRiskFactors(System.Data.DataRow row)
        {
            var factors = new List<string>();
            if (Convert.ToDouble(row["SystolicBP"]) >= 140)
                factors.Add("Hypertension (Systolic BP ≥ 140 mmHg)");
            if (Convert.ToDouble(row["CholesterolTotal"]) >= 240)
                factors.Add("High Total Cholesterol (≥ 240 mg/dL)");
            if (Convert.ToDouble(row["BMI"]) >= 30)
                factors.Add("Obesity (BMI ≥ 30)");
            if (Convert.ToDouble(row["BloodGlucose"]) >= 126)
                factors.Add("Elevated Fasting Glucose (≥ 126 mg/dL – Diabetic range)");
            if (row["SmokingStatus"].ToString() == "Current")
                factors.Add("Current smoker");
            if (Convert.ToBoolean(row["HasDiabetes"]))
                factors.Add("Diagnosed Diabetes Mellitus");
            if (Convert.ToBoolean(row["FamilyHistory"]))
                factors.Add("Family history of cardiovascular disease");
            if (row["ExerciseFrequency"].ToString() == "None")
                factors.Add("Sedentary lifestyle (no exercise)");
            if (factors.Count == 0)
                factors.Add("No major individual risk factors identified");
            return factors;
        }

        // ── Build recommendations based on risk tier
        private List<string> BuildRecommendations(string riskClass, System.Data.DataRow row)
        {
            var recs = new List<string>();
            switch (riskClass)
            {
                case "Low":
                    recs.Add("Maintain current healthy lifestyle");
                    recs.Add("Annual cardiovascular check-up recommended");
                    recs.Add("Continue regular physical activity");
                    break;
                case "Medium":
                    recs.Add("Dietary modification: reduce saturated fats and sodium");
                    recs.Add("Increase physical activity to at least 150 min/week");
                    recs.Add("Monitor blood pressure monthly");
                    recs.Add("Follow-up in 6 months");
                    break;
                case "High":
                    recs.Add("Immediate clinical evaluation recommended");
                    recs.Add("Medication review and possible pharmacotherapy initiation");
                    recs.Add("Strict blood pressure and lipid monitoring (monthly)");
                    recs.Add("Lifestyle intervention program");
                    recs.Add("Cardiology referral advised");
                    break;
                case "Critical":
                    recs.Add("⚠️ URGENT: Refer to cardiologist immediately");
                    recs.Add("Hospitalization or intensive monitoring may be required");
                    recs.Add("Comprehensive cardiac evaluation (ECG, echo, stress test)");
                    recs.Add("Immediate pharmacological intervention");
                    recs.Add("Daily BP and glucose monitoring");
                    break;
            }
            return recs;
        }

        // ── Convert DB training data to ML samples
        private List<ClinicalSample> ConvertToSamples(List<TrainingDataModel> data)
        {
            var samples = new List<ClinicalSample>();
            foreach (var d in data)
            {
                samples.Add(new ClinicalSample
                {
                    Age               = d.Age,
                    SystolicBP        = d.SystolicBP,
                    DiastolicBP       = d.DiastolicBP,
                    CholesterolTotal  = d.CholesterolTotal,
                    BMI               = d.BMI,
                    BloodGlucose      = d.BloodGlucose,
                    SmokingStatus     = FeatureEncoder.EncodeSmokingStatus(d.SmokingStatus),
                    HasDiabetes       = d.HasDiabetes ? 1 : 0,
                    FamilyHistory     = d.FamilyHistory ? 1 : 0,
                    ExerciseFrequency = FeatureEncoder.EncodeExerciseFrequency(d.ExerciseFrequency),
                    Label             = FeatureEncoder.StringToRiskClass(d.RiskClass)
                });
            }
            return samples;
        }
    }
}
