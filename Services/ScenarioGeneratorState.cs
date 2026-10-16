using SkyrimRpEncyclopedia.Models;

namespace SkyrimRpEncyclopedia.Services;

/// <summary>Current view result; rejected loads never replace the accepted scenario.</summary>
public sealed class ScenarioGeneratorState
{
    public GeneratedScenario? Current { get; private set; }
    public int? LastSeed { get; private set; }

    public void Clear()
    {
        Current = null;
        LastSeed = null;
    }

    public void Accept(GeneratedScenario scenario)
    {
        Current = scenario;
        LastSeed = scenario.Seed;
    }

    public bool TryAcceptLoad(ScenarioExportResult result)
    {
        if (!result.IsSuccess || result.Scenario is null) return false;
        Current = result.Scenario;
        var canReplay = new[] { "Genre", "ParticipantCount", "DurationMinutes", "Tone", "Difficulty" }
            .All(field => result.SavedOptionFields.Contains(field, StringComparer.OrdinalIgnoreCase));
        LastSeed = !canReplay ? null : result.HasSavedSeed ? Current.Seed :
            result.SavedOptionFields.Contains("Seed", StringComparer.OrdinalIgnoreCase) ? Current.Options.Seed : null;
        return true;
    }

    public void UpdateCurrent(GeneratedScenario scenario)
    {
        Current = scenario; // Reference changes do not supply missing replay parameters.
    }
}
