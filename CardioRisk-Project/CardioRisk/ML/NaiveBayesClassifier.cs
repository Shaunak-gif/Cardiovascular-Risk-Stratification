// =============================================
// CardioRisk / ML / NaiveBayesClassifier.cs
// Gaussian + Categorical Naive Bayes Classifier
// for Cardiovascular Risk Stratification
// =============================================

using System;
using System.Collections.Generic;
using System.Linq;

namespace CardioRisk.ML
{
    public enum RiskClass
    {
        Low,
        Medium,
        High,
        Critical
    }

    /// <summary>
    /// A single training / prediction sample
    /// </summary>
    public class ClinicalSample
    {
        // Continuous features
        public double Age              { get; set; }
        public double SystolicBP       { get; set; }
        public double DiastolicBP      { get; set; }
        public double CholesterolTotal { get; set; }
        public double BMI              { get; set; }
        public double BloodGlucose     { get; set; }

        // Categorical features (encoded as doubles)
        // SmokingStatus: Never=0, Former=1, Current=2
        public double SmokingStatus    { get; set; }
        // HasDiabetes: No=0, Yes=1
        public double HasDiabetes      { get; set; }
        // FamilyHistory: No=0, Yes=1
        public double FamilyHistory    { get; set; }
        // ExerciseFrequency: None=0, Low=1, Moderate=2, High=3
        public double ExerciseFrequency{ get; set; }

        // Label (used during training)
        public RiskClass Label         { get; set; }
    }

    /// <summary>
    /// Gaussian statistics per feature per class
    /// </summary>
    internal class GaussianStats
    {
        public double Mean   { get; set; }
        public double StdDev { get; set; }
    }

    /// <summary>
    /// Naive Bayes classifier combining Gaussian (continuous)
    /// and categorical likelihood estimation
    /// </summary>
    public class NaiveBayesClassifier
    {
        // Prior probability per class: P(class)
        private Dictionary<RiskClass, double> _classPriors;

        // Gaussian stats for continuous features: [class][featureIndex]
        private Dictionary<RiskClass, List<GaussianStats>> _gaussianStats;

        // Categorical counts for discrete features: [class][featureIndex][value]
        private Dictionary<RiskClass, List<Dictionary<int, int>>> _categoricalCounts;

        // Total samples per class (for categorical smoothing)
        private Dictionary<RiskClass, int> _classCounts;

        // Feature names in order
        private static readonly string[] _continuousFeatureNames = {
            "Age", "SystolicBP", "DiastolicBP", "CholesterolTotal", "BMI", "BloodGlucose"
        };

        private static readonly string[] _categoricalFeatureNames = {
            "SmokingStatus", "HasDiabetes", "FamilyHistory", "ExerciseFrequency"
        };

        // Categorical cardinalities (number of distinct values)
        private static readonly int[] _categoricalCardinalities = { 3, 2, 2, 4 };

        public bool IsTrained { get; private set; } = false;

        // ──────────────────────────────────────────────────────
        // TRAIN
        // ──────────────────────────────────────────────────────
        public void Train(List<ClinicalSample> trainingData)
        {
            if (trainingData == null || trainingData.Count == 0)
                throw new ArgumentException("Training data cannot be empty.");

            _classPriors      = new Dictionary<RiskClass, double>();
            _gaussianStats    = new Dictionary<RiskClass, List<GaussianStats>>();
            _categoricalCounts = new Dictionary<RiskClass, List<Dictionary<int, int>>>();
            _classCounts      = new Dictionary<RiskClass, int>();

            var classes = Enum.GetValues(typeof(RiskClass)).Cast<RiskClass>().ToList();
            int totalSamples = trainingData.Count;

            foreach (var cls in classes)
            {
                var classSamples = trainingData.Where(s => s.Label == cls).ToList();
                int count = classSamples.Count;
                _classCounts[cls] = count;

                // ── Prior probability (Laplace smoothed)
                _classPriors[cls] = (count + 1.0) / (totalSamples + classes.Count);

                // ── Gaussian statistics for continuous features
                _gaussianStats[cls] = new List<GaussianStats>();
                var continuousVectors = GetContinuousFeatures(classSamples);

                for (int f = 0; f < _continuousFeatureNames.Length; f++)
                {
                    var values = continuousVectors.Select(v => v[f]).ToList();
                    _gaussianStats[cls].Add(new GaussianStats
                    {
                        Mean   = Mean(values),
                        StdDev = StdDev(values)
                    });
                }

                // ── Categorical counts (Laplace smoothed)
                _categoricalCounts[cls] = new List<Dictionary<int, int>>();
                var categoricalVectors = GetCategoricalFeatures(classSamples);

                for (int f = 0; f < _categoricalFeatureNames.Length; f++)
                {
                    var countDict = new Dictionary<int, int>();
                    // Initialize all possible values
                    for (int v = 0; v < _categoricalCardinalities[f]; v++)
                        countDict[v] = 1; // Laplace = +1

                    foreach (var vec in categoricalVectors)
                        countDict[(int)vec[f]]++;

                    _categoricalCounts[cls].Add(countDict);
                }
            }

            IsTrained = true;
        }

