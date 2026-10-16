namespace SkyrimRpEncyclopedia.Models;

public static class RpSimpleEventThemes
{
    public const string Any = "Любая";
    public const string Religious = "Религиозные встречи";
    public const string Community = "Общественные собрания";
    public const string Fairs = "Ярмарки";
    public const string Craft = "Ремесло";
    public const string Celebrations = "Праздники и памятные даты";
    public const string Walks = "Прогулки";
    public const string Art = "Искусство";
    public const string Training = "Тренировки";
    public const string Charity = "Благотворительность";
    public static IReadOnlyList<string> All { get; } = Array.AsReadOnly(new[]
    {
        Religious, Community, Fairs, Craft, Celebrations, Walks, Art, Training, Charity
    });
}

public sealed record RpSimpleEventGenerationOptions
{
    public string Theme { get; init; } = RpSimpleEventThemes.Any;
    public int Seed { get; init; }
    public int DurationMinutes { get; init; } = 60;
    public string? LocationOverride { get; init; }
    public string? TimeDescriptionOverride { get; init; }
}

/// <summary>Author-created RP material, not a statement of canonical Skyrim history.</summary>
public sealed record RpSimpleEventTemplate
{
    public string Id { get; init; } = "";
    public string Theme { get; init; } = "";
    public string Title { get; init; } = "";
    public IReadOnlyList<string> Locations { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> Times { get; init; } = Array.Empty<string>();
    public int MinDurationMinutes { get; init; }
    public int MaxDurationMinutes { get; init; }
    public string GatheringReason { get; init; } = "";
    public string ParticipantDescription { get; init; } = "";
    public string GmOpening { get; init; } = "";
    public IReadOnlyList<string> OpeningIdeas { get; init; } = Array.Empty<string>();
}

public sealed record RpSimpleEvent
{
    public string TemplateId { get; init; } = "";
    public string CatalogVersion { get; init; } = "";
    public int Seed { get; init; }
    public string Theme { get; init; } = "";
    public string Title { get; init; } = "";
    public string Location { get; init; } = "";
    public string TimeDescription { get; init; } = "";
    public int DurationMinutes { get; init; }
    public string GatheringReason { get; init; } = "";
    public string ParticipantDescription { get; init; } = "";
    public string GmOpening { get; init; } = "";
    public IReadOnlyList<string> OpeningIdeas { get; init; } = Array.Empty<string>();
}

public enum RpSimpleEventGenerationMode { Ordinary, ReplaySeed }
public enum RpSimpleEventDiagnosticSeverity { Error, Warning }
public sealed record RpSimpleEventDiagnostic(string Code, string Message,
    RpSimpleEventDiagnosticSeverity Severity = RpSimpleEventDiagnosticSeverity.Error);

public sealed record RpSimpleEventValidationResult
{
    public IReadOnlyList<RpSimpleEventDiagnostic> Diagnostics { get; init; } = Array.Empty<RpSimpleEventDiagnostic>();
    public bool IsValid => Diagnostics.All(d => d.Severity != RpSimpleEventDiagnosticSeverity.Error);
}

public sealed record RpSimpleEventGenerationResult
{
    public RpSimpleEvent? Event { get; init; }
    public IReadOnlyList<RpSimpleEventDiagnostic> Diagnostics { get; init; } = Array.Empty<RpSimpleEventDiagnostic>();
    public bool IsSuccess => Event is not null && Diagnostics.All(d => d.Severity != RpSimpleEventDiagnosticSeverity.Error);
}
