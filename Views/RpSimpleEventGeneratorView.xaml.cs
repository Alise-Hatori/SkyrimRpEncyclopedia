using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using SkyrimRpEncyclopedia.Models;
using SkyrimRpEncyclopedia.Services;

namespace SkyrimRpEncyclopedia.Views;

public partial class RpSimpleEventGeneratorView : UserControl
{
    internal const string Automatic = "Автоматически";
    private readonly RpSimpleEventGeneratorService _generator;
    private readonly Action<string> _copy;
    private bool _ready;
    private bool _updating;
    internal RpSimpleEvent? CurrentEvent { get; private set; }

    public RpSimpleEventGeneratorView() : this(new RpSimpleEventGeneratorService(), Clipboard.SetText) { }

    // Injection is limited to the existing core and clipboard side effect; used by windowless interaction tests.
    internal RpSimpleEventGeneratorView(RpSimpleEventGeneratorService generator, Action<string> copy)
    {
        _generator = generator;
        _copy = copy;
        InitializeComponent();
        MaterialNoticeText.Text = RpSimpleEventTemplates.MaterialNotice;
        _updating = true;
        ThemeBox.ItemsSource = new[] { RpSimpleEventThemes.Any }.Concat(RpSimpleEventThemes.All).ToArray();
        ThemeBox.SelectedIndex = 0;
        RefreshDurations();
        RefreshLocations();
        _updating = false;
        _ready = true;
        ClearResult("Выберите параметры и сгенерируйте событие.");
    }

    private IEnumerable<RpSimpleEventTemplate> ThemeTemplates => RpSimpleEventTemplates.All.Where(t =>
        ThemeBox.SelectedItem as string == RpSimpleEventThemes.Any || t.Theme == ThemeBox.SelectedItem as string);

    private void RefreshDurations()
    {
        var previous = DurationBox.SelectedItem as int?;
        var values = ThemeTemplates.SelectMany(t => Enumerable.Range(t.MinDurationMinutes, t.MaxDurationMinutes - t.MinDurationMinutes + 1))
            .Where(duration => duration % 15 == 0).Distinct().OrderBy(duration => duration).ToArray();
        DurationBox.ItemsSource = values;
        DurationBox.SelectedItem = previous is not null && values.Contains(previous.Value) ? previous.Value :
            values.Contains(60) ? 60 : values.FirstOrDefault();
    }

    private IEnumerable<RpSimpleEventTemplate> CompatibleTemplates => DurationBox.SelectedItem is int duration
        ? ThemeTemplates.Where(t => RpSimpleEventValidator.IsCompatible(t,
            new RpSimpleEventGenerationOptions { Theme = ThemeBox.SelectedItem as string ?? "", DurationMinutes = duration }))
        : Enumerable.Empty<RpSimpleEventTemplate>();

