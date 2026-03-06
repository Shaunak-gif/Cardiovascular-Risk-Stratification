// =============================================
// CardioRisk / DAL / DatabaseHelper.cs
// ADO.NET connection and query utilities
// =============================================

using System;
using System.Data;
using System.Data.SqlClient;
using System.Configuration;

namespace CardioRisk.DAL
{
    public static class DatabaseHelper
    {
        private static string ConnectionString =>
            ConfigurationManager.ConnectionStrings["CardioRiskDB"].ConnectionString;

        public static SqlConnection GetConnection()
        {
            return new SqlConnection(ConnectionString);
        }

        public static int ExecuteNonQuery(string sql, SqlParameter[] parameters = null)
        {
            using (var conn = GetConnection())
            using (var cmd = new SqlCommand(sql, conn))
            {
                cmd.CommandType = CommandType.Text;
                if (parameters != null) cmd.Parameters.AddRange(parameters);
                conn.Open();
                return cmd.ExecuteNonQuery();
            }
        }

        public static object ExecuteScalar(string sql, SqlParameter[] parameters = null)
        {
            using (var conn = GetConnection())
            using (var cmd = new SqlCommand(sql, conn))
            {
                cmd.CommandType = CommandType.Text;
                if (parameters != null) cmd.Parameters.AddRange(parameters);
                conn.Open();
                return cmd.ExecuteScalar();
            }
        }

        public static DataTable ExecuteQuery(string sql, SqlParameter[] parameters = null)
        {
            using (var conn = GetConnection())
            using (var cmd = new SqlCommand(sql, conn))
            using (var adapter = new SqlDataAdapter(cmd))
            {
                cmd.CommandType = CommandType.Text;
                if (parameters != null) cmd.Parameters.AddRange(parameters);
                var dt = new DataTable();
                adapter.Fill(dt);
                return dt;
            }
        }

        public static DataTable ExecuteStoredProcedure(string procName, SqlParameter[] parameters = null)
        {
            using (var conn = GetConnection())
            using (var cmd = new SqlCommand(procName, conn))
            using (var adapter = new SqlDataAdapter(cmd))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                if (parameters != null) cmd.Parameters.AddRange(parameters);
                var dt = new DataTable();
                adapter.Fill(dt);
                return dt;
            }
        }
    }
}


// =============================================
// CardioRisk / DAL / PatientRepository.cs
// =============================================

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using CardioRisk.Models;

namespace CardioRisk.DAL
{
    public class PatientRepository
    {
        // ── INSERT patient
        public int AddPatient(PatientModel patient)
        {
            string sql = @"
                INSERT INTO dbo.Patients (FirstName, LastName, DateOfBirth, Gender, Email, Phone, Address)
                VALUES (@FirstName, @LastName, @DateOfBirth, @Gender, @Email, @Phone, @Address);
                SELECT SCOPE_IDENTITY();";

            var parameters = new[]
            {
                new SqlParameter("@FirstName",   patient.FirstName),
                new SqlParameter("@LastName",    patient.LastName),
                new SqlParameter("@DateOfBirth", patient.DateOfBirth),
                new SqlParameter("@Gender",      patient.Gender),
                new SqlParameter("@Email",       (object)patient.Email  ?? DBNull.Value),
                new SqlParameter("@Phone",       (object)patient.Phone  ?? DBNull.Value),
                new SqlParameter("@Address",     (object)patient.Address ?? DBNull.Value)
            };

            var result = DatabaseHelper.ExecuteScalar(sql, parameters);
            return Convert.ToInt32(result);
        }

        // ── GET patient by ID
        public PatientModel GetPatientByID(int id)
        {
            string sql = "SELECT * FROM dbo.Patients WHERE PatientID = @PatientID";
            var dt = DatabaseHelper.ExecuteQuery(sql, new[]
            {
                new SqlParameter("@PatientID", id)
            });

            if (dt.Rows.Count == 0) return null;
            return MapPatient(dt.Rows[0]);
        }

