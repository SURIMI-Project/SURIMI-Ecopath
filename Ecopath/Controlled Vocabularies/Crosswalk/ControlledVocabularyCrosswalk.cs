using ControlledVocabularies.Core;
using ControlledVocabularies.CrossWalk;
using ControlledVocabularies.Descriptors;
using ControlledVocabularies.Utils;
using ControlledVocabularies.Vocabularies;
using Google.Api;
using Microsoft.Win32;
using System;

namespace ControlledVocabularies.CrossWalk
{
    /// <summary>
    /// Scores compatibility between vocabularies for cross-vocabulary resolution
    /// </summary>
    public class VocabularyCompatibilityScorer
    {
        private const int MIN_VIABLE_SCORE = 50;
        private const double FIELD_WEIGHT = 0.4;
        private const double PURPOSE_WEIGHT = 0.3;
        private const double DOMAIN_WEIGHT = 0.2;
        private const double SIZE_WEIGHT = 0.1;

        public CompatibilityScore ScoreCompatibility(IControlledVocabulary source, IControlledVocabulary target)
        {
            if (source == null || target == null || ReferenceEquals(source, target))
                return CompatibilityScore.NoMatch(source?.VocabularyName ?? "null", target?.VocabularyName ?? "null");

            var score = new CompatibilityScore(source.VocabularyName, target.VocabularyName);

            // 1. Domain alignment (must match for any compatibility)
            score.DomainMatch = (source.Domain == target.Domain) ? 100 : 0;
            if (score.DomainMatch == 0)
            {
                score.Explanation = "Different domains - no compatibility possible";
                return score;
            }

            // 2. Purpose overlap (flags can partially overlap)
            score.PurposeOverlap = CalculatePurposeOverlap(source.Purpose, target.Purpose);

            // 3. Field-level compatibility analysis
            var fieldAnalysis = AnalyzeFieldCompatibility(source, target);
            score.FieldMatches = fieldAnalysis.CompatibleFields;
            score.FieldMismatches = fieldAnalysis.IncompatibleFields;
            score.SharedFieldQuality = fieldAnalysis.AverageQuality;
            score.KeyFieldMatch = fieldAnalysis.CodeFieldsCompatible;

            // 4. Vocabulary size similarity
            score.SizeSimilarity = CalculateSizeSimilarity(
                source.Records.Count(),
                target.Records.Count());

            // 5. Calculate composite score
            score.OverallScore = CalculateCompositeScore(score);

            // 6. Generate explanation
            score.Explanation = GenerateExplanation(score, fieldAnalysis);

            return score;
        }

        public IEnumerable<CompatibilityScore> ScoreAllCompatible(
            IControlledVocabulary source,
            IEnumerable<IControlledVocabulary> candidates)
        {
            return candidates
                .Select(target => ScoreCompatibility(source, target))
                .Where(score => score.IsViable)
                .OrderByDescending(score => score.OverallScore);
        }

        private int CalculatePurposeOverlap(KeyPurpose sourcePurpose, KeyPurpose targetPurpose)
        {
            if (sourcePurpose == KeyPurpose.NotSet || targetPurpose == KeyPurpose.NotSet)
                return 50; // Neutral - can't determine

            var overlap = sourcePurpose & targetPurpose;
            if (overlap == 0) return 0;

            // Count overlapping flags vs total flags
            var sourceFlags = CountFlags(sourcePurpose);
            var targetFlags = CountFlags(targetPurpose);
            var overlapFlags = CountFlags(overlap);

            var totalFlags = Math.Max(sourceFlags, targetFlags);
            return (int)((double)overlapFlags / totalFlags * 100);
        }

        private int CountFlags(KeyPurpose purpose)
        {
            int count = 0;
            foreach (KeyPurpose flag in Enum.GetValues<KeyPurpose>())
            {
                if (flag != KeyPurpose.NotSet && purpose.HasFlag(flag))
                    count++;
            }
            return count;
        }

        private FieldAnalysis AnalyzeFieldCompatibility(IControlledVocabulary source, IControlledVocabulary target)
        {
            var analysis = new FieldAnalysis();
            var sourceFields = source.FieldNames.ToHashSet();
            var targetFields = target.FieldNames.ToHashSet();

            var sharedFields = sourceFields.Intersect(targetFields);
            var qualityScores = new List<double>();

            foreach (var fieldName in sharedFields)
            {
                var sourceDescriptor = source.GetKeyFieldDescriptor(fieldName);
                var targetDescriptor = target.GetKeyFieldDescriptor(fieldName);

                if (sourceDescriptor != null && targetDescriptor != null)
                {
                    if (MatchHelpers.CanMatch(sourceDescriptor, targetDescriptor))
                    {
                        analysis.CompatibleFields++;
                        var quality = CalculateFieldQuality(sourceDescriptor, targetDescriptor);
                        qualityScores.Add(quality);

                        // Check if code fields are compatible
                        if (fieldName == source.CodeFieldName && fieldName == target.CodeFieldName)
                            analysis.CodeFieldsCompatible = true;
                    }
                    else
                    {
                        analysis.IncompatibleFields++;
                    }
                }
            }

            analysis.AverageQuality = qualityScores.Any() ? qualityScores.Average() : 0.0;
            return analysis;
        }

