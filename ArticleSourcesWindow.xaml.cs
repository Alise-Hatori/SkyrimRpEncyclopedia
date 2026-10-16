using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using SkyrimRpEncyclopedia.Models;
using SkyrimRpEncyclopedia.Services;

namespace SkyrimRpEncyclopedia;

public partial class ArticleSourcesWindow : Window
{
    private readonly DatabaseService _database;

    private readonly int _articleId;
    private readonly bool _isOfficial;

    private readonly string _articleTitle;

    private List<ArticleSource> _sources = new();


    public ArticleSourcesWindow(
        DatabaseService database,
        int articleId,
        bool isOfficial,
        string articleTitle)
    {
        InitializeComponent();

        _database = database;

        _articleId = articleId;
        _isOfficial = isOfficial;
        _articleTitle = articleTitle;

        ArticleTitleText.Text =
            $"Источники: {_articleTitle}";

        Loaded += (_, _) => LoadSources();
    }


    private void LoadSources()
    {
        _sources =
            _database.GetSourcesForArticle(
                _articleId,
                _isOfficial);

        SourcesList.ItemsSource = null;
        SourcesList.ItemsSource = _sources;
    }


    private void AddSource_Click(
        object sender,
        RoutedEventArgs e)
    {
        var source =
            new ArticleSource
            {
                ArticleId = _articleId,
                ArticleIsOfficial = _isOfficial,
                Title = "",
                Url = "",
                SourceType = "Website",
                Author = "",
                Description = "",
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };

        var window =
            new ArticleSourceEditorWindow(
                source,
                isNew: true);

        window.Owner = this;

        if (window.ShowDialog() == true)
        {
            _database.AddSource(source);

            LoadSources();
        }
    }


    private void EditSource_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (sender is not Button button ||
            button.Tag is not ArticleSource source)
        {
            return;
        }

        var window =
            new ArticleSourceEditorWindow(
                source,
                isNew: false);

        window.Owner = this;

        if (window.ShowDialog() == true)
        {
            _database.UpdateSource(source);

            LoadSources();
        }
    }


    private void DeleteSource_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (sender is not Button button ||
            button.Tag is not ArticleSource source)
        {
            return;
        }

        var result =
            MessageBox.Show(
                $"Удалить источник «{source.Title}»?",
                "Удаление источника",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        _database.DeleteSource(source.Id);

        LoadSources();
    }


    private void OpenSource_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (sender is not Button button ||
            button.Tag is not ArticleSource source)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(source.Url))
        {
            MessageBox.Show(
                "У источника отсутствует ссылка.",
                "Источник",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            return;
        }

        try
        {
            Process.Start(
                new ProcessStartInfo
                {
                    FileName = source.Url,
                    UseShellExecute = true
                });
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Не удалось открыть ссылку:\n\n{ex.Message}",
                "Ошибка",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }


    private void Close_Click(
        object sender,
        RoutedEventArgs e)
    {
        Close();
    }
}