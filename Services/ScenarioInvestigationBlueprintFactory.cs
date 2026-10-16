using SkyrimRpEncyclopedia.Models;

namespace SkyrimRpEncyclopedia.Services;

/// <summary>
/// Small, deterministic pilot for investigation scenarios. Knowledge DB
/// candidates remain proposed reference anchors; this class never infers a
/// motive, relationship or wrongdoing from an encyclopaedia relation.
/// </summary>
public sealed class ScenarioInvestigationBlueprintFactory
{
    public static IReadOnlyList<string> BlueprintIds { get; } =
        ["lost-property", "conflicting-record", "access-dispute"];

    private readonly ScenarioCanonicalAnchorSelector? _anchorSelector;

    public ScenarioInvestigationBlueprintFactory(IScenarioKnowledgeCatalog? catalog = null)
    {
        _anchorSelector = catalog is null ? null : new ScenarioCanonicalAnchorSelector(catalog);
    }

    public GeneratedScenario Build(ScenarioGenerationOptions options, ScenarioTemplate template)
    {
        var blueprintId = BlueprintIds[PositiveModulo(StableSeed(options.Seed, "investigation-blueprint"), BlueprintIds.Count)];
        var anchors = _anchorSelector?.SuggestCandidates(template.Id, options.Seed).Anchors ?? [];
        return blueprintId switch
        {
            "lost-property" => BuildLostProperty(options, template, anchors),
            "conflicting-record" => BuildConflictingRecord(options, template, anchors),
            "access-dispute" => BuildAccessDispute(options, template, anchors),
            _ => throw new InvalidOperationException("Unknown investigation blueprint: " + blueprintId)
        };
    }

    private static GeneratedScenario BuildLostProperty(ScenarioGenerationOptions o, ScenarioTemplate t, IReadOnlyList<ScenarioCanonicalAnchor> anchors)
    {
        const string id = "lost-property";
        var clues = new[]
        {
            Clue("cabinet-scratch", "На внутренней кромке шкафа свежая царапина от ключа.", "к шкафу обращались после вечерней описи", "версия о простом недосмотре ослабевает", ["caretaker"], ["mistake"]),
            Clue("shelf-dust", "На пустом месте в пыли виден ровный прямоугольник, а не след поспешного выноса.", "документ сняли сознательно и аккуратно", "искали именно этот предмет", ["caretaker", "protection"], ["mistake"]),
            Clue("delivery-slip", "В журнале приёма есть отметка о передаче копии на хранение до рассвета.", "часть сведений могла быть сохранена законно", "появляется версия защитного изъятия", ["protection"], ["caretaker"]),
            Clue("market-note", "У смотрителя найдено письмо о срочном платеже, но не запись о продаже документа.", "у смотрителя есть давление, но не доказан мотив кражи", "подозрение требует проверки, а не обвинения", ["caretaker"], [])
        };
        return BuildScenario(o, t, id, "Пропавшая опись", "Из закрытого шкафа исчезла опись спорного имущества; без неё нельзя завершить передачу до рассвета.",
            "Вечерняя опись лежала в шкафу, утром её нет, а передача имущества остановлена.",
            "Авторская истина ведущего: служащая убрала копию на временное хранение, опасаясь немедленного несправедливого изъятия; смотритель действительно скрывает личные долги, но документ не продавал.",
            new[]
            {
                Hypothesis("mistake", "Опись потеряли при обычной вечерней работе.", "Никому", "передача затягивается", "Почему шкаф запирали после описи?"),
                Hypothesis("caretaker", "Смотритель вынес опись ради денег.", "Смотритель", "невиновного можно обвинить по его долгам", "Есть ли след продажи или только давление?"),
                Hypothesis("protection", "Служащая спрятала копию, чтобы остановить спорное изъятие.", "Люди, чьи вещи должны передать", "она нарушила порядок", "Где хранится копия и можно ли проверить её содержание?")
            }, clues,
            new[]
            {
                Scene("opening", "Пустой шкаф", "Игроки решают, сохранять ли молчание до проверки или открыть спор немедленно."),
                Scene("records", "Журнал приёма", "Следы описи и отметки о передаче позволяют отделить факт исчезновения от слухов.", ["cabinet-scratch", "shelf-dust"]),
                Scene("caretaker", "Разговор со смотрителем", "Долги смотрителя дают мотив, но не заменяют доказательство.", ["market-note"]),
                Scene("storage", "Временное хранение", "Запись в журнале позволяет проверить версию защитного изъятия.", ["delivery-slip"]),
                Scene("hearing", "Решение до рассвета", "Герои выбирают обвинение, восстановление записи или осторожную проверку.")
            },
            new[]
            {
                Branch("opening", "records", "Осмотреть шкаф и журналы", "Игроки начинают с материальных следов."),
                Branch("opening", "caretaker", "Спросить смотрителя", "Игроки сначала проверяют личное давление."),
                Branch("records", "storage", "Проверить отметку о хранении", "В журнале найден след передачи.", ["shelf-dust"]),
                Branch("records", "hearing", "Вынести вопрос на разбор", "Собраны первоначальные факты."),
                Branch("caretaker", "records", "Сверить слова с записью", "Подозрение проверяют документами."),
                Branch("storage", "hearing", "Вернуться с найденной копией", "Игроки могут сопоставить копию и порядок передачи.", ["delivery-slip"]),
                EndingBranch("hearing", "prove-protection", "Подтвердить защитное изъятие", "Есть аккуратный след снятия и запись о хранении.", ["shelf-dust", "delivery-slip"]),
                EndingBranch("hearing", "charge-caretaker", "Обвинить смотрителя", "Игроки считают царапину и долги достаточными.", ["cabinet-scratch", "market-note"]),
                EndingBranch("hearing", "restore-process", "Восстановить передачу под надзором", "Стороны признают нарушение порядка, но избегают поспешного обвинения."),
                EndingBranch("hearing", "defer-review", "Отложить решение", "Дело передают независимому хранителю до утра.")
            }, anchors);
    }

