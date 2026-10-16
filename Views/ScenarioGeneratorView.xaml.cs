using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using SkyrimRpEncyclopedia.Models;
using SkyrimRpEncyclopedia.Services;

namespace SkyrimRpEncyclopedia.Views;

public partial class ScenarioGeneratorView : UserControl
{
    private readonly ScenarioGeneratorService _generator;
    private readonly IScenarioKnowledgeCatalog _knowledgeCatalog;
    private readonly ScenarioCanonicalAnchorSelector _anchorSelector;
    private readonly ScenarioExportService _scenarioExport = new();
    private readonly Dictionary<int, ScenarioCanonicalAnchorStatus> _anchorStatuses = new();
    private int _knowledgeOperationVersion;
    private readonly ScenarioGeneratorState _state = new();
    private int? _lastSeed => _state.LastSeed;
    private GeneratedScenario? _currentScenario => _state.Current;
    private IReadOnlyList<ScenarioCanonicalAnchor> _candidateAnchors = Array.Empty<ScenarioCanonicalAnchor>();
    private string? _candidateTemplateId;

    public ScenarioGeneratorView(
        ScenarioGeneratorService generator,
        IScenarioKnowledgeCatalog? knowledgeCatalog = null)
    {
        InitializeComponent();
        _generator = generator;
        _knowledgeCatalog = knowledgeCatalog ?? new ScenarioKnowledgeCatalog();
        _anchorSelector = new ScenarioCanonicalAnchorSelector(_knowledgeCatalog);
        GenreBox.ItemsSource = _generator.GetTemplates().Select(template => template.Genre).Distinct().ToArray();
        ParticipantsBox.ItemsSource = new[] { 2, 3, 4, 5, 6 };
        DurationBox.ItemsSource = new[] { 45, 60, 90, 120, 150, 180, 240 };
        ToneBox.ItemsSource = new[] { "Напряжённый", "Мрачный", "Дипломатичный", "Приключенческий" };
        DifficultyBox.ItemsSource = new[] { "Низкая", "Средняя", "Высокая" };
        GenreBox.SelectedIndex = 0; ParticipantsBox.SelectedItem = 3; DurationBox.SelectedItem = 90;
        ToneBox.SelectedIndex = 0; DifficultyBox.SelectedItem = "Средняя";
        KnowledgeTypeBox.ItemsSource = new[] { "Любой", "NPC", "Place", "Faction", "Quest", "Item", "Weapon", "Armor", "Creature", "Race", "Spell", "Skill", "Book", "Deity", "Concept" };
        KnowledgeTypeBox.SelectedIndex = 0;
        KnowledgeStatusText.Text = _knowledgeCatalog.GetStatus().Diagnostic;
    }

    private void Generate_Click(object sender, RoutedEventArgs e) => Generate(null);
    private void Replay_Click(object sender, RoutedEventArgs e)
    {
        if (_currentScenario is null || !_lastSeed.HasValue)
        {
            ScenarioStatusText.Text = "Сначала создайте сценарий или откройте JSON с сохранённым seed.";
            return;
        }
        Generate(_lastSeed);
    }

    private void CopyScenario_Click(object sender, RoutedEventArgs e)
    {
        if (_currentScenario is null)
        {
            ScenarioStatusText.Text = "Сначала сгенерируйте или откройте сценарий.";
            return;
        }

        try
        {
            CopyToClipboard(ScenarioTextFormatter.FormatScenario(_currentScenario), "Сценарий скопирован с заголовками и переносами строк.");
        }
        catch (Exception exception)
        {
            ScenarioStatusText.Text = "Не удалось скопировать сценарий: " + exception.Message;
        }
    }

    private void ExportScenario_Click(object sender, RoutedEventArgs e)
    {
        if (_currentScenario is null)
        {
            ScenarioStatusText.Text = "Сначала сгенерируйте или откройте сценарий.";
            return;
        }

        var dialog = new SaveFileDialog
        {
            Filter = "Сценарий Skyrim RP (*.json)|*.json|Все файлы (*.*)|*.*",
            DefaultExt = ".json",
            FileName = "skyrim-rp-scenario.json"
        };
        if (dialog.ShowDialog() != true)
            return;

        var result = _scenarioExport.Save(dialog.FileName, _currentScenario);
        ScenarioStatusText.Text = result.Diagnostic;
    }

