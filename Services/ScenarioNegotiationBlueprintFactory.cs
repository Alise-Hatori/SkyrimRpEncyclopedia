using SkyrimRpEncyclopedia.Models;

namespace SkyrimRpEncyclopedia.Services;

/// <summary>Evidence opens negotiation options; consent and execution remain GM decisions.</summary>
public sealed class ScenarioNegotiationBlueprintFactory
{
    public static IReadOnlyList<string> BlueprintIds { get; } = ["hostage-exchange", "border-agreement", "debt-settlement"];
    private readonly ScenarioCanonicalAnchorSelector? _selector;
    public ScenarioNegotiationBlueprintFactory(IScenarioKnowledgeCatalog? catalog = null) =>
        _selector = catalog is null ? null : new ScenarioCanonicalAnchorSelector(catalog);

    public GeneratedScenario Build(ScenarioGenerationOptions options, ScenarioTemplate template)
    {
        var id = BlueprintIds[(options.Seed & int.MaxValue) % BlueprintIds.Count];
        return id switch
        {
            "hostage-exchange" => Exchange(options, template),
            "border-agreement" => Border(options, template),
            "debt-settlement" => Debt(options, template),
            _ => throw new InvalidOperationException("Unknown negotiation blueprint: " + id)
        };
    }

    private GeneratedScenario Exchange(ScenarioGenerationOptions o, ScenarioTemplate t)
    {
        var clues = new List<ScenarioClue>
        {
            Clue("person-message", "Сообщение удерживаемой Иды: она хочет выйти к нейтральному лекарю и не согласна на передачу другой группе.", "воля Иды отличается от предложений переговорщиков"),
            Clue("delegation", "Обе делегации могут предложить освобождение, но их письма не подтверждают законность самого удержания.", "объём мандатов ограничен"),
            Clue("safe-place", "Лекарь подтвердил возможность принять освобождённых на нейтральном дворе.", "есть проверяемая гарантия безопасности")
        };
        var scenes = new List<ScenarioScene>
        {
            Scene("exchange-opening", "Условия освобождения", "Обсуждается освобождение Иды и встречное освобождение посыльного, а не право распоряжаться людьми."),
            Scene("exchange-contact", "Слово удерживаемой", "Игроки получают её позицию и проверяют мандаты.", "person-message", "delegation"),
            Scene("exchange-guarantees", "Нейтральный двор", "Согласуют безопасность, независимый выбор освобождённых и порядок выхода.", "safe-place"),
            Scene("exchange-sequence", "Последовательность", "Посредник предлагает сначала вывести Иду к лекарю, затем освободить посыльного.")
        };
        var proof = new List<string> { "person-message", "delegation", "safe-place" };
        AddDifficulty(o, clues, scenes, proof, "exchange-contact", "authority-check", "Письма делегаций сверены: право обещать освобождение не даёт права назначать человеку нового владельца.");
        var branches = new List<ScenarioBranch>
        {
            Go("exchange-opening", "exchange-contact", "Услышать Иду", "Обеспечивается безопасный контакт без принуждения."),
            Go("exchange-contact", "exchange-guarantees", "Проверить гарантии", "Позиции людей и мандаты известны.", "person-message", "delegation"),
            Go("exchange-guarantees", "exchange-sequence", "Предложить посредника", "Посредник и обе стороны принимают обсуждение порядка выхода.", "safe-place"),
            End("exchange-guarantees", "exchange-simultaneous", "Одновременное освобождение", "Оба человека согласны выйти к лекарю; делегации принимают взаимную проверку.", proof.ToArray()),
            End("exchange-sequence", "exchange-staged", "Освободить поэтапно", "Ида выбирает нейтральное укрытие; посредник обеспечивает второе освобождение после подтверждения первого.", proof.ToArray()),
            End("exchange-guarantees", "exchange-unconditional", "Освободить без встречной передачи", "Удерживающая сторона принимает независимое наблюдение вместо встречного освобождения.", proof.ToArray()),
            End("exchange-opening", "exchange-paused", "Прервать обсуждение", "Безопасные условия не согласованы; удержание сохраняется, назначается следующий контакт.")
        };
        var endings = new List<ScenarioEnding>
        {
            Ending("exchange-simultaneous", "Взаимное освобождение", "Оба человека свободны одновременно и сами выбирают дальнейший путь; встречных обязательств нет."),
            Ending("exchange-staged", "Два подтверждённых шага", "Ида выходит первой, посыльный после проверки; посредник отвечает за процедуру, стороны несут риск ожидания."),
            Ending("exchange-unconditional", "Свобода под наблюдением", "Ида освобождена без встречной передачи; другой случай удержания остаётся нерешённым."),
            Ending("exchange-paused", "Следующий контакт", "Освобождение не состоялось; стороны сохраняют безопасный канал связи и согласуют новую встречу.")
        };
        AddDuration(o, 120, clues, scenes, branches, endings, "exchange-guarantees", "exchange-inspection", "health-check", "Независимый осмотр", "Лекарь может проверить состояние Иды до обсуждения следующего шага.");
        return Create(o, t, "hostage-exchange", "Право выйти", "Две группы предлагают взаимное освобождение Иды и посыльного, но Ида требует нейтрального укрытия и собственного выбора.",
            "Гипотеза ведущего: Ида опасается новой зависимости после освобождения; её мотивы уточняются разговором, а не выводятся из принадлежности к группе.",
            [Role("Ида", "выйти к нейтральному лекарю", "сохранить свободу выбора", "согласиться на безопасный осмотр", "не принимать передачу другой группе", "знает условия собственного удержания"),
             Role("делегат удерживающей группы", "получить безопасное взаимное решение", "боится нарушения обещаний другой стороной", "принять посредника", "не обещать полномочия сверх письма", "может организовать выход Иды, но не распоряжаться её волей"),
             Role("представитель посыльного", "освободить обоих", "не хочет оставить посыльного после первого шага", "принять последовательность", "не считать освобождение состоявшимся без проверки", "знает условия встречного освобождения"),
             Role("лекарь", "принять людей в безопасном месте", "отвечает за их состояние", "провести осмотр", "не участвовать в принуждении", "знает возможности нейтрального двора"),
             Role("посыльный", "выйти без нового удержания", "опасается обмена одной зависимости на другую", "подтвердить собственный выход", "не соглашаться за Иду", "знает второе место удержания"),
             Role("посредник", "проверить каждый шаг", "рискует доверием сторон", "принять наблюдение", "не гарантировать действия без мандата", "может связать два места")], clues, scenes, branches, endings);
    }

