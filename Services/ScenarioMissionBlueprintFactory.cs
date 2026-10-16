using SkyrimRpEncyclopedia.Models;

namespace SkyrimRpEncyclopedia.Services;

/// <summary>Authored missions; canonical anchors are proposed reference material only.</summary>
public sealed class ScenarioMissionBlueprintFactory
{
    public static IReadOnlyList<string> BlueprintIds { get; } = ["escort", "recovery", "sabotage-response"];
    private readonly ScenarioCanonicalAnchorSelector? _selector;

    public ScenarioMissionBlueprintFactory(IScenarioKnowledgeCatalog? catalog = null) =>
        _selector = catalog is null ? null : new ScenarioCanonicalAnchorSelector(catalog);

    public GeneratedScenario Build(ScenarioGenerationOptions options, ScenarioTemplate template)
    {
        // Explicit ordinal mapping is independent of process hash randomization and collection order.
        var id = BlueprintIds[(options.Seed & int.MaxValue) % BlueprintIds.Count];
        var source = id switch
        {
            "escort" => ScenarioGeneratorService.BuildMountainPass(options, template),
            "recovery" => Recovery(options, template),
            _ => SabotageResponse(options, template)
        };
        var anchors = _selector?.SuggestCandidates(template.Id, options.Seed).Anchors ?? [];
        var hypothesis = id == "escort"
            ? "Гипотеза ведущего: обвал у теснины мог быть устроен для контроля короткого пути; проверяемые следы и действия игроков определяют, что станет известно."
            : source.GmNotes[0];
        return new GeneratedScenario
        {
            Title = source.Title, Seed = options.Seed, Options = options, Template = template,
            MissionBlueprintId = id, Hook = source.Hook, ConflictCause = source.ConflictCause,
            StartSceneId = source.StartSceneId, PublicBriefing = source.PublicBriefing, KnownFacts = source.KnownFacts,
            NarrativeFacts =
            [
                new() { Id = id + "-premise", Text = source.PublicBriefing, Origin = ScenarioFactOrigin.FactoryFiction },
                new() { Id = id + "-explanation", Text = hypothesis, Origin = ScenarioFactOrigin.GmHypothesis }
            ],
            GenerationDiagnostic = anchors.Count == 0
                ? "Проверенные опоры не найдены: автономная авторская миссия без вымышленного канона."
                : "Опоры предложены как справка. Авторское поручение и гипотезы не являются фактами канона.",
            GmNotes = [hypothesis, .. source.GmNotes.Skip(1),
                "Условия согласия, срок и расход ресурсов оценивает ведущий; наличие улики само по себе не означает согласия стороны.",
                "Опоры не доказывают мотив, виновность или право владения; авторские роли не являются каноническими персонажами."],
            Characters = source.Characters, Clues = source.Clues, Scenes = source.Scenes,
            Branches = source.Branches, Endings = source.Endings, CanonicalAnchors = anchors
        };
    }

