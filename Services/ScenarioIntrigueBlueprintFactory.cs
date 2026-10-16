using SkyrimRpEncyclopedia.Models;

namespace SkyrimRpEncyclopedia.Services;

/// <summary>
/// Deterministic political scenarios. Reference anchors describe only verified
/// encyclopaedia context; every dispute, promise and hidden explanation below
/// is explicit factory fiction or a GM hypothesis.
/// </summary>
public sealed class ScenarioIntrigueBlueprintFactory
{
    public static IReadOnlyList<string> BlueprintIds { get; } =
        ["resource-allocation", "coalition-charter", "mandate-dispute"];

    private readonly ScenarioCanonicalAnchorSelector? _anchorSelector;

    public ScenarioIntrigueBlueprintFactory(IScenarioKnowledgeCatalog? catalog = null) =>
        _anchorSelector = catalog is null ? null : new ScenarioCanonicalAnchorSelector(catalog);

    public GeneratedScenario Build(ScenarioGenerationOptions options, ScenarioTemplate template)
    {
        var id = BlueprintIds[PositiveModulo(StableSeed(options.Seed, "intrigue-blueprint"), BlueprintIds.Count)];
        var anchors = _anchorSelector?.SuggestCandidates(template.Id, options.Seed).Anchors ?? [];
        return id switch
        {
            "resource-allocation" => BuildResourceAllocation(options, template, anchors),
            "coalition-charter" => BuildCoalitionCharter(options, template, anchors),
            "mandate-dispute" => BuildMandateDispute(options, template, anchors),
            _ => throw new InvalidOperationException("Unknown intrigue blueprint: " + id)
        };
    }

    private static GeneratedScenario BuildResourceAllocation(ScenarioGenerationOptions o, ScenarioTemplate t, IReadOnlyList<ScenarioCanonicalAnchor> anchors)
    {
        var clues = new[]
        {
            Clue("stock", "Пересчёт подтверждает: запасов хватит только одной стороне полностью или обеим частично.", "ресурс объективно ограничен", "решение требует выбора цены", ["guard-priority", "relief-priority", "shared-control"]),
            Clue("road", "Донесение дозора объясняет цену сокращения охраны дорог.", "у гарнизона есть проверяемая потребность", "полное распределение в пользу квартала имеет риск", ["guard-priority"]),
            Clue("clinic", "Список лечебницы показывает цену сокращения пайков.", "у квартала есть проверяемая потребность", "полное распределение в пользу караула имеет риск", ["relief-priority"]),
            Clue("audit", "Старый отчёт доказывает, что общий надзор снижает потери, но требует уступок всех сторон.", "компромисс требует контроля, а не обещаний", "совместный надзор реалистичен", ["shared-control"])
        };
        return Create(o, t, "resource-allocation", "Пайки до ночи", "Запас продовольствия не покрывает запросы караула и лечебницы.",
            "До ночи совет должен распределить ограниченный запас либо объяснить последствия отсрочки.",
            "Авторская истина ведущего: запас подсчитан верно; ни одна сторона не лжёт о базовой нужде, но каждая предпочитает скрыть цену своего решения.",
            Roles("распорядитель караула", "представитель лечебницы", "счётчик запасов", "посредник общины", "наблюдатель купцов", "писарь решения", o.ParticipantCount),
            clues,
            [Coalition("guard-priority", "Караул и обоз", ["Роль 1", "Роль 3"], "караваны получают основной запас", "лечебница принимает сокращение помощи", ["stock", "road"]),
             Coalition("relief-priority", "Лечебница и община", ["Роль 2", "Роль 4"], "больные получают основной запас", "ночная охрана сокращается", ["stock", "clinic"]),
             Coalition("shared-control", "Общий надзор", ["Роль 1", "Роль 2", "Роль 3"], "запас делят и ежедневно сверяют", "ни одна сторона не получает желаемое полностью", ["stock", "audit"])],
            [Scene("opening", "Пустой склад", "Игроки предотвращают немедленное изъятие запаса."),
             Scene("evidence", "Цена дефицита", "Проверяются объём запаса, дорога, лечебница и старый отчёт.", clues.Select(c => c.Id).ToArray()),
             Scene("bargain", "Условия уступок", "Стороны формируют коалиции, называя цену решения."),
             Scene("vote", "Распределение", "Совет утверждает один из проверенных вариантов.")],
            [Branch("opening", "evidence", "Проверить запас", "Игроки требуют цифры до голосования."),
             Branch("opening", "bargain", "Начать торг", "Стороны предлагают уступки без окончательного решения."),
             Branch("evidence", "bargain", "Перейти к условиям", "Потребности подтверждены.", ["stock"]),
             Branch("bargain", "vote", "Вынести пакет", "Состав коалиции и цена названы."),
             End("vote", "guard-win", "Поддержать караул", "Коалиция выбирает защиту дороги.", ["stock", "road"]),
             End("vote", "relief-win", "Поддержать лечебницу", "Коалиция выбирает пайки.", ["stock", "clinic"]),
             End("vote", "supervised-share", "Утвердить общий надзор", "Стороны принимают частичное распределение и контроль.", ["stock", "audit"]),
             End("vote", "failed-allocation", "Отложить распределение", "Совет не принимает цену ни одной коалиции.")], anchors);
    }