    private GeneratedScenario Border(ScenarioGenerationOptions o, ScenarioTemplate t)
    {
        var clues = new List<ScenarioClue>
        {
            Clue("access-charter", "Грамота допускает проход общины, но не описывает досмотр и ночное движение.", "доступ имеет основание, а режим исполнения требует соглашения"),
            Clue("traffic-log", "Журнал отмечает два опасных ночных прохода и безопасные дневные обозы.", "контроль можно ограничить временем и объёмом"),
            Clue("repair-estimate", "Смета ремонта моста допускает работу общины вместо пошлины.", "есть альтернативная компенсация")
        };
        var scenes = new List<ScenarioScene>
        {
            Scene("border-opening", "Спорный проход", "Община требует доступа, караул требует контроля; полная правота ни одной стороны не установлена."),
            Scene("border-records", "Объём доступа", "Сверяют основание прохода и риски времени суток.", "access-charter", "traffic-log"),
            Scene("border-control", "Ответственность сторон", "Определяют дневное окно, число обозов, досмотр, жалобы и ответственность за нарушение."),
            Scene("border-compensation", "Плата или работа", "Обсуждают ремонт вместо пошлины.", "repair-estimate"),
            Scene("border-arbitration", "Независимое рассмотрение", "Стороны передают документы арбитру, сохраняя временный запрет.")
        };
        var proof = new List<string> { "access-charter", "traffic-log" };
        AddDifficulty(o, clues, scenes, proof, "border-records", "control-mandate", "Проверка мандата показывает: караул может согласовать досмотр, но не отменить грамоту единолично.");
        var branches = new List<ScenarioBranch>
        {
            Go("border-opening", "border-records", "Сверить основания", "Стороны дают документы для обсуждения."),
            Go("border-records", "border-control", "Определить режим", "Основание доступа и конкретные риски проверены.", proof.ToArray()),
            Go("border-control", "border-compensation", "Заменить пошлину", "Стороны готовы оценить встречную работу вместо денег."),
            Go("border-records", "border-arbitration", "Передать спор", "Стороны согласны на независимый разбор.", "access-charter"),
            End("border-control", "border-permanent", "Подписать постоянный режим", "Обе стороны принимают расписание, досмотр, плату и процедуру жалоб.", proof.ToArray()),
            End("border-control", "border-trial", "Испытать ограниченный режим", "Стороны согласуют один дневной обоз и пересмотр после проверки.", proof.ToArray()),
            End("border-compensation", "border-work", "Принять ремонт вместо пошлины", "Согласованы объём работ, проход и ответственность за срыв ремонта.", "access-charter", "traffic-log", "repair-estimate"),
            End("border-arbitration", "border-referred", "Ждать независимого решения", "Арбитр принимает документы; стороны не объявляют спор решённым.", "access-charter"),
            End("border-opening", "border-closed", "Зафиксировать отсутствие соглашения", "Стороны не принимают предложенные условия; проход остаётся закрытым.")
        };
        var endings = new List<ScenarioEnding>
        {
            Ending("border-permanent", "Постоянный режим", "Проход открыт по двусторонним правилам, действует пошлина и механизм ответственности обеих сторон."),
            Ending("border-trial", "Один дневной обоз", "Доступ ограничен пробной партией; дальнейшее право прохода зависит от пересмотра."),
            Ending("border-work", "Мост вместо пошлины", "Община получает проход за ремонт; денежной пошлины нет, возникает обязательство выполнить работы."),
            Ending("border-referred", "Спор у арбитра", "Проход временно закрыт, но документы переданы независимой стороне и назначен разбор."),
            Ending("border-closed", "Закрытый проход", "Соглашения и независимой процедуры нет; община ищет другой маршрут, караул несёт цену прекращения торговли.")
        };
        AddDuration(o, 150, clues, scenes, branches, endings, "border-control", "border-monitoring", "monitor-record", "Совместное наблюдение", "Община и караул могут сверять журнал каждого прохода до пересмотра режима.");
        return Create(o, t, "border-agreement", "Проход по двум подписям", "Община и караул спорят о дневном проходе обозов, досмотре, пошлине и ответственности за нарушения.",
            "Гипотеза ведущего: за требованием жёсткого контроля стоит страх потери доверия после ночных происшествий, а не доказанная вражда к общине.",
            [Role("делегат общины", "получить предсказуемый доступ", "обозы кормят общину", "принять дневной досмотр", "не признать безусловную отмену грамоты", "знает грамоту и потребности обозов"),
             Role("начальник караула", "снизить риск ночного прохода", "отвечает за безопасность", "согласовать пробный режим", "не обещать безопасность без контроля", "знает журнал происшествий"),
             Role("смотритель моста", "обеспечить ремонт", "разрушение прекратит движение", "принять работы вместо платы", "не принять непроверенную смету", "знает объём ремонта"),
             Role("возница", "получить расписание", "задержки портят груз", "принять очередь обозов", "не отвечать за закрытие ворот караулом", "знает сроки доставки"),
             Role("арбитр", "разделить основания и режим", "обе стороны доверяют его процедуре", "назначить пересмотр", "не решать без документов", "знает порядок жалоб"),
             Role("учётчик", "сверять двусторонний журнал", "его записи определят ответственность", "принять совместные подписи", "не подписывать чужой осмотр", "знает способы проверки прохода")], clues, scenes, branches, endings);
    }

