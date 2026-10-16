using System.Windows;
using System.Windows.Controls;
using SkyrimRpEncyclopedia.Models;
using SkyrimRpEncyclopedia.Services;

namespace SkyrimRpEncyclopedia;

public partial class ArticleFilterWindow : Window
{
    public string Source { get; private set; } = "Все материалы";
    public ArticleFilterRequest Request { get; private set; } = new(null,null,[],[]);

    public ArticleFilterWindow(IReadOnlyCollection<Article> articles)
    {
        InitializeComponent();

        CategoryBox.ItemsSource = ArticleFilterRules.Options(articles,ArticleFilterRules.Categories);
        CategoryBox.SelectedIndex = -1;
        CategoryBox.ToolTip = "Без выбранного значения — все категории. Для сброса используйте «Сбросить».";
        SourceBox.SelectedIndex = 0;
        TagsListBox.ItemsSource = ArticleFilterRules.Options(articles,a=>ArticleFilterRules.Tokens(a.Tags));
        FlagsListBox.ItemsSource = ArticleFilterRules.Options(articles,a=>ArticleFilterRules.Tokens(a.Flags));
    }

    private void Apply_Click(object sender, RoutedEventArgs e)
    {
        Source = (SourceBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Все материалы";
        ArticleCatalogSource? source = Source switch { "Только RP-правила"=>ArticleCatalogSource.Rules,"Только личные"=>ArticleCatalogSource.User,"Только официальные"=>ArticleCatalogSource.Official,"Только Knowledge"=>ArticleCatalogSource.Knowledge,_=>null };
        Request = new(source,CategoryBox.SelectedItem as ArticleFilterValue,
            TagsListBox.SelectedItems.OfType<ArticleFilterValue>().ToArray(),FlagsListBox.SelectedItems.OfType<ArticleFilterValue>().ToArray());
        DialogResult = true;
    }

    private void Reset_Click(object sender, RoutedEventArgs e)
    {
        SourceBox.SelectedIndex = 0;
        CategoryBox.SelectedIndex = -1;
        TagsListBox.UnselectAll();
        FlagsListBox.UnselectAll();
    }
}