    private static GeneratedScenario BuildCoalitionCharter(ScenarioGenerationOptions o, ScenarioTemplate t, IReadOnlyList<ScenarioCanonicalAnchor> anchors)
    {
        var clues = new[]
        {
            Clue("draft", "Черновик устава делит голоса между тремя палатами и предусматривает право возражения.", "устав меняет распределение полномочий", "поддержка требует явного компромисса", ["broad-charter", "limited-charter"]),
            Clue("quorum", "Список присутствующих подтверждает: обычного большинства нет.", "ни одна сторона не может провести устав одна", "требуется коалиция или временный порядок", ["broad-charter", "limited-charter", "temporary-protocol"]),
            Clue("guarantee", "Письменная гарантия позволяет меньшинству приостановить опасное решение на один срок.", "право вето можно ограничить, не отменяя полностью", "широкая коалиция становится достижима", ["broad-charter"]),
            Clue("deadline", "Срок полномочий текущего совета истекает до следующего заседания.", "отказ от решения тоже имеет цену", "временный протокол имеет основание", ["temporary-protocol"])
        };
        return Create(o, t, "coalition-charter", "Устав без большинства", "Совет не может утвердить общий устав без уступок меньшинству.",
            "Три палаты согласны, что правила нужны, но не согласны, кто получит право блокировать решения.",
            "Авторская истина ведущего: ни одна сторона не получила тайного обещания; спор вызван реальными различиями в приемлемой цене полномочий.",
            Roles("делегат первой палаты", "делегат второй палаты", "делегат третьей палаты", "писарь устава", "наблюдатель меньшинства", "посредник совета", o.ParticipantCount),
            clues,
            [Coalition("broad-charter", "Широкий устав", ["Роль 1", "Роль 2", "Роль 4"], "меньшинство получает ограниченное вето", "решения проходят медленнее", ["draft", "quorum", "guarantee"]),
             Coalition("limited-charter", "Ограниченный союз", ["Роль 1", "Роль 3"], "две палаты утверждают устав без права вето", "третья палата остаётся вне соглашения", ["draft", "quorum"]),
             Coalition("temporary-protocol", "Временный протокол", ["Роль 2", "Роль 4"], "совет принимает краткие правила до нового состава", "основной устав откладывается", ["quorum", "deadline"])],
            [Scene("opening", "Спорный черновик", "Игроки решают, обсуждать текст публично или собирать поддержку по палатам."),
             Scene("text", "Статьи устава", "Черновик и кворум показывают, какие уступки нужны.", ["draft", "quorum"]),
             Scene("caucus", "Переговоры палат", "Стороны выдвигают условия поддержки и красные линии."),
             Scene("guarantees", "Гарантии меньшинству", "Игроки проверяют ограниченное право возражения и срок совета.", ["guarantee", "deadline"]),
             Scene("ratification", "Ратификация", "Совет голосует за пакет или признаёт тупик.")],
            [Branch("opening", "text", "Прочесть черновик", "Игроки начинают с текста полномочий."),
             Branch("opening", "caucus", "Собрать позиции", "Игроки проверяют, какие условия поддержки реальны."),
             Branch("text", "caucus", "Обсудить условия", "Палаты видят цену черновика.", ["quorum"]),
             Branch("caucus", "guarantees", "Согласовать гарантии", "Меньшинство требует проверяемую защиту."),
             Branch("guarantees", "ratification", "Вынести пакет", "Условия коалиции оформлены."),
             End("ratification", "broad-ratified", "Утвердить широкий устав", "Три стороны принимают ограниченное вето.", ["draft", "quorum", "guarantee"]),
             End("ratification", "limited-ratified", "Утвердить ограниченный союз", "Две палаты проводят устав без третьей.", ["draft", "quorum"]),
             End("ratification", "charter-rejected", "Отклонить устав", "Стороны не принимают цену уступок."),
             End("ratification", "temporary-protocol", "Принять временный протокол", "Совет переносит основной спор, но сохраняет процедуру.", ["quorum", "deadline"])], anchors);
    }

