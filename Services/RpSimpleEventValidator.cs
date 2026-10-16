using System.Globalization;
using System.Text.RegularExpressions;
using SkyrimRpEncyclopedia.Models;

namespace SkyrimRpEncyclopedia.Services;

/// <summary>Checks structural consistency; literary quality still requires editorial review.</summary>
public static class RpSimpleEventValidator
{
    public static RpSimpleEventValidationResult ValidateCatalog(IEnumerable<RpSimpleEventTemplate> templates)
    {
        var rows = templates.ToArray();
        var diagnostics = rows.SelectMany(t => ValidateTemplate(t).Diagnostics).ToList();
        if (rows.Length == 0) Add(diagnostics, "empty_catalog", "Каталог шаблонов пуст.");
        foreach (var group in rows.GroupBy(t => t.Id, StringComparer.Ordinal).Where(g => g.Count() > 1))
            Add(diagnostics, "duplicate_id", $"Повторяется ID шаблона: {group.Key}.");
        return Result(diagnostics);
    }

    public static RpSimpleEventValidationResult ValidateTemplate(RpSimpleEventTemplate template)
    {
        var diagnostics = new List<RpSimpleEventDiagnostic>();
        if (string.IsNullOrWhiteSpace(template.Id) || !Regex.IsMatch(template.Id, "^[a-z0-9]+(?:-[a-z0-9]+)*$"))
            Add(diagnostics, "invalid_id", "ID шаблона должен быть непустым устойчивым slug.");
        if (!RpSimpleEventThemes.All.Contains(template.Theme, StringComparer.Ordinal))
            Add(diagnostics, "unknown_theme", $"Неизвестная тематика шаблона {template.Id}.");
        if (template.MinDurationMinutes <= 0 || template.MaxDurationMinutes < template.MinDurationMinutes)
            Add(diagnostics, "duration_range", $"Некорректный диапазон длительности: {template.Id}.");
        ValidateChoices(template.Locations, "места", diagnostics);
        ValidateChoices(template.Times, "время", diagnostics);
        ValidateIdeas(template.OpeningIdeas, diagnostics);
        foreach (var text in Texts(template))
        {
            if (string.IsNullOrWhiteSpace(text)) Add(diagnostics, "empty_field", $"Пустое поле шаблона {template.Id}.");
            if (HasBraces(StripMarkers(text))) Add(diagnostics, "unknown_marker", $"Неизвестная или повреждённая подстановка: {template.Id}.");
        }
        return Result(diagnostics);
    }

    public static RpSimpleEventValidationResult ValidateOptions(RpSimpleEventGenerationOptions options)
    {
        var diagnostics = new List<RpSimpleEventDiagnostic>();
        if (options.Theme != RpSimpleEventThemes.Any && !RpSimpleEventThemes.All.Contains(options.Theme, StringComparer.Ordinal))
            Add(diagnostics, "unknown_theme", "Выберите известную тематику или «Любая».");
        if (options.DurationMinutes <= 0) Add(diagnostics, "invalid_duration", "Длительность должна быть положительной.");
        if (HasBraces(options.LocationOverride) || HasBraces(options.TimeDescriptionOverride))
            Add(diagnostics, "override_marker", "Место и время не должны содержать маркеры подстановок.");
        return Result(diagnostics);
    }

    public static bool IsCompatible(RpSimpleEventTemplate template, RpSimpleEventGenerationOptions options) =>
        (options.Theme == RpSimpleEventThemes.Any || options.Theme == template.Theme) &&
        options.DurationMinutes >= template.MinDurationMinutes && options.DurationMinutes <= template.MaxDurationMinutes &&
        MatchesOverride(template.Locations, options.LocationOverride) && MatchesOverride(template.Times, options.TimeDescriptionOverride);