    private void RefreshLocations()
    {
        LocationBox.ItemsSource = new[] { Automatic }.Concat(CompatibleTemplates.SelectMany(t => t.Locations)
            .Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal)).ToArray();
        LocationBox.SelectedIndex = 0;
        RefreshTimes(false);
    }

    private bool RefreshTimes(bool preserve)
    {
        var previous = TimeBox.SelectedItem as string;
        var location = Setting(LocationBox);
        var values = new[] { Automatic }.Concat(CompatibleTemplates.Where(t => location is null || t.Locations.Contains(location, StringComparer.Ordinal))
            .SelectMany(t => t.Times).Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal)).ToArray();
        TimeBox.ItemsSource = values;
        TimeBox.SelectedItem = preserve && previous is not null && values.Contains(previous, StringComparer.Ordinal) ? previous : Automatic;
        return preserve && previous is not null && previous != Automatic && !values.Contains(previous, StringComparer.Ordinal);
    }

    private static string? Setting(ComboBox box) => box.SelectedItem is string value && value != Automatic ? value : null;

    private void Parameter_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!_ready || _updating) return;
        ClearResult("Параметры изменены. Сгенерируйте событие заново.");
        _updating = true;
        try
        {
            if (ReferenceEquals(sender, ThemeBox))
            {
                RefreshDurations();
                RefreshLocations();
                FeedbackText.Text += " Место и время переключены на автоматический выбор.";
            }
            else if (ReferenceEquals(sender, DurationBox))
            {
                RefreshLocations();
                FeedbackText.Text += " Место и время переключены на автоматический выбор.";
            }
            else if (ReferenceEquals(sender, LocationBox) && RefreshTimes(true))
                FeedbackText.Text += " Предыдущее время несовместимо с местом; включён автоматический выбор времени.";
        }
        finally { _updating = false; }
    }

    private void Seed_Changed(object sender, TextChangedEventArgs e)
    {
        if (_ready && !_updating) ClearResult("Seed изменён. Сгенерируйте событие заново.");
    }

    private void ClearResult(string message)
    {
        CurrentEvent = null;
        AnnouncementText.Clear();
        GmNotesText.Clear();
        IdentityText.Text = "";
        DiagnosticText.Text = "";
        CopyAnnouncementButton.IsEnabled = false;
        CopyGmButton.IsEnabled = false;
        FeedbackText.Text = message;
    }

    private void Generate_Click(object sender, RoutedEventArgs e) => Generate(RpSimpleEventGenerationMode.Ordinary);
    private void Replay_Click(object sender, RoutedEventArgs e) => Generate(RpSimpleEventGenerationMode.ReplaySeed);

    private void Generate(RpSimpleEventGenerationMode mode)
    {
        ClearResult("");
        if (!int.TryParse(SeedBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var seed))
        {
            DiagnosticText.Text = "Ошибка: seed должен быть целым числом от −2147483648 до 2147483647.";
            return;
        }
        if (ThemeBox.SelectedItem is not string theme || DurationBox.SelectedItem is not int duration ||
            LocationBox.SelectedItem is null || TimeBox.SelectedItem is null)
        {
            DiagnosticText.Text = "Ошибка: выберите тематику, длительность, место и время.";
            return;
        }
        try
        {
            var result = _generator.Generate(new RpSimpleEventGenerationOptions
            {
                Theme = theme, Seed = seed, DurationMinutes = duration,
                LocationOverride = Setting(LocationBox), TimeDescriptionOverride = Setting(TimeBox)
            }, mode);
            DiagnosticText.Text = string.Join("\n", result.Diagnostics.Select(d =>
                (d.Severity == RpSimpleEventDiagnosticSeverity.Warning ? "Предупреждение: " : "Ошибка: ") + d.Message));
            if (!result.IsSuccess || result.Event is null) return;
            CurrentEvent = result.Event;
            AnnouncementText.Text = RpSimpleEventTextFormatter.FormatAnnouncement(CurrentEvent);
            GmNotesText.Text = RpSimpleEventTextFormatter.FormatGmNotes(CurrentEvent);
            IdentityText.Text = $"Шаблон: {CurrentEvent.TemplateId} · Каталог: {CurrentEvent.CatalogVersion} · Seed: {CurrentEvent.Seed}";
            CopyAnnouncementButton.IsEnabled = CopyGmButton.IsEnabled = true;
            FeedbackText.Text = mode == RpSimpleEventGenerationMode.ReplaySeed ? "Событие воспроизведено по seed без учёта истории." : "Новое событие сгенерировано с учётом истории.";
        }
        catch (Exception exception)
        {
            ClearResult("");
            DiagnosticText.Text = "Не удалось сгенерировать событие: " + exception.Message;
        }
    }

    private void CopyAnnouncement_Click(object sender, RoutedEventArgs e) => Copy(AnnouncementText.Text, "Публичное объявление скопировано.");
    private void CopyGm_Click(object sender, RoutedEventArgs e) => Copy(GmNotesText.Text, "Заметки ведущего скопированы.");
    private void Copy(string text, string success)
    {
        if (CurrentEvent is null || string.IsNullOrWhiteSpace(text)) { FeedbackText.Text = "Сначала сгенерируйте событие."; return; }
        try { _copy(text); FeedbackText.Text = success; }
        catch (Exception exception) { FeedbackText.Text = "Не удалось скопировать текст: " + exception.Message; }
    }
}
