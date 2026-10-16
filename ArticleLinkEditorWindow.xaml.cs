using System.Windows;
using System.Windows.Controls;
using SkyrimRpEncyclopedia.Models;
using SkyrimRpEncyclopedia.Services;

namespace SkyrimRpEncyclopedia;

public partial class ArticleLinkEditorWindow : Window
{
    private readonly DatabaseService _database;
    private readonly Article _sourceArticle;
    private readonly List<Article> _articles;
    private readonly ArticleLink? _link;

    public ArticleLinkEditorWindow(DatabaseService database, Article sourceArticle, List<Article> articles)
    {
        InitializeComponent();
        _database = database;
        _sourceArticle = sourceArticle;
        _articles = articles;
        SourceArticleText.Text = $"Источник: {_sourceArticle.Title}";
        LinkTypeBox.SelectedIndex = 0;
        LoadTargetArticles();
    }

    public ArticleLinkEditorWindow(DatabaseService database, Article sourceArticle, List<Article> articles, ArticleLink link)
        : this(database, sourceArticle, articles)
    {
        _link = link;
        Title = "Редактировать связь";
        LoadLink();
    }

    private void LoadTargetArticles()
    {
        TargetArticleBox.Items.Clear();
        foreach (var article in _articles
                     .Where(article => article.Id != _sourceArticle.Id || article.IsOfficial != _sourceArticle.IsOfficial)
                     .OrderBy(article => article.Title))
        {
            TargetArticleBox.Items.Add(article);
        }

        TargetArticleBox.SelectedIndex = -1;
    }

    private void LoadLink()
    {
        if (_link is null)
            return;

        TargetArticleBox.SelectedItem = TargetArticleBox.Items.OfType<Article>().FirstOrDefault(article =>
            article.Id == _link.TargetArticleId && article.IsOfficial == _link.TargetIsOfficial);
        LinkTypeBox.SelectedItem = LinkTypeBox.Items.OfType<ComboBoxItem>().FirstOrDefault(item =>
            item.Content?.ToString() == _link.LinkType);
        DescriptionBox.Text = _link.Description;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (TargetArticleBox.SelectedItem is not Article targetArticle)
        {
            MessageBox.Show("Выберите статью, с которой нужно создать связь.", "Связь", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (_database.LinkExists(_sourceArticle.Id, _sourceArticle.IsOfficial, targetArticle.Id, targetArticle.IsOfficial, _link?.Id))
        {
            MessageBox.Show($"Связь между «{_sourceArticle.Title}» и «{targetArticle.Title}» уже существует.", "Связь уже существует", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var linkType = (LinkTypeBox.SelectedItem as ComboBoxItem)?.Content?.ToString();
        if (string.IsNullOrWhiteSpace(linkType))
            linkType = "связан с";

        if (_link is not null)
        {
            _link.TargetArticleId = targetArticle.Id;
            _link.TargetIsOfficial = targetArticle.IsOfficial;
            _link.LinkType = linkType;
            _link.Description = DescriptionBox.Text.Trim();
            _database.UpdateLink(_link);
        }
        else
        {
            _database.AddLink(new ArticleLink
            {
                SourceArticleId = _sourceArticle.Id,
                SourceIsOfficial = _sourceArticle.IsOfficial,
                TargetArticleId = targetArticle.Id,
                TargetIsOfficial = targetArticle.IsOfficial,
                LinkType = linkType,
                Description = DescriptionBox.Text.Trim()
            });
        }

        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
