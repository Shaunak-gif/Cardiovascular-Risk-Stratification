-- =============================================
-- Cardiovascular Risk Stratification Framework
-- SQL Server Database Schema
-- =============================================

USE master;
GO

IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = N'CardioRiskDB')
    CREATE DATABASE CardioRiskDB;
GO

USE CardioRiskDB;
GO

-- =============================================
-- TABLE: Patients
-- =============================================
IF OBJECT_ID('dbo.Patients', 'U') IS NOT NULL DROP TABLE dbo.Patients;
GO

CREATE TABLE dbo.Patients (
    PatientID       INT IDENTITY(1,1) PRIMARY KEY,
    FirstName       NVARCHAR(100)   NOT NULL,
    LastName        NVARCHAR(100)   NOT NULL,
    DateOfBirth     DATE            NOT NULL,
    Gender          NVARCHAR(10)    NOT NULL CHECK (Gender IN ('Male','Female','Other')),
    Email           NVARCHAR(200)   UNIQUE,
    Phone           NVARCHAR(20),
    Address         NVARCHAR(500),
    CreatedAt       DATETIME        NOT NULL DEFAULT GETDATE(),
    UpdatedAt       DATETIME        NOT NULL DEFAULT GETDATE()
);
GO

-- =============================================
-- TABLE: ClinicalParameters
-- Stores each assessment's vital/lab values
-- =============================================
IF OBJECT_ID('dbo.ClinicalParameters', 'U') IS NOT NULL DROP TABLE dbo.ClinicalParameters;
GO

CREATE TABLE dbo.ClinicalParameters (
    ParameterID         INT IDENTITY(1,1) PRIMARY KEY,
    PatientID           INT             NOT NULL REFERENCES dbo.Patients(PatientID),
    AssessmentDate      DATETIME        NOT NULL DEFAULT GETDATE(),
    Age                 INT             NOT NULL,
    SystolicBP          FLOAT           NOT NULL,   -- mmHg
    DiastolicBP         FLOAT           NOT NULL,   -- mmHg
    CholesterolTotal    FLOAT           NOT NULL,   -- mg/dL
    HDLCholesterol      FLOAT,                      -- mg/dL
    LDLCholesterol      FLOAT,                      -- mg/dL
    BMI                 FLOAT           NOT NULL,
    BloodGlucose        FLOAT           NOT NULL,   -- mg/dL (fasting)
    HeartRate           INT,                        -- bpm
    SmokingStatus       NVARCHAR(20)    NOT NULL CHECK (SmokingStatus IN ('Never','Former','Current')),
    HasDiabetes         BIT             NOT NULL DEFAULT 0,
    FamilyHistory       BIT             NOT NULL DEFAULT 0,
    ExerciseFrequency   NVARCHAR(20)    NOT NULL CHECK (ExerciseFrequency IN ('None','Low','Moderate','High')),
    StressLevel         NVARCHAR(10)    CHECK (StressLevel IN ('Low','Medium','High')),
    AlcoholUse          NVARCHAR(20)    CHECK (AlcoholUse IN ('None','Occasional','Regular','Heavy')),
    Notes               NVARCHAR(1000)
);
GO

-- =============================================
-- TABLE: RiskPredictions
-- Stores ML output for each assessment
-- =============================================
IF OBJECT_ID('dbo.RiskPredictions', 'U') IS NOT NULL DROP TABLE dbo.RiskPredictions;
GO

CREATE TABLE dbo.RiskPredictions (
    PredictionID        INT IDENTITY(1,1) PRIMARY KEY,
    ParameterID         INT             NOT NULL REFERENCES dbo.ClinicalParameters(ParameterID),
    PatientID           INT             NOT NULL REFERENCES dbo.Patients(PatientID),
    PredictedRiskClass  NVARCHAR(20)    NOT NULL CHECK (PredictedRiskClass IN ('Low','Medium','High','Critical')),
    ProbLow             FLOAT           NOT NULL,
    ProbMedium          FLOAT           NOT NULL,
    ProbHigh            FLOAT           NOT NULL,
    ProbCritical        FLOAT           NOT NULL,
    ConfidenceScore     FLOAT           NOT NULL,   -- 0.0 - 1.0
    CreatedAt           DATETIME        NOT NULL DEFAULT GETDATE()
);
GO