    private static GeneratedScenario BuildMandateDispute(ScenarioGenerationOptions o, ScenarioTemplate t, IReadOnlyList<ScenarioCanonicalAnchor> anchors)
    {
        var clues = new[]
        {
            Clue("charter", "Старая грамота даёт общине право доступа, но не описывает режим тревоги.", "основание общины реально, но ограниченно", "право нельзя применить автоматически", ["recognize-charter", "limited-mandate"]),
            Clue("order", "Приказ караула ограничивает доступ после тревоги, но не содержит подписи старшего начальника.", "основание караула неполно", "запрет требует проверки", ["recognize-order", "limited-mandate"]),
            Clue("cargo", "Печать на грузе подтверждает срочность помощи, а не коммерческую перевозку.", "действие имеет цену для третьих лиц", "появляется вариант ограниченного исполнения", ["limited-mandate"]),
            Clue("patrol", "Записка патруля подтверждает тревогу у другого входа, но не у спорных ворот.", "опасность реальна, но её масштаб не установлен", "полная остановка или свободный доступ не следуют автоматически", ["suspend-action", "limited-mandate"])
        };
        return Create(o, t, "mandate-dispute", "Полномочия у ворот", "Караул и община предъявляют разные документы на одно действие — пропуск груза к лечебнице.",
            "У ворот стоят запечатанные корзины: приказ требует закрытия, а грамота требует доступа.",
            "Авторская истина ведущего: тревога реальна, но начальник смены превысил данные ему полномочия; община имеет право доступа, которое всё же допускает разумный досмотр.",
            Roles("начальник караула", "представитель общины", "лекарь", "писарь грамоты", "патрульный", "нейтральный посредник", o.ParticipantCount),
            clues,
            [Coalition("limited-mandate", "Проверенный проход", ["Роль 1", "Роль 2", "Роль 3"], "груз проходит после досмотра", "обе стороны принимают запись ответственности", ["charter", "order", "cargo"]),
             Coalition("recognize-order", "Приказ караула", ["Роль 1", "Роль 4"], "ворота остаются закрыты", "лечебница несёт цену задержки", ["order", "patrol"]),
             Coalition("recognize-charter", "Грамота общины", ["Роль 2", "Роль 4"], "община проходит по грамоте", "караул теряет полный контроль проверки", ["charter"])],
            [Scene("opening", "Закрытые ворота", "Игроки предотвращают спор и выбирают, какой документ проверить первым."),
             Scene("documents", "Два основания", "Грамота и приказ проверяются вместе.", ["charter", "order"]),
             Scene("cargo", "Проверка груза", "Игроки устанавливают срочность помощи.", ["cargo"]),
             Scene("patrol", "Записка патруля", "Проверяется связь тревоги со спорными воротами.", ["patrol"]),
             Scene("ruling", "Предел полномочий", "Игроки формулируют исполнимое решение.")],
            [Branch("opening", "documents", "Сверить документы", "Игроки проверяют основания сторон."),
             Branch("opening", "cargo", "Осмотреть груз", "Игроки проверяют срочность действия."),
             Branch("documents", "ruling", "Подготовить решение", "Основания сторон сопоставлены.", ["charter", "order"]),
             Branch("documents", "cargo", "Проверить груз после документов", "Игроки уточняют цену исполнения после проверки оснований."),
             Branch("cargo", "patrol", "Проверить тревогу", "Срочность груза сопоставляется с риском."),
             Branch("patrol", "ruling", "Вернуться с оценкой риска", "Масштаб тревоги проверен.", ["patrol"]),
             End("ruling", "limited-passage", "Выдать ограниченный мандат", "Проход разрешён после досмотра и записи ответственности.", ["charter", "order", "cargo"]),
             End("ruling", "order-recognized", "Признать приказ караула", "Караул сохраняет закрытие до нового распоряжения.", ["order", "patrol"]),
             End("ruling", "charter-recognized", "Признать грамоту", "Община проходит без отмены самой процедуры проверки.", ["charter"]),
             End("ruling", "mandate-suspended", "Приостановить действие", "Спор передают старшему, принимая кризис полномочий.", ["patrol"])], anchors);
    }

