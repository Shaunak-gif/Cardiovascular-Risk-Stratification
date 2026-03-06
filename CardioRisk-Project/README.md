# 🫀 Cardiovascular Risk Stratification and Prognostic Modeling Framework

> An intelligent clinical decision support system using **Naive Bayes ML**, **ASP.NET**, and **C#** to stratify cardiovascular risk and provide real-time prognostic insights.

---

## 📋 Table of Contents

- [Overview](#overview)
- [Tech Stack](#tech-stack)
- [Project Structure](#project-structure)
- [Database Setup](#database-setup)
- [Backend Setup](#backend-setup)
- [Frontend Setup](#frontend-setup)
- [ML Model](#ml-model)
- [Features](#features)
- [API Endpoints](#api-endpoints)
- [Screenshots](#screenshots)
- [How to Run](#how-to-run)

---

## Overview

The **Cardiovascular Risk Stratification and Prognostic Modeling Framework** is a web-based medical analytics platform that helps clinicians assess patient cardiovascular risk using a trained **Naive Bayes classifier**. The system collects patient vitals and clinical parameters, runs probabilistic risk classification, and returns a risk tier (Low / Medium / High / Critical) along with a prognostic report.

---

## Tech Stack

| Layer        | Technology                          |
|--------------|-------------------------------------|
| Frontend     | ASP.NET WebForms / MVC (.NET 4.8)   |
| Backend      | C# (ASP.NET)                        |
| ML Algorithm | Naive Bayes Classifier (custom C#)  |
| Database     | Microsoft SQL Server (T-SQL)        |
| ORM          | ADO.NET                             |
| Styling      | Bootstrap 5 + custom CSS            |

---

## Project Structure

```
CardioRisk/
│
├── CardioRisk.sln
├── CardioRisk/
│   ├── App_Start/
│   ├── Controllers/
│   │   ├── HomeController.cs
│   │   ├── PatientController.cs
│   │   └── PredictionController.cs
│   ├── Models/
│   │   ├── PatientModel.cs
│   │   ├── RiskPredictionModel.cs
│   │   └── TrainingDataModel.cs
│   ├── ML/
│   │   └── NaiveBayesClassifier.cs
│   ├── DAL/
│   │   ├── DatabaseHelper.cs
│   │   └── PatientRepository.cs
│   ├── Views/
│   │   ├── Home/
│   │   │   └── Index.cshtml
│   │   ├── Patient/
│   │   │   ├── Register.cshtml
│   │   │   └── Profile.cshtml
│   │   └── Prediction/
│   │       ├── Assess.cshtml
│   │       └── Result.cshtml
│   ├── Scripts/
│   ├── Content/
│   ├── Web.config
│   └── Global.asax
└── Database/
    ├── schema.sql
    └── seed_data.sql
```

---

## Features

- **Patient Registration & Management** – Store and retrieve full patient profiles
- **Clinical Parameter Input** – Age, BP, cholesterol, BMI, glucose, smoking status, family history, etc.
- **Naive Bayes Risk Prediction** – Probabilistic multi-class risk stratification (Low / Medium / High / Critical)
- **Prognostic Report** – Detailed report with risk factors, recommendations
- **Dashboard** – Summary statistics, recent assessments
- **History Tracking** – All assessments stored and retrievable per patient
- **Export** – Risk report printable / downloadable

---

## How to Run

### Prerequisites

- Visual Studio 2019/2022
- .NET Framework 4.8
- SQL Server 2019+ or SQL Server Express
- SQL Server Management Studio (SSMS)

### Steps

1. **Clone the repository**
   ```bash
   git clone https://github.com/your-username/CardioRisk.git
   ```

2. **Database Setup**
   - Open SSMS
   - Run `Database/schema.sql`
   - Run `Database/seed_data.sql`
   - Update `Web.config` connection string

3. **Open in Visual Studio**
   - Open `CardioRisk.sln`
   - Restore NuGet packages
   - Build the solution

4. **Run**
   - Press `F5` or click **IIS Express**
   - Navigate to `http://localhost:PORT/`

---

## API Endpoints

| Method | Endpoint                     | Description                     |
|--------|------------------------------|---------------------------------|
| GET    | `/Patient/Register`          | Registration form               |
| POST   | `/Patient/Register`          | Submit patient data             |
| GET    | `/Prediction/Assess/{id}`    | Assessment form for patient     |
| POST   | `/Prediction/Assess`         | Run Naive Bayes prediction      |
| GET    | `/Prediction/Result/{id}`    | View risk result                |
| GET    | `/Patient/History/{id}`      | Patient prediction history      |
| GET    | `/Home/Dashboard`            | Admin dashboard                 |

---

## ML Model — Naive Bayes

The classifier trains on historical cardiovascular data with the following features:

| Feature              | Type        |
|----------------------|-------------|
| Age                  | Continuous  |
| Systolic BP          | Continuous  |
| Diastolic BP         | Continuous  |
| Cholesterol          | Continuous  |
| BMI                  | Continuous  |
| Blood Glucose        | Continuous  |
| Smoking Status       | Categorical |
| Diabetes             | Categorical |
| Family History       | Categorical |
| Exercise Frequency   | Categorical |

**Output Classes:** `Low`, `Medium`, `High`, `Critical`

The model uses **Gaussian Naive Bayes** for continuous features and **Categorical Naive Bayes** for discrete features, combined with **log-probability** scoring to avoid numerical underflow.

---

## License

MIT License — free to use for educational and clinical research purposes.

---

## Author

Developed as part of a graduate-level Health Informatics / AI in Medicine project.