        // ── GET all patients
        public List<PatientModel> GetAllPatients()
        {
            string sql = "SELECT * FROM dbo.Patients ORDER BY CreatedAt DESC";
            var dt = DatabaseHelper.ExecuteQuery(sql);
            var list = new List<PatientModel>();
            foreach (DataRow row in dt.Rows)
                list.Add(MapPatient(row));
            return list;
        }

        // ── SEARCH patients
        public List<PatientModel> SearchPatients(string term)
        {
            string sql = @"
                SELECT * FROM dbo.Patients
                WHERE FirstName LIKE @Term OR LastName LIKE @Term OR Email LIKE @Term
                ORDER BY FirstName";

            var dt = DatabaseHelper.ExecuteQuery(sql, new[]
            {
                new SqlParameter("@Term", $"%{term}%")
            });

            var list = new List<PatientModel>();
            foreach (DataRow row in dt.Rows)
                list.Add(MapPatient(row));
            return list;
        }

        private PatientModel MapPatient(DataRow row)
        {
            return new PatientModel
            {
                PatientID   = Convert.ToInt32(row["PatientID"]),
                FirstName   = row["FirstName"].ToString(),
                LastName    = row["LastName"].ToString(),
                DateOfBirth = Convert.ToDateTime(row["DateOfBirth"]),
                Gender      = row["Gender"].ToString(),
                Email       = row["Email"] == DBNull.Value ? null : row["Email"].ToString(),
                Phone       = row["Phone"] == DBNull.Value ? null : row["Phone"].ToString(),
                Address     = row["Address"] == DBNull.Value ? null : row["Address"].ToString(),
                CreatedAt   = Convert.ToDateTime(row["CreatedAt"])
            };
        }
    }


    // =============================================
    // CardioRisk / DAL / ClinicalRepository.cs
    // =============================================
    public class ClinicalRepository
    {
        // ── INSERT clinical parameters, return new ParameterID
        public int SaveClinicalParameters(ClinicalParametersModel model)
        {
            string sql = @"
                INSERT INTO dbo.ClinicalParameters
                    (PatientID, Age, SystolicBP, DiastolicBP, CholesterolTotal, HDLCholesterol,
                     LDLCholesterol, BMI, BloodGlucose, HeartRate, SmokingStatus, HasDiabetes,
                     FamilyHistory, ExerciseFrequency, StressLevel, AlcoholUse, Notes)
                VALUES
                    (@PatientID, @Age, @SystolicBP, @DiastolicBP, @CholesterolTotal, @HDLCholesterol,
                     @LDLCholesterol, @BMI, @BloodGlucose, @HeartRate, @SmokingStatus, @HasDiabetes,
                     @FamilyHistory, @ExerciseFrequency, @StressLevel, @AlcoholUse, @Notes);
                SELECT SCOPE_IDENTITY();";

            var p = new[]
            {
                new SqlParameter("@PatientID",         model.PatientID),
                new SqlParameter("@Age",               model.Age),
                new SqlParameter("@SystolicBP",        model.SystolicBP),
                new SqlParameter("@DiastolicBP",       model.DiastolicBP),
                new SqlParameter("@CholesterolTotal",  model.CholesterolTotal),
                new SqlParameter("@HDLCholesterol",    (object)model.HDLCholesterol ?? DBNull.Value),
                new SqlParameter("@LDLCholesterol",    (object)model.LDLCholesterol ?? DBNull.Value),
                new SqlParameter("@BMI",               model.BMI),
                new SqlParameter("@BloodGlucose",      model.BloodGlucose),
                new SqlParameter("@HeartRate",         (object)model.HeartRate ?? DBNull.Value),
                new SqlParameter("@SmokingStatus",     model.SmokingStatus),
                new SqlParameter("@HasDiabetes",       model.HasDiabetes),
                new SqlParameter("@FamilyHistory",     model.FamilyHistory),
                new SqlParameter("@ExerciseFrequency", model.ExerciseFrequency),
                new SqlParameter("@StressLevel",       (object)model.StressLevel ?? DBNull.Value),
                new SqlParameter("@AlcoholUse",        (object)model.AlcoholUse ?? DBNull.Value),
                new SqlParameter("@Notes",             (object)model.Notes ?? DBNull.Value)
            };

            return Convert.ToInt32(DatabaseHelper.ExecuteScalar(sql, p));
        }