-- =============================================
-- TABLE: RiskReports
-- Clinician-readable report per assessment
-- =============================================
IF OBJECT_ID('dbo.RiskReports', 'U') IS NOT NULL DROP TABLE dbo.RiskReports;
GO

CREATE TABLE dbo.RiskReports (
    ReportID            INT IDENTITY(1,1) PRIMARY KEY,
    PredictionID        INT             NOT NULL REFERENCES dbo.RiskPredictions(PredictionID),
    PatientID           INT             NOT NULL REFERENCES dbo.Patients(PatientID),
    RiskFactorsSummary  NVARCHAR(2000),
    Recommendations     NVARCHAR(2000),
    FollowUpDate        DATE,
    GeneratedAt         DATETIME        NOT NULL DEFAULT GETDATE()
);
GO

-- =============================================
-- TABLE: TrainingData
-- Historical labeled data for Naive Bayes
-- =============================================
IF OBJECT_ID('dbo.TrainingData', 'U') IS NOT NULL DROP TABLE dbo.TrainingData;
GO

CREATE TABLE dbo.TrainingData (
    TrainingID          INT IDENTITY(1,1) PRIMARY KEY,
    Age                 INT             NOT NULL,
    SystolicBP          FLOAT           NOT NULL,
    DiastolicBP         FLOAT           NOT NULL,
    CholesterolTotal    FLOAT           NOT NULL,
    BMI                 FLOAT           NOT NULL,
    BloodGlucose        FLOAT           NOT NULL,
    SmokingStatus       NVARCHAR(20)    NOT NULL,
    HasDiabetes         BIT             NOT NULL,
    FamilyHistory       BIT             NOT NULL,
    ExerciseFrequency   NVARCHAR(20)    NOT NULL,
    RiskClass           NVARCHAR(20)    NOT NULL
);
GO

-- =============================================
-- TABLE: Users (Login / Auth)
-- =============================================
IF OBJECT_ID('dbo.Users', 'U') IS NOT NULL DROP TABLE dbo.Users;
GO

CREATE TABLE dbo.Users (
    UserID          INT IDENTITY(1,1) PRIMARY KEY,
    Username        NVARCHAR(100)   NOT NULL UNIQUE,
    PasswordHash    NVARCHAR(256)   NOT NULL,
    Role            NVARCHAR(50)    NOT NULL CHECK (Role IN ('Admin','Doctor','Nurse','Viewer')),
    FullName        NVARCHAR(200),
    Email           NVARCHAR(200),
    IsActive        BIT             NOT NULL DEFAULT 1,
    CreatedAt       DATETIME        NOT NULL DEFAULT GETDATE()
);
GO

-- =============================================
-- INDEXES
-- =============================================
CREATE INDEX IX_ClinicalParameters_PatientID ON dbo.ClinicalParameters(PatientID);
CREATE INDEX IX_RiskPredictions_PatientID ON dbo.RiskPredictions(PatientID);
CREATE INDEX IX_RiskReports_PatientID ON dbo.RiskReports(PatientID);
GO

-- =============================================
-- STORED PROCEDURES
-- =============================================

-- SP: Get full patient profile with latest prediction
CREATE OR ALTER PROCEDURE dbo.sp_GetPatientProfile
    @PatientID INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        p.PatientID, p.FirstName, p.LastName, p.DateOfBirth, p.Gender,
        p.Email, p.Phone,
        cp.SystolicBP, cp.DiastolicBP, cp.CholesterolTotal,
        cp.BMI, cp.BloodGlucose, cp.SmokingStatus, cp.HasDiabetes,
        cp.FamilyHistory, cp.ExerciseFrequency, cp.AssessmentDate,
        rp.PredictedRiskClass, rp.ConfidenceScore
    FROM dbo.Patients p
    LEFT JOIN dbo.ClinicalParameters cp ON cp.PatientID = p.PatientID
        AND cp.ParameterID = (
            SELECT TOP 1 ParameterID FROM dbo.ClinicalParameters
            WHERE PatientID = p.PatientID ORDER BY AssessmentDate DESC
        )
    LEFT JOIN dbo.RiskPredictions rp ON rp.ParameterID = cp.ParameterID
    WHERE p.PatientID = @PatientID;