    private static GeneratedScenario BuildConflictingRecord(ScenarioGenerationOptions o, ScenarioTemplate t, IReadOnlyList<ScenarioCanonicalAnchor> anchors)
    {
        const string id = "conflicting-record";
        var clues = new[]
        {
            Clue("ink-order", "Две записи сделаны разными чернилами, но обе поверх старой даты.", "книга менялась более одного раза", "нельзя свести спор к одному виновнику", ["clerk", "correction"], ["forgery"]),
            Clue("copy-stamp", "На архивной копии стоит поздний штамп приёмки без исправленной строки.", "первая запись уже существовала до исправления", "часть расхождения могла возникнуть из-за законной поправки", ["correction"], ["forgery"]),
            Clue("guard-list", "Список дежурств подтверждает доступ посыльного к книге в ночь перед спором.", "посыльный мог видеть книгу", "возможность не равна доказательству подделки", ["forgery"], []),
            Clue("witness-letter", "Письмо старосты просит писаря исправить ошибочный номер до выдачи пропуска.", "у писаря был открытый служебный повод менять запись", "исправление могло быть добросовестным", ["correction"], ["clerk"])
        };
        return BuildScenario(o, t, id, "Две строки в книге", "Пропуск выдан по двум противоречивым строкам журнала, и спор о праве прохода грозит сорвать смену караула.",
            "В журнале есть две несовпадающие записи; одна сторона уже требует назвать фальсификатора.",
            "Авторская истина ведущего: писарь исправил старую ошибку по письму старосты, а посыльный позднее попытался выдать эту правку за подлог, чтобы скрыть собственную просрочку.",
            new[]
            {
                Hypothesis("clerk", "Писарь тайно подделал строку ради выгоды одной стороны.", "Писарь или её покровитель", "служебная ошибка превращается в обвинение", "Есть ли след личной выгоды?"),
                Hypothesis("forgery", "Посыльный сам испортил книгу, чтобы скрыть просрочку.", "Посыльный", "подозрение основано лишь на доступе", "Когда именно появилась вторая строка?"),
                Hypothesis("correction", "Одна строка — законная, но плохо оформленная правка старой ошибки.", "Стороны, которым нужен точный пропуск", "формальность была нарушена", "Подтверждает ли это архивная копия?" )
            }, clues,
            new[]
            {
                Scene("opening", "Спорный пропуск", "Игроки удерживают стороны от немедленного решения и выбирают, что проверять первым."),
                Scene("ledger", "Слои чернил", "Осмотр книги показывает, что у расхождения несколько моментов появления.", ["ink-order"]),
                Scene("archive", "Архивная копия", "Штамп и ранняя копия позволяют сверить хронологию.", ["copy-stamp"]),
                Scene("watch", "Смена караула", "Список дежурств и письмо старосты раскрывают доступ и законный повод для правки.", ["guard-list", "witness-letter"]),
                Scene("ruling", "Оформление решения", "Герои выбирают формальную правку, обвинение или временный порядок доступа.")
            },
            new[]
            {
                Branch("opening", "ledger", "Осмотреть книгу", "Игроки начинают с материального документа."),
                Branch("opening", "watch", "Проверить доступ", "Игроки сначала выясняют, кто мог видеть книгу."),
                Branch("ledger", "archive", "Сверить с архивом", "Разные чернила требуют хронологии.", ["ink-order"]),
                Branch("watch", "archive", "Проверить письмо и копию", "Служебный повод сверяют с архивом.", ["witness-letter"]),
                Branch("archive", "ruling", "Подготовить решение", "Игроки собрали подтверждаемую последовательность.", ["copy-stamp"]),
                Branch("watch", "ruling", "Ввести временный порядок", "Смена не может ждать полной проверки."),
                EndingBranch("ruling", "formal-correction", "Признать и оформить правку", "Архив и письмо объясняют исправление.", ["copy-stamp", "witness-letter"]),
                EndingBranch("ruling", "charge-messenger", "Обвинить посыльного", "Игроки считают доступ к книге достаточным основанием.", ["guard-list"]),
                EndingBranch("ruling", "charge-clerk", "Обвинить писаря", "Игроки считают разные чернила доказательством тайной подделки.", ["ink-order"]),
                EndingBranch("ruling", "temporary-pass", "Выдать временный пропуск", "Полную проверку откладывают, сохраняя смену караула.")
            }, anchors);
    }