    private static GeneratedScenario Recovery(ScenarioGenerationOptions o, ScenarioTemplate t)
    {
        var difficult = o.Difficulty == "Высокая";
        var clues = new List<ScenarioClue>
        {
            Clue("ownership", "Старая опись и номер печати связывают документ с архивом, но не отменяют права хранителя требовать компенсацию.", "предмет идентифицирован; условия возврата спорны"),
            Clue("custody", "Расписка подтверждает, что хранитель принял документ после пожара, не покупая его.", "текущее хранение не равнозначно собственности"),
            Clue("copy", "Сверенная копия сохраняет сведения для разбирательства, пока оригинал остаётся опечатанным.", "возможна независимая опека вместо немедленного изъятия")
        };
        if (difficult) clues.Add(Clue("expenses", "Счёт за спасение и хранение признан обеими сторонами, но ещё не оплачен.", "возврат требует согласования расходов, а не обвинения в краже"));
        var scenes = new List<ScenarioScene>
        {
            Scene("recovery-request", "Заказ на возврат", "Архив просит вернуть оригинал, а хранитель отказывается от безусловной передачи."),
            Scene("recovery-identify", "Право и предмет", "Стороны сверяют опись и расписку.", "ownership", "custody"),
            Scene("recovery-terms", "Условия передачи", "Игроки предлагают компенсацию и порядок получения оригинала.", difficult ? ["expenses"] : []),
            Scene("recovery-custody", "Независимое хранение", "Снимается проверенная копия; оригинал передают нейтральному хранителю только по соглашению.", "copy")
        };
        var proof = difficult ? new[] { "ownership", "custody", "expenses" } : new[] { "ownership", "custody" };
        var branches = new List<ScenarioBranch>
        {
            Go("recovery-request", "recovery-identify", "Сверить право", "Стороны допускают проверку документов."),
            Go("recovery-identify", "recovery-terms", "Договориться о возврате", "Предмет и история хранения установлены.", "ownership", "custody"),
            Go("recovery-identify", "recovery-custody", "Предложить опеку", "Стороны обсуждают сохранение оригинала вместо изъятия.", "custody"),
            End("recovery-terms", "recovery-return", "Вернуть по соглашению", "Хранитель согласен передать документ; стороны принимают условия расходов.", proof),
            End("recovery-terms", "recovery-compensation", "Оплатить спасение", "Архив принимает цену возврата и получает оригинал с новым обязательством.", proof),
            End("recovery-custody", "recovery-sealed", "Оставить под опекой", "Стороны согласны на проверенную копию и нейтральное хранение оригинала.", "ownership", "copy"),
            End("recovery-request", "recovery-refused", "Отказаться от изъятия", "Герои возвращаются без предмета, объясняя нерешённый спор о передаче.")
        };
        if (o.DurationMinutes >= 120)
        {
            clues.Add(Clue("hearing-date", "Архив согласен ждать до назначенного разбирательства.", "отсрочка возможна без уничтожения доказательств"));
            scenes.Add(Scene("recovery-hearing", "Отсроченная передача", "Герои закрепляют срок и ответственность хранителя.", "hearing-date"));
            branches.Add(Go("recovery-custody", "recovery-hearing", "Назначить разбирательство", "Проверенная копия позволяет сохранить работу архива.", "copy"));
            branches.Add(End("recovery-hearing", "recovery-deferred", "Закрепить отсрочку", "Хранитель сохраняет оригинал до разбора, принимая письменную ответственность.", "hearing-date"));
        }
        return Story(o, t, "Возврат под расписку", "После пожара документ оказался у хранителя; архив требует оригинал, но расходы спасения и условия передачи не урегулированы.",
            "Гипотеза ведущего: хранитель опасается не утраты денег, а уничтожения записи после возврата; это предположение проверяется разговором, а не объявляется фактом.",
            [Role("архивный посланник", "получить оригинал", "архив не может закрыть передачу без документа", "принять копию на время", "не уничтожать свидетельства"),
             Role("хранитель", "сохранить предмет до соглашения", "несёт расходы спасения", "принять независимую опеку", "не отдавать без расписки"),
             Role("представитель владельца", "восстановить право", "опись нужна для защиты имущества", "согласовать компенсацию", "не признать покупку, которой не было"),
             Role("сверщик", "проверить соответствие", "ошибка навредит обеим сторонам", "сделать копию", "не заверять подмену"),
             Role("посредник", "обеспечить передачу", "обе стороны доверили ему процедуру", "принять хранение", "не присваивать оригинал"),
             Role("свидетель пожара", "сохранить хронологию", "его показания объясняют хранение", "дать письменные сведения", "не обвинять без основания")], clues, scenes, branches,
            [Ending("recovery-return", "Возврат по соглашению", "Оригинал возвращён без нового долга; спор закрыт взаимной распиской."),
             Ending("recovery-compensation", "Цена спасения", "Оригинал возвращён, архив оплачивает расходы и принимает обязательство перед хранителем."),
             Ending("recovery-sealed", "Оригинал под опекой", "Архив работает с копией; оригинал недоступен обеим сторонам до независимого решения."),
             Ending("recovery-refused", "Возврат без предмета", "Поручение не выполнено, оригинал остаётся у хранителя; насильственного изъятия не происходит."),
             .. (o.DurationMinutes >= 120 ? new[] { Ending("recovery-deferred", "Передача отложена", "Хранитель сохраняет оригинал до назначенного срока и отвечает за его сохранность; право ещё не разрешено.") } : [])]);
    }