        // ── INSERT prediction result
        public int SavePrediction(RiskPredictionModel prediction)
        {
            string sql = @"
                INSERT INTO dbo.RiskPredictions
                    (ParameterID, PatientID, PredictedRiskClass, ProbLow, ProbMedium, ProbHigh, ProbCritical, ConfidenceScore)
                VALUES
                    (@ParameterID, @PatientID, @PredictedRiskClass, @ProbLow, @ProbMedium, @ProbHigh, @ProbCritical, @ConfidenceScore);
                SELECT SCOPE_IDENTITY();";

            var p = new[]
            {
                new SqlParameter("@ParameterID",        prediction.ParameterID),
                new SqlParameter("@PatientID",          prediction.PatientID),
                new SqlParameter("@PredictedRiskClass", prediction.PredictedRiskClass),
                new SqlParameter("@ProbLow",            prediction.ProbLow),
                new SqlParameter("@ProbMedium",         prediction.ProbMedium),
                new SqlParameter("@ProbHigh",           prediction.ProbHigh),
                new SqlParameter("@ProbCritical",       prediction.ProbCritical),
                new SqlParameter("@ConfidenceScore",    prediction.ConfidenceScore)
            };

            return Convert.ToInt32(DatabaseHelper.ExecuteScalar(sql, p));
        }

        // ── GET all training data
        public List<TrainingDataModel> GetTrainingData()
        {
            string sql = "SELECT * FROM dbo.TrainingData";
            var dt = DatabaseHelper.ExecuteQuery(sql);
            var list = new List<TrainingDataModel>();
            foreach (DataRow row in dt.Rows)
            {
                list.Add(new TrainingDataModel
                {
                    TrainingID         = Convert.ToInt32(row["TrainingID"]),
                    Age                = Convert.ToInt32(row["Age"]),
                    SystolicBP         = Convert.ToDouble(row["SystolicBP"]),
                    DiastolicBP        = Convert.ToDouble(row["DiastolicBP"]),
                    CholesterolTotal   = Convert.ToDouble(row["CholesterolTotal"]),
                    BMI                = Convert.ToDouble(row["BMI"]),
                    BloodGlucose       = Convert.ToDouble(row["BloodGlucose"]),
                    SmokingStatus      = row["SmokingStatus"].ToString(),
                    HasDiabetes        = Convert.ToBoolean(row["HasDiabetes"]),
                    FamilyHistory      = Convert.ToBoolean(row["FamilyHistory"]),
                    ExerciseFrequency  = row["ExerciseFrequency"].ToString(),
                    RiskClass          = row["RiskClass"].ToString()
                });
            }
            return list;
        }

        // ── GET patient prediction history
        public List<RiskPredictionModel> GetPatientHistory(int patientID)
        {
            var dt = DatabaseHelper.ExecuteStoredProcedure("sp_GetPatientHistory", new[]
            {
                new SqlParameter("@PatientID", patientID)
            });

            var list = new List<RiskPredictionModel>();
            foreach (DataRow row in dt.Rows)
            {
                list.Add(new RiskPredictionModel
                {
                    ParameterID       = Convert.ToInt32(row["ParameterID"]),
                    PredictedRiskClass = row["PredictedRiskClass"].ToString(),
                    ConfidenceScore   = Convert.ToDouble(row["ConfidenceScore"]),
                    ProbLow           = Convert.ToDouble(row["ProbLow"]),
                    ProbMedium        = Convert.ToDouble(row["ProbMedium"]),
                    ProbHigh          = Convert.ToDouble(row["ProbHigh"]),
                    ProbCritical      = Convert.ToDouble(row["ProbCritical"]),
                    CreatedAt         = Convert.ToDateTime(row["AssessmentDate"])
                });
            }
            return list;
        }
    }
}