    private static GeneratedScenario BuildAccessDispute(ScenarioGenerationOptions o, ScenarioTemplate t, IReadOnlyList<ScenarioCanonicalAnchor> anchors)
    {
        const string id = "access-dispute";
        var clues = new[]
        {
            Clue("old-charter", "Старая грамота даёт общине доступ в сумерки, но не описывает военное положение.", "право доступа существует, однако границы спорны", "обе стороны опираются на часть текста", ["community-right"], ["guard-order"]),
            Clue("new-order", "Новый приказ караула ограничивает проход после тревоги, но не содержит подписи старшего начальника.", "приказ мог быть временной мерой", "полномочия караула не доказаны полностью", ["guard-order"], ["community-right"]),
            Clue("supply-seal", "Печать на корзинах показывает, что груз предназначен не для торговли, а для лечебницы.", "задержка затрагивает больных", "появляется путь к пропорциональному решению", ["emergency"], []),
            Clue("patrol-note", "Записка патруля подтверждает тревогу у другого входа, но не угрозу у спорных ворот.", "опасность реальна, но её масштаб неизвестен", "ни запрет, ни свободный проход не следуют автоматически", ["guard-order", "community-right", "emergency"], [])
        };
        return BuildScenario(o, t, id, "Ворота после тревоги", "Караул закрыл проход к лечебнице, а община требует пропустить груз до ночи.",
            "У ворот стоят люди с запечатанными корзинами; приказ караула и старая грамота противоречат друг другу.",
            "Авторская истина ведущего: караул получил реальное, но неполное предупреждение. Начальник смены превысил полномочия из страха ошибки; община везёт помощь, однако старая грамота не отменяет разумные меры безопасности.",
            new[]
            {
                Hypothesis("guard-order", "Караул законно закрыл ворота по действующему приказу.", "Безопасность смены", "груз может не попасть к больным", "Подписан ли приказ и относится ли тревога к этим воротам?"),
                Hypothesis("community-right", "Община имеет безусловное право пройти по старой грамоте.", "Жители и лечебница", "караул теряет возможность проверять угрозу", "Распространяется ли грамота на тревогу?"),
                Hypothesis("emergency", "Обе стороны могут сохранить безопасность через проверенный ограниченный проход.", "Больные и караул", "кто-то должен принять ответственность за досмотр", "Что именно в корзинах и насколько срочна помощь?" )
            }, clues,
            new[]
            {
                Scene("opening", "Закрытые ворота", "Игроки решают, предотвращать ли столкновение, проверять документы или осматривать груз."),
                Scene("charter", "Старая грамота", "Текст грамоты показывает право общины и его неясные границы.", ["old-charter"]),
                Scene("guardhouse", "Караульный приказ", "Игроки проверяют подпись и связь тревоги со спорными воротами.", ["new-order", "patrol-note"]),
                Scene("cargo", "Корзины для лечебницы", "Печать и содержимое позволяют оценить срочность и риск.", ["supply-seal"]),
                Scene("terms", "Условия прохода", "Герои формулируют решение, которое можно исполнить до ночи.")
            },
            new[]
            {
                Branch("opening", "charter", "Прочесть грамоту", "Игроки начинают с заявленного права."),
                Branch("opening", "guardhouse", "Проверить приказ", "Игроки требуют основания для запрета."),
                Branch("opening", "cargo", "Осмотреть груз", "Игроки проверяют срочность и безопасность помощи."),
                Branch("charter", "terms", "Сопоставить право и риск", "Текст грамоты прочитан."),
                Branch("guardhouse", "terms", "Сопоставить приказ и тревогу", "Основания караула проверены.", ["new-order"]),
                Branch("cargo", "terms", "Согласовать досмотр", "Груз подтверждён как помощь.", ["supply-seal"]),
                EndingBranch("terms", "controlled-passage", "Организовать досмотренный проход", "Груз пропускают ограниченно и под ответственностью сторон.", ["supply-seal"]),
                EndingBranch("terms", "uphold-closure", "Подтвердить закрытие", "Игроки считают приказ и тревогу достаточными.", ["new-order"]),
                EndingBranch("terms", "uphold-charter", "Открыть ворота по грамоте", "Игроки принимают старое право как достаточное основание.", ["old-charter"]),
                EndingBranch("terms", "defer-command", "Передать решение старшему", "Ворота остаются закрыты до прибытия полномочного начальника.")
            }, anchors);
    }