    private void ImportScenario_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Сценарий Skyrim RP (*.json)|*.json|Все файлы (*.*)|*.*",
            Multiselect = false
        };
        if (dialog.ShowDialog() != true)
            return;

        var result = _scenarioExport.Load(dialog.FileName);
        if (!result.IsSuccess || result.Scenario is null)
        {
            ScenarioStatusText.Text = result.Diagnostic;
            return;
        }

        var loaded = result.Scenario;
        var fields = result.SavedOptionFields;
        if (fields.Contains("Genre", StringComparer.OrdinalIgnoreCase)) SetSavedSelection(GenreBox, loaded.Options.Genre);
        if (fields.Contains("ParticipantCount", StringComparer.OrdinalIgnoreCase)) SetSavedSelection(ParticipantsBox, loaded.Options.ParticipantCount);
        if (fields.Contains("DurationMinutes", StringComparer.OrdinalIgnoreCase)) SetSavedSelection(DurationBox, loaded.Options.DurationMinutes);
        if (fields.Contains("Tone", StringComparer.OrdinalIgnoreCase)) SetSavedSelection(ToneBox, loaded.Options.Tone);
        if (fields.Contains("Difficulty", StringComparer.OrdinalIgnoreCase)) SetSavedSelection(DifficultyBox, loaded.Options.Difficulty);
        SeedBox.Text = result.HasSavedSeed ? loaded.Seed.ToString() :
            fields.Contains("Seed", StringComparer.OrdinalIgnoreCase) ? loaded.Options.Seed.ToString() : "";
        ResetCanonicalCandidateState();
        _state.TryAcceptLoad(result);
        RenderScenario(loaded);
        ScenarioStatusText.Text += " " + result.Diagnostic + (_lastSeed.HasValue ? "" : " Не все параметры сохранены: повтор по seed недоступен.");
    }

    private void GenreBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _state.Clear();
        ResultPanel.Children.Clear();
        SeedBox.Text = "";
        ResetCanonicalCandidateState();
        ScenarioStatusText.Text = "Жанр изменён: создайте новый сценарий.";
    }

    private static void SetSavedSelection(ComboBox box, object value)
    {
        if (!box.Items.Contains(value))
            box.ItemsSource = box.Items.Cast<object>().Append(value).ToArray();
        box.SelectedItem = value;
    }

    private async void SearchKnowledge_Click(object sender, RoutedEventArgs e)
    {
        var operationVersion = ++_knowledgeOperationVersion;
        KnowledgeResultsPanel.Children.Clear();
        KnowledgeStatusText.Text = "Поиск канонических кандидатов…";
        var selectedType = KnowledgeTypeBox.SelectedItem?.ToString();
        var request = new ScenarioKnowledgeSearchRequest
        {
            SemanticType = selectedType == "Любой" ? "" : selectedType ?? "",
            Category = KnowledgeCategoryBox.Text,
            Title = KnowledgeTitleBox.Text,
            IncludeNeedsReview = IncludeNeedsReviewBox.IsChecked == true,
            Limit = 12
        };
        ScenarioKnowledgeSearchResult result;
        try
        {
            result = await Task.Run(() => _knowledgeCatalog.Search(request));
        }
        catch (Exception exception)
        {
            if (operationVersion == _knowledgeOperationVersion)
                KnowledgeStatusText.Text = "Не удалось выполнить поиск канонических кандидатов: " + exception.Message;
            return;
        }
        if (operationVersion != _knowledgeOperationVersion)
            return;
        KnowledgeStatusText.Text = result.Status.IsCompatible
            ? result.Candidates.Count == 0 ? "Подходящих проверенных кандидатов не найдено." : $"Найдено кандидатов: {result.Candidates.Count}."
            : result.Status.Diagnostic;
        foreach (var candidate in result.Candidates)
        {
            var review = candidate.NeedsReview ? " — требует проверки" : "";
            var categories = candidate.Categories.Count == 0 ? "" : $"\nКатегории: {string.Join(", ", candidate.Categories)}";
            var source = string.IsNullOrWhiteSpace(candidate.SourceUrl) ? "Источник не указан." : candidate.SourceUrl;
            KnowledgeResultsPanel.Children.Add(CreateSelectableText(
                $"{candidate.Title} — {candidate.SemanticType}{review}\nИсточник: {source}{categories}\n{candidate.ContextExcerpt}"));
        }
    }

    private async void SelectCanonicalAnchors_Click(object sender, RoutedEventArgs e)
    {
        var template = GetSelectedTemplate();
        if (template is null)
        {
            KnowledgeStatusText.Text = "Выберите существующий каркас сценария.";
            return;
        }

        var operationVersion = ++_knowledgeOperationVersion;
        KnowledgeStatusText.Text = "Подбор канонических опор…";
        ScenarioCanonicalSelectionResult result;
        try
        {
            result = await Task.Run(() => _anchorSelector.FindCandidates(template.Id));
        }
        catch (Exception exception)
        {
            if (operationVersion == _knowledgeOperationVersion)
                KnowledgeStatusText.Text = "Не удалось подобрать канонические опоры: " + exception.Message;
            return;
        }
        if (operationVersion != _knowledgeOperationVersion)
            return;
        LoadCanonicalCandidates(template.Id, result);
    }

    private async void SuggestCanonicalAnchors_Click(object sender, RoutedEventArgs e)
    {
        var template = GetSelectedTemplate();
        if (template is null)
        {
            KnowledgeStatusText.Text = "Выберите существующий каркас сценария.";
            return;
        }

        var seed = int.TryParse(SeedBox.Text, out var parsed) ? parsed : _lastSeed ?? 0;
        SeedBox.Text = seed.ToString();
        var operationVersion = ++_knowledgeOperationVersion;
        KnowledgeStatusText.Text = "Подбор предложений по seed…";
        ScenarioCanonicalSelectionResult result;
        try
        {
            result = await Task.Run(() => _anchorSelector.SuggestCandidates(template.Id, seed));
        }
        catch (Exception exception)
        {
            if (operationVersion == _knowledgeOperationVersion)
                KnowledgeStatusText.Text = "Не удалось предложить канонические опоры: " + exception.Message;
            return;
        }
        if (operationVersion != _knowledgeOperationVersion)
            return;
        LoadCanonicalCandidates(template.Id, result);
    }

    private void ApplyCanonicalAnchors_Click(object sender, RoutedEventArgs e)
    {
        if (_currentScenario is null)
        {
            KnowledgeStatusText.Text = "Сначала сгенерируйте сценарий, затем подтвердите подходящие опоры.";
            return;
        }
        if (!string.Equals(_candidateTemplateId, _currentScenario.Template.Id, StringComparison.Ordinal))
        {
            KnowledgeStatusText.Text = "Подбор относится к другому каркасу; выполните его заново перед применением.";
            return;
        }

        var selected = _candidateAnchors
            .Where(anchor => _anchorStatuses.TryGetValue(anchor.EntityId, out var status) && status == ScenarioCanonicalAnchorStatus.Selected)
            .Select(anchor => ScenarioCanonicalAnchorSelector.WithStatus(anchor, ScenarioCanonicalAnchorStatus.Selected))
            .ToArray();
        _state.UpdateCurrent(ScenarioCanonicalAnchorApplicator.Apply(_currentScenario, selected));
        RenderScenario(_currentScenario);
        KnowledgeStatusText.Text = selected.Length == 0
            ? "Выбранных опор нет: сценарий остаётся автономным."
            : $"Подтверждено канонических справочных опор: {selected.Length}. Они не меняют фабричный сюжет.";
    }

    private void Generate(int? forcedSeed)
    {
        var template = GetSelectedTemplate();
        if (template is null)
        {
            _state.Clear();
            ResetCanonicalCandidateState();
            ResultPanel.Children.Clear();
            ScenarioStatusText.Text = "Выберите один из доступных жанров перед генерацией. Неизвестный выбор не будет заменён переговорным сценарием.";
            return;
        }

        int seed = forcedSeed ?? (int.TryParse(SeedBox.Text, out var parsed) ? parsed : Random.Shared.Next());
        SeedBox.Text = seed.ToString();
        var options = new ScenarioGenerationOptions
        {
            Genre = template.Genre,
            ParticipantCount = (int)(ParticipantsBox.SelectedItem ?? 3),
            DurationMinutes = (int)(DurationBox.SelectedItem ?? 90),
            Tone = ToneBox.SelectedItem?.ToString() ?? "Напряжённый",
            Difficulty = DifficultyBox.SelectedItem?.ToString() ?? "Средняя",
            Seed = seed,
            AllowMandatoryCombat = false,
        };
        var replayLegacy = forcedSeed.HasValue && _currentScenario?.Template.Id == "negotiation" &&
            string.IsNullOrWhiteSpace(_currentScenario.NegotiationBlueprintId);
        var result = replayLegacy ? _generator.GenerateLegacyNegotiation(options) : _generator.Generate(options);
        if (result.Scenario is null)
        {
            _state.Clear();
            ResetCanonicalCandidateState();
            ResultPanel.Children.Clear();
            ScenarioStatusText.Text = string.Join(" ", result.Validation.Errors.Concat([result.Diagnostic]).Where(text => !string.IsNullOrWhiteSpace(text)));
            return;
        }
        ResetCanonicalCandidateState("Сценарий сгенерирован заново: подберите и подтвердите опоры для этого результата отдельно.");
        _state.Accept(result.Scenario);
        RenderScenario(result.Scenario);
    }

    private void RenderScenario(GeneratedScenario scenario)
    {
        ResultPanel.Children.Clear();
        ScenarioStatusText.Text = $"Каркас: {ScenarioTextFormatter.GetDisplayName(scenario)}. Seed: {scenario.Seed}. Проверка пройдена." +
                                  (string.IsNullOrWhiteSpace(scenario.GenerationDiagnostic) ? "" : " " + scenario.GenerationDiagnostic);
        AddText(scenario.Title, 22, true);
        ResultPanel.Children.Add(CreateCopyableBlock("Описание сценария", $"Завязка: {scenario.Hook}\nПублично известно: {scenario.PublicBriefing}\nПричина конфликта: {scenario.ConflictCause}"));
        AddSection("Вариант фабулы", BlueprintDescriptions(scenario));
        AddSection("Исходные факты", scenario.KnownFacts);
        AddSection("Происхождение фактов", scenario.NarrativeFacts.Select(FormatNarrativeFact));
        AddSection("Канонические справочные опоры", scenario.CanonicalAnchors.Select(anchor => $"{anchor.Title} — {anchor.SemanticType}\nСтатус: {StatusText(anchor.Status)}\nНазначение: {anchor.Purpose}\nИсточник: {anchor.SourceUrl}\n{anchor.ContextExcerpt}\nЭто справочный материал, а не обязательный сюжетный факт."));
        AddSection("Версии расследования", scenario.Hypotheses.Select(hypothesis => $"{hypothesis.Statement}\nКому выгодно: {hypothesis.Beneficiary}; цена: {hypothesis.Cost}\nНеизвестно: {hypothesis.Unknown}"));
        AddSection("Коалиции и уступки", scenario.Coalitions.Select(coalition => $"{coalition.Name}\nУчастники: {string.Join(", ", coalition.Members)}\nУступка: {coalition.Concession}\nЦена: {coalition.Cost}\n{FormatClueRequirements("Обязательные улики", coalition.RequiredClueIds, scenario.Clues)}"));
        AddSection("Персональные карточки ролей", scenario.Characters.Select(role => $"{role.Name} — {role.Role}\nЦель: {role.Goal}\nМотив: {role.Motive}\nПублично: {role.PublicInformation}\nЛично: {role.PersonalInformation}\nГотов принять: {role.Concession}\nКрасная линия: {role.RedLine}"));
        AddSection("Улики", scenario.Clues.Select(clue => $"{clue.Text}\nФакт: {clue.KnownFact}; раскрывает: {clue.Reveals}"));
        AddSection("Сцены", scenario.Scenes.Select(scene => $"{scene.Title}: {scene.Purpose}\n{FormatClueRequirements("Связанные улики", scene.ClueIds, scenario.Clues)}"));
        AddSection("Ветвления", scenario.Branches.Select(branch => $"{branch.FromSceneId} → {(string.IsNullOrWhiteSpace(branch.ToEndingId) ? branch.ToSceneId : "финал: " + branch.ToEndingId)}\nДействие: {branch.Decision}\nУсловие: {branch.Condition}{(branch.RequiredClueIds.Count == 0 ? "" : "\nНужные улики: " + string.Join(", ", branch.RequiredClueIds))}"));
        AddSection("Финалы", scenario.Endings.Select(ending => $"{ending.Title}: {ending.Outcome}\nУсловия достижения: {FormatEndingConditions(ending, scenario)}"));
        AddSection("Только для ведущего", scenario.GmNotes.Concat(scenario.Characters.Where(role => !string.IsNullOrWhiteSpace(role.Secret)).Select(role => $"{role.Name}: {role.Secret}")));
    }

    private ScenarioTemplate? GetSelectedTemplate() => _generator.GetTemplateByGenre(GenreBox.SelectedItem as string);

    private void LoadCanonicalCandidates(string templateId, ScenarioCanonicalSelectionResult result)
    {
        _candidateTemplateId = templateId;
        _candidateAnchors = result.Anchors;
        _anchorStatuses.Clear();
        foreach (var anchor in _candidateAnchors) _anchorStatuses[anchor.EntityId] = anchor.Status;
        KnowledgeStatusText.Text = result.Diagnostic;
        RenderCanonicalCandidates();
    }

    private void RenderCanonicalCandidates()
    {
        KnowledgeResultsPanel.Children.Clear();
        foreach (var anchor in _candidateAnchors)
        {
            var status = _anchorStatuses.GetValueOrDefault(anchor.EntityId, anchor.Status);
            var categories = anchor.Categories.Count == 0 ? "" : $"\nКатегории: {string.Join(", ", anchor.Categories)}";
            var checkBox = new CheckBox
            {
                IsChecked = status == ScenarioCanonicalAnchorStatus.Selected,
                IsEnabled = status != ScenarioCanonicalAnchorStatus.Rejected,
                Content = $"{StatusText(status)}: {anchor.Title} — {anchor.SemanticType}",
                Margin = new Thickness(0, 4, 0, 2)
            };
            checkBox.Checked += (_, _) => _anchorStatuses[anchor.EntityId] = ScenarioCanonicalAnchorStatus.Selected;
            checkBox.Unchecked += (_, _) => _anchorStatuses[anchor.EntityId] = ScenarioCanonicalAnchorStatus.Candidate;
            var reject = new Button { Content = status == ScenarioCanonicalAnchorStatus.Rejected ? "Отклонено" : "Отклонить", IsEnabled = status != ScenarioCanonicalAnchorStatus.Rejected, Padding = new Thickness(8, 3, 8, 3), Margin = new Thickness(24, 0, 0, 4) };
            reject.Click += (_, _) =>
            {
                _anchorStatuses[anchor.EntityId] = ScenarioCanonicalAnchorStatus.Rejected;
                RenderCanonicalCandidates();
            };
            KnowledgeResultsPanel.Children.Add(checkBox);
            KnowledgeResultsPanel.Children.Add(CreateCopyableBlock(
                "Контекст канонической опоры",
                $"Назначение: {anchor.Purpose}\nИсточник: {anchor.SourceUrl}{categories}\n{anchor.ContextExcerpt}\nКаноническая опора не станет сюжетным фактом без решения ведущего."));
            KnowledgeResultsPanel.Children.Add(reject);
        }
    }

    private void ResetCanonicalCandidateState(string diagnostic = "Подбор канонических опор сброшен: выбран другой каркас.")
    {
        _knowledgeOperationVersion++;
        _candidateTemplateId = null;
        _candidateAnchors = Array.Empty<ScenarioCanonicalAnchor>();
        _anchorStatuses.Clear();
        KnowledgeResultsPanel.Children.Clear();
        KnowledgeStatusText.Text = diagnostic;
    }

    private static string StatusText(ScenarioCanonicalAnchorStatus status) => status switch
    {
        ScenarioCanonicalAnchorStatus.Proposed => "Предложено по seed",
        ScenarioCanonicalAnchorStatus.Selected => "Подтверждено ведущим",
        ScenarioCanonicalAnchorStatus.Rejected => "Отклонено ведущим",
        _ => "Кандидат"
    };

    private static IReadOnlyList<string> BlueprintDescriptions(GeneratedScenario scenario)
    {
        var values = new List<string>();
        if (!string.IsNullOrWhiteSpace(scenario.InvestigationBlueprintId))
            values.Add("Blueprint расследования: " + scenario.InvestigationBlueprintId);
        if (!string.IsNullOrWhiteSpace(scenario.IntrigueBlueprintId))
            values.Add("Blueprint интриги: " + scenario.IntrigueBlueprintId);
        if (!string.IsNullOrWhiteSpace(scenario.MissionBlueprintId))
            values.Add("Blueprint миссии: " + scenario.MissionBlueprintId);
        if (!string.IsNullOrWhiteSpace(scenario.NegotiationBlueprintId))
            values.Add("Blueprint переговоров: " + scenario.NegotiationBlueprintId);
        return values;
    }

    private static string FormatNarrativeFact(ScenarioNarrativeFact fact)
    {
        var source = fact.Origin == ScenarioFactOrigin.CanonicalKnowledge && !string.IsNullOrWhiteSpace(fact.SourceUrl)
            ? "\nИсточник: " + fact.SourceUrl
            : "";
        return $"{FactOriginText(fact.Origin)}: {fact.Text}{source}";
    }

    private static string FormatClueRequirements(string label, IReadOnlyList<string> clueIds, IReadOnlyList<ScenarioClue> clues)
    {
        if (clueIds.Count == 0)
            return label + ": не заданы.";

        var byId = clues.ToDictionary(clue => clue.Id, StringComparer.Ordinal);
        return label + ": " + string.Join("; ", clueIds.Select(id =>
            byId.TryGetValue(id, out var clue) ? $"{id} — {clue.Text}" : id));
    }

    private static string FormatEndingConditions(ScenarioEnding ending, GeneratedScenario scenario)
    {
        var branches = scenario.Branches.Where(branch => branch.ToEndingId == ending.Id).ToArray();
        if (branches.Length == 0)
            return "не заданы в переходах.";

        return string.Join("; ", branches.Select(branch =>
            $"из сцены {branch.FromSceneId}: {branch.Condition}" +
            (branch.RequiredClueIds.Count == 0 ? "" : "; нужны улики: " + string.Join(", ", branch.RequiredClueIds))));
    }

    private static string FactOriginText(ScenarioFactOrigin origin) => origin switch
    {
        ScenarioFactOrigin.CanonicalKnowledge => "Подтверждённый канон",
        ScenarioFactOrigin.GmHypothesis => "Гипотеза ведущего, не подтверждённая истина",
        _ => "Вымышленный исходный факт сценария"
    };

    private void AddSection(string title, IEnumerable<string> items)
    {
        var values = items.Where(item => !string.IsNullOrWhiteSpace(item)).ToArray();
        if (values.Length == 0)
            return;

        var content = new StackPanel();
        foreach (var item in values)
            content.Children.Add(CreateCopyableBlock(title, item));

        ResultPanel.Children.Add(new Expander
        {
            Header = new TextBlock { Text = title, Foreground = System.Windows.Media.Brushes.White, FontWeight = FontWeights.SemiBold, FontSize = 16 },
            IsExpanded = true,
            Content = content,
            Margin = new Thickness(0, 8, 0, 0)
        });
    }

    private void AddText(string text, double fontSize = 15, bool bold = false) =>
        ResultPanel.Children.Add(CreateSelectableText(text, fontSize, bold));

    private TextBox CreateSelectableText(string text, double fontSize = 15, bool bold = false) => new()
    {
        Style = (Style)FindResource("ScenarioReadableText"),
        Text = text,
        FontSize = fontSize,
        FontWeight = bold ? FontWeights.Bold : FontWeights.Normal
    };

    private StackPanel CreateCopyableBlock(string label, string text)
    {
        var panel = new StackPanel { Margin = new Thickness(0, 3, 0, 7) };
        var header = new DockPanel();
        var copyButton = new Button { Style = (Style)FindResource("ScenarioCopyButton") };
        copyButton.Click += (_, _) => CopyToClipboard(text, $"Блок «{label}» скопирован.");
        DockPanel.SetDock(copyButton, Dock.Right);
        header.Children.Add(copyButton);
        header.Children.Add(new TextBlock
        {
            Text = label,
            Style = (Style)FindResource("ScenarioMetaText"),
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center
        });
        panel.Children.Add(header);
        panel.Children.Add(CreateSelectableText(text));
        return panel;
    }

    private void CopyToClipboard(string text, string successMessage)
    {
        try
        {
            Clipboard.SetText(text);
            ScenarioStatusText.Text = successMessage;
        }
        catch (Exception exception)
        {
            ScenarioStatusText.Text = "Не удалось скопировать текст: " + exception.Message;
        }
    }
}
