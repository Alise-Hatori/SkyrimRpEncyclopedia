using System.Security.Cryptography;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Data.Sqlite;
using SkyrimRpEncyclopedia;
using SkyrimRpEncyclopedia.Models;
using SkyrimRpEncyclopedia.Services;

static class ArticleMetadataSelectionTests
{
    // Called inside the existing STA resource test, using the real app resource dictionaries.
    public static void Run()
    {
        var checks = 0;
        void Check(bool condition, string message)
        {
            checks++;
            if (!condition) throw new InvalidOperationException("Article metadata: " + message);
        }
        T Control<T>(Window window, string name) where T : FrameworkElement => (T)window.FindName(name);
        string Hash(string path)
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            return Convert.ToHexString(SHA256.HashData(stream));
        }
        var folder = Path.Combine(Path.GetTempPath(), "skyrim-article-metadata-" + Guid.NewGuid().ToString("N"));
        var windows = new List<Window>();
        Directory.CreateDirectory(folder);
        try
        {
            var empty = new DatabaseService(Path.Combine(folder, "empty.db"));
            var before = Hash(empty.DatabasePath);
            Check(empty.GetTags().Count == 0 && empty.GetFlags().Count == 0, "Fresh user database must not silently seed invented metadata.");
            var emptySelector = new MetadataSelectionWindow(empty, "", ""); windows.Add(emptySelector);
            Check(Control<ListBox>(emptySelector, "TagsListBox").Items.Count == 0 && Control<ListBox>(emptySelector, "FlagsListBox").Items.Count == 0, "Empty source must remain empty.");
            Check(Control<TextBlock>(emptySelector, "TagsEmptyMessage").Visibility == Visibility.Visible && Control<TextBlock>(emptySelector, "FlagsEmptyMessage").Visibility == Visibility.Visible, "Empty lists need an explanation and management instructions.");
            emptySelector.CaptureSelection();
            Check(emptySelector.SelectedTags.Count == 0 && emptySelector.SelectedFlags.Count == 0, "Empty selection invented values.");
            var draftSelector = new MetadataSelectionWindow(empty, "Введённый тег", "Введённый флаг"); windows.Add(draftSelector);
            draftSelector.CaptureSelection();
            Check(draftSelector.SelectedTags.SequenceEqual(new[] { "Введённый тег" }) && draftSelector.SelectedFlags.SequenceEqual(new[] { "Введённый флаг" }), "Empty catalog erased draft values.");
            Check(Control<TextBlock>(draftSelector, "RetainedMetadataHint").Visibility == Visibility.Visible, "Retained manual values must be explained.");
            Check(Hash(empty.DatabasePath) == before, "Selector seeded or modified empty database.");

            var db = new DatabaseService(Path.Combine(folder, "populated.db"));
            // Test fixture values only: no defaults or production corpus writes.
            db.AddTag("Личный тег");
            db.AddTag("Другой тег");
            db.AddFlag(new ContentFlag { Name = "Личный флаг", Icon = "*" });
            before = Hash(db.DatabasePath);
            var selector = new MetadataSelectionWindow(db, "#Личный тег; Ручной тег", "Личный флаг, Ручной флаг"); windows.Add(selector);
            var tags = Control<ListBox>(selector, "TagsListBox");
            var flags = Control<ListBox>(selector, "FlagsListBox");
            Check(tags.Items.Count == 2 && flags.Items.Count == 1, "Persisted metadata missing from ItemsSource.");
            Check(tags.DisplayMemberPath == nameof(ContentTag.DisplayName) && flags.DisplayMemberPath == nameof(ContentFlag.DisplayName), "Display binding does not target model display names.");
            foreach (var list in new[] { tags, flags })
            {
                list.Measure(new Size(250, 180));
                list.Arrange(new Rect(0, 0, 250, 180));
                list.UpdateLayout();
                var frame = new DispatcherFrame();
                Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => frame.Continue = false));
                Dispatcher.PushFrame(frame);
                list.UpdateLayout();
                var item = list.ItemContainerGenerator.ContainerFromIndex(0) as ListBoxItem;
                var expected = list.Items[0] is ContentTag tag ? tag.DisplayName : ((ContentFlag)list.Items[0]).DisplayName;
                Check(item is not null && VisualDescendants(item).OfType<TextBlock>().Any(text => text.Text == expected), "Rendered list item did not display the actual metadata name.");
            }
            Check(tags.Items.OfType<ContentTag>().All(t => t.UsageCount == 0 && t.DisplayName.Contains(t.Name)) && flags.Items.OfType<ContentFlag>().All(f => f.UsageCount == 0 && f.DisplayName.Contains(f.Name)), "Unused dictionary values must not be filtered out or rendered blank.");
            Check(tags.SelectedItems.OfType<ContentTag>().Single().Name == "Личный тег" && flags.SelectedItems.OfType<ContentFlag>().Single().Name == "Личный флаг", "Current stored values are not preselected.");
            Check(Control<TextBlock>(selector, "TagsEmptyMessage").Visibility == Visibility.Collapsed && Control<TextBlock>(selector, "FlagsEmptyMessage").Visibility == Visibility.Collapsed, "Populated lists must not show empty diagnostics.");
            selector.CaptureSelection();
            Check(selector.SelectedTags.ToHashSet().SetEquals(new[] { "Личный тег", "Ручной тег" }) && selector.SelectedFlags.ToHashSet().SetEquals(new[] { "Личный флаг", "Ручной флаг" }), "Applying lost listed or unlisted values.");
            selector.CaptureSelection();
            Check(selector.SelectedTags.Count == 2 && selector.SelectedFlags.Count == 2, "Repeated capture duplicates metadata.");
            tags.SelectedItems.Clear(); flags.SelectedItems.Clear(); selector.CaptureSelection();
            Check(selector.SelectedTags.SequenceEqual(new[] { "Ручной тег" }) && selector.SelectedFlags.SequenceEqual(new[] { "Ручной флаг" }), "Deselecting listed values must work without erasing unlisted input.");
            var article = new Article { Title = "Черновик", Category = "Локации", Tags = "Личный тег, Ручной тег", Flags = "Личный флаг", Content = "Личный текст" };
            var editor = new ArticleEditorWindow(db, article); windows.Add(editor);
            Check(Control<TextBox>(editor, "TagsBox").Text == article.Tags && Control<TextBox>(editor, "FlagsBox").Text == article.Flags, "Editor did not load article metadata into editable text fields.");
            Check(windows.All(w => !w.IsVisible), "No window may be shown by this test.");
            Check(Hash(db.DatabasePath) == before, "Constructing editor/selector or capturing selection changed database.");

            var tagsOnly = new DatabaseService(Path.Combine(folder, "tags-only.db"));
            tagsOnly.AddTag("Только тег");
            var partial = new MetadataSelectionWindow(tagsOnly, "", ""); windows.Add(partial);
            Check(Control<TextBlock>(partial, "TagsEmptyMessage").Visibility == Visibility.Collapsed && Control<TextBlock>(partial, "FlagsEmptyMessage").Visibility == Visibility.Visible, "Tag and flag empty states must be independent.");
        }
        finally
        {
            foreach (var window in windows) window.Close();
            SqliteConnection.ClearAllPools();
            Directory.Delete(folder, true);
        }
        Console.WriteLine($"Article metadata selection OK: {checks} checks; actual unshown editor/selector, empty and populated catalogs, display bindings, unused values, preserved draft metadata and unchanged temporary DBs.");
    }

    private static IEnumerable<DependencyObject> VisualDescendants(DependencyObject parent)
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);
            yield return child;
            foreach (var nested in VisualDescendants(child)) yield return nested;
        }
    }
}