    private GeneratedScenario Debt(ScenarioGenerationOptions o, ScenarioTemplate t)
    {
        var clues = new List<ScenarioClue>
        {
            Clue("balance", "Подписанный акт подтверждает остаток в сорок монет после ранее внесённой платы.", "сумма основного долга проверена, новые штрафы не подтверждены"),
            Clue("budget", "Опись отделяет свободные деньги от средств на содержание зависимых работников.", "полная выплата возможна только без изъятия защищённых средств"),
            Clue("counterclaim", "Кредитор подписал приёмку ремонта стоимостью пятнадцать монет.", "встречное требование имеет проверяемое основание"),
            Clue("guarantee", "Поручитель предлагает две выплаты с установленными датами, сохраняя работникам средства на жизнь.", "рассрочка имеет конкретный график и ограниченную гарантию")
        };
        var scenes = new List<ScenarioScene>
        {
            Scene("debt-opening", "Сорок монет", "Кредитор требует расчёта; должник просит учесть ремонт и интересы работников."),
            Scene("debt-balance", "Проверка обязательств", "Сверяют остаток и оплаченный ремонт.", "balance", "counterclaim"),
            Scene("debt-payment", "Способность платить", "Отделяют свободные средства от защищённого содержания работников.", "budget"),
            Scene("debt-schedule", "Две выплаты", "Проверяют даты, объём поручительства и последствия просрочки.", "guarantee"),
            Scene("debt-hearing", "Независимая сверка", "Стороны обсуждают передачу спорных требований на разбор.")
        };
        var proof = new List<string> { "balance", "budget" };
        AddDifficulty(o, clues, scenes, proof, "debt-payment", "protected-funds", "Дополнительная сверка подтверждает, какие средства принадлежат работникам и не входят в имущество должника.");
        var branches = new List<ScenarioBranch>
        {
            Go("debt-opening", "debt-balance", "Сверить сумму", "Обе стороны раскрывают акты платежей и встречных работ."),
            Go("debt-balance", "debt-payment", "Проверить свободные средства", "Основной остаток подтверждён.", "balance"),
            Go("debt-payment", "debt-schedule", "Предложить график", "Мгновенная выплата неприемлема или рискованна; обсуждается гарантия.", "budget"),
            Go("debt-balance", "debt-hearing", "Попросить сверку", "Стороны согласны остановить взыскание до независимого разбора.", "balance"),
            End("debt-payment", "debt-paid", "Выплатить полностью", "Свободных средств хватает, должник согласен платить, кредитор подписывает погашение.", proof.ToArray()),
            End("debt-schedule", "debt-instalments", "Принять рассрочку", "Кредитор, должник и поручитель принимают даты, сумму и ограничение гарантии.", [.. proof, "guarantee"]),
            End("debt-payment", "debt-offset", "Согласовать взаимозачёт", "Стороны засчитывают пятнадцать монет ремонта и оформляют остаток без новых штрафов.", [.. proof, "counterclaim"]),
            End("debt-hearing", "debt-arbitrated", "Передать расчёт арбитру", "Акты переданы, требования остаются спорными до решения.", "balance", "counterclaim"),
            End("debt-opening", "debt-refused", "Отказаться от пакета", "Нет согласованного графика или расчёта; взыскание остаётся спорным, отказ не отменяет долг.")
        };
        var endings = new List<ScenarioEnding>
        {
            Ending("debt-paid", "Долг погашен", "Сорок монет выплачены из свободных средств; кредитор подтверждает закрытие основного обязательства."),
            Ending("debt-instalments", "График под гарантией", "Долг остаётся до двух выплат, поручитель несёт только согласованную ответственность; работники защищены."),
            Ending("debt-offset", "Ремонт в зачёт", "Пятнадцать монет зачтены, остаётся двадцать пять; встречное обязательство закрыто, основной долг сокращён."),
            Ending("debt-arbitrated", "Расчёт у арбитра", "Взыскание приостановлено по соглашению до разбора, размер спорных требований ещё не разрешён."),
            Ending("debt-refused", "Без соглашения", "Долг не прощён и новый график не принят; стороны несут расходы продолжения спора.")
        };
        AddDuration(o, 90, clues, scenes, branches, endings, "debt-schedule", "debt-review", "review-date", "Пересмотр графика", "Поручитель допускает промежуточный пересмотр после первой выплаты при сохранении защищённых средств.");
        return Create(o, t, "debt-settlement", "Остаток по двум актам", "Подтверждён долг в сорок монет, но есть встречный ремонт на пятнадцать; немедленная выплата может затронуть работников.",
            "Гипотеза ведущего: кредитору важнее предсказуемость платежа, чем немедленное взыскание; это можно проверить предложением графика, но нельзя объявить согласием заранее.",
            [Role("должник", "урегулировать расчёт", "хочет сохранить работу и доход", "принять проверенный график", "не платить чужими средствами", "знает бюджет и акты платежей"),
             Role("кредитор", "получить подтверждённый остаток", "ему нужны средства для своей поставки", "принять ограниченную гарантию", "не считать обещание оплатой", "знает поступившие платежи"),
             Role("поручитель", "обеспечить исполнимый график", "не хочет принять бесконечный риск", "гарантировать согласованную сумму", "не покрывать новые неподтверждённые штрафы", "знает предел собственных средств"),
             Role("представитель работников", "защитить содержание работников", "от этих денег зависит жильё семей", "согласовать оплату из свободных средств", "не отдать зарплату за чужой долг", "знает защищённые суммы"),
             Role("сверщик ремонта", "подтвердить встречное требование", "отвечает за правильный зачёт", "дать акт приёмки", "не завысить стоимость работ", "знает цену принятого ремонта"),
             Role("арбитр", "разделить подтверждённое и спорное", "не хочет узаконить непроверенный штраф", "назначить временный порядок", "не решить без актов", "знает процедуру расчёта")], clues, scenes, branches, endings);
    }

