using System.Text.Json;
using System.IO;
using SkyrimRpEncyclopedia.Models;

namespace SkyrimRpEncyclopedia.Services;

/// <summary>
/// Persists a completed scenario as a portable JSON document. The document is
/// self-contained: loading it never queries a Knowledge DB or refreshes source
/// facts from it.
/// </summary>
public sealed class ScenarioExportService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public ScenarioExportResult Save(string path, GeneratedScenario scenario)
    {
        if (string.IsNullOrWhiteSpace(path))
            return ScenarioExportResult.Failure("Не выбран путь для экспорта сценария.");

        try
        {
            var snapshot = CreateSnapshot(scenario);
            var content = JsonSerializer.Serialize(snapshot, JsonOptions);
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            var temporaryPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(temporaryPath, content);
                File.Move(temporaryPath, path, true);
            }
            finally
            {
                if (File.Exists(temporaryPath))
                    File.Delete(temporaryPath);
            }

            return ScenarioExportResult.Successful(snapshot, "Сценарий экспортирован.");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or NotSupportedException or ArgumentException)
        {
            return ScenarioExportResult.Failure($"Не удалось экспортировать сценарий: {exception.Message}");
        }
    }

    public ScenarioExportResult Load(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return ScenarioExportResult.Failure("Файл сценария не найден.");

        try
        {
            var content = File.ReadAllText(path);
            var scenario = JsonSerializer.Deserialize<GeneratedScenario>(content, JsonOptions);
            if (scenario is null)
                return ScenarioExportResult.Failure("Экспортированный сценарий пуст или имеет неизвестный формат.");

            var validation = ScenarioValidator.Validate(scenario);
            if (!validation.IsValid)
                return ScenarioExportResult.Failure("Экспортированный сценарий неполный: " + string.Join(" ", validation.Errors));

            using var document = JsonDocument.Parse(content);
            var properties = document.RootElement.EnumerateObject().ToArray();
            var options = properties.FirstOrDefault(property => property.Name.Equals("Options", StringComparison.OrdinalIgnoreCase)).Value;
            return ScenarioExportResult.Successful(CreateSnapshot(scenario), "Сценарий восстановлен.",
                options.ValueKind == JsonValueKind.Object ? options.EnumerateObject().Select(property => property.Name).ToArray() : [],
                properties.Any(property => property.Name.Equals("Seed", StringComparison.OrdinalIgnoreCase)));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or NotSupportedException or ArgumentException)
        {
            return ScenarioExportResult.Failure($"Не удалось открыть сценарий: {exception.Message}");
        }
    }

    private static GeneratedScenario CreateSnapshot(GeneratedScenario scenario)
    {
        var anchors = scenario.CanonicalAnchors
            .Where(anchor => anchor.Status == ScenarioCanonicalAnchorStatus.Selected &&
                             anchor.Origin == ScenarioFactOrigin.CanonicalKnowledge &&
                             !anchor.NeedsReview)
            .GroupBy(anchor => anchor.EntityId)
            .Select(group => group.First())
            .ToArray();

        return new GeneratedScenario
        {
            Title = scenario.Title,
            Seed = scenario.Seed,
            Options = scenario.Options,
            Template = scenario.Template,
            Hook = scenario.Hook,
            ConflictCause = scenario.ConflictCause,
            GenerationDiagnostic = scenario.GenerationDiagnostic,
            InvestigationBlueprintId = scenario.InvestigationBlueprintId,
            IntrigueBlueprintId = scenario.IntrigueBlueprintId,
            MissionBlueprintId = scenario.MissionBlueprintId,
            NegotiationBlueprintId = scenario.NegotiationBlueprintId,
            StartSceneId = scenario.StartSceneId,
            PublicBriefing = scenario.PublicBriefing,
            KnownFacts = scenario.KnownFacts,
            NarrativeFacts = scenario.NarrativeFacts,
            GmNotes = scenario.GmNotes,
            Hypotheses = scenario.Hypotheses,
            Coalitions = scenario.Coalitions,
            Characters = scenario.Characters,
            Clues = scenario.Clues,
            Scenes = scenario.Scenes,
            Branches = scenario.Branches,
            Endings = scenario.Endings,
            CanonicalAnchors = anchors
        };
    }
}

public sealed class ScenarioExportResult
{
    public bool IsSuccess { get; init; }
    public GeneratedScenario? Scenario { get; init; }
    public string Diagnostic { get; init; } = "";
    public IReadOnlyList<string> SavedOptionFields { get; init; } = Array.Empty<string>();
    public bool HasSavedSeed { get; init; }

    public static ScenarioExportResult Successful(GeneratedScenario scenario, string diagnostic,
        IReadOnlyList<string>? savedOptionFields = null, bool hasSavedSeed = false) => new()
    {
        IsSuccess = true,
        Scenario = scenario,
        Diagnostic = diagnostic,
        SavedOptionFields = savedOptionFields ?? Array.Empty<string>(),
        HasSavedSeed = hasSavedSeed
    };

    public static ScenarioExportResult Failure(string diagnostic) => new() { Diagnostic = diagnostic };
}