    private static GeneratedScenario BuildScenario(
        ScenarioGenerationOptions o, ScenarioTemplate t, string blueprintId, string title, string hook, string briefing, string hiddenTruth,
        IReadOnlyList<ScenarioHypothesis> hypotheses, IReadOnlyList<ScenarioClue> clues, IReadOnlyList<ScenarioScene> scenes,
        IReadOnlyList<ScenarioBranch> branches, IReadOnlyList<ScenarioCanonicalAnchor> anchors)
    {
        var endings = branches.Where(branch => !string.IsNullOrWhiteSpace(branch.ToEndingId)).Select(branch => branch.ToEndingId).Distinct().Select(endingId =>
            new ScenarioEnding { Id = endingId, Title = EndingTitle(endingId), Outcome = EndingOutcome(endingId), IsReachable = true }).ToArray();
        return new GeneratedScenario
        {
            Title = title,
            Seed = o.Seed,
            Options = o,
            Template = t,
            InvestigationBlueprintId = blueprintId,
            Hook = hook,
            ConflictCause = hook,
            GenerationDiagnostic = anchors.Count == 0
                ? "Подходящие проверенные канонические опоры не найдены: использован автономный авторский blueprint без вымышленного канона."
                : "Канонические опоры предложены только как справка; обстоятельства расследования остаются авторскими.",
            StartSceneId = "opening",
            PublicBriefing = briefing,
            KnownFacts = [briefing, "Все обвинения в этой истории — версии игроков и ведущего, пока их не подтверждают улики."],
            NarrativeFacts =
            [
                new() { Id = blueprintId + "-premise", Text = hook, Origin = ScenarioFactOrigin.FactoryFiction },
                new() { Id = blueprintId + "-truth", Text = hiddenTruth, Origin = ScenarioFactOrigin.GmHypothesis }
            ],
            GmNotes = [hiddenTruth, "Названия и URL канонических опор — справка. Они не доказывают мотив, вину или отношения участников этой авторской ситуации.", anchors.Count == 0 ? "Канонические опоры недоступны или не подходят; не заменяйте это вымышленными фактами лора." : "Выберите и подтвердите опоры отдельно перед экспортом сценария."],
            Hypotheses = hypotheses,
            Characters = RolesFor(blueprintId, o.ParticipantCount),
            Clues = clues,
            Scenes = scenes,
            Branches = branches,
            Endings = endings,
            CanonicalAnchors = anchors
        };
    }