END;
GO

-- SP: Get all assessments for a patient
CREATE OR ALTER PROCEDURE dbo.sp_GetPatientHistory
    @PatientID INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        cp.ParameterID, cp.AssessmentDate,
        cp.SystolicBP, cp.DiastolicBP, cp.CholesterolTotal, cp.BMI, cp.BloodGlucose,
        rp.PredictedRiskClass, rp.ConfidenceScore,
        rp.ProbLow, rp.ProbMedium, rp.ProbHigh, rp.ProbCritical
    FROM dbo.ClinicalParameters cp
    INNER JOIN dbo.RiskPredictions rp ON rp.ParameterID = cp.ParameterID
    WHERE cp.PatientID = @PatientID
    ORDER BY cp.AssessmentDate DESC;
END;
GO

-- SP: Dashboard statistics
CREATE OR ALTER PROCEDURE dbo.sp_GetDashboardStats
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        COUNT(*) AS TotalPatients,
        SUM(CASE WHEN rp.PredictedRiskClass = 'Low'      THEN 1 ELSE 0 END) AS LowRiskCount,
        SUM(CASE WHEN rp.PredictedRiskClass = 'Medium'   THEN 1 ELSE 0 END) AS MediumRiskCount,
        SUM(CASE WHEN rp.PredictedRiskClass = 'High'     THEN 1 ELSE 0 END) AS HighRiskCount,
        SUM(CASE WHEN rp.PredictedRiskClass = 'Critical' THEN 1 ELSE 0 END) AS CriticalRiskCount
    FROM dbo.Patients p
    LEFT JOIN dbo.ClinicalParameters cp ON cp.PatientID = p.PatientID
        AND cp.ParameterID = (
            SELECT TOP 1 ParameterID FROM dbo.ClinicalParameters
            WHERE PatientID = p.PatientID ORDER BY AssessmentDate DESC
        )
    LEFT JOIN dbo.RiskPredictions rp ON rp.ParameterID = cp.ParameterID;
END;
GO

-- =============================================
-- SEED DATA: Training Dataset (50 records)
-- =============================================
INSERT INTO dbo.TrainingData
    (Age, SystolicBP, DiastolicBP, CholesterolTotal, BMI, BloodGlucose, SmokingStatus, HasDiabetes, FamilyHistory, ExerciseFrequency, RiskClass)