        // ──────────────────────────────────────────────────────
        // PREDICT
        // Returns dictionary of class → probability
        // ──────────────────────────────────────────────────────
        public Dictionary<RiskClass, double> PredictProbabilities(ClinicalSample sample)
        {
            if (!IsTrained)
                throw new InvalidOperationException("Model must be trained before prediction.");

            var logScores = new Dictionary<RiskClass, double>();
            var classes   = Enum.GetValues(typeof(RiskClass)).Cast<RiskClass>().ToList();

            var contFeatures = GetContinuousFeatures(sample);
            var catFeatures  = GetCategoricalFeatures(sample);

            foreach (var cls in classes)
            {
                double logScore = Math.Log(_classPriors[cls]);

                // Gaussian likelihood for continuous features
                for (int f = 0; f < contFeatures.Length; f++)
                {
                    var stats = _gaussianStats[cls][f];
                    double likelihood = GaussianPDF(contFeatures[f], stats.Mean, stats.StdDev);
                    // Clamp to avoid log(0)
                    likelihood = Math.Max(likelihood, 1e-10);
                    logScore  += Math.Log(likelihood);
                }

                // Categorical likelihood
                for (int f = 0; f < catFeatures.Length; f++)
                {
                    var counts   = _categoricalCounts[cls][f];
                    int val      = (int)catFeatures[f];
                    int featureCount = counts.Values.Sum();
                    double prob  = (double)counts[val] / featureCount;
                    prob         = Math.Max(prob, 1e-10);
                    logScore    += Math.Log(prob);
                }

                logScores[cls] = logScore;
            }

            // Convert log scores to probabilities via softmax
            double maxLog = logScores.Values.Max();
            var expScores = logScores.ToDictionary(
                kv => kv.Key,
                kv => Math.Exp(kv.Value - maxLog)
            );
            double sumExp = expScores.Values.Sum();
            return expScores.ToDictionary(kv => kv.Key, kv => kv.Value / sumExp);
        }

        /// <summary>Returns the highest-probability class</summary>
        public RiskClass Predict(ClinicalSample sample)
        {
            var probs = PredictProbabilities(sample);
            return probs.OrderByDescending(kv => kv.Value).First().Key;
        }

        // ──────────────────────────────────────────────────────
        // CROSS-VALIDATION ACCURACY
        // ──────────────────────────────────────────────────────
        public double CrossValidateAccuracy(List<ClinicalSample> data, int folds = 5)
        {
            int foldSize = data.Count / folds;
            var shuffled = data.OrderBy(_ => Guid.NewGuid()).ToList();
            double totalAccuracy = 0;

            for (int i = 0; i < folds; i++)
            {
                var testSet  = shuffled.Skip(i * foldSize).Take(foldSize).ToList();
                var trainSet = shuffled.Except(testSet).ToList();

                var tempModel = new NaiveBayesClassifier();
                tempModel.Train(trainSet);

                int correct = testSet.Count(s => tempModel.Predict(s) == s.Label);
                totalAccuracy += (double)correct / testSet.Count;
            }

            return totalAccuracy / folds;
        }

        // ──────────────────────────────────────────────────────
        // HELPERS
        // ──────────────────────────────────────────────────────

        private double GaussianPDF(double x, double mean, double stddev)
        {
            if (stddev < 1e-6) stddev = 1e-6; // avoid div by zero
            double exponent = -Math.Pow(x - mean, 2) / (2 * Math.Pow(stddev, 2));
            return (1.0 / (stddev * Math.Sqrt(2 * Math.PI))) * Math.Exp(exponent);
        }

        private double Mean(List<double> values) =>
            values.Count == 0 ? 0 : values.Average();

        private double StdDev(List<double> values)
        {
            if (values.Count < 2) return 1.0;
            double avg = Mean(values);
            double variance = values.Sum(v => Math.Pow(v - avg, 2)) / (values.Count - 1);
            return Math.Sqrt(variance);
        }

        private List<double[]> GetContinuousFeatures(List<ClinicalSample> samples) =>
            samples.Select(s => new double[]
            {
                s.Age, s.SystolicBP, s.DiastolicBP,
                s.CholesterolTotal, s.BMI, s.BloodGlucose
            }).ToList();

        private double[] GetContinuousFeatures(ClinicalSample s) =>
            new[] { s.Age, s.SystolicBP, s.DiastolicBP, s.CholesterolTotal, s.BMI, s.BloodGlucose };

        private List<double[]> GetCategoricalFeatures(List<ClinicalSample> samples) =>
            samples.Select(s => new double[]
            {
                s.SmokingStatus, s.HasDiabetes, s.FamilyHistory, s.ExerciseFrequency
            }).ToList();

        private double[] GetCategoricalFeatures(ClinicalSample s) =>
            new[] { s.SmokingStatus, s.HasDiabetes, s.FamilyHistory, s.ExerciseFrequency };
    }

    // ──────────────────────────────────────────────────────────
    // Helper: Encode categorical strings to doubles
    // ──────────────────────────────────────────────────────────
    public static class FeatureEncoder
    {
        public static double EncodeSmokingStatus(string val)
        {
            return val switch
            {
                "Never"   => 0,
                "Former"  => 1,
                "Current" => 2,
                _         => 0
            };
        }

        public static double EncodeExerciseFrequency(string val)
        {
            return val switch
            {
                "None"     => 0,
                "Low"      => 1,
                "Moderate" => 2,
                "High"     => 3,
                _          => 0
            };
        }

        public static string RiskClassToString(RiskClass rc) => rc.ToString();

        public static RiskClass StringToRiskClass(string val)
        {
            return val switch
            {
                "Low"      => RiskClass.Low,
                "Medium"   => RiskClass.Medium,
                "High"     => RiskClass.High,
                "Critical" => RiskClass.Critical,
                _          => RiskClass.Low
            };
        }
    }
}
