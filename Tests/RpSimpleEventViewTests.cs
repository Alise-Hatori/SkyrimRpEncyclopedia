using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using SkyrimRpEncyclopedia.Models;
using SkyrimRpEncyclopedia.Services;
using SkyrimRpEncyclopedia.Views;

static class RpSimpleEventViewTests
{
    public static void Run()
    {
        Exception? failure = null;
        var checks = 0;
        var thread = new Thread(() =>
        {
            try
            {
                void Check(bool condition, string message)
                {
                    checks++;
                    if (!condition) throw new InvalidOperationException("Simple event view: " + message);
                }
                T Control<T>(RpSimpleEventGeneratorView view, string name) where T : FrameworkElement => (T)view.FindName(name);
                void Click(RpSimpleEventGeneratorView view, string name) => Control<Button>(view, name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                string? copied = null;
                var service = new RpSimpleEventGeneratorService();
                var view = new RpSimpleEventGeneratorView(service, text => copied = text);
                var theme = Control<ComboBox>(view, "ThemeBox");
                var duration = Control<ComboBox>(view, "DurationBox");
                var location = Control<ComboBox>(view, "LocationBox");
                var time = Control<ComboBox>(view, "TimeBox");
                var seed = Control<TextBox>(view, "SeedBox");
                var announcement = Control<TextBox>(view, "AnnouncementText");
                var notes = Control<TextBox>(view, "GmNotesText");
                var diagnostics = Control<TextBlock>(view, "DiagnosticText");
                var feedback = Control<TextBlock>(view, "FeedbackText");
                Check(theme.Items.Count == 10 && (string)theme.SelectedItem == RpSimpleEventThemes.Any, "Theme choices/default lost.");
                Check(!Control<Button>(view, "CopyAnnouncementButton").IsEnabled && !Control<Button>(view, "CopyGmButton").IsEnabled,
                    "Copy enabled before generation.");
                Check((string)location.SelectedItem == RpSimpleEventGeneratorView.Automatic && (string)time.SelectedItem == RpSimpleEventGeneratorView.Automatic,
                    "Automatic place/time absent.");
                foreach (var selectedTheme in RpSimpleEventThemes.All)
                {
                    theme.SelectedItem = selectedTheme;
                    Check(duration.Items.Cast<int>().All(d => RpSimpleEventTemplates.All.Any(t => t.Theme == selectedTheme &&
                        t.MinDurationMinutes <= d && t.MaxDurationMinutes >= d)), "UI offers invalid duration.");
                    foreach (var selectedLocation in location.Items.Cast<string>().Where(x => x != RpSimpleEventGeneratorView.Automatic).ToArray())
                    {
                        location.SelectedItem = selectedLocation;
                        Check(time.Items.Cast<string>().Where(x => x != RpSimpleEventGeneratorView.Automatic).All(selectedTime =>
                            RpSimpleEventTemplates.All.Any(t => t.Theme == selectedTheme && t.Locations.Contains(selectedLocation) && t.Times.Contains(selectedTime) &&
                                t.MinDurationMinutes <= (int)duration.SelectedItem && t.MaxDurationMinutes >= (int)duration.SelectedItem)),
                            "Place/time combination has no compatible template.");
                    }
                }
                theme.SelectedItem = RpSimpleEventThemes.Any;
                seed.Text = "42";
                Click(view, "ReplayButton");
                Check(view.CurrentEvent is not null && view.CurrentEvent.Seed == 42 && seed.Text == "42", "Seed changed or replay failed.");
                var snapshot = JsonSerializer.Serialize(view.CurrentEvent);
                Check(service.RecentTemplateIds.Count == 0, "Replay updated history.");
                Click(view, "GenerateButton");
                Click(view, "GenerateButton");
                Click(view, "ReplayButton");
                Check(snapshot == JsonSerializer.Serialize(view.CurrentEvent), "Replay depends on ordinary history.");
                Click(view, "CopyAnnouncementButton");
                Check(copied == announcement.Text && !copied.Contains(view.CurrentEvent!.GmOpening) &&
                    !copied.Contains("Шаблон:") && !copied.Contains("Seed:"), "Announcement leaks notes or technical metadata.");
                Click(view, "CopyGmButton");
                Check(copied == notes.Text && copied!.Contains(view.CurrentEvent!.GmOpening) &&
                    view.CurrentEvent.OpeningIdeas.All(copied.Contains), "Wrong GM text copied.");

                location.SelectedIndex = 1;
                Check(view.CurrentEvent is null && announcement.Text.Length == 0 && notes.Text.Length == 0, "Setting change leaves stale result.");
                time.SelectedIndex = 1;
                var requestedLocation = (string)location.SelectedItem;
                var requestedTime = (string)time.SelectedItem;
                Click(view, "ReplayButton");
                Check(view.CurrentEvent!.Location == requestedLocation && view.CurrentEvent.TimeDescription == requestedTime, "Explicit place/time ignored.");
                duration.SelectedItem = 45;
                Check(view.CurrentEvent is null && (string)location.SelectedItem == RpSimpleEventGeneratorView.Automatic &&
                    (string)time.SelectedItem == RpSimpleEventGeneratorView.Automatic, "Duration change leaves obsolete overrides/result.");
                Click(view, "ReplayButton");
                time.SelectedIndex = 1;
                Check(view.CurrentEvent is null, "Time change leaves stale result.");
                Click(view, "ReplayButton");
                theme.SelectedItem = RpSimpleEventThemes.Religious;
                Check(view.CurrentEvent is null && announcement.Text == "" && notes.Text == "", "Theme change leaves stale result.");
                seed.Text = "1337";
                duration.SelectedItem = 60;
                for (var i = 0; i < 5; i++) Click(view, "GenerateButton");
                Check(view.CurrentEvent is not null && diagnostics.Text.Contains("Предупреждение:") && seed.Text == "1337", "Warning hidden or seed modified.");
                var warning = diagnostics.Text;
                Click(view, "CopyAnnouncementButton");
                Check(diagnostics.Text == warning && !copied!.Contains(warning), "Copy erases or leaks diagnostics.");
                seed.Text = "не число";
                Check(view.CurrentEvent is null && !Control<Button>(view, "CopyAnnouncementButton").IsEnabled, "Seed edit leaves stale result/copy.");
                var before = service.RecentTemplateIds.ToArray();
                Click(view, "GenerateButton");
                Check(view.CurrentEvent is null && announcement.Text == "" && notes.Text == "" && diagnostics.Text.Contains("seed") &&
                    before.SequenceEqual(service.RecentTemplateIds), "Failed input retains content/history.");
                copied = null;
                Click(view, "CopyAnnouncementButton");
                Check(copied is null && feedback.Text.Contains("Сначала"), "Stale content copied.");
                seed.Text = "2147483648";
                Click(view, "ReplayButton");
                Check(view.CurrentEvent is null && diagnostics.Text.Contains("Ошибка:"), "Overflow seed accepted.");
                seed.Text = "-2147483648";
                Click(view, "ReplayButton");
                Check(view.CurrentEvent?.Seed == int.MinValue, "Signed seed not supported.");

                var clipboardFailureView = new RpSimpleEventGeneratorView(new RpSimpleEventGeneratorService(), _ => throw new InvalidOperationException("Clipboard занят"));
                Click(clipboardFailureView, "ReplayButton");
                var accepted = clipboardFailureView.CurrentEvent;
                Click(clipboardFailureView, "CopyAnnouncementButton");
                Check(Control<TextBlock>(clipboardFailureView, "FeedbackText").Text.Contains("Не удалось скопировать") &&
                    clipboardFailureView.CurrentEvent == accepted, "Clipboard error unhandled or destroys result.");
                Click(clipboardFailureView, "CopyGmButton");
                Check(Control<TextBlock>(clipboardFailureView, "FeedbackText").Text.Contains("Clipboard занят"), "GM clipboard error unhandled.");

                var invalidCatalogView = new RpSimpleEventGeneratorView(new RpSimpleEventGeneratorService([]), _ => { });
                Click(invalidCatalogView, "GenerateButton");
                Check(invalidCatalogView.CurrentEvent is null && Control<TextBlock>(invalidCatalogView, "DiagnosticText").Text.Contains("пуст"),
                    "Core error not displayed.");
                view.Measure(new Size(900, 1600));
                view.Arrange(new Rect(0, 0, 900, 1600));
                view.UpdateLayout();
                Check(announcement.IsReadOnly && announcement.TextWrapping == TextWrapping.Wrap && notes.IsReadOnly,
                    "Text blocks are not selectable/read-only/wrapped.");
            }
            catch (Exception exception) { failure = exception; }
            finally { Dispatcher.CurrentDispatcher.InvokeShutdown(); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null) throw new InvalidOperationException("Simple event view interaction tests failed.", failure);
        Console.WriteLine($"Simple event view OK: {checks} assertions, actual UserControl in STA, compatible choices, seed replay, stale-state clearing, warning and isolated copy handling; no visible window or system Clipboard.");
    }
}