    private static IReadOnlyList<ScenarioCharacter> RolesFor(string blueprintId, int count)
    {
        string[] labels = blueprintId switch
        {
            "lost-property" => ["хранитель ключей", "служащая описи", "представитель передачи", "свидетель квартала", "архивный помощник", "нейтральный наблюдатель"],
            "conflicting-record" => ["писарь", "посыльный", "караульный", "представитель старосты", "архивист", "свидетель смены"],
            _ => ["начальник караула", "представитель общины", "лекарь", "патрульный", "носитель груза", "нейтральный посредник"]
        };
        return labels.Take(count).Select((label, index) => new ScenarioCharacter
        {
            Name = "Роль " + (index + 1), Role = label, Goal = "добиться решения без ложного обвинения", Motive = "последствия решения затронут его сторону",
            PublicInformation = "Знает только свою часть ситуации.", PersonalInformation = "Готов обсуждать проверяемые условия.",
            Concession = "готов принять независимую проверку", RedLine = "не согласен признать вину без оснований"
        }).ToArray();
    }

    private static ScenarioHypothesis Hypothesis(string id, string statement, string beneficiary, string cost, string unknown) =>
        new() { Id = id, Statement = statement, Beneficiary = beneficiary, Cost = cost, Unknown = unknown };
    private static ScenarioClue Clue(string id, string text, string fact, string reveals, IReadOnlyList<string> supports, IReadOnlyList<string> contradicts) =>
        new() { Id = id, Text = text, KnownFact = fact, Reveals = reveals, SupportsHypothesisIds = supports, ContradictsHypothesisIds = contradicts };
    private static ScenarioScene Scene(string id, string title, string purpose, IReadOnlyList<string>? clues = null) =>
        new() { Id = id, Title = title, Purpose = purpose, ClueIds = clues ?? [] };
    private static ScenarioBranch Branch(string from, string to, string decision, string condition, IReadOnlyList<string>? clues = null) =>
        new() { FromSceneId = from, ToSceneId = to, Decision = decision, Condition = condition, RequiredClueIds = clues ?? [] };
    private static ScenarioBranch EndingBranch(string from, string ending, string decision, string condition, IReadOnlyList<string>? clues = null) =>
        new() { FromSceneId = from, ToEndingId = ending, Decision = decision, Condition = condition, RequiredClueIds = clues ?? [] };

    private static string EndingTitle(string id) => id switch
    {
        "prove-protection" => "Проверенное хранение", "charge-caretaker" => "Обвинение смотрителя", "restore-process" => "Передача под надзором", "defer-review" => "Проверка до утра",
        "formal-correction" => "Оформленная правка", "charge-messenger" => "Обвинение посыльного", "charge-clerk" => "Обвинение писаря", "temporary-pass" => "Временный пропуск",
        "controlled-passage" => "Досмотренный проход", "uphold-closure" => "Закрытые ворота", "uphold-charter" => "Проход по грамоте", "defer-command" => "Решение старшего",
        _ => id
    };
    private static string EndingOutcome(string id) => id switch
    {
        "prove-protection" => "Копию возвращают в процедуру, а нарушение оформления разбирают без ложного обвинения.",
        "charge-caretaker" => "Смотрителя обвиняют по косвенным признакам; цена ошибки остаётся частью последствий.",
        "restore-process" => "Передачу проводят под надзором и оставляют спор о нарушении на независимую проверку.",
        "defer-review" => "Никого не обвиняют до утра, но стороны принимают цену задержки.",
        "formal-correction" => "Исправление оформляют заново, а порядок доступа восстанавливают.",
        "charge-messenger" => "Посыльного обвиняют на основании доступа; верность вывода остаётся проверяемой ценой решения.",
        "charge-clerk" => "Писаря обвиняют по следам в книге; процедура продолжается с риском ошибочного вывода.",
        "temporary-pass" => "Проход разрешают временно, не выдавая спорную версию за установленный факт.",
        "controlled-passage" => "Груз проходит после досмотра: безопасность и помощь получают ограниченное, проверяемое решение.",
        "uphold-closure" => "Ворота остаются закрыты: караул принимает ответственность за задержку помощи.",
        "uphold-charter" => "Общину пропускают по грамоте: стороны принимают риск открытого прохода.",
        "defer-command" => "Решение ждёт старшего начальника, а цена задержки остаётся видимой.",
        _ => "Игроки принимают последствия выбранного решения."
    };

    private static int StableSeed(int seed, string value)
    {
        unchecked
        {
            var hash = 17;
            foreach (var character in value) hash = hash * 31 + character;
            return seed * 397 ^ hash;
        }
    }
    private static int PositiveModulo(int value, int divisor) => (value & int.MaxValue) % divisor;
}
