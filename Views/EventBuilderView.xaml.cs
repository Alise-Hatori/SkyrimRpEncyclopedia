using System.Windows;
using System.Windows.Controls;
using SkyrimRpEncyclopedia.Models;
using SkyrimRpEncyclopedia.Services;

namespace SkyrimRpEncyclopedia.Views;

public partial class EventBuilderView : UserControl
{
    private readonly DatabaseService _database;
    private readonly RpEventParameterService _parameterService;
    private readonly Dictionary<string, RpParameterField> _fields;
    private readonly Dictionary<string, string> _lastValues = new();
    private RpEvent? _editingEvent;
    private readonly Action<string> _notify;
    private readonly Action<Exception> _reportError;
    private readonly Action<string> _copy;

    public event Action? BackRequested;

    public EventBuilderView(DatabaseService database, RpEvent? editingEvent = null, Action<string>? notify=null, Action<Exception>? reportError=null, Action<string>? copy=null)
    {
        InitializeComponent();
        _database = database;
        _notify=notify??(message=>MessageBox.Show(message,"Конструктор ивентов",MessageBoxButton.OK,MessageBoxImage.Information));
        _reportError=reportError??(error=>UserDataErrors.Report("Event.Operation.Failed",error,Window.GetWindow(this)));
        _copy=copy??Clipboard.SetText;
        _editingEvent = editingEvent;
        _parameterService = new RpEventParameterService();
        _fields = _parameterService.CreateFields().ToDictionary(field => field.Key);
        ParametersList.ItemsSource = _fields.Values;

        if (_editingEvent is not null)
        {
            LoadEventForEditing(_editingEvent);
            SaveButton.Content="Обновить сохранённый ивент";
        }
    }

    private void Generate_Click(object sender, RoutedEventArgs e)
    {
        GenerateEvent();
    }

    private void RerollParameter_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not RpParameterField field)
            return;

        _parameterService.Reroll(field);
        GenerateEvent();
    }

    private void GenerateEvent()
    {
        _lastValues.Clear();
        foreach (var field in _fields.Values)
            _lastValues[field.Key] = _parameterService.Resolve(field);

        var eventType = Value("eventType");
        var location = Value("location");
        var faction = Value("faction");
        var characters = Value("characters");
        var objective = Value("objective");
        var conditions = Value("conditions");
        var reward = Value("reward");
        var extra = Value("extra");

        var lines = new List<string>
        {
            $"**{eventType}**",
            "",
            $"📍 **Место:** {location}"
        };

        AddOptional(lines, "⚔ **Фракция:**", faction);
        AddOptional(lines, "👤 **Участники:**", characters);
        lines.Add("");
        lines.Add($"🎯 **Цель:** {objective}");
        AddOptional(lines, "📜 **Условия:**", conditions);
        AddOptional(lines, "🎁 **Награда:**", reward);
        AddOptional(lines, "📝 **Дополнительно:**", extra);
        lines.Add("");
        lines.Add("━━━━━━━━━━━━━━━━━━━━");
        lines.Add("");
        lines.Add("📖 **Описание события**");
        lines.Add("");
        lines.Add(GenerateNarrative(eventType, location, faction, characters, objective, conditions));

        if (_fields.Values.Any(field => field.IsCustomValue))
        {
            lines.Add("");
            lines.Add("Примечание: пользовательские параметры являются игровым вводом и не подтверждают канонические факты лора.");
        }

        ResultBox.Text = string.Join(Environment.NewLine, lines);
    }

    private static void AddOptional(List<string> lines, string label, string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            lines.Add($"{label} {value}");
    }

    private static string GenerateNarrative(string eventType, string location, string faction, string characters, string objective, string conditions)
    {
        var participants = string.IsNullOrWhiteSpace(characters) ? "участники события" : characters;
        var factionText = string.IsNullOrWhiteSpace(faction) ? "" : $" Событие связано с фракцией «{faction}».";
        var conditionsText = string.IsNullOrWhiteSpace(conditions) ? "" : $" При этом необходимо учитывать следующие условия: {conditions}.";
        return $"В районе «{location}» начинается событие типа «{eventType}». В нём принимают участие {participants}.{factionText} Основная задача — {objective}.{conditionsText} Подробности развития ситуации определяются непосредственно в ходе ролевой игры.";
    }

    private string Value(string key) => _lastValues.TryGetValue(key, out var value) ? value : string.Empty;

    private void LoadEventForEditing(RpEvent rpEvent)
    {
        SetFieldValue("eventType", rpEvent.EventType);
        SetFieldValue("location", rpEvent.Location);
        SetFieldValue("faction", rpEvent.Faction);
        SetFieldValue("characters", rpEvent.Characters);
        SetFieldValue("objective", rpEvent.Objective);
        SetFieldValue("conditions", rpEvent.Conditions);
        SetFieldValue("reward", rpEvent.Reward);
        SetFieldValue("extra", rpEvent.Extra);
        ResultBox.Text = rpEvent.GeneratedText;
    }

    private void SetFieldValue(string key, string value)
    {
        _lastValues[key] = value;
        var field = _fields[key];
        field.Value = string.IsNullOrWhiteSpace(value) && field.IsOptional
            ? RpParameterField.UnspecifiedValue
            : value;
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(ResultBox.Text))
        {
            _notify("Сначала сгенерируйте событие.");
            return;
        }

        try { _copy(ResultBox.Text); }
        catch(Exception error) { _reportError(error);return; }
        _notify("RP-текст скопирован в буфер обмена.");
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(ResultBox.Text) || _lastValues.Count == 0)
        {
            _notify("Сначала сгенерируйте текст ивента.");
            return;
        }

        var eventType = Value("eventType");
        var location = Value("location");
        var title = string.IsNullOrWhiteSpace(location) ? $"Ивент — {eventType}" : $"Ивент — {eventType} в {location}";

        var candidate=new RpEvent { Id=_editingEvent?.Id??0,Title=title,EventType=eventType,Location=location,
            Faction=Value("faction"),Characters=Value("characters"),Objective=Value("objective"),Conditions=Value("conditions"),Reward=Value("reward"),Extra=Value("extra"),
            GeneratedText=ResultBox.Text,CreatedAt=_editingEvent?.CreatedAt??DateTime.Now,UpdatedAt=DateTime.Now };
        try
        {
            if(_editingEvent is null) candidate.Id=_database.AddRpEvent(candidate); else _database.UpdateRpEvent(candidate);
            _editingEvent=candidate; SaveButton.Content="Обновить сохранённый ивент";
        }
        catch(Exception error) { _reportError(error);return; }

        _notify("Ивент сохранён.");
    }

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        foreach (var field in _fields.Values)
            field.Value = RpParameterField.RandomValue;

        _lastValues.Clear();
        ResultBox.Clear();
        _editingEvent=null; SaveButton.Content="Сохранить ивент";
    }

    private void Back_Click(object sender, RoutedEventArgs e)
    {
        BackRequested?.Invoke();
    }
}
