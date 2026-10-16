using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SkyrimRpEncyclopedia.Models;
using SkyrimRpEncyclopedia.Services;

namespace SkyrimRpEncyclopedia.Views;

public partial class EventsLibraryView : UserControl
{
    private readonly DatabaseService _database;

    private List<RpEvent> _events = new();

    public event Action? BackRequested;

    public event Action<RpEvent>? EditRequested;

    public EventsLibraryView(DatabaseService database)
    {
        InitializeComponent();

        _database = database;

        LoadEvents();
    }

    private void LoadEvents()
    {
        _events = _database.GetRpEvents();

        DisplayEvents(_events);
    }

    private void DisplayEvents(IEnumerable<RpEvent> events)
    {
        EventsPanel.Children.Clear();

        var list = events.ToList();

        if (list.Count == 0)
        {
            var emptyState = new Border
            {
                Style = (Style)FindResource("NordEmptyState"),
                Margin = new Thickness(10, 30, 10, 10)
            };

            emptyState.Child = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                Children =
                {
                    new TextBlock
                    {
                        Text = "ᛟ",
                        FontFamily = (FontFamily)FindResource("NordDisplayFont"),
                        FontSize = 38,
                        Foreground = (Brush)FindResource("NordBlueGlowBrush"),
                        HorizontalAlignment = HorizontalAlignment.Center
                    },
                    new TextBlock
                    {
                        Text = "Журнал ивентов пока пуст",
                        Style = (Style)FindResource("NordArticleTitle"),
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Margin = new Thickness(0, 10, 0, 6)
                    },
                    new TextBlock
                    {
                        Text = "Созданные события появятся здесь как записи архивной хроники.",
                        Foreground = (Brush)FindResource("NordMutedSilverBrush"),
                        FontSize = 14,
                        TextAlignment = TextAlignment.Center,
                        TextWrapping = TextWrapping.Wrap,
                        MaxWidth = 480,
                        HorizontalAlignment = HorizontalAlignment.Center
                    }
                }
            };

            EventsPanel.Children.Add(emptyState);

            return;
        }