    private static GeneratedScenario SabotageResponse(ScenarioGenerationOptions o, ScenarioTemplate t)
    {
        var difficult = o.Difficulty == "Высокая";
        var clues = new List<ScenarioClue>
        {
            Clue("damage-map", "Осмотр показывает повреждённый затвор и направление воды к нижним домам.", "нужно остановить поток или вывести людей"),
            Clue("bypass-plan", "План старого канала позволяет отвести воду, затопив пустой склад.", "ущерб можно ограничить ценой имущества"),
            Clue("assembly", "Запасной штифт подходит к затвору; мастер может закрепить его при остановленном потоке.", "ремонт возможен после безопасной изоляции")
        };
        if (difficult) clues.Add(Clue("cut-rope", "Основная страховочная верёвка перерезана; найдена независимая опора для временного крепления.", "ремонт без новой страховки опасен; виновник не установлен"));
        var scenes = new List<ScenarioScene>
        {
            Scene("response-alarm", "Вода у домов", "Игроки решают, спасать людей немедленно или сначала проверить затвор."),
            Scene("response-inspect", "Осмотр затвора", "Проверяется направление угрозы и состояние креплений.", difficult ? ["damage-map", "cut-rope"] : ["damage-map"]),
            Scene("response-divert", "Цена отвода", "Выбирают затопление пустого склада вместо жилых домов.", "bypass-plan"),
            Scene("response-repair", "Временное крепление", "Мастер оценивает ремонт, не требуя назвать диверсанта.", "assembly"),
            Scene("response-evacuate", "Вывод жителей", "Эвакуация сохраняет жизни, но не устраняет повреждение.")
        };
        var repairProof = difficult ? new[] { "damage-map", "assembly", "cut-rope" } : new[] { "damage-map", "assembly" };
        var branches = new List<ScenarioBranch>
        {
            Go("response-alarm", "response-inspect", "Проверить угрозу", "Игроки организуют осмотр, не подменяя спасение поиском виновника."),
            Go("response-alarm", "response-evacuate", "Сразу вывести людей", "Жители принимают помощь и уходят выше по склону."),
            Go("response-inspect", "response-divert", "Отвести поток", "Игроки оценивают направление воды.", "damage-map"),
            Go("response-inspect", "response-repair", "Изолировать и починить", "Мастер согласен работать после остановки воды.", "damage-map"),
            Go("response-divert", "response-repair", "Починить после отвода", "Отвод даёт мастеру время для ремонта.", "bypass-plan"),
            End("response-divert", "response-contained", "Принять потерю склада", "Отвод согласован, люди защищены ценой имущества.", "damage-map", "bypass-plan"),
            End("response-repair", "response-restored", "Закрепить затвор", "Поток изолирован, мастер завершает ремонт со страховкой.", repairProof),
            End("response-evacuate", "response-evacuated", "Сохранить людей", "Эвакуация завершена; строения остаются под угрозой."),
            End("response-alarm", "response-uncontained", "Отложить вмешательство", "Герои не согласуют безопасное действие до прихода воды; угроза реализуется.")
        };
        if (o.DurationMinutes >= 120)
        {
            clues.Add(Clue("watch-plan", "Сменные наблюдатели могут удерживать временный затвор до прибытия мастеров.", "аварийная мера требует постоянной смены людей"));
            scenes.Add(Scene("response-watch", "Ночная смена", "Игроки организуют временную защиту вместо полного ремонта.", "watch-plan"));
            branches.Add(Go("response-inspect", "response-watch", "Организовать дежурство", "Стороны обсуждают временный заслон.", "damage-map"));
            branches.Add(End("response-watch", "response-temporary", "Удержать до утра", "Есть согласованная смена наблюдателей; ремонт отложен.", "watch-plan"));
        }
        return Story(o, t, "Затвор до прилива", "Повреждённый затвор угрожает нижним домам. Нужно предотвратить ущерб; поиск виновника может подождать.",
            "Гипотеза ведущего: крепление повредили намеренно ради остановки мельницы; ни осмотр, ни перерезанная верёвка не доказывают личность или мотив виновника.",
            [Role("мастер затвора", "восстановить механизм", "от воды зависит работа общины", "принять временный ремонт", "не работать без изоляции"),
             Role("представитель жителей", "вывести людей", "нижние дома первыми примут поток", "принять эвакуацию", "не ждать обвинения ради спасения"),
             Role("владелец склада", "сохранить запас", "отвод затопит его имущество", "согласовать потерю с компенсацией", "не объявлять согласие за него"),
             Role("смотритель канала", "ограничить поток", "отвечает за старый отвод", "организовать заслон", "не открывать неизвестный канал"),
             Role("лекарь", "помочь эвакуированным", "переохлаждение опасно", "выделить укрытие", "не отправлять раненых к затвору"),
             Role("посыльный", "вызвать мастеров", "временная защита не заменяет ремонт", "взять ночную смену", "не обещать мгновенную помощь")], clues, scenes, branches,
            [Ending("response-restored", "Затвор восстановлен", "Поток остановлен, дома сохранены и механизм работает; виновник остаётся неизвестным."),
             Ending("response-contained", "Ущерб ограничен", "Дома защищены отводом, склад затоплен, полноценный ремонт ещё предстоит."),
             Ending("response-evacuated", "Люди спасены", "Жители в укрытии, но вода повреждает дома и поручение по ремонту не выполнено."),
             Ending("response-uncontained", "Цена промедления", "Защита не организована; вода достигает домов, требуется спасение после затопления."),
             .. (o.DurationMinutes >= 120 ? new[] { Ending("response-temporary", "До смены мастеров", "Временная защита удерживает воду ценой непрерывного дежурства; механизм ещё не восстановлен.") } : [])]);
    }

