// Supporting classes for clean results
using ControlledVocabularies.Core;
using ControlledVocabularies.Inference;

public class SemanticInferenceResult
{
    private List<FieldInferenceInfo> m_fieldInferences { get; } = new();
    private List<ForeignKeyMatchResult> m_fkCandidates { get; } = new();
    private List<(KeyDomain domain, double confidence, string reason)> m_domainHints { get; } = new();
    private List<(KeyPurpose purpose, double confidence, string reason)> m_purposeHints { get; } = new(); // Fixed: purpose not domain

    public string VocabularyName { get; }
    public KeyDomain InferredDomain { get; set; } = KeyDomain.NotSet;
    public KeyPurpose InferredPurpose { get; set; } = KeyPurpose.NotSet;
    public double DomainConfidence { get; set; }
    public List<string> Diagnostics { get; } = new(); // Add this for diagnostic messages

    public SemanticInferenceResult(string vocabName) => VocabularyName = vocabName;

    public void AddDomainHint(KeyDomain domain, double confidence, string reason) =>
        m_domainHints.Add((domain, confidence, reason));
    public IEnumerable<(KeyDomain Domain, double Confidence, string Reason)> DomainHints =>
        [.. m_domainHints];

    public void AddPurposeHint(KeyPurpose purpose, double confidence, string reason) =>
        m_purposeHints.Add((purpose, confidence, reason));
    public IEnumerable<(KeyPurpose Purpose, double Confidence, string Reason)> PurposeHints =>
        [.. m_purposeHints];

    public void AddFieldInference(FieldInferenceInfo field) => m_fieldInferences.Add(field);
    public IEnumerable<FieldInferenceInfo> FieldInferences => [.. m_fieldInferences];

    public void AddForeignKeyCandidate(ForeignKeyMatchResult fk) => m_fkCandidates.Add(fk);
    public IEnumerable<ForeignKeyMatchResult> ForeignKeyCandidates => [.. m_fkCandidates];

    public void AddDiagnostic(string diagnostic) => Diagnostics.Add(diagnostic);
}