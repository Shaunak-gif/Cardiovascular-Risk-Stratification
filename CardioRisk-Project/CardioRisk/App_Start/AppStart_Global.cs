// =============================================
// CardioRisk / App_Start / RouteConfig.cs
// =============================================

using System.Web.Mvc;
using System.Web.Routing;

namespace CardioRisk
{
    public class RouteConfig
    {
        public static void RegisterRoutes(RouteCollection routes)
        {
            routes.IgnoreRoute("{resource}.axd/{*pathInfo}");

            // Custom route: Patient assessment
            routes.MapRoute(
                name: "PatientAssess",
                url: "Prediction/Assess/{id}",
                defaults: new { controller = "Prediction", action = "Assess", id = UrlParameter.Optional }
            );

            // Custom route: Patient history
            routes.MapRoute(
                name: "PatientHistory",
                url: "Patient/History/{id}",
                defaults: new { controller = "Patient", action = "History", id = UrlParameter.Optional }
            );

            // Default route
            routes.MapRoute(
                name: "Default",
                url: "{controller}/{action}/{id}",
                defaults: new { controller = "Home", action = "Index", id = UrlParameter.Optional }
            );
        }
    }
}


// =============================================
// CardioRisk / Global.asax.cs
// =============================================

using System;
using System.Web.Mvc;
using System.Web.Routing;

namespace CardioRisk
{
    public class MvcApplication : System.Web.HttpApplication
    {
        protected void Application_Start()
        {
            AreaRegistration.RegisterAllAreas();
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);

            // Train Naive Bayes model on startup and cache it
            ModelTrainer.InitializeModel();
        }

        protected void Application_Error(object sender, EventArgs e)
        {
            var ex = Server.GetLastError();
            System.Diagnostics.Debug.WriteLine($"[CardioRisk Error] {ex?.Message}");
        }
    }
}


// =============================================
// CardioRisk / App_Start / FilterConfig.cs
// =============================================

using System.Web.Mvc;

namespace CardioRisk
{
    public class FilterConfig
    {
        public static void RegisterGlobalFilters(GlobalFilterCollection filters)
        {
            filters.Add(new HandleErrorAttribute());
        }
    }
}


// =============================================
// CardioRisk / ML / ModelTrainer.cs
// Singleton model trainer - trains once on startup
// and caches for fast repeated predictions
// =============================================

using System.Collections.Generic;
using System.Web;
using CardioRisk.DAL;
using CardioRisk.ML;
using CardioRisk.Models;

namespace CardioRisk
{
    public static class ModelTrainer
    {
        private static NaiveBayesClassifier _cachedClassifier;
        private static readonly object _lock = new object();

        public static NaiveBayesClassifier GetClassifier()
        {
            if (_cachedClassifier != null && _cachedClassifier.IsTrained)
                return _cachedClassifier;

            lock (_lock)
            {
                if (_cachedClassifier == null || !_cachedClassifier.IsTrained)
                    InitializeModel();
            }
            return _cachedClassifier;
        }

        public static void InitializeModel()
        {
            var repo = new ClinicalRepository();
            var trainingData = repo.GetTrainingData();
            var samples = ConvertToSamples(trainingData);

            _cachedClassifier = new NaiveBayesClassifier();
            _cachedClassifier.Train(samples);

            double accuracy = _cachedClassifier.CrossValidateAccuracy(samples, folds: 5);
            System.Diagnostics.Debug.WriteLine($"[CardioRisk] Naive Bayes model trained. 5-Fold CV Accuracy: {accuracy:P1}");
        }

        private static List<ClinicalSample> ConvertToSamples(List<TrainingDataModel> data)
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
