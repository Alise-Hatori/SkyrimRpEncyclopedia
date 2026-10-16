namespace SkyrimRpEncyclopedia.Models;

public sealed class ScenarioKnowledgeSearchRequest
{
    public string SemanticType { get; init; } = "";
    public string Category { get; init; } = "";
    public string Title { get; init; } = "";
    public int Limit { get; init; } = 12;
    public bool IncludeNeedsReview { get; init; }
}

public sealed class ScenarioKnowledgeCandidate
{
    public int EntityId { get; init; }
    public string Title { get; init; } = "";
    public string SemanticType { get; init; } = "";
    public string PrimaryCategory { get; init; } = "";
    public IReadOnlyList<string> Categories { get; init; } = Array.Empty<string>();
    public string SourceUrl { get; init; } = "";
    public string ContextExcerpt { get; init; } = "";
    public double Confidence { get; init; }
    public bool NeedsReview { get; init; }
}

public sealed class ScenarioKnowledgeCatalogStatus
{
    public bool IsAvailable { get; init; }
    public bool IsCompatible { get; init; }
    public string Diagnostic { get; init; } = "";
    public string SchemaVersion { get; init; } = "";
}

public sealed class ScenarioKnowledgeSearchResult
{
    public ScenarioKnowledgeCatalogStatus Status { get; init; } = new();
    public IReadOnlyList<ScenarioKnowledgeCandidate> Candidates { get; init; } = Array.Empty<ScenarioKnowledgeCandidate>();
}

/// <summary>Source-backed excerpt, not a verified interpretation or narrative assertion.</summary>
public sealed class ScenarioKnowledgeEvidence
{
    public int EntityId { get; init; }
    public string Title { get; init; } = "";
    public string SemanticType { get; init; } = "";
    public string SourceUrl { get; init; } = "";
    public long? SourceId { get; init; }
    public string SourceName { get; init; } = "";
    public string RevisionTimestamp { get; init; } = "";
    public string RetrievedAt { get; init; } = "";
    public string License { get; init; } = "";
    public double Confidence { get; init; }
    public bool NeedsReview { get; init; }
    public string TextField { get; init; } = "";
    public string Fragment { get; init; } = "";
    public bool IsTruncated { get; init; }
    public string FragmentSha256 { get; init; } = "";
    /// <summary>Hash of the returned source metadata, not of the full article or DB.</summary>
    public string SourceMetadataSha256 { get; init; } = "";
}

public sealed class ScenarioKnowledgeEvidenceResult
{
    public ScenarioKnowledgeCatalogStatus Status { get; init; } = new();
    public ScenarioKnowledgeEvidence? Evidence { get; init; }
    public string Reason { get; init; } = "";
    public string Diagnostic { get; init; } = "";
}

public enum ScenarioCanonicalAnchorStatus
{
    Candidate,
    Proposed,
    Selected,
    Rejected
}

public enum ScenarioFactOrigin
{
    CanonicalKnowledge,
    FactoryFiction,
    GmHypothesis
}

public sealed class ScenarioCanonicalAnchorRule
{
    public string Purpose { get; init; } = "";
    public IReadOnlyList<string> SemanticTypes { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> PreferredCategories { get; init; } = Array.Empty<string>();
    public int Limit { get; init; } = 1;
}

public sealed class ScenarioCanonicalSelectionRule
{
    public string TemplateId { get; init; } = "";
    public IReadOnlyList<ScenarioCanonicalAnchorRule> AnchorRules { get; init; } = Array.Empty<ScenarioCanonicalAnchorRule>();
}

/// <summary>
/// Provenance of a canonical candidate. Factory events, motivations and outcomes
/// remain ScenarioFactOrigin.FactoryFiction until separately sourced.
/// </summary>
public sealed class ScenarioCanonicalAnchor
{
    public int EntityId { get; init; }
    public string Title { get; init; } = "";
    public string SemanticType { get; init; } = "";
    public IReadOnlyList<string> Categories { get; init; } = Array.Empty<string>();
    public string SourceUrl { get; init; } = "";
    public string ContextExcerpt { get; init; } = "";
    public bool NeedsReview { get; init; }
    public double Confidence { get; init; }
    public string Purpose { get; init; } = "";
    public ScenarioCanonicalAnchorStatus Status { get; init; } = ScenarioCanonicalAnchorStatus.Candidate;
    public ScenarioFactOrigin Origin { get; init; } = ScenarioFactOrigin.CanonicalKnowledge;
}

public sealed class ScenarioCanonicalSelectionResult
{
    public ScenarioKnowledgeCatalogStatus CatalogStatus { get; init; } = new();
    public IReadOnlyList<ScenarioCanonicalAnchor> Anchors { get; init; } = Array.Empty<ScenarioCanonicalAnchor>();
    public string Diagnostic { get; init; } = "";
}