    public static RpSimpleEventValidationResult ValidateEvent(RpSimpleEvent value, RpSimpleEventTemplate template,
        RpSimpleEventGenerationOptions options, string catalogVersion)
    {
        var diagnostics = ValidateTemplate(template).Diagnostics.Concat(ValidateOptions(options).Diagnostics).ToList();
        if (diagnostics.Any(d => d.Severity == RpSimpleEventDiagnosticSeverity.Error)) return Result(diagnostics);
        if (!IsCompatible(template, options)) Add(diagnostics, "incompatible", "Параметры несовместимы с выбранным шаблоном.");
        if (value.TemplateId != template.Id || value.CatalogVersion != catalogVersion || string.IsNullOrWhiteSpace(catalogVersion) ||
            value.Theme != template.Theme || value.Seed != options.Seed || value.DurationMinutes != options.DurationMinutes)
            Add(diagnostics, "identity_mismatch", "Идентификаторы или параметры результата не соответствуют запросу.");
        if (!template.Locations.Contains(value.Location, StringComparer.Ordinal) || !template.Times.Contains(value.TimeDescription, StringComparer.Ordinal) ||
            !MatchesResultOverride(value.Location, options.LocationOverride) || !MatchesResultOverride(value.TimeDescription, options.TimeDescriptionOverride))
            Add(diagnostics, "setting_mismatch", "Место или время результата не соответствуют допустимым значениям.");
        var expected = Texts(template).Select(text => Render(text, value.Location, value.TimeDescription, value.DurationMinutes)).ToArray();
        var actual = new[] { value.Title, value.GatheringReason, value.ParticipantDescription, value.GmOpening }.Concat(value.OpeningIdeas ?? Array.Empty<string>()).ToArray();
        if (!expected.SequenceEqual(actual, StringComparer.Ordinal)) Add(diagnostics, "content_mismatch", "Результат смешивает или изменяет содержание выбранного шаблона.");
        foreach (var text in actual.Concat(new[] { value.Location, value.TimeDescription }))
        {
            if (string.IsNullOrWhiteSpace(text)) Add(diagnostics, "empty_field", "Не заполнено обязательное поле события.");
            if (HasBraces(text)) Add(diagnostics, "unresolved_marker", "В событии осталась незаполненная подстановка.");
        }
        ValidateIdeas(value.OpeningIdeas, diagnostics);
        return Result(diagnostics);
    }

    internal static string Render(string text, string location, string time, int duration) => text
        .Replace("{location}", location, StringComparison.Ordinal)
        .Replace("{time}", time, StringComparison.Ordinal)
        .Replace("{duration}", duration.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal);

    private static IEnumerable<string> Texts(RpSimpleEventTemplate t) =>
        new[] { t.Title, t.GatheringReason, t.ParticipantDescription, t.GmOpening }.Concat(t.OpeningIdeas ?? Array.Empty<string>());
    private static bool MatchesOverride(IReadOnlyList<string> choices, string? value) => string.IsNullOrWhiteSpace(value) ||
        choices.Contains(value.Trim(), StringComparer.OrdinalIgnoreCase);
    private static bool MatchesResultOverride(string actual, string? value) => string.IsNullOrWhiteSpace(value) ||
        string.Equals(actual, value.Trim(), StringComparison.OrdinalIgnoreCase);
    private static bool HasBraces(string? text) => text?.IndexOfAny(['{', '}']) >= 0;
    private static string StripMarkers(string? text) => (text ?? "").Replace("{location}", "").Replace("{time}", "").Replace("{duration}", "");
    private static void ValidateChoices(IReadOnlyList<string>? choices, string field, List<RpSimpleEventDiagnostic> diagnostics)
    {
        if (choices is null || choices.Count == 0 || choices.Any(x => string.IsNullOrWhiteSpace(x) || x != x.Trim() || HasBraces(x)) ||
            choices.Distinct(StringComparer.OrdinalIgnoreCase).Count() != choices.Count)
            Add(diagnostics, "invalid_choices", $"Некорректные допустимые значения: {field}.");
    }
    private static void ValidateIdeas(IReadOnlyList<string>? ideas, List<RpSimpleEventDiagnostic> diagnostics)
    {
        if (ideas is null || ideas.Count is < 2 or > 4 || ideas.Any(string.IsNullOrWhiteSpace) || ideas.Distinct(StringComparer.OrdinalIgnoreCase).Count() != ideas.Count)
            Add(diagnostics, "invalid_ideas", "Нужны 2–4 непустые различающиеся идеи начала отыгрыша.");
    }
    private static void Add(List<RpSimpleEventDiagnostic> diagnostics, string code, string message) => diagnostics.Add(new(code, message));
    private static RpSimpleEventValidationResult Result(List<RpSimpleEventDiagnostic> diagnostics) => new() { Diagnostics = diagnostics.AsReadOnly() };
}
