using System.Windows;
using SkyrimRpEncyclopedia.Models;
using SkyrimRpEncyclopedia.Services;

namespace SkyrimRpEncyclopedia;

public partial class MetadataSelectionWindow : Window
{
    private readonly List<string> _unlistedTags;
    private readonly List<string> _unlistedFlags;

    public List<string> SelectedTags { get; } = new();
    public List<string> SelectedFlags { get; } = new();

    public MetadataSelectionWindow(
        DatabaseService database,
        string currentTags,
        string currentFlags)
    {
        InitializeComponent();

        var tags = database.GetTags();
        var flags = database.GetFlags();
        var selectedTags = SplitValues(currentTags);
        var selectedFlags = SplitValues(currentFlags);

        TagsListBox.ItemsSource = tags;
        FlagsListBox.ItemsSource = flags;
        TagsEmptyMessage.Visibility = tags.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        FlagsEmptyMessage.Visibility = flags.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        _unlistedTags = selectedTags.Except(tags.Select(tag => tag.Name), StringComparer.OrdinalIgnoreCase).ToList();
        _unlistedFlags = selectedFlags.Except(flags.Select(flag => flag.Name), StringComparer.OrdinalIgnoreCase).ToList();
        RetainedMetadataHint.Visibility = _unlistedTags.Count > 0 || _unlistedFlags.Count > 0
            ? Visibility.Visible : Visibility.Collapsed;

        foreach (var tag in tags.Where(tag => selectedTags.Contains(tag.Name)))
        {
            TagsListBox.SelectedItems.Add(tag);
        }

        foreach (var flag in flags.Where(flag => selectedFlags.Contains(flag.Name)))
        {
            FlagsListBox.SelectedItems.Add(flag);
        }
    }

    private static HashSet<string> SplitValues(string values)
    {
        return values
            .Split([',', ';', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(value => value.Trim().TrimStart('#'))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private void Apply_Click(object sender, RoutedEventArgs e)
    {
        CaptureSelection();
        DialogResult = true;
    }

    internal void CaptureSelection()
    {
        SelectedTags.Clear();
        SelectedFlags.Clear();
        SelectedTags.AddRange(
            _unlistedTags.Concat(TagsListBox.SelectedItems
                .OfType<ContentTag>()
                .Select(tag => tag.Name)).Distinct(StringComparer.OrdinalIgnoreCase));

        SelectedFlags.AddRange(
            _unlistedFlags.Concat(FlagsListBox.SelectedItems
                .OfType<ContentFlag>()
                .Select(flag => flag.Name)).Distinct(StringComparer.OrdinalIgnoreCase));
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