    private static GeneratedScenario Story(ScenarioGenerationOptions o, ScenarioTemplate t, string title, string premise, string hypothesis,
        IReadOnlyList<ScenarioCharacter> roles, IReadOnlyList<ScenarioClue> clues, IReadOnlyList<ScenarioScene> scenes,
        IReadOnlyList<ScenarioBranch> branches, IReadOnlyList<ScenarioEnding> endings) => new()
    {
        Title = title, Seed = o.Seed, Options = o, Template = t, StartSceneId = scenes[0].Id,
        Hook = premise + " " + (o.Tone switch
        {
            "Мрачный" => "Цена промедления видна каждому, но страх не заменяет проверку.",
            "Дипломатичный" => "Стороны готовы обсуждать проверяемые уступки.",
            "Приключенческий" => "Есть несколько смелых решений без обязательного боя.",
            _ => "Срок требует выбрать действие, не дожидаясь полной уверенности."
        }),
        ConflictCause = premise, PublicBriefing = premise, KnownFacts = [premise], GmNotes = [hypothesis],
        Characters = roles.Take(o.ParticipantCount).ToArray(), Clues = clues, Scenes = scenes, Branches = branches, Endings = endings
    };
    private static ScenarioCharacter Role(string role, string goal, string motive, string concession, string redLine) =>
        new() { Name = role, Role = role, Goal = goal, Motive = motive, Concession = concession, RedLine = redLine,
            PublicInformation = goal, PersonalInformation = motive };
    private static ScenarioClue Clue(string id, string text, string fact) => new() { Id = id, Text = text, KnownFact = fact, Reveals = fact };
    private static ScenarioScene Scene(string id, string title, string purpose, params string[] clues) => new() { Id = id, Title = title, Purpose = purpose, ClueIds = clues };
    private static ScenarioBranch Go(string from, string to, string decision, string condition, params string[] clues) =>
        new() { FromSceneId = from, ToSceneId = to, Decision = decision, Condition = condition, RequiredClueIds = clues };
    private static ScenarioBranch End(string from, string to, string decision, string condition, params string[] clues) =>
        new() { FromSceneId = from, ToEndingId = to, Decision = decision, Condition = condition, RequiredClueIds = clues };
    private static ScenarioEnding Ending(string id, string title, string outcome) => new() { Id = id, Title = title, Outcome = outcome };
}