        foreach (var rpEvent in list)
        {
            EventsPanel.Children.Add(
                CreateEventCard(rpEvent));
        }
    }

    private Border CreateEventCard(RpEvent rpEvent)
    {
        var border = new Border
        {
            Style =
                (Style)FindResource("NordArticleCard"),

            Padding =
                new Thickness(15),

            Margin =
                new Thickness(
                    0,
                    0,
                    0,
                    12)
        };

        var main = new Grid();

        main.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width =
                    new GridLength(
                        1,
                        GridUnitType.Star)
            });

        main.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width = GridLength.Auto
            });

        var info = new StackPanel();

        info.Children.Add(
            new TextBlock
            {
                Text = rpEvent.Title,

                Style =
                    (Style)FindResource("NordArticleTitle"),

                FontSize = 19
            });

        var details =
            new TextBlock
            {
                Margin =
                    new Thickness(
                        0,
                        7,
                        0,
                        0),

                Style =
                    (Style)FindResource("NordMetadataText"),

                FontSize = 13
            };

        var detailParts =
            new List<string>();

        if (!string.IsNullOrWhiteSpace(
                rpEvent.EventType))
        {
            detailParts.Add(
                $"Тип: {rpEvent.EventType}");
        }

        if (!string.IsNullOrWhiteSpace(
                rpEvent.Location))
        {
            detailParts.Add(
                $"Место: {rpEvent.Location}");
        }

        if (!string.IsNullOrWhiteSpace(
                rpEvent.Faction))
        {
            detailParts.Add(
                $"Фракция: {rpEvent.Faction}");
        }

        details.Text =
            string.Join(
                "  •  ",
                detailParts);

        info.Children.Add(details);

        if (!string.IsNullOrWhiteSpace(
                rpEvent.Characters))
        {
            info.Children.Add(
                new TextBlock
                {
                    Text =
                        $"Участники: {rpEvent.Characters}",

                    Margin =
                        new Thickness(
                            0,
                            7,
                            0,
                            0),

                    Foreground =
                        (Brush)FindResource("NordMutedSilverBrush"),

                    FontSize = 13,

                    TextWrapping =
                        TextWrapping.Wrap
                });
        }

        if (!string.IsNullOrWhiteSpace(
                rpEvent.Objective))
        {
            info.Children.Add(
                new TextBlock
                {
                    Text =
                        $"Цель: {rpEvent.Objective}",

                    Margin =
                        new Thickness(
                            0,
                            8,
                            0,
                            0),

                    Foreground =
                        (Brush)FindResource("NordSilverBrush"),

                    FontSize = 14,

                    TextWrapping =
                        TextWrapping.Wrap
                });
        }

        info.Children.Add(
            new TextBlock
            {
                Text =
                    $"Создан: {rpEvent.CreatedAt:dd.MM.yyyy HH:mm}",

                Margin =
                    new Thickness(
                        0,
                        8,
                        0,
                        0),

                FontSize = 11,

                Foreground =
                    (Brush)FindResource("NordMutedSilverBrush")
            });

        Grid.SetColumn(info, 0);

        main.Children.Add(info);

        var buttons =
            new StackPanel
            {
                Orientation =
                    Orientation.Vertical,

                HorizontalAlignment =
                    HorizontalAlignment.Right
            };

        // Изменить
        var editButton =
            CreateButton(
                "Изменить");

        editButton.Click += (_, _) =>
        {
            EditRequested?.Invoke(
                rpEvent);
        };

        buttons.Children.Add(
            editButton);

        // Копировать
        var copyButton =
            CreateButton(
                "Копировать");

        copyButton.Click += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(
                    rpEvent.GeneratedText))
            {
                MessageBox.Show(
                    "У этого ивента нет сгенерированного текста.",
                    "Копирование",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return;
            }

            Clipboard.SetText(
                rpEvent.GeneratedText);

            MessageBox.Show(
                "Ивент скопирован в буфер обмена.",
                "Готово",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        };

        buttons.Children.Add(
            copyButton);

        // Удалить
        var deleteButton =
            new Button
            {
                Content = "Удалить",

                Width = 125,

                Height = 32,

                Margin =
                    new Thickness(
                        10,
                        0,
                        0,
                        0),

                Style =
                    (Style)FindResource("NordDangerButton")
            };

        deleteButton.Click += (_, _) =>
        {
            DeleteEvent(
                rpEvent);
        };

        buttons.Children.Add(
            deleteButton);

        Grid.SetColumn(
            buttons,
            1);

        main.Children.Add(
            buttons);

        border.Child =
            main;

        return border;
    }

    private Button CreateButton(
        string text)
    {
        return new Button
        {
            Content = text,

            Width = 125,

            Height = 32,

            Margin =
                new Thickness(
                    10,
                    0,
                    0,
                    6),

            Style =
                (Style)FindResource("NordActionButton")
        };
    }

    private void DeleteEvent(
        RpEvent rpEvent)
    {
        var result =
            MessageBox.Show(
                $"Удалить ивент «{rpEvent.Title}»?",

                "Удаление ивента",

                MessageBoxButton.YesNo,

                MessageBoxImage.Warning);

        if (result !=
            MessageBoxResult.Yes)
        {
            return;
        }

        try { _database.DeleteRpEvent(rpEvent.Id); }
        catch(Exception error) { UserDataErrors.Report("Event.Delete.Failed",error,Window.GetWindow(this),false);return; }

        LoadEvents();
    }

    private void SearchBox_TextChanged(
        object sender,
        TextChangedEventArgs e)
    {
        string query =
            SearchBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(
                query))
        {
            DisplayEvents(_events);
            return;
        }

        var filtered =
            _events.Where(
                x =>
                    Contains(
                        x.Title,
                        query)
                    ||
                    Contains(
                        x.EventType,
                        query)
                    ||
                    Contains(
                        x.Location,
                        query)
                    ||
                    Contains(
                        x.Faction,
                        query)
                    ||
                    Contains(
                        x.Characters,
                        query)
                    ||
                    Contains(
                        x.Objective,
                        query)
                    ||
                    Contains(
                        x.Conditions,
                        query)
                    ||
                    Contains(
                        x.Reward,
                        query)
                    ||
                    Contains(
                        x.Extra,
                        query)
                    ||
                    Contains(
                        x.GeneratedText,
                        query));

        DisplayEvents(filtered);
    }

    private static bool Contains(
        string? text,
        string query)
    {
        return !string.IsNullOrWhiteSpace(text)
            &&
            text.Contains(
                query,
                StringComparison.OrdinalIgnoreCase);
    }

    private void Refresh_Click(
        object sender,
        RoutedEventArgs e)
    {
        LoadEvents();
    }

    private void Back_Click(
        object sender,
        RoutedEventArgs e)
    {
        BackRequested?.Invoke();
    }
}