VALUES
-- Low Risk
(28, 110, 70, 170, 22.1, 85,  'Never',   0, 0, 'High',     'Low'),
(32, 115, 75, 180, 23.5, 88,  'Never',   0, 0, 'Moderate', 'Low'),
(25, 108, 68, 165, 21.0, 82,  'Never',   0, 0, 'High',     'Low'),
(35, 120, 78, 190, 24.0, 90,  'Former',  0, 0, 'Moderate', 'Low'),
(29, 112, 72, 175, 22.8, 86,  'Never',   0, 0, 'High',     'Low'),
(40, 118, 76, 195, 24.5, 92,  'Never',   0, 0, 'Moderate', 'Low'),
(27, 110, 70, 168, 21.5, 84,  'Never',   0, 0, 'High',     'Low'),
(33, 116, 74, 182, 23.2, 89,  'Never',   0, 0, 'High',     'Low'),
(38, 119, 77, 188, 23.8, 91,  'Former',  0, 0, 'Moderate', 'Low'),
(31, 113, 71, 172, 22.3, 87,  'Never',   0, 0, 'High',     'Low'),
-- Medium Risk
(45, 130, 85, 210, 27.0, 100, 'Former',  0, 0, 'Low',      'Medium'),
(50, 135, 88, 220, 28.0, 105, 'Former',  0, 1, 'Low',      'Medium'),
(42, 128, 83, 205, 26.5, 98,  'Current', 0, 0, 'Moderate', 'Medium'),
(55, 138, 90, 225, 29.0, 108, 'Former',  0, 0, 'Low',      'Medium'),
(48, 133, 86, 215, 27.5, 102, 'Never',   1, 0, 'Low',      'Medium'),
(52, 136, 89, 218, 28.5, 106, 'Former',  0, 1, 'Low',      'Medium'),
(46, 131, 84, 212, 27.2, 101, 'Current', 0, 0, 'None',     'Medium'),
(53, 137, 90, 222, 28.8, 107, 'Never',   0, 1, 'Low',      'Medium'),
(49, 134, 87, 216, 27.8, 103, 'Former',  1, 0, 'Low',      'Medium'),
(44, 129, 84, 208, 26.8, 99,  'Current', 0, 0, 'Moderate', 'Medium'),
-- High Risk
(58, 150, 95, 250, 31.0, 130, 'Current', 1, 1, 'None',     'High'),
(62, 155, 98, 260, 32.0, 140, 'Current', 1, 1, 'None',     'High'),
(60, 152, 96, 255, 31.5, 135, 'Former',  1, 1, 'Low',      'High'),
(65, 158, 100, 265, 33.0, 145, 'Current', 0, 1, 'None',    'High'),
(57, 148, 94, 248, 30.5, 128, 'Current', 1, 0, 'None',     'High'),
(63, 156, 99, 258, 32.5, 142, 'Current', 1, 1, 'None',     'High'),
(59, 151, 95, 252, 31.2, 132, 'Former',  1, 1, 'Low',      'High'),
(64, 157, 100, 262, 32.8, 143, 'Current', 0, 1, 'None',    'High'),
(61, 153, 97, 257, 31.8, 138, 'Current', 1, 0, 'None',     'High'),
(56, 149, 94, 249, 30.8, 129, 'Current', 0, 1, 'None',     'High'),
-- Critical Risk
(68, 175, 110, 290, 36.0, 200, 'Current', 1, 1, 'None',    'Critical'),
(72, 180, 115, 300, 38.0, 220, 'Current', 1, 1, 'None',    'Critical'),
(70, 178, 112, 295, 37.0, 210, 'Current', 1, 1, 'None',    'Critical'),
(75, 182, 118, 305, 39.0, 230, 'Former',  1, 1, 'None',    'Critical'),
(67, 172, 108, 285, 35.5, 195, 'Current', 1, 1, 'None',    'Critical'),
(73, 181, 116, 302, 38.5, 225, 'Current', 1, 1, 'None',    'Critical'),
(69, 176, 111, 292, 36.5, 205, 'Current', 1, 1, 'None',    'Critical'),
(74, 182, 117, 304, 39.2, 228, 'Former',  1, 1, 'None',    'Critical'),
(71, 179, 113, 298, 37.5, 215, 'Current', 1, 1, 'None',    'Critical'),
(66, 170, 107, 282, 35.0, 190, 'Current', 1, 1, 'None',    'Critical'),
-- Extra mixed records
(36, 122, 80, 192, 24.8, 93,  'Never',   0, 0, 'Moderate', 'Low'),
(47, 132, 85, 213, 27.3, 101, 'Former',  0, 0, 'Low',      'Medium'),
(61, 154, 97, 255, 32.0, 136, 'Current', 1, 0, 'None',     'High'),
(69, 176, 112, 293, 36.8, 207, 'Current', 1, 1, 'None',    'Critical'),
(30, 111, 71, 171, 22.0, 85,  'Never',   0, 0, 'High',     'Low'),
(51, 135, 88, 219, 28.2, 105, 'Former',  1, 0, 'Low',      'Medium'),
(60, 152, 96, 254, 31.4, 134, 'Current', 0, 1, 'None',     'High'),
(71, 179, 114, 297, 37.8, 213, 'Current', 1, 1, 'None',    'Critical'),
(39, 120, 78, 189, 24.1, 91,  'Never',   0, 0, 'Moderate', 'Low'),
(43, 129, 83, 207, 26.6, 99,  'Current', 0, 1, 'Low',      'Medium');
GO

-- Seed default admin user (password: Admin@123 - hashed)
INSERT INTO dbo.Users (Username, PasswordHash, Role, FullName, Email)
VALUES ('admin', 'EF92B778BAFE771E89245B89ECBC08A44A4E166C06659911881F383D4473E94F', 'Admin', 'System Administrator', 'admin@cardiorisk.com');
GO

PRINT 'CardioRiskDB schema and seed data created successfully.';
GO
