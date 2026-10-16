using SkyrimRpEncyclopedia.Models;

namespace SkyrimRpEncyclopedia.Services;

/// <summary>Offline core. Recent IDs belong to this service instance; replay neither reads nor changes them.</summary>
public sealed class RpSimpleEventGeneratorService
{
    public const int RecentHistoryCapacity = 8;
    private readonly object _gate = new();
    private readonly IReadOnlyList<RpSimpleEventTemplate> _templates;
    private readonly IReadOnlyList<RpSimpleEventDiagnostic> _catalogDiagnostics;
    private readonly List<string> _recent = new();
    private readonly string _catalogVersion;

    public RpSimpleEventGeneratorService(IEnumerable<RpSimpleEventTemplate>? templates = null,
        string catalogVersion = RpSimpleEventTemplates.CatalogVersion)
    {
        _catalogVersion = catalogVersion;
        _templates = Array.AsReadOnly((templates ?? RpSimpleEventTemplates.All).Select(t => t with
        {
            Locations = Array.AsReadOnly((t.Locations ?? Array.Empty<string>()).OrderBy(x => x, StringComparer.Ordinal).ToArray()),
            Times = Array.AsReadOnly((t.Times ?? Array.Empty<string>()).OrderBy(x => x, StringComparer.Ordinal).ToArray()),
            OpeningIdeas = Array.AsReadOnly((t.OpeningIdeas ?? Array.Empty<string>()).ToArray())
        }).OrderBy(t => t.Id, StringComparer.Ordinal).ToArray());
        _catalogDiagnostics = RpSimpleEventValidator.ValidateCatalog(_templates).Diagnostics;
    }

    public IReadOnlyList<string> RecentTemplateIds { get { lock (_gate) return Array.AsReadOnly(_recent.ToArray()); } }

    public RpSimpleEventGenerationResult Generate(RpSimpleEventGenerationOptions? options,
        RpSimpleEventGenerationMode mode = RpSimpleEventGenerationMode.Ordinary)
    {
        lock (_gate)
        {
            if (options is null) return new() { Diagnostics = Array.AsReadOnly(new[] { new RpSimpleEventDiagnostic("missing_options", "Не заданы параметры генерации.") }) };
            var diagnostics = _catalogDiagnostics.Concat(RpSimpleEventValidator.ValidateOptions(options).Diagnostics).ToList();
            if (string.IsNullOrWhiteSpace(_catalogVersion)) diagnostics.Add(new("catalog_version", "Не задана версия каталога."));
            if (!Enum.IsDefined(mode)) diagnostics.Add(new("unknown_mode", "Неизвестный режим генерации."));
            if (diagnostics.Any(d => d.Severity == RpSimpleEventDiagnosticSeverity.Error)) return Failure(diagnostics);
            var compatible = _templates.Where(t => RpSimpleEventValidator.IsCompatible(t, options)).ToArray();
            if (compatible.Length == 0)
            {
                diagnostics.Add(new("no_compatible_template", "Нет шаблона для этой тематики, длительности, места и времени. Проверьте допустимые значения каталога."));
                return Failure(diagnostics);
            }
            var candidates = compatible;
            if (mode == RpSimpleEventGenerationMode.Ordinary)
            {
                candidates = compatible.Where(t => !_recent.Contains(t.Id, StringComparer.Ordinal)).ToArray();
                if (candidates.Length == 0)
                {
                    // All compatible IDs are recent: choose the least recently used, never the last if an alternative exists.
                    candidates = compatible.Where(t => compatible.Length == 1 || t.Id != _recent.LastOrDefault())
                        .OrderBy(t => _recent.IndexOf(t.Id)).ThenBy(t => t.Id, StringComparer.Ordinal).Take(1).ToArray();
                    diagnostics.Add(new("history_exhausted", "Все совместимые шаблоны недавно использованы; выбран наиболее давно использованный.", RpSimpleEventDiagnosticSeverity.Warning));
                }
            }
            var random = new StableSeedSequence(options.Seed);
            var template = candidates[random.Next(candidates.Length)];
            var location = Choose(template.Locations, options.LocationOverride, random);
            var time = Choose(template.Times, options.TimeDescriptionOverride, random);
            string Fill(string text) => RpSimpleEventValidator.Render(text, location, time, options.DurationMinutes);
            var value = new RpSimpleEvent
            {
                TemplateId = template.Id, CatalogVersion = _catalogVersion, Seed = options.Seed, Theme = template.Theme,
                Title = Fill(template.Title), Location = location, TimeDescription = time, DurationMinutes = options.DurationMinutes,
                GatheringReason = Fill(template.GatheringReason), ParticipantDescription = Fill(template.ParticipantDescription),
                GmOpening = Fill(template.GmOpening), OpeningIdeas = Array.AsReadOnly(template.OpeningIdeas.Select(Fill).ToArray())
            };
            diagnostics.AddRange(RpSimpleEventValidator.ValidateEvent(value, template, options, _catalogVersion).Diagnostics);
            if (diagnostics.Any(d => d.Severity == RpSimpleEventDiagnosticSeverity.Error)) return Failure(diagnostics);
            if (mode == RpSimpleEventGenerationMode.Ordinary)
            {
                _recent.Remove(template.Id);
                _recent.Add(template.Id);
                if (_recent.Count > RecentHistoryCapacity) _recent.RemoveAt(0);
            }
            return new() { Event = value, Diagnostics = diagnostics.AsReadOnly() };
        }
    }

    private static RpSimpleEventGenerationResult Failure(List<RpSimpleEventDiagnostic> diagnostics) => new() { Diagnostics = diagnostics.AsReadOnly() };
    private static string Choose(IReadOnlyList<string> values, string? requested, StableSeedSequence random) =>
        string.IsNullOrWhiteSpace(requested) ? values[random.Next(values.Count)] :
        values.Single(x => string.Equals(x, requested.Trim(), StringComparison.OrdinalIgnoreCase));

    // SplitMix64: explicit integer arithmetic makes choices independent of runtime Random implementation and string hashing.
    private sealed class StableSeedSequence(int seed)
    {
        private ulong _state = unchecked((uint)seed);
        public int Next(int count)
        {
            unchecked
            {
                _state += 0x9E3779B97F4A7C15UL;
                var value = _state;
                value = (value ^ (value >> 30)) * 0xBF58476D1CE4E5B9UL;
                value = (value ^ (value >> 27)) * 0x94D049BB133111EBUL;
                value ^= value >> 31;
                return (int)(value % (ulong)count);
            }
        }
    }
}