    private GeneratedScenario Create(ScenarioGenerationOptions o, ScenarioTemplate t, string id, string title, string premise, string hypothesis,
        IReadOnlyList<ScenarioCharacter> roles, IReadOnlyList<ScenarioClue> clues, IReadOnlyList<ScenarioScene> scenes,
        IReadOnlyList<ScenarioBranch> branches, IReadOnlyList<ScenarioEnding> endings)
    {
        var anchors = (_selector?.SuggestCandidates(t.Id, o.Seed).Anchors ?? [])
            .Where(a => id != "hostage-exchange" || a.SemanticType == "Place").ToArray();
        return new GeneratedScenario
        {
            Title = title, Options = o, Seed = o.Seed, Template = t, NegotiationBlueprintId = id,
            StartSceneId = scenes[0].Id, PublicBriefing = premise, ConflictCause = premise,
            Hook = premise + " " + (o.Tone switch { "Мрачный" => "Промедление усиливает страх сторон.", "Дипломатичный" => "Проверяемые уступки помогают сохранить лицо.", "Приключенческий" => "Посредничество открывает неожиданные мирные решения.", _ => "Доверие придётся подкрепить конкретными гарантиями." }),
            KnownFacts = [premise], NarrativeFacts = [new() { Id = id + "-premise", Text = premise, Origin = ScenarioFactOrigin.FactoryFiction }, new() { Id = id + "-hypothesis", Text = hypothesis, Origin = ScenarioFactOrigin.GmHypothesis }],
            GenerationDiagnostic = anchors.Length == 0 ? "Автономные авторские переговоры: подходящих проверенных опор нет." : "Опоры предложены только как справка; они не доказывают мотивы, права или личность участников.",
            GmNotes = [hypothesis, "Согласие, подпись, оплата и исполнение оценивает ведущий. Улика открывает обсуждение, а не даёт согласие за персонажа.", "Роли вне выбранного количества участников остаются самостоятельными NPC ведущего со своими условиями; их согласие не подразумевается.", "Тон меняет только атмосферу. Высокая сложность добавляет проверку гарантии; длительность добавляет этап независимой проверки и отдельный исход."],
            Characters = roles.Take(o.ParticipantCount).ToArray(), Clues = clues, Scenes = scenes, Branches = branches, Endings = endings, CanonicalAnchors = anchors
        };
    }