    private static GeneratedScenario Create(ScenarioGenerationOptions o, ScenarioTemplate t, string id, string title, string hook, string briefing,
        string hiddenTruth, IReadOnlyList<ScenarioCharacter> roles, IReadOnlyList<ScenarioClue> clues, IReadOnlyList<ScenarioCoalition> coalitions,
        IReadOnlyList<ScenarioScene> scenes, IReadOnlyList<ScenarioBranch> branches, IReadOnlyList<ScenarioCanonicalAnchor> anchors)
    {
        var endings = branches.Where(b => !string.IsNullOrWhiteSpace(b.ToEndingId)).Select(b => b.ToEndingId).Distinct().Select(end =>
            new ScenarioEnding { Id = end, Title = EndTitle(end), Outcome = EndOutcome(end), IsReachable = true }).ToArray();
        return new GeneratedScenario
        {
            Title = title, Seed = o.Seed, Options = o, Template = t, IntrigueBlueprintId = id,
            Hook = hook, ConflictCause = hook, StartSceneId = "opening", PublicBriefing = briefing,
            GenerationDiagnostic = anchors.Count == 0 ? "Подходящие проверенные канонические опоры не найдены: интрига остаётся автономной, без вымышленного канона." : "Канонические опоры предложены только как справка; политический конфликт остаётся авторской фабулой.",
            KnownFacts = [briefing, "Решение меняет распределение власти, ресурсов или ответственности; ничья позиция не считается доказанной без условий сценария."],
            NarrativeFacts = [new() { Id = id + "-premise", Text = hook, Origin = ScenarioFactOrigin.FactoryFiction }, new() { Id = id + "-gm", Text = hiddenTruth, Origin = ScenarioFactOrigin.GmHypothesis }],
            GmNotes = [hiddenTruth, "Канонические опоры задают только справочный контекст. Они не доказывают мотивы, союзы, вину или полномочия авторских ролей."],
            Characters = roles, Clues = clues, Coalitions = coalitions, Scenes = scenes, Branches = branches, Endings = endings, CanonicalAnchors = anchors
        };
    }

    private static IReadOnlyList<ScenarioCharacter> Roles(string a, string b, string c, string d, string e, string f, int count) =>
        new[] { a, b, c, d, e, f }.Take(count).Select((role, index) => new ScenarioCharacter
        {
            Name = "Роль " + (index + 1), Role = role, Goal = "защитить интерес своей стороны", Motive = "последствия решения затронут его обязательства",
            PublicInformation = "Озвучивает свою позицию и условия.", PersonalInformation = "Готов обсуждать проверяемые уступки.",
            Concession = "готов принять оформленное условие", RedLine = "не признает решение без проверяемого основания"
        }).ToArray();
    // Intrigue evidence is attached through Coalition.RequiredClueIds and branch
    // conditions; SupportsHypothesisIds remains reserved for investigations.
    private static ScenarioClue Clue(string id, string text, string fact, string reveal, IReadOnlyList<string> supports) => new() { Id = id, Text = text, KnownFact = fact, Reveals = reveal };
    private static ScenarioCoalition Coalition(string id, string name, IReadOnlyList<string> members, string concession, string cost, IReadOnlyList<string> clues) => new() { Id = id, Name = name, Members = members, Concession = concession, Cost = cost, RequiredClueIds = clues };
    private static ScenarioScene Scene(string id, string title, string purpose, IReadOnlyList<string>? clues = null) => new() { Id = id, Title = title, Purpose = purpose, ClueIds = clues ?? [] };
    private static ScenarioBranch Branch(string from, string to, string decision, string condition, IReadOnlyList<string>? clues = null) => new() { FromSceneId = from, ToSceneId = to, Decision = decision, Condition = condition, RequiredClueIds = clues ?? [] };
    private static ScenarioBranch End(string from, string end, string decision, string condition, IReadOnlyList<string>? clues = null) => new() { FromSceneId = from, ToEndingId = end, Decision = decision, Condition = condition, RequiredClueIds = clues ?? [] };
    private static string EndTitle(string id) => id.Replace('-', ' ');
    private static string EndOutcome(string id) => id switch
    {
        "guard-win" => "Караул получает ресурс; лечебница принимает цену сокращения помощи.", "relief-win" => "Лечебница получает ресурс; ночная безопасность дороги снижается.", "supervised-share" => "Стороны делят ресурс и вводят общий надзор.", "failed-allocation" => "Распределение сорвано, а дефицит остаётся нерешённым.",
        "broad-ratified" => "Устав принят широкой коалицией с ограниченным правом возражения.", "limited-ratified" => "Устав проходит узким союзом, оставляя третью сторону вне соглашения.", "charter-rejected" => "Устав отклонён, полномочия остаются неурегулированными.", "temporary-protocol" => "Временный протокол сохраняет процедуру, перенося основной спор.",
        "limited-passage" => "Разрешён ограниченный проход под досмотром и ответственностью.", "order-recognized" => "Закрытие признано действующим, помощь задержана.", "charter-recognized" => "Право общины признано достаточным для прохода.", "mandate-suspended" => "Действие приостановлено до решения старшего, создавая кризис полномочий.", _ => id
    };
    private static int StableSeed(int seed, string text) { unchecked { var hash = 17; foreach (var c in text) hash = hash * 31 + c; return seed * 397 ^ hash; } }
    private static int PositiveModulo(int value, int divisor) => (value & int.MaxValue) % divisor;
}