        private double CalculateFieldQuality(KeyFieldDescriptor source, KeyFieldDescriptor target)
        {
            double quality = 1.0;

            // Strategy compatibility
            if ((source.Strategy & target.Strategy) == 0)
                quality *= 0.7; // Partial penalty for different strategies

            // Field kind compatibility  
            if (source.Kind != FieldKind.Unknown && target.Kind != FieldKind.Unknown)
            {
                if (source.Kind != target.Kind)
                {
                    // Code<->Label is OK, others are suspicious
                    bool isCodeLabel = (source.Kind, target.Kind) is
                        (FieldKind.Code, FieldKind.Label) or (FieldKind.Label, FieldKind.Code);
                    quality *= isCodeLabel ? 0.9 : 0.6;
                }
            }

            // Weight similarity (indicates field importance alignment)
            var weightRatio = Math.Min(source.Weight, target.Weight) /
                             (double)Math.Max(source.Weight, target.Weight);
            quality *= 0.7 + (0.3 * weightRatio);

            return quality;
        }

        private int CalculateSizeSimilarity(int sourceCount, int targetCount)
        {
            if (sourceCount == 0 || targetCount == 0) return 0;

            var ratio = Math.Min(sourceCount, targetCount) / (double)Math.Max(sourceCount, targetCount);
            return (int)(ratio * 100);
        }

        private int CalculateCompositeScore(CompatibilityScore score)
        {
            if (score.DomainMatch == 0) return 0;

            double composite = 0;
            composite += score.DomainMatch * DOMAIN_WEIGHT;
            composite += score.PurposeOverlap * PURPOSE_WEIGHT;
            composite += score.SizeSimilarity * SIZE_WEIGHT;

            // Field quality component
            if (score.FieldMatches > 0)
            {
                double fieldScore = Math.Min(100, score.FieldMatches * 20); // Cap at 100
                fieldScore *= score.SharedFieldQuality;

                // Bonus for code field compatibility
                if (score.KeyFieldMatch)
                    fieldScore *= 1.2;

                // Penalty for mismatches
                if (score.FieldMismatches > 0)
                {
                    var penalty = Math.Min(0.5, score.FieldMismatches * 0.1);
                    fieldScore *= (1.0 - penalty);
                }

                composite += fieldScore * FIELD_WEIGHT;
            }

            return Math.Clamp((int)composite, 0, 100);
        }

        private string GenerateExplanation(CompatibilityScore score, FieldAnalysis analysis)
        {
            var reasons = new List<string>();

            if (score.DomainMatch == 100)
                reasons.Add("Same domain");

            if (score.PurposeOverlap > 70)
                reasons.Add("Strong purpose alignment");
            else if (score.PurposeOverlap > 30)
                reasons.Add("Partial purpose overlap");

            if (score.FieldMatches > 0)
            {
                reasons.Add($"{score.FieldMatches} compatible field(s)");
                if (score.KeyFieldMatch)
                    reasons.Add("Code fields compatible");
            }

            if (score.FieldMismatches > 0)
                reasons.Add($"{score.FieldMismatches} field conflict(s)");

            if (score.SizeSimilarity > 80)
                reasons.Add("Similar vocabulary sizes");

            return string.Join("; ", reasons);
        }

        private class FieldAnalysis
        {
            public int CompatibleFields { get; set; }
            public int IncompatibleFields { get; set; }
            public double AverageQuality { get; set; }
            public bool CodeFieldsCompatible { get; set; }
        }
    }

    public class CompatibilityScore
    {
        public CompatibilityScore(string sourceName, string targetName)
        {
            SourceVocabulary = sourceName;
            TargetVocabulary = targetName;
        }

        public string SourceVocabulary { get; }
        public string TargetVocabulary { get; }

        public int DomainMatch { get; set; }            // 0-100
        public int PurposeOverlap { get; set; }         // 0-100  
        public int FieldMatches { get; set; }           // Count of compatible fields
        public int FieldMismatches { get; set; }        // Count of incompatible fields
        public double SharedFieldQuality { get; set; }  // 0.0-1.0 average field quality
        public bool KeyFieldMatch { get; set; }         // Code fields are compatible
        public int SizeSimilarity { get; set; }         // 0-100
        public int OverallScore { get; set; }           // 0-100 composite score

        public string Explanation { get; set; } = "";
        public bool IsViable => OverallScore >= 50;

        public static CompatibilityScore NoMatch(string source, string target)
            => new(source, target) { Explanation = "No compatibility possible" };

        public override string ToString()
            => $"{SourceVocabulary} → {TargetVocabulary}: {OverallScore}% ({Explanation})";
    }
}