    // Duration thresholds: exchange 120, border 150, debt 90 minutes.
    private static void AddDuration(ScenarioGenerationOptions o, int threshold, List<ScenarioClue> clues, List<ScenarioScene> scenes,
        List<ScenarioBranch> branches, List<ScenarioEnding> endings, string from, string sceneId, string clueId, string title, string fact)
    {
        if (o.DurationMinutes < threshold) return;
        clues.Add(Clue(clueId, fact, "промежуточная процедура возможна при отдельном согласии сторон"));
        scenes.Add(Scene(sceneId, title, "Обсуждают промежуточный шаг вместо окончательного урегулирования.", clueId));
        branches.Add(Go(from, sceneId, "Организовать проверку", "Стороны принимают отдельную проверку без обещания окончательного соглашения."));
        branches.Add(End(sceneId, sceneId + "-pending", "Оформить промежуточный шаг", "Стороны согласны на проверку; окончательный пакет ещё не принят.", clueId));
        endings.Add(Ending(sceneId + "-pending", title, "Выполнена промежуточная процедура: " + fact + " Окончательное соглашение отложено до результатов проверки."));
    }
    private static void AddDifficulty(ScenarioGenerationOptions o, List<ScenarioClue> clues, List<ScenarioScene> scenes,
        List<string> proof, string sceneId, string clueId, string text)
    {
        if (o.Difficulty != "Высокая") return;
        clues.Add(Clue(clueId, text, "дополнительная граница полномочий или средств проверена"));
        var index = scenes.FindIndex(s => s.Id == sceneId);
        var scene = scenes[index];
        scenes[index] = Scene(scene.Id, scene.Title, scene.Purpose, [.. scene.ClueIds, clueId]);
        proof.Add(clueId);
    }
    private static ScenarioCharacter Role(string name, string goal, string motive, string concession, string redLine, string information) => new()
    { Name = name, Role = name, Goal = goal, Motive = motive, Concession = concession, RedLine = redLine, PublicInformation = goal, PersonalInformation = information + "; без дополнительных гарантий: " + redLine };
    private static ScenarioClue Clue(string id, string text, string fact) => new() { Id = id, Text = text, KnownFact = fact, Reveals = fact };
    private static ScenarioScene Scene(string id, string title, string purpose, params string[] clues) => new() { Id = id, Title = title, Purpose = purpose, ClueIds = clues };
    private static ScenarioBranch Go(string from, string to, string decision, string condition, params string[] clues) => new() { FromSceneId = from, ToSceneId = to, Decision = decision, Condition = condition, RequiredClueIds = clues };
    private static ScenarioBranch End(string from, string to, string decision, string condition, params string[] clues) => new() { FromSceneId = from, ToEndingId = to, Decision = decision, Condition = condition, RequiredClueIds = clues };
    private static ScenarioEnding Ending(string id, string title, string outcome) => new() { Id = id, Title = title, Outcome = outcome };
}
