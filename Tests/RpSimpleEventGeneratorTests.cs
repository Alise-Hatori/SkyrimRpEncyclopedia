using System.Reflection;
using System.Text.Json;
using SkyrimRpEncyclopedia.Models;
using SkyrimRpEncyclopedia.Services;

static class RpSimpleEventGeneratorTests
{
    public static void Run()
    {
        var checks = 0;
        void Check(bool condition, string message)
        {
            checks++;
            if (!condition) throw new InvalidOperationException("Simple RP events: " + message);
        }
        var catalog = RpSimpleEventTemplates.All;
        Check(catalog.Count == 36, "Expected exactly 36 templates.");
        Check(RpSimpleEventThemes.All.Count == 9 && catalog.GroupBy(t => t.Theme).Count() == 9 &&
              RpSimpleEventThemes.All.All(theme => catalog.Count(t => t.Theme == theme) == 4), "Expected four templates per theme.");
        Check(catalog.Select(t => t.Id).Distinct(StringComparer.Ordinal).Count() == 36, "Template IDs collide.");
        Check(RpSimpleEventValidator.ValidateCatalog(catalog).IsValid, "Catalog validation failed.");
        Check(catalog.Select(t => t.GatheringReason).Distinct().Count() == 36 && catalog.Select(t => t.GmOpening).Distinct().Count() == 36,
            "Gatherings reuse the same reason or opening.");
        foreach (var template in catalog)
        {
            Check(RpSimpleEventValidator.ValidateTemplate(template).IsValid, "Invalid template " + template.Id);
            var service = new RpSimpleEventGeneratorService([template]);
            foreach (var duration in new[] { template.MinDurationMinutes, template.MaxDurationMinutes })
            foreach (var seed in new[] { 0, -1, int.MinValue, int.MaxValue })
            {
                var options = new RpSimpleEventGenerationOptions { Theme = template.Theme, Seed = seed, DurationMinutes = duration };
                var result = service.Generate(options, RpSimpleEventGenerationMode.ReplaySeed);
                Check(result.IsSuccess && result.Event!.TemplateId == template.Id &&
                      RpSimpleEventValidator.ValidateEvent(result.Event, template, options, RpSimpleEventTemplates.CatalogVersion).IsValid,
                    "Cannot generate " + template.Id);
                Check(!JsonSerializer.Serialize(result.Event).Contains('{'.ToString() + "location") &&
                      !new[] { result.Event!.Title, result.Event.ParticipantDescription, result.Event.GmOpening }
                          .Concat(result.Event.OpeningIdeas).Any(text => text.IndexOfAny(['{', '}']) >= 0), "Unresolved marker.");
                Check(Snapshot(result.Event!) == Snapshot(service.Generate(options, RpSimpleEventGenerationMode.ReplaySeed).Event!), "Replay differs.");
            }
            var overrides = new RpSimpleEventGenerationOptions
            {
                Theme = template.Theme, DurationMinutes = template.MinDurationMinutes, Seed = 17,
                LocationOverride = " " + template.Locations[0].ToUpperInvariant() + " ",
                TimeDescriptionOverride = template.Times[0].ToUpperInvariant()
            };
            var explicitResult = service.Generate(overrides);
            Check(explicitResult.IsSuccess && explicitResult.Event!.Location == template.Locations[0] &&
                explicitResult.Event.TimeDescription == template.Times[0], "Overrides were ignored or not canonicalized.");
            Check(!service.Generate(overrides with { DurationMinutes = template.MaxDurationMinutes + 1 }).IsSuccess, "Exceeded duration range.");
            Check(!service.Generate(overrides with { LocationOverride = "неподтверждённая площадка" }).IsSuccess, "Accepted unrelated place.");
            Check(!service.Generate(overrides with { TimeDescriptionOverride = "несовместимое время" }).IsSuccess, "Accepted unrelated time.");
            Check(!RpSimpleEventValidator.ValidateEvent(explicitResult.Event! with { GmOpening = "Чужая завязка" }, template, overrides,
                RpSimpleEventTemplates.CatalogVersion).IsValid, "Mixed-template result accepted.");
        }

        var replayOptions = new RpSimpleEventGenerationOptions { Seed = 247, DurationMinutes = 60 };
        var first = new RpSimpleEventGeneratorService();
        var expected = first.Generate(replayOptions, RpSimpleEventGenerationMode.ReplaySeed).Event!;
        var reversed = new RpSimpleEventGeneratorService(catalog.Reverse().Select(t => t with
        { Locations = t.Locations.Reverse().ToArray(), Times = t.Times.Reverse().ToArray() }));
        Check(Snapshot(expected) == Snapshot(reversed.Generate(replayOptions, RpSimpleEventGenerationMode.ReplaySeed).Event!),
            "Selection depends on collection enumeration order.");
        for (var seed = 0; seed < 20; seed++) Check(first.Generate(replayOptions with { Seed = seed }).IsSuccess, "Ordinary generation failed.");
        var history = first.RecentTemplateIds.ToArray();
        Check(history.Length == RpSimpleEventGeneratorService.RecentHistoryCapacity, "History is not bounded.");
        Check(Snapshot(expected) == Snapshot(first.Generate(replayOptions, RpSimpleEventGenerationMode.ReplaySeed).Event!), "History affects replay.");
        Check(history.SequenceEqual(first.RecentTemplateIds), "Replay changed ordinary history.");

        var limited = new RpSimpleEventGeneratorService();
        var religious = replayOptions with { Theme = RpSimpleEventThemes.Religious };
        var ids = Enumerable.Range(0, 4).Select(_ => limited.Generate(religious).Event!.TemplateId).ToArray();
        Check(ids.Distinct().Count() == 4, "Ordinary generation repeats while fresh alternatives exist.");
        var exhausted = limited.Generate(religious);
        Check(exhausted.IsSuccess && exhausted.Event!.TemplateId == ids[0] && exhausted.Event.TemplateId != ids[^1] &&
            exhausted.Diagnostics.Any(d => d.Code == "history_exhausted" && d.Severity == RpSimpleEventDiagnosticSeverity.Warning),
            "Exhausted history did not use LRU with a diagnostic.");
        for (var i = 0; i < 32; i++)
        {
            var previous = limited.RecentTemplateIds.Last();
            Check(limited.Generate(religious).Event!.TemplateId != previous, "Immediate repeat with alternatives.");
        }
        var single = new RpSimpleEventGeneratorService([catalog[0]]);
        var singleOptions = religious with { DurationMinutes = catalog[0].MinDurationMinutes };
        Check(single.Generate(singleOptions).IsSuccess && single.Generate(singleOptions).Diagnostics.Any(d => d.Code == "history_exhausted"),
            "Single-template exhaustion does not terminate predictably.");
        var failureHistory = first.RecentTemplateIds.ToArray();
        foreach (var invalid in new[]
        {
            replayOptions with { Theme = "Несуществующая тематика" }, replayOptions with { Theme = "" },
            replayOptions with { DurationMinutes = 0 }, replayOptions with { DurationMinutes = -10 },
            replayOptions with { DurationMinutes = int.MaxValue }, replayOptions with { LocationOverride = "{location}" },
            replayOptions with { TimeDescriptionOverride = "{time}" }, replayOptions with { LocationOverride = "случайная чужая локация" }
        })
        {
            var result = first.Generate(invalid);
            Check(!result.IsSuccess && result.Event is null && result.Diagnostics.Any(d => d.Severity == RpSimpleEventDiagnosticSeverity.Error),
                "Invalid options not diagnosed.");
        }
        Check(failureHistory.SequenceEqual(first.RecentTemplateIds), "Failed generation changes history.");
        Check(!first.Generate(null).IsSuccess && failureHistory.SequenceEqual(first.RecentTemplateIds), "Null options not diagnosed.");
        Check(!first.Generate(replayOptions, (RpSimpleEventGenerationMode)999).IsSuccess, "Unknown mode accepted.");

        var good = catalog[0];
        foreach (var invalid in new[]
        {
            good with { Id = "" }, good with { Title = "" }, good with { Title = null! }, good with { Theme = "Другой жанр" },
            good with { MinDurationMinutes = 0 }, good with { MinDurationMinutes = 90, MaxDurationMinutes = 30 },
            good with { Locations = [] }, good with { Times = [] }, good with { Locations = ["место", "МЕСТО"] },
            good with { Locations = null! }, good with { Times = null! }, good with { OpeningIdeas = null! },
            good with { Title = "{unknown}" }, good with { GmOpening = "{{location}}" },
            good with { ParticipantDescription = "Незакрытая {location" },
            good with { OpeningIdeas = ["одна"] }, good with { OpeningIdeas = ["", "вторая"] },
            good with { OpeningIdeas = ["одна", "одна"] }, good with { OpeningIdeas = ["1", "2", "3", "4", "5"] }
        })
        {
            Check(!RpSimpleEventValidator.ValidateTemplate(invalid).IsValid, "Invalid template accepted.");
            var invalidService = new RpSimpleEventGeneratorService([invalid]);
            Check(!invalidService.Generate(replayOptions).IsSuccess && invalidService.RecentTemplateIds.Count == 0, "Invalid catalog generated/history updated.");
        }
        Check(!RpSimpleEventValidator.ValidateCatalog([good, good]).IsValid, "Duplicate ID accepted.");
        foreach (var count in new[] { 2, 4 })
        {
            var ideasTemplate = good with { OpeningIdeas = Enumerable.Range(1, count).Select(i => "Добровольная идея " + i).ToArray() };
            Check(new RpSimpleEventGeneratorService([ideasTemplate]).Generate(singleOptions).IsSuccess, "Valid idea count rejected.");
        }
        Check(!new RpSimpleEventGeneratorService([]).Generate(replayOptions).IsSuccess, "Empty catalog accepted.");
        Check(!new RpSimpleEventGeneratorService(catalog, "").Generate(replayOptions).IsSuccess, "Empty version accepted.");
        var changed = expected with { CatalogVersion = "wrong", Seed = 999, DurationMinutes = 0, Location = "чужое место" };
        var expectedTemplate = catalog.Single(t => t.Id == expected.TemplateId);
        Check(!RpSimpleEventValidator.ValidateEvent(changed, expectedTemplate, replayOptions, RpSimpleEventTemplates.CatalogVersion).IsValid,
            "Contradictory event accepted.");
        Check(!RpSimpleEventValidator.ValidateEvent(expected with { OpeningIdeas = null! }, expectedTemplate, replayOptions,
            RpSimpleEventTemplates.CatalogVersion).IsValid, "Null result ideas not diagnosed.");

        // Core's public contract and collaborators must not accept SQL, HTTP or scenario services.
        var coreTypes = new[] { typeof(RpSimpleEventGeneratorService), typeof(RpSimpleEventTemplates), typeof(RpSimpleEventValidator), typeof(RpSimpleEvent) };
        foreach (var type in coreTypes)
        {
            var dependencies = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static).Select(f => f.FieldType)
                .Concat(type.GetConstructors().SelectMany(c => c.GetParameters().Select(p => p.ParameterType)))
                .Concat(type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                    .SelectMany(m => m.GetParameters().Select(p => p.ParameterType).Append(m.ReturnType)));
            Check(dependencies.All(t => !t.FullName!.Contains("Sqlite") && !t.FullName.Contains("System.Net") &&
                !t.FullName.Contains("DatabaseService") && !t.FullName.Contains("Scenario")), "Core exposes DB/network/scenario dependencies.");
        }
        Check(typeof(RpSimpleEvent).GetProperties().All(p => !new[] { "Characters", "Clues", "Secrets", "Branches", "Endings", "ConflictCause" }.Contains(p.Name)),
            "Core acquired mandatory scenario structure.");
        Console.WriteLine($"Simple RP events OK: {checks} assertions, 36 templates, nine themes, replay, bounded history, compatibility and negative validation; no DB/network required.");
        foreach (var seed in new[] { 7, 42, 247 })
        {
            var value = first.Generate(replayOptions with { Seed = seed }, RpSimpleEventGenerationMode.ReplaySeed).Event!;
            Console.WriteLine($"Simple event example: seed={seed}, template={value.TemplateId}, title={value.Title}, theme={value.Theme}, location={value.Location}, time={value.TimeDescription}, duration={value.DurationMinutes}, ideas={value.OpeningIdeas.Count}.");
        }
    }

    private static string Snapshot(RpSimpleEvent value) => JsonSerializer.Serialize(value);
}
