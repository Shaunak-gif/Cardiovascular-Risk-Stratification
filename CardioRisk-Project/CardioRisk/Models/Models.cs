// =============================================
// CardioRisk / Models / PatientModel.cs
// =============================================

using System;
using System.ComponentModel.DataAnnotations;

namespace CardioRisk.Models
{
    public class PatientModel
    {
        public int PatientID { get; set; }

        [Required(ErrorMessage = "First name is required")]
        [Display(Name = "First Name")]
        public string FirstName { get; set; }

        [Required(ErrorMessage = "Last name is required")]
        [Display(Name = "Last Name")]
        public string LastName { get; set; }

        [Required(ErrorMessage = "Date of birth is required")]
        [DataType(DataType.Date)]
        [Display(Name = "Date of Birth")]
        public DateTime DateOfBirth { get; set; }

        [Required]
        public string Gender { get; set; }

        [EmailAddress]
        public string Email { get; set; }

        public string Phone { get; set; }
        public string Address { get; set; }
        public DateTime CreatedAt { get; set; }

        public string FullName => $"{FirstName} {LastName}";

        public int Age => (int)((DateTime.Today - DateOfBirth).TotalDays / 365.25);
    }
}


// =============================================
// CardioRisk / Models / ClinicalParametersModel.cs
// =============================================
namespace CardioRisk.Models
{
    using System.ComponentModel.DataAnnotations;

    public class ClinicalParametersModel
    {
        public int ParameterID { get; set; }

        [Required]
        public int PatientID { get; set; }

        [Required]
        [Range(1, 120, ErrorMessage = "Age must be 1-120")]
        public int Age { get; set; }

        [Required]
        [Range(60, 250, ErrorMessage = "Systolic BP must be 60-250 mmHg")]
        [Display(Name = "Systolic Blood Pressure (mmHg)")]
        public double SystolicBP { get; set; }

        [Required]
        [Range(40, 150, ErrorMessage = "Diastolic BP must be 40-150 mmHg")]
        [Display(Name = "Diastolic Blood Pressure (mmHg)")]
        public double DiastolicBP { get; set; }

        [Required]
        [Range(100, 400, ErrorMessage = "Total Cholesterol must be 100-400 mg/dL")]
        [Display(Name = "Total Cholesterol (mg/dL)")]
        public double CholesterolTotal { get; set; }

        [Range(100, 200)]
        [Display(Name = "HDL Cholesterol (mg/dL)")]
        public double? HDLCholesterol { get; set; }

        [Range(50, 250)]
        [Display(Name = "LDL Cholesterol (mg/dL)")]
        public double? LDLCholesterol { get; set; }

        [Required]
        [Range(10.0, 60.0, ErrorMessage = "BMI must be 10-60")]
        [Display(Name = "BMI (kg/m²)")]
        public double BMI { get; set; }

        [Required]
        [Range(50, 500, ErrorMessage = "Fasting Blood Glucose must be 50-500 mg/dL")]
        [Display(Name = "Fasting Blood Glucose (mg/dL)")]
        public double BloodGlucose { get; set; }

        [Range(30, 250)]
        [Display(Name = "Heart Rate (bpm)")]
        public int? HeartRate { get; set; }

        [Required]
        [Display(Name = "Smoking Status")]
        public string SmokingStatus { get; set; }

        [Required]
        [Display(Name = "Has Diabetes")]
        public bool HasDiabetes { get; set; }

        [Required]
        [Display(Name = "Family History of CVD")]
        public bool FamilyHistory { get; set; }

        [Required]
        [Display(Name = "Exercise Frequency")]
        public string ExerciseFrequency { get; set; }

        [Display(Name = "Stress Level")]
        public string StressLevel { get; set; }

        [Display(Name = "Alcohol Use")]
        public string AlcoholUse { get; set; }

        public string Notes { get; set; }

        public System.DateTime AssessmentDate { get; set; }
    }
}


// =============================================
// CardioRisk / Models / RiskPredictionModel.cs
// =============================================
namespace CardioRisk.Models
{
    public class RiskPredictionModel
    {
        public int PredictionID         { get; set; }
        public int ParameterID          { get; set; }
        public int PatientID            { get; set; }
        public string PredictedRiskClass { get; set; }
        public double ProbLow           { get; set; }
        public double ProbMedium        { get; set; }
        public double ProbHigh          { get; set; }
        public double ProbCritical      { get; set; }
        public double ConfidenceScore   { get; set; }
        public System.DateTime CreatedAt { get; set; }

        // Derived — for display
        public string RiskColor => PredictedRiskClass switch
        {
            "Low"      => "success",
            "Medium"   => "warning",
            "High"     => "danger",
            "Critical" => "dark",
            _          => "secondary"
        };

        public string RiskIcon => PredictedRiskClass switch
        {
            "Low"      => "✅",
            "Medium"   => "⚠️",
            "High"     => "🔴",
            "Critical" => "☠️",
            _          => "❓"
        };
    }

    public class RiskResultViewModel
    {
        public PatientModel Patient            { get; set; }
        public ClinicalParametersModel Parameters { get; set; }
        public RiskPredictionModel Prediction  { get; set; }
        public System.Collections.Generic.List<string> RiskFactors { get; set; }
        public System.Collections.Generic.List<string> Recommendations { get; set; }
    }
}


// =============================================
// CardioRisk / Models / TrainingDataModel.cs
// =============================================
namespace CardioRisk.Models
{
    public class TrainingDataModel
    {
        public int TrainingID           { get; set; }
        public int Age                  { get; set; }
        public double SystolicBP        { get; set; }
        public double DiastolicBP       { get; set; }
        public double CholesterolTotal  { get; set; }
        public double BMI               { get; set; }
        public double BloodGlucose      { get; set; }
        public string SmokingStatus     { get; set; }
        public bool HasDiabetes         { get; set; }
        public bool FamilyHistory       { get; set; }
        public string ExerciseFrequency { get; set; }
        public string RiskClass         { get; set; }
    }
}


// =============================================
// CardioRisk / Models / DashboardViewModel.cs
// =============================================
namespace CardioRisk.Models
{
    public class DashboardViewModel
    {
        public int TotalPatients      { get; set; }
        public int LowRiskCount       { get; set; }
        public int MediumRiskCount    { get; set; }
        public int HighRiskCount      { get; set; }
        public int CriticalRiskCount  { get; set; }

        public System.Collections.Generic.List<PatientRiskSummary> RecentAssessments { get; set; }
    }

    public class PatientRiskSummary
    {
        public int PatientID            { get; set; }
        public string PatientName       { get; set; }
        public string RiskClass         { get; set; }
        public double ConfidenceScore   { get; set; }
        public System.DateTime AssessmentDate { get; set; }
    }
}
