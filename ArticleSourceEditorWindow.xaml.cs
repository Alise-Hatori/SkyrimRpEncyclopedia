using System.Windows;
using System.Windows.Controls;
using SkyrimRpEncyclopedia.Models;

namespace SkyrimRpEncyclopedia;

public partial class ArticleSourceEditorWindow : Window
{
    private readonly ArticleSource _source;

    private readonly bool _isNew;


    public ArticleSourceEditorWindow(
        ArticleSource source,
        bool isNew)
    {
        InitializeComponent();

        _source = source;
        _isNew = isNew;

        LoadSource();
    }


    private void LoadSource()
    {
        TitleTextBox.Text =
            _source.Title;

        UrlTextBox.Text =
            _source.Url;

        AuthorTextBox.Text =
            _source.Author;

        DescriptionTextBox.Text =
            _source.Description;

        foreach (var item in SourceTypeComboBox.Items)
        {
            if (item is ComboBoxItem comboItem &&
                comboItem.Content?.ToString() ==
                _source.SourceType)
            {
                SourceTypeComboBox.SelectedItem =
                    comboItem;

                break;
            }
        }

        if (SourceTypeComboBox.SelectedItem == null)
        {
            SourceTypeComboBox.SelectedIndex = 2;
        }
    }


    private void Save_Click(
        object sender,
        RoutedEventArgs e)
    {
        string title =
            TitleTextBox.Text.Trim();

        string url =
            UrlTextBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(title))
        {
            MessageBox.Show(
                "Введите название источника.",
                "Источник",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            TitleTextBox.Focus();

            return;
        }

        if (string.IsNullOrWhiteSpace(url))
        {
            MessageBox.Show(
                "Введите ссылку на источник.",
                "Источник",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            UrlTextBox.Focus();

            return;
        }

        if (!Uri.TryCreate(
                url,
                UriKind.Absolute,
                out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp &&
             uri.Scheme != Uri.UriSchemeHttps))
        {
            MessageBox.Show(
                "Введите корректную ссылку HTTP или HTTPS.",
                "Источник",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            UrlTextBox.Focus();

            return;
        }

        _source.Title =
            title;

        _source.Url =
            url;

        _source.SourceType =
            (SourceTypeComboBox.SelectedItem
                as ComboBoxItem)
                ?.Content
                ?.ToString()
            ?? "Website";

        _source.Author =
            AuthorTextBox.Text.Trim();

        _source.Description =
            DescriptionTextBox.Text.Trim();

        _source.UpdatedAt =
            DateTime.Now;

        if (_isNew)
        {
            _source.CreatedAt =
                DateTime.Now;
        }

        DialogResult = true;
    }


    private void Cancel_Click(
        object sender,
        RoutedEventArgs e)
    {
        DialogResult = false;
    }
}