using System.Windows;
using System.Windows.Controls;
using SkyrimRpEncyclopedia.Models;
using SkyrimRpEncyclopedia.Services;

namespace SkyrimRpEncyclopedia;

public partial class ArticleEditorWindow : Window
{
    private readonly DatabaseService _database;
    private readonly Article? _article;

    public ArticleEditorWindow(DatabaseService database)
    {
        InitializeComponent();
        _database = database;
        CategoryBox.SelectedIndex = 0;
    }

    public ArticleEditorWindow(DatabaseService database, Article article)
        : this(database)
    {
        _article = article;
        Title = "Редактирование статьи";
        LoadArticle();
    }

    private void LoadArticle()
    {
        if (_article is null)
            return;

        TitleBox.Text = _article.Title;
        FlagsBox.Text = _article.Flags;
        TagsBox.Text = _article.Tags;
        SummaryBox.Text = _article.Summary;
        ContentBox.Text = _article.Content;

        var category = CategoryBox.Items.OfType<ComboBoxItem>().FirstOrDefault(item =>
            item.Content?.ToString() == _article.Category);
        if (category is null && !string.IsNullOrWhiteSpace(_article.Category))
        {
            category = new ComboBoxItem { Content = _article.Category };
            CategoryBox.Items.Add(category);
        }

        CategoryBox.SelectedItem = category;
    }

    private void SelectMetadata_Click(object sender, RoutedEventArgs e)
    {
        var selector = new MetadataSelectionWindow(_database, TagsBox.Text, FlagsBox.Text)
        {
            Owner = this
        };

        if (selector.ShowDialog() != true)
            return;

        TagsBox.Text = string.Join(", ", selector.SelectedTags);
        FlagsBox.Text = string.Join(", ", selector.SelectedFlags);
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var title = TitleBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(title))
        {
            MessageBox.Show("Укажите название статьи.", "Статья", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var category = (CategoryBox.SelectedItem as ComboBoxItem)?.Content?.ToString()?.Trim();
        if (string.IsNullOrWhiteSpace(category))
        {
            MessageBox.Show("Выберите категорию статьи.", "Статья", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var candidate=new Article { Id=_article?.Id??0,Title=title,Category=category,Flags=FlagsBox.Text.Trim(),Tags=TagsBox.Text.Trim(),
            Summary=SummaryBox.Text.Trim(),Content=ContentBox.Text.Trim(),CreatedAt=_article?.CreatedAt??DateTime.Now,UpdatedAt=DateTime.Now,
            IsOfficial=_article?.IsOfficial??false,IsMvp=_article?.IsMvp??false };
        try { if(_article is null) _database.AddArticle(candidate); else _database.UpdateArticle(candidate); }
        catch(Exception error) { UserDataErrors.Report("Article.Save.Failed",error,this);return; }

        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
