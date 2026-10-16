using System.Windows;
using System.Windows.Controls;
using SkyrimRpEncyclopedia.Models;
using SkyrimRpEncyclopedia.Services;

namespace SkyrimRpEncyclopedia;

public partial class ArticleNotesWindow : Window
{
    private readonly DatabaseService _database;
    private readonly Article _article;
    private ArticleNote? _selectedNote;

    public ArticleNotesWindow(DatabaseService database, Article article)
    {
        InitializeComponent();
        _database = database;
        _article = article;
        TitleText.Text = $"Заметки: {_article.Title}";
        NoteTypeBox.SelectedIndex = 0;
        LoadNotes();
    }

    private void LoadNotes()
    {
        NotesListBox.ItemsSource = _database.GetArticleNotes(
            _article.Id,
            _article.IsOfficial);
    }

    private void NotesListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _selectedNote = NotesListBox.SelectedItem as ArticleNote;
        NoteContentBox.Text = _selectedNote?.Content ?? "";

        string noteType = _selectedNote?.NoteType ?? "Private";
        NoteTypeBox.SelectedIndex = noteType == "Public" ? 1 : 0;
    }

    private void NewNote_Click(object sender, RoutedEventArgs e)
    {
        _selectedNote = null;
        NotesListBox.SelectedItem = null;
        NoteTypeBox.SelectedIndex = 0;
        NoteContentBox.Clear();
        NoteContentBox.Focus();
    }

    private void SaveNote_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(NoteContentBox.Text))
        {
            MessageBox.Show("Введите текст заметки.", "Заметки", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        string noteType = (NoteTypeBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Private";

        try
        {
        if (_selectedNote == null)
        {
            _database.AddArticleNote(new ArticleNote
            {
                ArticleId = _article.Id,
                ArticleIsOfficial = _article.IsOfficial,
                NoteType = noteType,
                Content = NoteContentBox.Text
            });
        }
        else
        {
            _database.UpdateArticleNote(new ArticleNote { Id=_selectedNote.Id,ArticleId=_selectedNote.ArticleId,ArticleIsOfficial=_selectedNote.ArticleIsOfficial,
                NoteType=noteType,Content=NoteContentBox.Text,CreatedAt=_selectedNote.CreatedAt,UpdatedAt=_selectedNote.UpdatedAt });
        }
        }
        catch(Exception error) { UserDataErrors.Report("Note.Save.Failed",error,this);return; }

        LoadNotes();
        NewNote_Click(sender, e);
    }

    private void DeleteNote_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedNote == null)
        {
            return;
        }

        if (MessageBox.Show("Удалить выбранную заметку?", "Заметки", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
        {
            return;
        }

        try { _database.DeleteArticleNote(_selectedNote.Id); }
        catch(Exception error) { UserDataErrors.Report("Note.Delete.Failed",error,this);return; }
        LoadNotes();
        NewNote_Click(sender, e);
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
