using SkyrimRpEncyclopedia.Models;

namespace SkyrimRpEncyclopedia.Services;

public sealed class ScenarioGeneratorService
{
    public const int MaxBuildAttempts = 3;
    private readonly ScenarioInvestigationBlueprintFactory _investigationBlueprints;
    private readonly ScenarioIntrigueBlueprintFactory _intrigueBlueprints;
    private readonly ScenarioMissionBlueprintFactory _missionBlueprints;
    private readonly ScenarioNegotiationBlueprintFactory _negotiationBlueprints;

    public ScenarioGeneratorService(IScenarioKnowledgeCatalog? knowledgeCatalog = null)
    {
        _investigationBlueprints = new ScenarioInvestigationBlueprintFactory(knowledgeCatalog);
        _intrigueBlueprints = new ScenarioIntrigueBlueprintFactory(knowledgeCatalog);
        _missionBlueprints = new ScenarioMissionBlueprintFactory(knowledgeCatalog);
        _negotiationBlueprints = new ScenarioNegotiationBlueprintFactory(knowledgeCatalog);
    }

    private static readonly IReadOnlyList<ScenarioTemplate> Templates =
    [
        new() { Id="negotiation", Name="Последняя повозка до заката", Genre="Переговоры", MinParticipants=2, MaxParticipants=6, MinDurationMinutes=60, MaxDurationMinutes=180, ConflictCause="в накладной указано восемь ящиков, а в повозке оказался девятый; возница не желает отвечать за несоответствие", Hook="Перед закатом от трактира уходит последняя повозка. Торговцу нужно отправить груз, но возница остановил погрузку из-за расхождения в накладной.", RequiresCombat=false },
        new() { Id="investigation", Name="Пепел на печати", Genre="Расследование", MinParticipants=4, MaxParticipants=6, MinDurationMinutes=60, MaxDurationMinutes=180, ConflictCause="повреждённая печать на распоряжении о зерне остановила выдачу, а поспешное обвинение может наказать не того человека", Hook="Утром на распоряжении о выдаче зерна находят пепел и повреждённую печать. Склад заперт, а квартал ждёт решения.", RequiresCombat=false },
        new() { Id="mission", Name="Поручение через перевал", Genre="Опасное поручение", MinParticipants=4, MaxParticipants=6, MinDurationMinutes=60, MaxDurationMinutes=180, ConflictCause="до рассвета нужно доставить запечатанный приказ и лекарство на Каменную заставу, но лавина перекрыла дорогу, а короткий путь контролируют контрабандисты", Hook="Курьер не вернулся с перевала. Его сумка с приказом и лекарством должна попасть на Каменную заставу до рассвета.", RequiresCombat=false },
        new() { Id="intrigue", Name="Голос совета", Genre="Фракционная интрига", MinParticipants=4, MaxParticipants=6, MinDurationMinutes=90, MaxDurationMinutes=240, ConflictCause="совет Морфала должен распределить шестьдесят мешков зимнего зерна между гарнизоном и кварталом, но запас не покрывает требования обеих сторон", Hook="Перед вечерним голосованием совет Морфала запирает зерновую книгу: решение определит, кто переживёт следующую неделю без потерь.", RequiresCombat=false },
        new() { Id="conflict", Name="Долг старого друга", Genre="Личный конфликт", MinParticipants=4, MaxParticipants=6, MinDurationMinutes=60, MaxDurationMinutes=180, ConflictCause="старый друг просит спрятать долговую книгу, но она одновременно доказывает ущерб третьей стороне и вымогательство судебного пристава", Hook="Старый друг просит забрать долговую книгу до рассвета — когда-то он спас героев, но теперь его просьба может лишить другого человека справедливости.", RequiresCombat=false },
    ];

    public IReadOnlyList<ScenarioTemplate> GetTemplates() => Templates;

    public ScenarioTemplate? GetTemplateByGenre(string? genre) =>
        string.IsNullOrWhiteSpace(genre)
            ? null
            : Templates.SingleOrDefault(template => template.Genre.Equals(genre, StringComparison.OrdinalIgnoreCase));

    public ScenarioGenerationResult Generate(ScenarioGenerationOptions options)
    {
        if (GetTemplateByGenre(options.Genre) is null)
        {
            return new ScenarioGenerationResult
            {
                Validation = new ScenarioValidationResult { Errors = ["Выберите один из доступных жанров сценария."] },
                Diagnostic = "Неизвестный или пустой жанр не может быть подменён другим каркасом."
            };
        }
        for (var attempt = 0; attempt < MaxBuildAttempts; attempt++)
        {
            var candidate = Build(options, attempt);
            var validation = ScenarioValidator.Validate(candidate);
            if (validation.IsValid) return new ScenarioGenerationResult { Scenario = candidate, Validation = validation };
        }
        return new ScenarioGenerationResult { Validation = new ScenarioValidationResult { Errors = ["Не удалось собрать согласованный сценарий за ограниченное число попыток."] }, Diagnostic = "Проверьте жанр, число участников и длительность." };
    }

    private GeneratedScenario Build(ScenarioGenerationOptions options, int attempt)
    {
        var compatible = Templates.Where(t => t.Genre.Equals(options.Genre, StringComparison.OrdinalIgnoreCase) && options.ParticipantCount >= t.MinParticipants && options.ParticipantCount <= t.MaxParticipants && options.DurationMinutes >= t.MinDurationMinutes && options.DurationMinutes <= t.MaxDurationMinutes && (options.AllowMandatoryCombat || !t.RequiresCombat)).ToArray();
        if (compatible.Length == 0) return new GeneratedScenario { Options = options, Seed = options.Seed, Template = new ScenarioTemplate { Name = "Несовместимые параметры" } };
        var random = new Random(unchecked(options.Seed + attempt));
        var template = compatible[random.Next(compatible.Length)];
        return template.Id switch
        {
            "negotiation" => _negotiationBlueprints.Build(options, template),
            "investigation" => _investigationBlueprints.Build(options, template),
            "mission" => _missionBlueprints.Build(options, template),
            "intrigue" => _intrigueBlueprints.Build(options, template),
            "conflict" => BuildOldFriend(options, template),
            _ => BuildGeneric(options, template, random)
        };
    }

    private static GeneratedScenario BuildOldFriend(ScenarioGenerationOptions o, ScenarioTemplate t)
    {
        var highDifficulty = o.Difficulty == "Высокая";
        var toneComplication = o.Tone switch
        {
            "Мрачный" => "Ночной дождь размывает следы у мельницы, а каждый час делает чужую потерю менее заметной для тех, кто хочет забыть её.",
            "Дипломатичный" => "Слова о старой услуге звучат мягко, но все присутствующие понимают: благодарность не равна согласию.",
            "Приключенческий" => "До рассвета книгу можно отнести к храму, мельнице или приставу — и каждый путь станет обещанием новой истории.",
            _ => "Письмо о старом долге лежит на столе рядом с книгой, а время до рассвета не позволяет никому спрятаться за общими словами."
        };
        var hypotheses = new[]
        {
            new ScenarioHypothesis { Id="friend-victim", Statement="Свен и его племянница стали жертвами ложного долга; книгу нужно спрятать немедленно.", Beneficiary="Свен и Несса.", Cost="Вира теряет возможность доказать ущерб, а пристав остаётся без проверки.", Unknown="Кто и зачем менял записи?" },
            new ScenarioHypothesis { Id="niece-guilt", Statement="Несса сознательно подменила записи и должна отвечать за ущерб мельнице.", Beneficiary="Вира получает путь к компенсации.", Cost="Защитный мотив Нессы и злоупотребления пристава могут остаться скрыты.", Unknown="Была ли запись единственной причиной потерь?" },
            new ScenarioHypothesis { Id="bailiff-extortion", Statement="Пристав Хальден использует настоящую ошибку для вымогательства и подделал добавочный долг.", Beneficiary="Хальден, пока книга остаётся только у него.", Cost="Все стороны рискуют стать его должниками или обвиняемыми.", Unknown="Кто осмелится передать книгу независимому судье?" },
        };
        var clues = new List<ScenarioClue>
        {
            new() { Id="old-ledger", Text="Старая запись показывает, что Несса отвела зимнее масло к убежищу, а не в личный карман.", KnownFact="ошибка Нессы была реальной, но её целью было согреть людей в убежище", Reveals="долг Свена нельзя свести к невиновности или корысти", SupportsHypothesisIds=["niece-guilt"], ContradictsHypothesisIds=["friend-victim"] },
            new() { Id="miller-receipt", Text="Расписка Виры подтверждает простой мельницы и реальный ущерб её семье.", KnownFact="последствия отвода масла затронули третьих лиц", Reveals="цена помощи Свену не абстрактна", SupportsHypothesisIds=["niece-guilt"], ContradictsHypothesisIds=[] },
            new() { Id="bailiff-seal", Text="Добавочный долг скреплён устаревшей печатью Хальдена и вписан позднее основного расчёта.", KnownFact="пристав исказил сумму долга", Reveals="независимая проверка способна защитить всех от вымогательства", SupportsHypothesisIds=["bailiff-extortion"], ContradictsHypothesisIds=["friend-victim"] },
            new() { Id="temple-record", Text="Храмовый список подтверждает: в ночь отвода масла убежище действительно грело детей и раненых.", KnownFact="у Нессы был защитный мотив", Reveals="объяснение не отменяет ущерб, но меняет условия компромисса", SupportsHypothesisIds=["friend-victim"], ContradictsHypothesisIds=[] },
        };
        if (highDifficulty) clues.Add(new ScenarioClue { Id="witness-statement", Text="Старый мельник видел, как Хальден угрожал Нессe до появления добавочного долга.", KnownFact="пристав давил на семью ещё до официального требования", Reveals="вымогательство можно доказать независимо от книги", SupportsHypothesisIds=["bailiff-extortion"], ContradictsHypothesisIds=[] });
        if (o.DurationMinutes >= 120) clues.Add(new ScenarioClue { Id="audit-petition", Text="Храмовый судья готов принять книгу в опечатанное хранение и назначить утреннюю проверку.", KnownFact="есть независимый путь без уничтожения книги и немедленного публичного позора", Reveals="возможность формального компромисса", SupportsHypothesisIds=["bailiff-extortion"], ContradictsHypothesisIds=[] });
        var scenes = new List<ScenarioScene>
        {
            new() { Id="opening", Title="Письмо из прошлого", Purpose="Свен напоминает о спасении героев и просит забрать книгу до рассвета; игроки решают, доверять ли просьбе." },
            new() { Id="friend", Title="Разговор со Свеном", Purpose="Друг объясняет свой страх за Нессу, но не может честно назвать цену для Виры.", ClueIds=["old-ledger"] },
            new() { Id="mill", Title="Мельница Виры", Purpose="Игроки видят последствия остановки мельницы и получают возможность говорить о компенсации.", ClueIds=["miller-receipt"] },
            new() { Id="archive", Title="Канцелярия пристава", Purpose="Проверьте, кто и когда добавил долг к старой записи.", ClueIds=highDifficulty ? ["bailiff-seal", "witness-statement"] : ["bailiff-seal"] },
            new() { Id="mediation", Title="Цена долга", Purpose="Игроки сопоставляют старую услугу, ущерб Виры и вымогательство Хальдена; доверие не считается решённым автоматически." },
        };
        if (o.DurationMinutes >= 120) scenes.Add(new ScenarioScene { Id="temple", Title="Храмовая опека", Purpose="Храм предлагает опечатать книгу и провести утреннюю проверку без немедленного приговора.", ClueIds=["temple-record", "audit-petition"] });
        else scenes[1] = new ScenarioScene { Id="friend", Title="Разговор со Свеном", Purpose="Друг объясняет свой страх за Нессу, но не может честно назвать цену для Виры.", ClueIds=["old-ledger", "temple-record"] };
        var trueProof = highDifficulty ? new[] { "old-ledger", "miller-receipt", "bailiff-seal", "witness-statement" } : new[] { "old-ledger", "miller-receipt", "bailiff-seal" };
        var branches = new List<ScenarioBranch>
        {
            new() { FromSceneId="opening", ToSceneId="friend", Decision="Выслушать старого друга", Condition="Игроки признают прошлый долг как повод поговорить, но не как приказ." },
            new() { FromSceneId="opening", ToSceneId="mill", Decision="Сначала проверить ущерб", Condition="Игроки не хотят принимать просьбу Свена без голоса третьей стороны." },
            new() { FromSceneId="opening", ToEndingId="refuse-honestly", Decision="Отказаться от участия", Condition="Игроки честно говорят Свену, что не могут тайно забирать книгу без понимания последствий." },
            new() { FromSceneId="friend", ToSceneId="mill", Decision="Проверить цену помощи", Condition="Игроки хотят узнать, кого затронет исчезновение книги." },
            new() { FromSceneId="friend", ToSceneId="archive", Decision="Проверить долг", Condition="Игроки сомневаются в добавочной сумме и ищут запись пристава." },
            new() { FromSceneId="mill", ToSceneId="archive", Decision="Искать источник давления", Condition="Реальный ущерб не объясняет, почему Хальден требует именно эту сумму." },
            new() { FromSceneId="archive", ToSceneId="mediation", Decision="Сопоставить записи", Condition="Игроки готовы обсуждать помощь, компенсацию и поддельный долг.", RequiredClueIds=["bailiff-seal"] },
            new() { FromSceneId="mill", ToSceneId="mediation", Decision="Предложить разговор", Condition="Игроки пытаются остановить конфликт до нового вреда." },
            new() { FromSceneId="mediation", ToEndingId="hide-ledger", Decision="Спрятать книгу для Свена", Condition="Игроки ставят старый долг выше права Виры на проверку ущерба." },
            new() { FromSceneId="mediation", ToEndingId="public-niece-charge", Decision="Передать Нессу на публичный суд", Condition="Игроки считают подтверждённый ущерб важнее защитного мотива.", RequiredClueIds=["old-ledger", "miller-receipt"] },
            new() { FromSceneId="mediation", ToEndingId="fair-compromise", Decision="Передать книгу независимому хранителю", Condition="Игроки требуют проверку долга, компенсацию Вире и защиту Нессы от вымогательства.", RequiredClueIds=trueProof },
            new() { FromSceneId="mediation", ToEndingId="refuse-honestly", Decision="Отказаться от тайной помощи", Condition="Игроки не берут на себя чужой долг и открыто объясняют пределы дружбы." },
        };
        if (o.DurationMinutes >= 120)
        {
            branches.Add(new ScenarioBranch { FromSceneId="friend", ToSceneId="temple", Decision="Искать независимую опеку", Condition="Игроки не хотят выбирать между уничтожением книги и немедленным обвинением." });
            branches.Add(new ScenarioBranch { FromSceneId="archive", ToSceneId="temple", Decision="Унести книгу из-под власти пристава", Condition="Игроки хотят сохранить доказательство и не отдать его вымогателю." });
            branches.Add(new ScenarioBranch { FromSceneId="temple", ToSceneId="mediation", Decision="Вернуться с формальным путём", Condition="Храм готов принять книгу и объяснить сторонам условия проверки.", RequiredClueIds=["audit-petition"] });
        }
        var endings = new[]
        {
            new ScenarioEnding { Id="hide-ledger", Title="Долг без свидетелей", Outcome="Свен получает книгу, Несса избегает немедленного суда, но Вира теряет путь к компенсации, а Хальден сохраняет рычаги давления.", IsReachable=true },
            new ScenarioEnding { Id="public-niece-charge", Title="Правда без милости", Outcome="Ущерб Виры признают публично, а Несса отвечает за поступок. Защитный мотив остаётся известен, но отношения со Свеном повреждены.", IsReachable=true },
            new ScenarioEnding { Id="fair-compromise", Title="Книга под печатью", Outcome="Книгу передают независимому хранителю: Вира получает шанс на компенсацию, Несса — на честное объяснение, а Хальден — на проверку вымогательства.", IsReachable=true },
            new ScenarioEnding { Id="refuse-honestly", Title="Предел старого долга", Outcome="Герои не берут книгу. Свен остаётся с выбором сам, но получает честный ответ; ущерб Виры и давление Хальдена остаются нерешёнными.", IsReachable=true },
        };
        return new GeneratedScenario { Title=t.Name, Seed=o.Seed, Options=o, Template=t, Hook=$"{t.Hook} {toneComplication}", ConflictCause=t.ConflictCause, StartSceneId="opening", PublicBriefing="Свен когда-то спас героев и теперь просит забрать долговую книгу до рассвета. Вира, хозяйка мельницы, считает книгу единственным доказательством ущерба.", KnownFacts=["Старый долг Свена перед героями реален, но не отменяет интересы Виры.", "Книга содержит записи о масле, мельнице и добавочном долге.", "До рассвета Хальден потребует книгу и сможет навязать собственное толкование долга."], Hypotheses=hypotheses, GmNotes=["Объективная истина: Несса отвела масло к убежищу, чтобы согреть детей и раненых; этим она реально остановила мельницу и нанесла Вире ущерб. Хальден использовал ошибку, чтобы вписать ложный добавочный долг и вымогать деньги.", "Свен действительно спас героев раньше. Это моральное обязательство, а не автоматическое согласие на тайную помощь.", "Если игроки предлагают иной план, ведущий соотносит его с четырьмя ценами: судьба Нессы, компенсация Вире, власть Хальдена и отношения со Свеном."], Characters=BuildOldFriendRoles(o.ParticipantCount), Clues=clues, Scenes=scenes, Branches=branches, Endings=endings };
    }

    private static IReadOnlyList<ScenarioCharacter> BuildOldFriendRoles(int count)
    {
        var roles = new List<ScenarioCharacter>
        {
            new() { Name="Свен", Role="старый друг", Goal="защитить Нессу от пристава", Motive="когда-то он спас героев и не хочет снова бросить близкого человека", PublicInformation="Просит забрать книгу до рассвета, ссылаясь на старый долг.", PersonalInformation="Знает, что Несса отводила масло к убежищу.", Concession="Согласен передать книгу храму, если Нессу не отдадут Хальдену сразу.", RedLine="Не позволит отдать книгу приставу без независимой проверки.", Secret="Умалчивает о реальном ущербе Вире, потому что боится потерять помощь героев." },
            new() { Name="Несса", Role="племянница Свена", Goal="не дать убежищу и семье развалиться", Motive="она выбирала между холодом детей и остановкой мельницы", PublicInformation="Утверждает, что книга использована против неё несправедливо.", PersonalInformation="Знает, где в книге следы отвода масла.", Concession="Готова признать ошибку и возместить часть ущерба при защите от вымогательства.", RedLine="Не согласится на тайную сделку с Хальденом.", Secret="Сама изменила запись об отвезённом масле, чтобы скрыть путь к убежищу." },
            new() { Name="Вира", Role="хозяйка мельницы", Goal="получить признание ущерба и компенсацию", Motive="простой мельницы лишил её семью дохода", PublicInformation="Требует сохранить книгу как доказательство.", PersonalInformation="Знает, что Хальден завысил добавочный долг, но боится спорить с ним в одиночку.", Concession="Примет храмовую проверку и рассрочку компенсации.", RedLine="Не согласится, чтобы книгу уничтожили или спрятали навсегда.", Secret="Уже обещала часть будущей компенсации торговцу в обмен на муку." },
            new() { Name="Хальден", Role="судебный пристав", Goal="получить книгу и контроль над долгом", Motive="вымогательство даёт ему деньги и влияние", PublicInformation="Называет себя единственным законным хранителем доказательств.", PersonalInformation="Знает, что устаревшая печать выдаст добавочный долг.", Concession="Согласится на отсрочку только при сохранении своего контроля над книгой.", RedLine="Не допустит независимой проверки печати.", Secret="Вписал добавочный долг после основного расчёта." },
            new() { Name="Сестра Иара", Role="храмовая посредница", Goal="не дать спору разрушить семьи", Motive="храм уже помогал убежищу и знает цену скандала", PublicInformation="Может предложить опечатанное хранение книги.", PersonalInformation="Знает порядок утренней проверки у храмового судьи.", Concession="Примет книгу и назначит слушание до рассвета.", RedLine="Не будет хранить книгу, если стороны требуют скрыть её навсегда.", Secret="Боится, что Хальден лишит храм разрешения на раздачу помощи." },
            new() { Name="Радд", Role="старый мельник", Goal="не допустить новой лжи", Motive="видел последствия остановки мельницы", PublicInformation="Помнит ночь с маслом и спор у склада.", PersonalInformation="Слышал угрозы Хальдена Нессe.", Concession="Готов свидетельствовать при защите от пристава.", RedLine="Не подпишет ложное признание Нессы.", Secret="Сначала хотел продать своё молчание Вире, но передумал." },
        };
        return roles.Take(count).ToArray();
    }

    private static GeneratedScenario BuildCouncilVoice(ScenarioGenerationOptions o, ScenarioTemplate t)
    {
        var highDifficulty = o.Difficulty == "Высокая";
        var toneComplication = o.Tone switch
        {
            "Мрачный" => "За окнами уже собирается очередь за пайками, и каждая уступка звучит как обещание, которое придётся нарушить.",
            "Дипломатичный" => "Даже самые жёсткие голоса готовы слушать, пока никто не вынуждает их публично потерять лицо.",
            "Приключенческий" => "Гонцы и охотники ждут решения у дверей: политический выбор сразу определит, кого поведут по опасной дороге.",
            _ => "Колокол совета отмеряет раунды переговоров, а каждое слово меняет цену будущей коалиции."
        };
        var clues = new List<ScenarioClue>
        {
            new() { Id="granary-count", Text="Пересчёт подтверждает: в запасе ровно шестьдесят мешков, а обе стороны просят по сорок.", KnownFact="совет не может выполнить оба исходных требования", Reveals="неизбежность чьей-то уступки" },
            new() { Id="road-report", Text="Донесение дозора: без тридцати мешков для гарнизона патруль сократит ночные выезды.", KnownFact="зерно для гарнизона влияет на защиту дороги", Reveals="цену решения в пользу квартала" },
            new() { Id="ration-roll", Text="Храмовый список показывает, что сокращение пайка кварталу оставит больных без еды через три дня.", KnownFact="квартал не переживёт обычное урезание без помощи", Reveals="цену решения в пользу гарнизона" },
            new() { Id="merchant-bond", Text="Купеческая грамота обещает оплатить сторожевые огни в обмен на будущий тарифный контракт.", KnownFact="частный заём может закрыть часть разрыва", Reveals="цену союза с купцами" },
        };
        if (highDifficulty) clues.Add(new ScenarioClue { Id="winter-ledger", Text="Старая зимняя книга доказывает: без общего пайка охрана и квартал начнут отбирать зерно друг у друга.", KnownFact="компромисс требует письменного контроля, а не доброй воли", Reveals="необходимость общего надзора" });
        if (o.DurationMinutes >= 150) clues.Add(new ScenarioClue { Id="quorum-letter", Text="Письмо отсутствующего старейшины разрешает временной комиссии принять решение до следующего совета.", KnownFact="отсутствие кворума не обязательно означает бездействие", Reveals="законный путь отложенного решения" });
        var scenes = new List<ScenarioScene>
        {
            new() { Id="opening", Title="Закрытое заседание", Purpose="Совет должен решить судьбу зерна до ночи: голосовать сразу, заслушать позиции или отложить вопрос." },
            new() { Id="positions", Title="Позиции сторон", Purpose="Капитан, квартал, купцы и храм называют свои требования, уступки и красные линии." },
            new() { Id="records", Title="Зерновая книга", Purpose="Игроки проверяют запас, донесение дороги, список пайков и цену купеческого займа.", ClueIds=highDifficulty ? ["granary-count", "road-report", "ration-roll", "merchant-bond", "winter-ledger"] : ["granary-count", "road-report", "ration-roll", "merchant-bond"] },
            new() { Id="caucus", Title="Тихие переговоры", Purpose="Игроки формируют коалицию: прямое большинство, помощь купцов или взаимные уступки." },
            new() { Id="vote", Title="Голос совета", Purpose="Ведущий решает, удалось ли собрать заявленную коалицию; улики показывают цену, но не заменяют убеждение." },
        };
        if (o.DurationMinutes >= 150) scenes.Add(new ScenarioScene { Id="second-session", Title="Временная комиссия", Purpose="При отсутствии кворума герои могут собрать узкий комитет и принять временное решение.", ClueIds=["quorum-letter"] });
        var balancedProof = highDifficulty ? new[] { "granary-count", "road-report", "ration-roll", "winter-ledger" } : new[] { "granary-count", "road-report", "ration-roll" };
        var coalitions = new[]
        {
            new ScenarioCoalition { Id="patrol-loan", Name="Патруль и купцы", Members=["Капитан Хродгар", "Мавенна, купеческий фактор"], Concession="Гарнизон получает сорок мешков; купцы оплачивают огни.", Cost="Квартал живёт на сокращённом пайке и принимает будущий тарифный контракт.", RequiredClueIds=["road-report", "merchant-bond"] },
            new ScenarioCoalition { Id="relief-kitchen", Name="Квартал и храм", Members=["Сигне, старейшина квартала", "Ордиc, храмовая лекарка"], Concession="Квартал получает сорок мешков и общую кухню.", Cost="Патруль сокращает ночные выезды, а храм расходует резерв лекарств.", RequiredClueIds=["ration-roll"] },
            new ScenarioCoalition { Id="balanced-charter", Name="Хрупкий устав", Members=["Капитан Хродгар", "Сигне, старейшина квартала", "Ордиc, храмовая лекарка"], Concession="Зерно делят поровну, купцы финансируют огни, а распределение контролирует общий надзор.", Cost="Ни одна сторона не получает желаемые сорок мешков; все принимают проверку и будущие обязательства.", RequiredClueIds=balancedProof },
        };
        var branches = new List<ScenarioBranch>
        {
            new() { FromSceneId="opening", ToSceneId="positions", Decision="Выслушать позиции", Condition="Игроки хотят понять интересы и красные линии до голосования." },
            new() { FromSceneId="opening", ToSceneId="records", Decision="Проверить цифры", Condition="Игроки не хотят строить решение на заявлениях сторон." },
            new() { FromSceneId="opening", ToEndingId="deferred-vote", Decision="Перенести вопрос", Condition="Совет отказывается принимать решение без кворума или сведений." },
            new() { FromSceneId="positions", ToSceneId="records", Decision="Сверить заявления с книгой", Condition="Игроки проверяют, что каждая сторона умолчала о цене своего требования." },
            new() { FromSceneId="positions", ToSceneId="caucus", Decision="Начать переговоры", Condition="Игроки сразу ищут уступки и коалиции, не обещая результата автоматически." },
            new() { FromSceneId="records", ToSceneId="caucus", Decision="Собрать коалиции на фактах", Condition="Игроки используют цифры как основу для переговоров.", RequiredClueIds=["granary-count"] },
            new() { FromSceneId="records", ToSceneId="vote", Decision="Вынести прямое голосование", Condition="Игроки предлагают выбрать одну сторону без долгих уступок.", RequiredClueIds=["granary-count"] },
            new() { FromSceneId="caucus", ToSceneId="vote", Decision="Вынести соглашение", Condition="Ведущий подтверждает, что участники приняли заявленные уступки." },
            new() { FromSceneId="vote", ToEndingId="patrol-majority", Decision="Принять союз патруля и купцов", Condition="Капитан и купцы собрали большинство; квартал принимает сокращённый паёк.", RequiredClueIds=["road-report", "merchant-bond"] },
            new() { FromSceneId="vote", ToEndingId="relief-majority", Decision="Принять союз квартала и храма", Condition="Квартал и храм собрали большинство; патруль сокращает ночные выезды.", RequiredClueIds=["ration-roll"] },
            new() { FromSceneId="vote", ToEndingId="balanced-compromise", Decision="Принять хрупкий устав", Condition="Все стороны приняли меньшую долю и общий надзор.", RequiredClueIds=balancedProof },
            new() { FromSceneId="vote", ToEndingId="deferred-vote", Decision="Признать провал переговоров", Condition="Ни одна коалиция не получила согласия или кворума." },
        };
        if (o.DurationMinutes >= 150)
        {
            branches.Add(new ScenarioBranch { FromSceneId="caucus", ToSceneId="second-session", Decision="Создать временную комиссию", Condition="Кворума нет, но стороны готовы принять ограниченное решение." });
            branches.Add(new ScenarioBranch { FromSceneId="second-session", ToEndingId="temporary-committee", Decision="Передать зерно комиссии", Condition="Комиссия получает полномочия на неделю и обязуется отчитаться на следующем совете.", RequiredClueIds=["quorum-letter"] });
            branches.Add(new ScenarioBranch { FromSceneId="second-session", ToEndingId="deferred-vote", Decision="Отказаться от временного решения", Condition="Стороны не доверяют комиссии и переносят вопрос." });
        }
        var endings = new List<ScenarioEnding>
        {
            new() { Id="patrol-majority", Title="Дорога прежде пайка", Outcome="Гарнизон получает сорок мешков и удерживает ночные выезды. Купцы получают будущий тариф, а квартал живёт на сокращённом пайке.", IsReachable=true },
            new() { Id="relief-majority", Title="Пайки прежде дороги", Outcome="Квартал получает сорок мешков и храмовую кухню. Патруль сокращает выезды, поэтому дорога становится опаснее.", IsReachable=true },
            new() { Id="balanced-compromise", Title="Хрупкий устав", Outcome="Зерно делят поровну, а общий надзор предотвращает немедленный конфликт. Каждая сторона платит уступкой и будущими обязательствами.", IsReachable=true },
            new() { Id="deferred-vote", Title="Совет без голоса", Outcome="Решение отложено: зерно остаётся запертым, недоверие растёт, а реальные потребности гарнизона и квартала никуда не исчезают.", IsReachable=true },
        };
        if (o.DurationMinutes >= 150) endings.Add(new ScenarioEnding { Id="temporary-committee", Title="Неделя на доверии", Outcome="Временная комиссия распределяет зерно под отчётность. Это спасает время, но переносит политический конфликт на следующий совет.", IsReachable=true });
        return new GeneratedScenario { Title=t.Name, Seed=o.Seed, Options=o, Template=t, Hook=$"{t.Hook} {toneComplication}", ConflictCause=t.ConflictCause, StartSceneId="opening", PublicBriefing="У совета есть шестьдесят мешков зерна, а гарнизон и квартал просят по сорок. До ночи нужно либо принять решение, либо объяснить цену отсрочки.", KnownFacts=["Полный запас составляет шестьдесят мешков.", "Гарнизон и квартал не могут получить по сорок одновременно.", "Совет вправе распределить резерв или создать временную комиссию, но не может вызвать новый обоз до оттепели."], Coalitions=coalitions, GmNotes=["Объективные факты: зерна объективно не хватает; донесение о дороге и список пайков верны; купеческий заём реален, но создаёт тарифный долг. Ни одна сторона не лжёт о базовой нужде, но каждая умалчивает о цене своего решения.", "Успех коалиции не рассчитывается автоматически: ведущий решает, приняты ли уступки и есть ли кворум. Улики проверяют только обоснованность и доступность политического выбора.", "Если игроки предлагают иной пакет, сопоставьте его с ценой: безопасность дороги, пайки квартала, будущий долг купцам, общий надзор или отсрочка."], Characters=BuildCouncilRoles(o.ParticipantCount), Clues=clues, Scenes=scenes, Branches=branches, Endings=endings };
    }

    private static IReadOnlyList<ScenarioCharacter> BuildCouncilRoles(int count)
    {
        var roles = new List<ScenarioCharacter>
        {
            new() { Name="Капитан Хродгар", Role="представитель гарнизона", Goal="сохранить ночные патрули", Motive="считает дорогу единственной защитой города зимой", PublicInformation="Просит сорок мешков для гарнизона.", PersonalInformation="Знает, что сокращение выездов оставит караваны без защиты.", Concession="Готов принять тридцать мешков, если сторожевые огни оплатят купцы.", RedLine="Не подпишет решение, которое полностью отменит ночные выезды.", Secret="Боится, что провал патруля используют против него при следующем назначении." },
            new() { Name="Сигне", Role="старейшина квартала", Goal="сохранить пайки для семей", Motive="она лично отвечает за списки голодающих", PublicInformation="Просит сорок мешков для квартала.", PersonalInformation="Знает, что через три дня храмовая кухня останется без зерна.", Concession="Примет тридцать мешков при общей кухне и запрете частных запасов.", RedLine="Не поддержит сокращение пайка больным и детям.", Secret="Скрыла, что часть квартала уже запасла зерно у частных торговцев." },
            new() { Name="Мавенна", Role="купеческий фактор", Goal="закрепить выгодный договор", Motive="её дом получит влияние через сторожевые огни и тариф", PublicInformation="Предлагает оплатить огни и доставку.", PersonalInformation="Её грамота действительно покрывает часть расходов, но создаёт долг на сезон.", Concession="Согласна на общий надзор, если тарифный контракт останется в силе.", RedLine="Не даст заём без записанного будущего дохода.", Secret="Надеется, что спор заставит совет подписать контракт без обсуждения." },
            new() { Name="Ордис", Role="храмовая лекарка", Goal="не оставить больных без пищи", Motive="считает здоровье квартала долгом храма", PublicInformation="Представляет список нуждающихся.", PersonalInformation="Может открыть резерв лекарств для общей кухни, но только один раз.", Concession="Поддержит равный раздел при храмовой кухне и проверке списков.", RedLine="Не согласится превратить пайки больных в разменную монету.", Secret="Боится, что публичный скандал лишит храм помощи купцов." },
            new() { Name="Келд", Role="писарь совета", Goal="сохранить законность решения", Motive="его подпись отвечает за процедуру", PublicInformation="Следит за кворумом и текстом постановления.", PersonalInformation="Знает о письме отсутствующего старейшины.", Concession="Допустит временную комиссию с недельным отчётом.", RedLine="Не заверит решение без полномочий или записи голосов.", Secret="Может раскрыть письмо только если стороны перестанут требовать невозможного." },
            new() { Name="Вигдис", Role="представитель дорожных охотников", Goal="сохранить безопасность пути", Motive="его люди первыми встретят последствия сокращения патруля", PublicInformation="Подтверждает угрозу на дороге.", PersonalInformation="Может выделить дозорных при оплате огней.", Concession="Готов поддержать квартал при гарантии сторожевых огней.", RedLine="Не позволит использовать охотников как бесплатный гарнизон.", Secret="Знает о готовности части купцов платить больше, чем говорит Мавенна." },
        };
        return roles.Take(count).ToArray();
    }

    internal static GeneratedScenario BuildMountainPass(ScenarioGenerationOptions o, ScenarioTemplate t)
    {
        var highDifficulty = o.Difficulty == "Высокая";
        var toneComplication = o.Tone switch
        {
            "Мрачный" => "Ветер заметает старые следы, а ночь делает любой огонь на склоне заметным издалека.",
            "Дипломатичный" => "Даже у перевала судьбу груза можно решить обещанием, обменом и правильно выбранным посредником.",
            "Приключенческий" => "Перевал опасен, но каждый маршрут даёт шанс проявить смелость без обязательной схватки.",
            _ => "Над перевалом сгущаются тучи: время, припасы и доверие спутников становятся одним ресурсом."
        };
        var clues = new List<ScenarioClue>
        {
            new() { Id="guide-map", Text="Карта проводника отмечает узкую тропу под каменным карнизом и свежий обвал у главной дороги.", KnownFact="короткая тропа экономит время, но требует точного выбора обхода", Reveals="путь через теснину реален", SupportsHypothesisIds=[], ContradictsHypothesisIds=[] },
            new() { Id="supply-list", Text="Список припасов показывает: еды и тёплых покрывал хватит на долгий гребень, но не на лишнюю ночёвку.", KnownFact="длинный маршрут безопаснее, но забирает срок и запас", Reveals="цену обходного пути", SupportsHypothesisIds=[], ContradictsHypothesisIds=[] },
            new() { Id="hunter-token", Text="Резной жетон местных охотников: по нему можно просить проводника и укрытие у северного стойбища.", KnownFact="местные знают безопасный ход и цену помощи", Reveals="возможность сопровождения без боя", SupportsHypothesisIds=[], ContradictsHypothesisIds=[] },
        };
        if (highDifficulty) clues.Add(new ScenarioClue { Id="smuggler-mark", Text="На камнях виден знак контрабандистов и перерезанная верёвка сигнальной пирамиды.", KnownFact="угроза в теснине создана людьми, а не случайной лавиной", Reveals="с коротким путём можно договариваться, скрыться или готовить обход", SupportsHypothesisIds=[], ContradictsHypothesisIds=[] });
        if (o.DurationMinutes >= 120) clues.Add(new ScenarioClue { Id="watch-fire", Text="С верхнего гребня виден огонь заставы: смена ещё не ушла, но ждёт знака до рассвета.", KnownFact="поздняя доставка всё ещё может спасти людей, но не первоначальный приказ", Reveals="цену задержки и смысл частичного успеха", SupportsHypothesisIds=[], ContradictsHypothesisIds=[] });
        var scenes = new List<ScenarioScene>
        {
            new() { Id="opening", Title="Пустая курьерская стоянка", Purpose="Герои находят брошенную сумку курьера и должны решить, кому доверить груз и сколько риска принять." },
            new() { Id="planning", Title="Карта и припасы", Purpose="Проводник объясняет короткий путь и долгий гребень; время и запас тепла нельзя потратить дважды.", ClueIds=highDifficulty ? ["guide-map", "supply-list", "smuggler-mark"] : ["guide-map", "supply-list"] },
            new() { Id="hunters", Title="Северное стойбище", Purpose="Местные охотники готовы помочь за обещание доли припасов, уважение к их земле или услугу позже.", ClueIds=["hunter-token"] },
            new() { Id="narrows", Title="Теснина", Purpose="Короткий путь ведёт под карнизом и к знакам контрабандистов; бой не обязателен, но последствия ошибки быстры." },
            new() { Id="ridge", Title="Дальний гребень", Purpose="Длинный путь бережёт людей и груз, однако рассвет может наступить раньше заставы." },
            new() { Id="escort", Title="Путь с охотниками", Purpose="Сопровождение ведёт к укрытию и известным тропам, но требует отдать часть времени или припасов." },
        };
        if (o.DurationMinutes >= 120) scenes.Add(new ScenarioScene { Id="signal", Title="Огонь заставы", Purpose="Герои решают, послать ли знак, разделить груз или остановиться на ночлег до опасного спуска.", ClueIds=["watch-fire"] });
        var narrowProof = highDifficulty ? new[] { "guide-map", "smuggler-mark" } : new[] { "guide-map" };
        var branches = new List<ScenarioBranch>
        {
            new() { FromSceneId="opening", ToSceneId="planning", Decision="Подготовить маршрут", Condition="Герои осматривают сумку и выбирают, как использовать время и припасы." },
            new() { FromSceneId="opening", ToSceneId="hunters", Decision="Искать помощь местных", Condition="Герои идут к стойбищу вместо того, чтобы сразу рисковать перевалом." },
            new() { FromSceneId="opening", ToEndingId="return-with-cargo", Decision="Вернуться с грузом", Condition="Герои считают риск неприемлемым и сохраняют сумку для нового поручения." },
            new() { FromSceneId="planning", ToSceneId="narrows", Decision="Выбрать короткую теснину", Condition="Проводник готов вести по карте; при высокой сложности герои должны учесть человеческую угрозу.", RequiredClueIds=narrowProof },
            new() { FromSceneId="planning", ToSceneId="ridge", Decision="Выбрать дальний гребень", Condition="Герои принимают задержку ради безопасности людей и груза.", RequiredClueIds=["supply-list"] },
            new() { FromSceneId="planning", ToSceneId="hunters", Decision="Не рисковать без сопровождения", Condition="Игроки отказываются от обоих прямых маршрутов и ищут местную помощь." },
            new() { FromSceneId="hunters", ToSceneId="escort", Decision="Договориться с охотниками", Condition="Герои предлагают услугу, припасы или уважение к правилам стойбища.", RequiredClueIds=["hunter-token"] },
            new() { FromSceneId="hunters", ToEndingId="return-with-cargo", Decision="Отказаться от условий охотников", Condition="Герои не готовы платить цену сопровождения и возвращают груз." },
            new() { FromSceneId="narrows", ToEndingId="timely-delivery", Decision="Пройти теснину", Condition="Герои обходят засаду переговорами, скрытностью или осторожным манёвром; обязательного боя нет.", RequiredClueIds=narrowProof },
            new() { FromSceneId="narrows", ToEndingId="rescue-delay", Decision="Спасти спутника и отступить", Condition="Герои выбирают жизнь сопровождающего или курьера вместо срока доставки." },
            new() { FromSceneId="ridge", ToEndingId="late-safe-delivery", Decision="Довести груз по гребню", Condition="Груз и люди доходят целыми, но приказ приходит после первоначального срока.", RequiredClueIds=["supply-list"] },
            new() { FromSceneId="ridge", ToEndingId="rescue-delay", Decision="Остановиться в укрытии", Condition="Погода или состояние спутника требуют перенести спуск и сохранить людей." },
            new() { FromSceneId="escort", ToEndingId="escorted-delivery", Decision="Идти с охотниками", Condition="Охотники проводят отряд к заставе, а герои оплачивают помощь обещанной ценой.", RequiredClueIds=["hunter-token"] },
            new() { FromSceneId="escort", ToEndingId="rescue-delay", Decision="Оставить часть груза в укрытии", Condition="Герои спасают людей от перегруза, жертвуя частью срока или поручения." },
        };
        if (o.DurationMinutes >= 120)
        {
            branches.Add(new ScenarioBranch { FromSceneId="ridge", ToSceneId="signal", Decision="Проверить огонь заставы", Condition="Герои хотят оценить, ещё ли имеет смысл срочный спуск." });
            branches.Add(new ScenarioBranch { FromSceneId="escort", ToSceneId="signal", Decision="Подать знак заставе", Condition="Охотники выводят отряд на видимый гребень." });
            branches.Add(new ScenarioBranch { FromSceneId="signal", ToEndingId="partial-delivery", Decision="Разделить поручение", Condition="Приказ отправляют со знаком или быстрым гонцом, а лекарство и люди идут безопаснее.", RequiredClueIds=["watch-fire"] });
            branches.Add(new ScenarioBranch { FromSceneId="signal", ToEndingId="late-safe-delivery", Decision="Идти вместе", Condition="Герои сохраняют целостность груза и принимают задержку.", RequiredClueIds=["watch-fire"] });
        }
        var endings = new List<ScenarioEnding>
        {
            new() { Id="timely-delivery", Title="До рассвета", Outcome="Приказ и лекарство доставлены вовремя. Застава меняет маршрут патруля, а квартал получает шанс пережить ночь.", IsReachable=true },
            new() { Id="late-safe-delivery", Title="Поздно, но целыми", Outcome="Груз и отряд прибывают без потерь, однако первоначальный приказ уже не успевает изменить ночной патруль.", IsReachable=true },
            new() { Id="escorted-delivery", Title="Цена проводника", Outcome="Охотники проводят груз безопасно; помощь оплачена обещанием, припасами или будущей услугой, а сроки зависят от решения ведущего по пути.", IsReachable=true },
            new() { Id="rescue-delay", Title="Люди важнее срока", Outcome="Герои спасают спутника или курьера и сохраняют то, что могут, но доставка откладывается или становится частичной.", IsReachable=true },
            new() { Id="return-with-cargo", Title="Возвращение с сумкой", Outcome="Груз не доставлен: герои честно возвращают его заказчику и сохраняют возможность подготовить новое, более безопасное поручение.", IsReachable=true },
        };
        if (o.DurationMinutes >= 120) endings.Add(new ScenarioEnding { Id="partial-delivery", Title="Приказ впереди", Outcome="Застава получает срочный приказ вовремя, но лекарство и часть груза приходят позже с основным отрядом.", IsReachable=true });
        return new GeneratedScenario { Title=t.Name, Seed=o.Seed, Options=o, Template=t, Hook=$"{t.Hook} {toneComplication}", ConflictCause=t.ConflictCause, StartSceneId="opening", PublicBriefing="На Каменную заставу нужно доставить запечатанный приказ и лекарство до рассвета. Главную дорогу перекрыла лавина, а перевал опасен.", KnownFacts=["В сумке есть приказ для заставы и лекарство для раненого дозора.", "Лавина закрыла главную дорогу.", "Короткая тропа быстрее, долгий гребень безопаснее, а местные охотники знают третий путь."], GmNotes=["Объективная истина: контрабандисты перерезали сигнальную верёвку и спровоцировали обвал у теснины, чтобы контролировать короткий путь. Слух о чудовище на перевале ложен.", "Ни один маршрут не требует обязательного боя: контрабандистов можно обойти, с ними можно торговаться или отказаться от теснины.", "Ведущий определяет цену импровизированного плана по тем же ресурсам: срок, припасы, состояние груза, безопасность людей и долг перед охотниками."], Characters=BuildMountainRoles(o.ParticipantCount), Clues=clues, Scenes=scenes, Branches=branches, Endings=endings };
    }

    private static IReadOnlyList<ScenarioCharacter> BuildMountainRoles(int count)
    {
        var roles = new List<ScenarioCharacter>
        {
            new() { Name="Руна", Role="курьер ярла", Goal="доставить приказ до рассвета", Motive="она уже потеряла одного напарника на службе и боится повторить ошибку", PublicInformation="Несёт ответственность за срок и печать приказа.", PersonalInformation="Знает, что заставе нужно изменить маршрут патруля до рассвета.", Secret="Курьер, которого ищут, не погиб: он ушёл в укрытие с частью пути, испугавшись теснины." },
            new() { Name="Хальвар", Role="проводник", Goal="не вести людей в ловушку", Motive="его имя зависит от того, вернутся ли клиенты живыми", PublicInformation="Знает короткую тропу и дальний гребень.", PersonalInformation="Видел знак контрабандистов, но не уверен, сколько их на перевале.", Secret="Однажды уже помогал охотникам обходить этот обвал и должен им услугу." },
            new() { Name="Фрейя", Role="лекарка", Goal="сохранить лекарство и людей", Motive="без лекарства раненый дозорный может не пережить ночь", PublicInformation="Проверяет состояние сумки и спутников.", PersonalInformation="Лекарство нельзя долго держать на сильном морозе.", Secret="Готова пожертвовать сроком приказа, если придётся спасать человека." },
            new() { Name="Дравин", Role="представитель заказчика", Goal="не допустить срыва приказа", Motive="его положение при дворе зависит от успеха поручения", PublicInformation="Требует довести хотя бы приказ.", PersonalInformation="Знает, что контрабандисты потеряют выгоду, если патруль сменит маршрут.", Secret="Скрыл от группы, что короткий путь может привлечь людей, заинтересованных в задержке груза." },
            new() { Name="Эйла", Role="охотница-посредник", Goal="защитить стойбище и его правила", Motive="не хочет, чтобы чужая погоня привела угрозу к её людям", PublicInformation="Может договориться об укрытии и проводнике.", PersonalInformation="Знает безопасный знак для стойбища.", Secret="Попросит будущую услугу вместо платы, если отряд честно расскажет о грузе." },
            new() { Name="Торвар", Role="сопровождающий дозора", Goal="вернуться к заставе живым", Motive="на заставе ждёт его сестра", PublicInformation="Умеет читать сигналы с гребня.", PersonalInformation="Старое ранение мешает ему идти по теснине.", Secret="Боится признаться, что может сорвать быстрый маршрут." },
        };
        return roles.Take(count).ToArray();
    }

    private static GeneratedScenario BuildAshOnSeal(ScenarioGenerationOptions o, ScenarioTemplate t)
    {
        var toneComplication = o.Tone switch
        {
            "Мрачный" => "Пепел от жаровни смешался с воском, а очередь у закрытого склада становится всё злее.",
            "Дипломатичный" => "Представители гарнизона и квартала требуют, чтобы обвинение не разрушило хрупкое перемирие.",
            "Приключенческий" => "Гонец может успеть к храмовой кладовой до смены караула, если герои решатся выйти за ворота.",
            _ => "Каждый допрос меняет, кому из сторон готовы верить свидетели."
        };
        var highDifficulty = o.Difficulty == "Высокая";
        var hypotheses = new[]
        {
            new ScenarioHypothesis { Id="profit", Statement="Смотритель склада Велен подменил печать ради хищения зерна.", Beneficiary="Велен и его тайные покупатели.", Cost="Квартал остаётся без пайков, а гарнизон теряет учёт.", Unknown="Исчезло ли зерно на самом деле?" },
            new ScenarioHypothesis { Id="protection", Statement="Писарь Селла изменила распоряжение, чтобы задержать реквизицию и защитить квартал.", Beneficiary="Больные и семьи, ожидающие зерно.", Cost="Селла нарушила порядок и рискует судом.", Unknown="Действовала ли она одна и можно ли оправдать её поступок?" },
            new ScenarioHypothesis { Id="access-coverup", Statement="Страж Борг испортил печать, скрывая несанкционированный доступ к архиву.", Beneficiary="Борг, если следы ночного визита исчезнут.", Cost="Подозрение падает на невиновных и задерживает выдачу.", Unknown="Он повредил документ или лишь оказался рядом после чужого поступка?" },
        };
        var clues = new List<ScenarioClue>
        {
            new() { Id="ash-mixture", Text="В пепле у печати есть храмовая смола, которой пользуется только писарская жаровня.", KnownFact="печать трогали у стола писаря, а не на складе", Reveals="след ведёт к работе с документом", SupportsHypothesisIds=["protection"], ContradictsHypothesisIds=["profit"] },
            new() { Id="requisition-copy", Text="Под соскобленной строкой видна прежняя цель: зерно хотели изъять для гарнизона, а не выдать кварталу.", KnownFact="изменение документа задержало именно реквизицию", Reveals="у подмены был мотив защиты квартала", SupportsHypothesisIds=["protection"], ContradictsHypothesisIds=["access-coverup"] },
            new() { Id="key-log", Text="В журнале ключей отмечен ночной вход Борга в архив.", KnownFact="Борг нарушил порядок доступа", Reveals="у стража есть причина бояться расследования", SupportsHypothesisIds=["access-coverup"], ContradictsHypothesisIds=[] },
            new() { Id="debt-ledger", Text="В долговой книге Велена есть срочные выплаты, совпадающие с днём распоряжения.", KnownFact="смотритель склада испытывает финансовое давление", Reveals="версия о личной выгоде правдоподобна, но ещё не доказана", SupportsHypothesisIds=["profit"], ContradictsHypothesisIds=[] },
        };
        if (highDifficulty) clues.Add(new ScenarioClue { Id="scribe-ink", Text="Под копотью сохранилась редкая синяя клякса с пера Селлы.", KnownFact="писарь работала с подлинником после его регистрации", Reveals="участие Селлы подтверждается независимо от её слов", SupportsHypothesisIds=["protection"], ContradictsHypothesisIds=[] });
        if (o.DurationMinutes >= 90) clues.Add(new ScenarioClue { Id="warehouse-tally", Text="Складской пересчёт показывает: зерно на месте, но выдача кварталу сорвана задержкой.", KnownFact="никто не успел получить выгоду от исчезновения зерна", Reveals="финансовая версия ослабевает", SupportsHypothesisIds=["protection"], ContradictsHypothesisIds=["profit"] });
        if (o.DurationMinutes >= 150) clues.Add(new ScenarioClue { Id="relief-route", Text="Гонец приносит храмовый список больных, для которых реквизиция означала бы голод.", KnownFact="у Селлы был реальный повод задержать приказ", Reveals="цену компромисса для квартала", SupportsHypothesisIds=["protection"], ContradictsHypothesisIds=[] });

        var scenes = new List<ScenarioScene>
        {
            new() { Id="opening", Title="Печать под пеплом", Purpose="Игроки видят закрытый склад и решают, осматривать документ, говорить с людьми или временно остановить публичное обвинение." },
            new() { Id="archive", Title="Архив распоряжений", Purpose="Осмотрите печать, соскобленную строку и следы работы писаря.", ClueIds=highDifficulty ? ["ash-mixture", "requisition-copy", "scribe-ink"] : ["ash-mixture", "requisition-copy"] },
            new() { Id="interviews", Title="Люди у склада", Purpose="Выслушайте Борга, Велена и жителей; личные страхи не равны доказательству вины.", ClueIds=["key-log", "debt-ledger"] },
            new() { Id="deliberation", Title="Разбор версий", Purpose="Игроки отделяют подтверждённые факты от удобных обвинений и выбирают публичный или осторожный исход." },
        };
        if (o.DurationMinutes >= 90) scenes.Add(new ScenarioScene { Id="warehouse", Title="Пересчёт склада", Purpose="Проверьте, исчезло ли зерно на самом деле, прежде чем обвинять Велена.", ClueIds=["warehouse-tally"] });
        if (o.DurationMinutes >= 150) scenes.Add(new ScenarioScene { Id="relief", Title="Храмовый маршрут", Purpose="Герои могут проверить, кого затронула бы реквизиция, и цену тихого компромисса.", ClueIds=["relief-route"] });

        var proof = highDifficulty ? new[] { "ash-mixture", "requisition-copy", "scribe-ink" } : new[] { "ash-mixture", "requisition-copy" };
        var branches = new List<ScenarioBranch>
        {
            new() { FromSceneId="opening", ToSceneId="archive", Decision="Осмотреть документ", Condition="Игроки проверяют печать и текст распоряжения." },
            new() { FromSceneId="opening", ToSceneId="interviews", Decision="Опросить участников", Condition="Игроки сначала собирают показания и причины сторон." },
            new() { FromSceneId="opening", ToEndingId="cautious-review", Decision="Приостановить обвинение", Condition="Игроки не готовы обвинять без доказательств и передают документ на проверку." },
            new() { FromSceneId="archive", ToSceneId="interviews", Decision="Сверить факты с показаниями", Condition="Игроки хотят проверить, кому выгодна каждая версия." },
            new() { FromSceneId="interviews", ToSceneId="archive", Decision="Проверить слова документом", Condition="Показания вызывают сомнения, и игроки ищут материальные следы." },
            new() { FromSceneId="archive", ToSceneId="deliberation", Decision="Разобрать версии", Condition="Найдена копия распоряжения.", RequiredClueIds=["requisition-copy"] },
            new() { FromSceneId="interviews", ToSceneId="deliberation", Decision="Действовать по показаниям", Condition="Игроки готовы принять риск неполной картины." },
            new() { FromSceneId="deliberation", ToEndingId="justified-charge", Decision="Предъявить обоснованное обвинение Селле", Condition="Следы писаря и изменённая цель распоряжения подтверждают сознательную подмену ради защиты квартала.", RequiredClueIds=proof },
            new() { FromSceneId="deliberation", ToEndingId="false-borg-charge", Decision="Обвинить Борга", Condition="Игроки принимают ночной вход за доказательство порчи печати.", RequiredClueIds=["key-log"] },
            new() { FromSceneId="deliberation", ToEndingId="false-velen-charge", Decision="Обвинить Велена", Condition="Игроки принимают долги смотрителя за доказательство хищения.", RequiredClueIds=["debt-ledger"] },
            new() { FromSceneId="deliberation", ToEndingId="cautious-review", Decision="Передать дело на проверку", Condition="Игроки не считают ни одну версию доказанной полностью." },
            new() { FromSceneId="deliberation", ToEndingId="quiet-compromise", Decision="Выбрать тихий компромисс", Condition="Игроки признают мотив защиты квартала, но не хотят публичного обвинения.", RequiredClueIds=["ash-mixture"] },
        };
        if (o.DurationMinutes >= 90)
        {
            branches.Add(new ScenarioBranch { FromSceneId="archive", ToSceneId="warehouse", Decision="Пересчитать зерно", Condition="Игроки проверяют, подтверждает ли склад личную выгоду Велена." });
            branches.Add(new ScenarioBranch { FromSceneId="interviews", ToSceneId="warehouse", Decision="Проверить слова пересчётом", Condition="Показания сторон расходятся." });
            branches.Add(new ScenarioBranch { FromSceneId="warehouse", ToSceneId="deliberation", Decision="Вернуться к версиям", Condition="Пересчёт добавил либо снял подозрение с Велена." });
        }
        if (o.DurationMinutes >= 150)
        {
            branches.Add(new ScenarioBranch { FromSceneId="warehouse", ToSceneId="relief", Decision="Проверить цену задержки", Condition="Игроки хотят понять, кого защищало изменение приказа." });
            branches.Add(new ScenarioBranch { FromSceneId="relief", ToSceneId="deliberation", Decision="Сопоставить человеческую цену и закон", Condition="Игроки готовы выбрать между публичным судом и защитой квартала." });
        }
        var endings = new[]
        {
            new ScenarioEnding { Id="justified-charge", Title="Правда под пеплом", Outcome="Селлу обоснованно обвиняют в подмене печати. Её мотив защиты квартала учитывают на слушании, но нарушение не скрывают.", IsReachable=true },
            new ScenarioEnding { Id="false-borg-charge", Title="Удобный виновный", Outcome="Борга обвиняют по косвенному следу. Его ночной проступок реален, но он не портил печать; доверие к расследованию повреждено.", IsReachable=true },
            new ScenarioEnding { Id="false-velen-charge", Title="Долг как приговор", Outcome="Долги Велена превращают в обвинение без доказательства хищения. Склад теряет смотрителя, а настоящая подмена остаётся нераскрытой.", IsReachable=true },
            new ScenarioEnding { Id="cautious-review", Title="Осторожное разбирательство", Outcome="Публичное обвинение откладывают, распоряжение проверяют заново, а выдачу зерна проводят под временным надзором.", IsReachable=true },
            new ScenarioEnding { Id="quiet-compromise", Title="Цена тишины", Outcome="Квартал получает зерно, а дело не становится публичным. Селла и герои принимают на себя риск будущего разоблачения.", IsReachable=true },
        };
        return new GeneratedScenario { Title=t.Name, Seed=o.Seed, Options=o, Template=t, Hook=$"{t.Hook} {toneComplication}", ConflictCause=t.ConflictCause, StartSceneId="opening", PublicBriefing="Печать повреждена, выдача зерна остановлена, а любой поспешный приговор может ударить по кварталу или гарнизону.", KnownFacts=["Распоряжение о зерне заверено повреждённой печатью.", "Склад закрыт до выяснения обстоятельств.", "У нескольких людей был доступ к архиву или учёту."], Hypotheses=hypotheses, GmNotes=["Объективная истина: Селла изменила распоряжение, чтобы задержать реквизицию и защитить квартал. Борг действительно нарушил порядок доступа, а Велен скрывает долги, но оба не подменяли печать.", "Обвинительный финал означает доказанную подмену, но не автоматическое моральное осуждение: мотив защиты квартала остаётся предметом игры.", "Версии — инструмент ведущего, а не автоматический приговор. Игроки могут выбрать иной план; сопоставляйте его последствиям финалов."], Characters=BuildAshRoles(o.ParticipantCount), Clues=clues, Scenes=scenes, Branches=branches, Endings=endings };
    }

    private static IReadOnlyList<ScenarioCharacter> BuildAshRoles(int count)
    {
        var roles = new List<ScenarioCharacter>
        {
            new() { Name="Селла", Role="писарь ярлского архива", Goal="не допустить голода в квартале", Motive="она видела список больных и не верит, что порядок важнее их жизни", PublicInformation="Оформляла распоряжение и требует не обвинять никого до проверки.", PersonalInformation="Знает, что прежняя цель приказа была гарнизонной реквизицией.", Secret="Подменила печать и изменила строку назначения, чтобы задержать изъятие зерна.", IsKeySecretHolder=true },
            new() { Name="Борг", Role="страж архива", Goal="не потерять службу", Motive="ему нужны деньги после карточного долга", PublicInformation="Ночью дежурил у архива и настаивает на строгом расследовании.", PersonalInformation="Входил в архив без разрешения, чтобы забрать долговую расписку.", Secret="Скрыл свой ночной вход, хотя печать не портил." },
            new() { Name="Велен", Role="смотритель склада", Goal="сохранить должность и учёт зерна", Motive="боится, что долги сделают его удобной мишенью", PublicInformation="Требует открыть склад только по безупречному приказу.", PersonalInformation="Вёл личные долги через торговца, но зерно не крал.", Secret="Спрятал долговую книгу, опасаясь обвинения в хищении." },
            new() { Name="Ирена", Role="лекарка квартала", Goal="получить пайки для больных", Motive="несёт ответственность за людей, которых не примет гарнизон", PublicInformation="Просит не оставлять квартал без зерна.", PersonalInformation="Знала, что Селла собиралась спорить с реквизицией, но не знала о подмене печати.", Secret="Готова защищать Селлу, если расследование поставит квартал под угрозу." },
            new() { Name="Рахди", Role="посланник гарнизона", Goal="вернуть распоряжение в законный порядок", Motive="его карьера зависит от точного отчёта", PublicInformation="Требует назвать виновного до смены караула.", PersonalInformation="Понимает, что реквизиция действительно ударит по кварталу.", Secret="Может предложить временный надзор вместо немедленного суда." },
            new() { Name="Нура", Role="гонец храма", Goal="сохранить поставки лекарств", Motive="видела последствия голода в прошлую зиму", PublicInformation="Знает маршрут храмовой помощи.", PersonalInformation="Может подтвердить список больных.", Secret="Боится, что храм лишат доступа к складу, если скандал станет публичным." },
        };
        return roles.Take(count).ToArray();
    }

    /// <summary>Preserves the original wagon story and its legacy JSON invariants.</summary>
    public ScenarioGenerationResult GenerateLegacyNegotiation(ScenarioGenerationOptions options)
    {
        var template = GetTemplateByGenre(options.Genre);
        if (template?.Id != "negotiation" || options.ParticipantCount < template.MinParticipants ||
            options.ParticipantCount > template.MaxParticipants || options.DurationMinutes < template.MinDurationMinutes ||
            options.DurationMinutes > template.MaxDurationMinutes)
            return new ScenarioGenerationResult { Validation = new() { Errors = ["Несовместимые параметры legacy-переговоров."] } };
        var scenario = BuildLastWagon(options, template, new Random(options.Seed));
        var validation = ScenarioValidator.Validate(scenario);
        return new ScenarioGenerationResult { Scenario = validation.IsValid ? scenario : null, Validation = validation };
    }

    private static GeneratedScenario BuildLastWagon(ScenarioGenerationOptions o, ScenarioTemplate t, Random random)
    {
        var atmosphere = o.Tone switch
        {
            "Мрачный" => "Снег гасит последние звуки трактира, а закат кажется слишком близким.",
            "Дипломатичный" => "Даже в спешке у сторон ещё есть шанс сохранить лицо и договор.",
            "Приключенческий" => "До заката можно успеть, если рискнуть и действовать сообща.",
            _ => "На дворе сгущаются сумерки, и каждая минута делает спор дороже."
        };
        var difficulty = o.Difficulty == "Высокая" ? 4 : o.Difficulty == "Низкая" ? 2 : 3;
        var toneComplication = o.Tone switch
        {
            "Мрачный" => "Холод пугает лошадей: спор повышает риск ночной задержки, но не меняет состав груза.",
            "Дипломатичный" => "Свидетели следят за тем, чтобы ни одна сторона не потеряла лицо при публичной оговорке.",
            "Приключенческий" => "Носильщики готовы рискнуть коротким путём, если герои заранее распределят ответственность.",
            _ => "Каждая минута спора делает возницу менее готовой брать на себя чужой риск."
        };
        var clues = new List<ScenarioClue>
        {
            new() { Id="invoice", Text="Оригинал накладной с печатью торгового дома: в нём восемь ящиков.", KnownFact="торговец обязан отправить именно восемь учтённых ящиков до заката", Reveals="расхождение возникло после оформления накладной" },
            new() { Id="load-mark", Text="Свежая верёвка и личная метка помощника на девятом ящике.", KnownFact="девятый ящик погружён отдельно от торгового груза", Reveals="лишний ящик не принадлежит партии торговца" },
        };
        if (difficulty >= 3) clues.Add(new ScenarioClue { Id="stable-ledger", Text="Конюх помнит, что помощник просил спрятать личный ящик до отъезда.", KnownFact="помощник имел доступ к повозке до спора", Reveals="личный ящик добавлен намеренно" });
        if (difficulty >= 4) clues.Add(new ScenarioClue { Id="gate-bell", Text="Смотритель ворот предупреждает: после второго колокола без новой бумаги выезд закроют.", KnownFact="у сторон осталось мало времени на оформление", Reveals="нужна быстрая, проверяемая договорённость" });
        var scenes = new List<ScenarioScene>
        {
            new() { Id="opening", Title="Повозка у ворот", Purpose="Игроки выбирают: проверять груз, вести переговоры или искать иной путь." },
            new() { Id="inspection", Title="Проверка накладной", Purpose="Сопоставьте накладную, метки на грузах и слова конюха.", ClueIds=clues.Select(c => c.Id).Take(difficulty).ToArray() },
            new() { Id="terms", Title="Новые условия", Purpose="Торговец и возница обсуждают письменную оговорку, цену риска и судьбу личного ящика.", ClueIds=["invoice"] },
        };
        if (o.DurationMinutes >= 90) scenes.Add(new ScenarioScene { Id="alternative", Title="Запасной маршрут", Purpose="Герои проверяют временное хранение, носильщиков или иной способ доставки." });
        if (o.DurationMinutes >= 150) scenes.Add(new ScenarioScene { Id="deadline", Title="Второй колокол", Purpose="Смотритель ворот требует окончательного решения; время превращает уступки в последствия.", ClueIds=difficulty >= 4 ? ["gate-bell"] : [] });
        var agreement = difficulty == 2 ? "Стороны соглашаются записать, что повозка везёт восемь ящиков." : "Найдена накладная, а торговец и возница подписывают оговорку о восьми ящиках.";
        var branches = new List<ScenarioBranch>
        {
            new() { FromSceneId="opening", ToSceneId="inspection", Decision="Проверить груз", Condition="Игроки требуют накладную или осматривают повозку." },
            new() { FromSceneId="opening", ToSceneId="terms", Decision="Сразу начать переговоры", Condition="Стороны готовы обсуждать ответственность без раскрытия всех деталей." },
            new() { FromSceneId="opening", ToEndingId="no-deal", Decision="Отказаться от договора", Condition="Игроки не хотят принимать риск: груз возвращают на хранение до нового решения." },
            new() { FromSceneId="inspection", ToSceneId="terms", Decision="Предложить письменные условия", Condition=agreement, RequiredClueIds=["invoice"] },
            new() { FromSceneId="inspection", ToSceneId="terms", Decision="Продолжить без доказательств", Condition="Игроки не нашли решающую улику или не желают предъявлять её, но пытаются договориться о временных условиях." },
            new() { FromSceneId="inspection", ToEndingId="truth-exposed", Decision="Раскрыть обман", Condition=difficulty >= 3 ? "Совпали метка помощника и свидетельство конюха; помощник признаёт личный ящик." : "Найдена личная метка помощника и ему предъявлены факты.", RequiredClueIds=difficulty >= 3 ? ["load-mark", "stable-ledger"] : ["load-mark"] },
            new() { FromSceneId="terms", ToEndingId="amended-contract", Decision="Изменить условия перевозки", Condition="Возница получает письменное освобождение от ответственности за личный ящик, а торговец оплачивает риск.", RequiredClueIds=["invoice"] },
            new() { FromSceneId="terms", ToEndingId="split-load", Decision="Отделить спорный ящик", Condition="Помощник соглашается оставить личный ящик, и восемь учтённых ящиков уезжают до заката." },
            new() { FromSceneId="terms", ToEndingId="no-deal", Decision="Зафиксировать отказ", Condition="Стороны не достигли соглашения; груз остаётся на хранении, а договорённость переносится или расторгается." },
        };
        if (o.DurationMinutes >= 90)
        {
            branches.Add(new ScenarioBranch { FromSceneId="opening", ToSceneId="alternative", Decision="Искать альтернативу", Condition="Игроки не доверяют переговорам или не нашли решающую улику." });
            branches.Add(new ScenarioBranch { FromSceneId="alternative", ToEndingId="alternate-route", Decision="Сменить план доставки", Condition="Герои находят носильщиков, складской залог или сопровождающего и фиксируют ответственность за груз." });
        }
        if (o.DurationMinutes >= 150)
        {
            branches.Add(new ScenarioBranch { FromSceneId="terms", ToSceneId="deadline", Decision="Отложить решение до второго колокола", Condition="Стороны спорят о цене риска и не подписали оговорку сразу." });
            branches.Add(new ScenarioBranch { FromSceneId="deadline", ToEndingId="late-departure", Decision="Принять задержку", Condition="Стороны отказываются от сделки до истечения срока и выбирают сохранность груза.", RequiredClueIds=difficulty >= 4 ? ["gate-bell"] : [] });
            branches.Add(new ScenarioBranch { FromSceneId="deadline", ToEndingId="no-deal", Decision="Оформить отказ", Condition="Игроки не получили подтверждения времени или больше не готовы брать риск до закрытия ворот." });
        }
        var endings = new List<ScenarioEnding>
        {
            new() { Id="amended-contract", Title="Оговорённый риск", Outcome="Повозка уходит с оформленной оговоркой: торговец отвечает за состав груза, возница — за дорогу. Личный ящик внесён отдельно.", IsReachable=true },
            new() { Id="split-load", Title="Восемь ящиков до заката", Outcome="Спорный ящик остаётся в трактире; торговый груз уезжает вовремя, а помощник платит за хранение.", IsReachable=true },
            new() { Id="truth-exposed", Title="Цена лишнего ящика", Outcome="Обман помощника становится известен. Торговец и возница решают судьбу груза с полной информацией, а помощник отвечает за риск.", IsReachable=true },
            new() { Id="no-deal", Title="Отложенная сделка", Outcome="Никто не принимает риск на словах: груз возвращают на хранение, а стороны переносят, расторгают или заново обсуждают договор без ложного согласия.", IsReachable=true },
        };
        if (o.DurationMinutes >= 90) endings.Add(new ScenarioEnding { Id="alternate-route", Title="План Б", Outcome="Герои обеспечивают другой способ доставки или временное хранение. Контракт не срывается, но каждая сторона несёт оговорённые расходы.", IsReachable=true });
        if (o.DurationMinutes >= 150) endings.Add(new ScenarioEnding { Id="late-departure", Title="Закрытые ворота", Outcome="Ворота закрываются раньше сделки: груз остаётся под охраной до утра, а спор о неустойке становится завязкой следующей игры.", IsReachable=true });
        return new GeneratedScenario { Title=t.Name, Seed=o.Seed, Options=o, Template=t, Hook=$"{t.Hook} {atmosphere}", ConflictCause=t.ConflictCause, StartSceneId="opening", PublicBriefing="Все знают: торговцу нужно отправить восемь учтённых ящиков до заката; возница не тронется, пока документы и груз не совпадут.", KnownFacts=["В накладной указано восемь ящиков.", "В повозке лежит девятый ящик.", "До закрытия ворот осталось ограниченное время."], GmNotes=["Единственный ключевой секрет: помощник сам добавил личный ящик. Не раскрывайте его без действий игроков или подходящей улики.", toneComplication, "Длительность определяет объём игровых этапов: запасной маршрут добавляется от 90 минут, второй колокол — от 150 минут.", "Перечень переходов не ограничивает импровизацию: сопоставляйте новый план игроков с финалом по его последствиям; не объявляйте согласие или раскрытие секрета без решения игроков."], Characters=BuildLastWagonRoles(o.ParticipantCount), Clues=clues, Scenes=scenes, Branches=branches, Endings=endings };
    }

    private static IReadOnlyList<ScenarioCharacter> BuildLastWagonRoles(int count)
    {
        var roles = new List<ScenarioCharacter>
        {
            new() { Name="Савос Рийт", Role="торговец", Goal="отправить восемь ящиков до заката", Motive="не потерять контракт и доверие заказчика", PublicInformation="Груз оплачен и должен уйти сегодня.", PersonalInformation="Неустойка за задержку разорит его лавку." },
            new() { Name="Хильда Ровные Вожжи", Role="возница", Goal="не принять на себя чужую ответственность", Motive="сохранить лицензию и безопасность каравана", PublicInformation="Отказывается ехать с несоответствием накладной.", PersonalInformation="После прошлой проверки ей грозит отзыв лицензии за неверную бумагу." },
            new() { Name="Тален", Role="помощник торговца", Goal="не потерять личный ящик и избежать позора", Motive="в ящике вещи, которыми он надеется рассчитаться с долгом", PublicInformation="Помогал грузить повозку и нервничает из-за задержки.", PersonalInformation="Знает, где спрятана старая накладная.", Secret="До погрузки добавил в повозку личный ящик и надеялся, что его примут за часть торгового груза.", IsKeySecretHolder=true },
            new() { Name="Мира Конюх", Role="свидетель погрузки", Goal="не оказаться виноватой в чужом споре", Motive="сохранить место на конюшне", PublicInformation="Видела погрузку до прихода возницы.", PersonalInformation="Помнит личную метку Талена на верёвке девятого ящика." },
            new() { Name="Варн", Role="смотритель ворот", Goal="закрыть ворота по уставу", Motive="не допустить контрабанду после тревоги", PublicInformation="Следит за временем и бумагами.", PersonalInformation="Может разрешить выезд по временной оговорке, если её подпишут обе стороны." },
            new() { Name="Лисса", Role="постоялица и посредник", Goal="сохранить мир в трактире", Motive="ей нужен спокойный выезд утром", PublicInformation="Знает местных носильщиков.", PersonalInformation="Может быстро найти склад и носильщиков для запасного плана." },
        };
        // Даже в версии для двух игроков ключевой секрет остаётся у Талена;
        // возница тогда остаётся важным NPC, а не исчезающей истиной сценария.
        return count == 2 ? [roles[0], roles[2]] : roles.Take(count).ToArray();
    }

    private static GeneratedScenario BuildGeneric(ScenarioGenerationOptions o, ScenarioTemplate t, Random random)
    {
        var roles = BuildRoles(o.ParticipantCount, random);
        var clues = new[] { new ScenarioClue { Id="clue-letter", Text="Письмо с противоречивой датой.", KnownFact="дата отъезда известна свидетелям", Reveals="кто лжёт о сроках" }, new ScenarioClue { Id="clue-witness", Text="Свидетель видел передачу знака доверия.", KnownFact="знак принадлежит одной из сторон", Reveals="скрытую связь сторон" } };
        var scenes = new[] { new ScenarioScene { Id="opening", Title="Завязка", Purpose=t.Hook }, new ScenarioScene { Id="discovery", Title="Раскрытие фактов", Purpose="Участники сопоставляют мотивы и находят две улики.", ClueIds=["clue-letter","clue-witness"] }, new ScenarioScene { Id="decision", Title="Решение", Purpose="Стороны выбирают, кому доверить последствия конфликта." } };
        var endings = new[] { new ScenarioEnding { Id="accord", Title="Соглашение", Outcome="Стороны раскрывают условия и заключают проверяемый договор.", IsReachable=true }, new ScenarioEnding { Id="exposure", Title="Разоблачение", Outcome="Обман становится публичным, а виновная сторона отвечает перед свидетелями.", IsReachable=true }, new ScenarioEnding { Id="departure", Title="Трудный выбор", Outcome="Герои жертвуют выгодой, чтобы предотвратить больший вред.", IsReachable=true } };
        return new GeneratedScenario { Title=t.Name, Seed=o.Seed, Options=o, Template=t, Hook=t.Hook, ConflictCause=t.ConflictCause, Characters=roles, Clues=clues, Scenes=scenes, Branches=[new ScenarioBranch { FromSceneId="opening", ToSceneId="discovery", Condition="Участники принимают поручение или решают вмешаться." }, new ScenarioBranch { FromSceneId="discovery", ToSceneId="decision", Condition="Найдена хотя бы одна улика." }, new ScenarioBranch { FromSceneId="decision", ToEndingId="accord", Condition="Стороны заключают договор." }, new ScenarioBranch { FromSceneId="decision", ToEndingId="exposure", Condition="Игроки предъявляют доказательство." }, new ScenarioBranch { FromSceneId="decision", ToEndingId="departure", Condition="Игроки выбирают меньший ущерб." }], Endings=endings };
    }

    private static IReadOnlyList<ScenarioCharacter> BuildRoles(int count, Random random)
    {
        string[] names = ["Заказчик", "Сторонник", "Свидетель", "Посредник", "Скептик", "Хранитель тайны"];
        var secretHolder = random.Next(count);
        return Enumerable.Range(0, count).Select(index => new ScenarioCharacter { Name=names[index], Role=index == 0 ? "ключевая сторона конфликта" : "участник с правом влиять на исход", Goal=index == 0 ? "добиться решения до истечения срока" : "защитить собственные интересы без разрушения соглашения", Motive=index == 0 ? "обещание, данное близкому человеку" : "репутация и безопасность сообщества", Secret=index == secretHolder ? "скрывает факт, связывающий его с причиной конфликта" : "", IsKeySecretHolder=index == secretHolder }).ToArray();
    }
}

public static class ScenarioValidator
{
    public static ScenarioValidationResult Validate(GeneratedScenario scenario)
    {
        var errors = new List<string>();
        if (scenario.Characters.Count != scenario.Options.ParticipantCount) errors.Add("Количество ролей не совпадает с настройкой.");
        if (scenario.Characters.Any(role => string.IsNullOrWhiteSpace(role.Goal) || string.IsNullOrWhiteSpace(role.Motive))) errors.Add("У ключевой роли отсутствует цель или мотив.");
        if (string.IsNullOrWhiteSpace(scenario.ConflictCause)) errors.Add("Не указана причина конфликта.");
        if (scenario.Clues.Any(clue => string.IsNullOrWhiteSpace(clue.KnownFact))) errors.Add("Улика не связана с известным фактом.");
        if (scenario.NarrativeFacts.Any(fact => string.IsNullOrWhiteSpace(fact.Id) || string.IsNullOrWhiteSpace(fact.Text)))
            errors.Add("У сюжетного факта отсутствует идентификатор или текст.");
        AddDuplicateIdentifierErrors(scenario.NarrativeFacts.Select(fact => fact.Id), "сюжетного факта", errors);
        foreach (var fact in scenario.NarrativeFacts.Where(fact => fact.Origin == ScenarioFactOrigin.CanonicalKnowledge))
        {
            if (fact.SourceEntityId is null || fact.SourceEntityId <= 0 || string.IsNullOrWhiteSpace(fact.SourceUrl))
                errors.Add($"Канонический сюжетный факт '{fact.Id}' не имеет проверяемого источника.");
            if (!scenario.CanonicalAnchors.Any(anchor => anchor.EntityId == fact.SourceEntityId &&
                                                         anchor.Origin == ScenarioFactOrigin.CanonicalKnowledge &&
                                                         !anchor.NeedsReview &&
                                                         string.Equals(anchor.SourceUrl, fact.SourceUrl, StringComparison.Ordinal)))
                errors.Add($"Канонический сюжетный факт '{fact.Id}' не связан с подтверждённой опорой.");
        }
        if (scenario.CanonicalAnchors.Any(anchor => anchor.Origin != ScenarioFactOrigin.CanonicalKnowledge ||
                                                    anchor.NeedsReview || anchor.EntityId <= 0 ||
                                                    string.IsNullOrWhiteSpace(anchor.SourceUrl)))
            errors.Add("Канонические опоры должны быть проверенными записями Knowledge DB с URL источника.");
        if (!scenario.Options.AllowMandatoryCombat && scenario.Endings.Any(ending => ending.RequiresCombat)) errors.Add("Сценарий требует обязательного боя, хотя он запрещён.");
        if (scenario.Options.DurationMinutes < scenario.Template.MinDurationMinutes || scenario.Options.DurationMinutes > scenario.Template.MaxDurationMinutes) errors.Add("Длительность не соответствует каркасу.");
        var scenes = scenario.Scenes.Select(scene => scene.Id).ToHashSet(StringComparer.Ordinal);
        var endings = scenario.Endings.Select(ending => ending.Id).ToHashSet(StringComparer.Ordinal);
        var clues = scenario.Clues.Select(clue => clue.Id).ToHashSet(StringComparer.Ordinal);
        var hypotheses = scenario.Hypotheses.Select(hypothesis => hypothesis.Id).ToHashSet(StringComparer.Ordinal);
        var coalitions = scenario.Coalitions.Select(coalition => coalition.Id).ToHashSet(StringComparer.Ordinal);
        var roleNames = scenario.Characters.Select(character => character.Name).ToHashSet(StringComparer.Ordinal);
        if (scenario.Scenes.Any(scene => string.IsNullOrWhiteSpace(scene.Id))) errors.Add("У сцены отсутствует идентификатор.");
        if (scenario.Endings.Any(ending => string.IsNullOrWhiteSpace(ending.Id))) errors.Add("У финала отсутствует идентификатор.");
        if (scenario.Clues.Any(clue => string.IsNullOrWhiteSpace(clue.Id))) errors.Add("У улики отсутствует идентификатор.");
        if (scenario.Characters.Any(character => string.IsNullOrWhiteSpace(character.Name))) errors.Add("У роли отсутствует имя.");
        if (scenario.Characters.Where(character => !string.IsNullOrWhiteSpace(character.Name)).Select(character => character.Name).Distinct(StringComparer.Ordinal).Count() != scenario.Characters.Count)
            errors.Add("Роли должны иметь уникальные имена.");
        AddDuplicateIdentifierErrors(scenario.Scenes.Select(scene => scene.Id), "сцены", errors);
        AddDuplicateIdentifierErrors(scenario.Endings.Select(ending => ending.Id), "финала", errors);
        AddDuplicateIdentifierErrors(scenario.Clues.Select(clue => clue.Id), "улики", errors);
        if (hypotheses.Count != scenario.Hypotheses.Count) errors.Add("Версии расследования должны иметь уникальные идентификаторы.");
        if (coalitions.Count != scenario.Coalitions.Count) errors.Add("Коалиции должны иметь уникальные идентификаторы.");
        if (scenario.Hypotheses.Any(hypothesis => string.IsNullOrWhiteSpace(hypothesis.Id) || string.IsNullOrWhiteSpace(hypothesis.Statement))) errors.Add("У версии расследования отсутствует идентификатор или формулировка.");
        foreach (var clue in scenario.Clues)
        {
            foreach (var hypothesisId in clue.SupportsHypothesisIds.Concat(clue.ContradictsHypothesisIds).Where(hypothesisId => !hypotheses.Contains(hypothesisId)))
                errors.Add($"Улика '{clue.Id}' ссылается на отсутствующую версию '{hypothesisId}'.");
        }
        foreach (var coalition in scenario.Coalitions)
        {
            if (string.IsNullOrWhiteSpace(coalition.Id) || string.IsNullOrWhiteSpace(coalition.Name) || coalition.Members.Count < 2 || string.IsNullOrWhiteSpace(coalition.Concession) || string.IsNullOrWhiteSpace(coalition.Cost)) errors.Add("Коалиция должна иметь идентификатор, участников, уступку и цену.");
            foreach (var clueId in coalition.RequiredClueIds.Where(clueId => !clues.Contains(clueId))) errors.Add($"Коалиция '{coalition.Id}' требует отсутствующую улику '{clueId}'.");
            foreach (var member in coalition.Members.Where(member => !roleNames.Contains(member))) errors.Add($"Коалиция '{coalition.Id}' ссылается на отсутствующую роль '{member}'.");
            if (coalition.Members.Distinct(StringComparer.Ordinal).Count() != coalition.Members.Count) errors.Add($"Коалиция '{coalition.Id}' повторяет роль участника.");
        }
        foreach (var scene in scenario.Scenes)
        {
            foreach (var clueId in scene.ClueIds.Where(clueId => !clues.Contains(clueId)))
                errors.Add($"Сцена '{scene.Id}' ссылается на отсутствующую улику '{clueId}'.");
        }
        foreach (var clue in scenario.Clues.Where(clue => !scenario.Scenes.Any(scene => scene.ClueIds.Contains(clue.Id))))
            errors.Add($"Улика '{clue.Id}' не доступна ни в одной сцене.");
        if (!scenes.Contains(scenario.StartSceneId)) errors.Add($"Стартовая сцена '{scenario.StartSceneId}' отсутствует.");
        foreach (var branch in scenario.Branches)
        {
            if (!scenes.Contains(branch.FromSceneId)) errors.Add($"Переход исходит из отсутствующей сцены '{branch.FromSceneId}'.");
            var destinations = (string.IsNullOrWhiteSpace(branch.ToSceneId) ? 0 : 1) + (string.IsNullOrWhiteSpace(branch.ToEndingId) ? 0 : 1);
            if (destinations != 1) errors.Add($"Переход из '{branch.FromSceneId}' должен вести ровно к одной сцене или одному финалу.");
            if (!string.IsNullOrWhiteSpace(branch.ToSceneId) && !scenes.Contains(branch.ToSceneId)) errors.Add($"Переход ведёт к отсутствующей сцене '{branch.ToSceneId}'.");
            if (!string.IsNullOrWhiteSpace(branch.ToEndingId) && !endings.Contains(branch.ToEndingId)) errors.Add($"Переход ведёт к отсутствующему финалу '{branch.ToEndingId}'.");
            if (string.IsNullOrWhiteSpace(branch.Condition)) errors.Add($"У перехода из '{branch.FromSceneId}' отсутствует условие.");
            foreach (var clueId in branch.RequiredClueIds.Where(clueId => !clues.Contains(clueId)))
                errors.Add($"Переход из '{branch.FromSceneId}' требует отсутствующую улику '{clueId}'.");
        }
        var reachableScenes = new HashSet<string>(StringComparer.Ordinal);
        var reachableEndings = new HashSet<string>(StringComparer.Ordinal);
        var reachableStates = new List<(string SceneId, IReadOnlySet<string> ClueIds)>();
        var initialClues = scenario.Scenes.FirstOrDefault(scene => scene.Id == scenario.StartSceneId)?.ClueIds.ToHashSet(StringComparer.Ordinal) ?? [];
        var frontier = new Queue<(string SceneId, HashSet<string> ClueIds)>();
        var visitedStates = new HashSet<string>(StringComparer.Ordinal);
        if (scenes.Contains(scenario.StartSceneId)) frontier.Enqueue((scenario.StartSceneId, initialClues));
        while (frontier.Count > 0)
        {
            var (current, collectedClues) = frontier.Dequeue();
            var stateKey = current + "|" + string.Join("|", collectedClues.OrderBy(clueId => clueId, StringComparer.Ordinal));
            if (!visitedStates.Add(stateKey)) continue;
            reachableScenes.Add(current);
            reachableStates.Add((current, new HashSet<string>(collectedClues, StringComparer.Ordinal)));
            foreach (var branch in scenario.Branches.Where(branch => branch.FromSceneId == current && branch.RequiredClueIds.All(collectedClues.Contains)))
            {
                if (!string.IsNullOrWhiteSpace(branch.ToSceneId) && scenes.Contains(branch.ToSceneId))
                {
                    var nextClues = new HashSet<string>(collectedClues, StringComparer.Ordinal);
                    nextClues.UnionWith(scenario.Scenes.First(scene => scene.Id == branch.ToSceneId).ClueIds);
                    frontier.Enqueue((branch.ToSceneId, nextClues));
                }
                if (!string.IsNullOrWhiteSpace(branch.ToEndingId) && endings.Contains(branch.ToEndingId)) reachableEndings.Add(branch.ToEndingId);
            }
        }
        foreach (var branch in scenario.Branches.Where(branch =>
                     branch.RequiredClueIds.Count > 0 && scenes.Contains(branch.FromSceneId) &&
                     branch.RequiredClueIds.All(clues.Contains)))
        {
            if (!reachableStates.Any(state => state.SceneId == branch.FromSceneId && branch.RequiredClueIds.All(state.ClueIds.Contains)))
                errors.Add($"Условие перехода из '{branch.FromSceneId}' требует недостижимые улики: {string.Join(", ", branch.RequiredClueIds)}.");
        }
        foreach (var coalition in scenario.Coalitions.Where(coalition => coalition.RequiredClueIds.Count > 0 && coalition.RequiredClueIds.All(clues.Contains)))
        {
            if (!reachableStates.Any(state => coalition.RequiredClueIds.All(state.ClueIds.Contains)))
                errors.Add($"Коалиция '{coalition.Id}' требует недостижимые улики: {string.Join(", ", coalition.RequiredClueIds)}.");
        }
        foreach (var ending in scenario.Endings.Where(ending => !reachableEndings.Contains(ending.Id))) errors.Add($"Финал '{ending.Id}' недостижим из стартовой сцены '{scenario.StartSceneId}'.");
        if (scenario.Template.Id == "negotiation")
        {
            if (reachableEndings.Count < 3) errors.Add("Для 'Последней повозки до заката' нужны минимум три достижимых финала.");
            if (scenario.Endings.Where(ending => reachableEndings.Contains(ending.Id)).Select(ending => ending.Outcome).Distinct(StringComparer.Ordinal).Count() < 3) errors.Add("Достижимые финалы переговорного сценария содержательно не различаются.");
            if (string.IsNullOrWhiteSpace(scenario.NegotiationBlueprintId))
            {
                if (scenario.Characters.Count(role => role.IsKeySecretHolder && !string.IsNullOrWhiteSpace(role.Secret)) != 1)
                    errors.Add("Для legacy-переговорного сценария должен быть ровно один носитель ключевого секрета.");
            }
            else
            {
                if (!ScenarioNegotiationBlueprintFactory.BlueprintIds.Contains(scenario.NegotiationBlueprintId, StringComparer.Ordinal))
                    errors.Add($"Неизвестный blueprint переговоров '{scenario.NegotiationBlueprintId}'.");
                if (scenario.Characters.Any(role => string.IsNullOrWhiteSpace(role.Concession) || string.IsNullOrWhiteSpace(role.RedLine)))
                    errors.Add("Роли blueprint переговоров должны иметь уступки и красные линии.");
                if (!scenario.NarrativeFacts.Any(fact => fact.Origin == ScenarioFactOrigin.FactoryFiction) ||
                    !scenario.NarrativeFacts.Any(fact => fact.Origin == ScenarioFactOrigin.GmHypothesis))
                    errors.Add("Blueprint переговоров должен разделять вымысел и гипотезы ведущего.");
            }
        }
        if (scenario.Template.Id == "investigation")
        {
            if (scenario.Hypotheses.Count < 3) errors.Add("Для расследования нужны минимум три правдоподобные версии.");
            if (scenario.Clues.Count < 4) errors.Add("Для расследования нужны минимум четыре содержательные улики.");
            if (scenario.Clues.Any(clue => clue.SupportsHypothesisIds.Count == 0 && clue.ContradictsHypothesisIds.Count == 0)) errors.Add("Каждая улика расследования должна подтверждать или опровергать версию.");
            if (reachableEndings.Count < 4) errors.Add("Для расследования нужны минимум четыре достижимых финала.");
            if (scenario.Endings.Where(ending => reachableEndings.Contains(ending.Id)).Select(ending => ending.Outcome).Distinct(StringComparer.Ordinal).Count() < 4) errors.Add("Финалы расследования содержательно не различаются.");
            if (!string.IsNullOrWhiteSpace(scenario.InvestigationBlueprintId))
            {
                foreach (var hypothesis in scenario.Hypotheses)
                {
                    var relatedEvidence = scenario.Clues.Count(clue => clue.SupportsHypothesisIds.Contains(hypothesis.Id) || clue.ContradictsHypothesisIds.Contains(hypothesis.Id));
                    if (relatedEvidence < 2)
                        errors.Add($"Blueprint расследования не различает версию '{hypothesis.Id}' достаточным числом улик.");
                }
                if (!scenario.NarrativeFacts.Any(fact => fact.Origin == ScenarioFactOrigin.FactoryFiction) ||
                    !scenario.NarrativeFacts.Any(fact => fact.Origin == ScenarioFactOrigin.GmHypothesis))
                    errors.Add("Blueprint расследования должен явно отделять авторскую ситуацию от гипотезы ведущего.");
            }
        }
        if (scenario.Template.Id == "intrigue")
        {
            if (scenario.Characters.Count < 4 || scenario.Characters.Any(role => string.IsNullOrWhiteSpace(role.Concession) || string.IsNullOrWhiteSpace(role.RedLine))) errors.Add("Для политического сценария нужны минимум четыре роли с уступкой и красной линией.");
            if (scenario.Coalitions.Count < 2) errors.Add("Для политического сценария нужны минимум две коалиции.");
            if (reachableEndings.Count < 4) errors.Add("Для политического сценария нужны минимум четыре достижимых финала.");
            if (scenario.Endings.Where(ending => reachableEndings.Contains(ending.Id)).Select(ending => ending.Outcome).Distinct(StringComparer.Ordinal).Count() < 4) errors.Add("Финалы политического сценария содержательно не различаются.");
            if (!string.IsNullOrWhiteSpace(scenario.IntrigueBlueprintId))
            {
                if (!ScenarioIntrigueBlueprintFactory.BlueprintIds.Contains(scenario.IntrigueBlueprintId, StringComparer.Ordinal))
                    errors.Add($"Неизвестный blueprint интриги '{scenario.IntrigueBlueprintId}'.");
                if (scenario.Coalitions.Count < 3)
                    errors.Add("Blueprint интриги должен содержать минимум три различимые коалиции.");
                if (scenario.Coalitions.Any(coalition => coalition.RequiredClueIds.Count == 0))
                    errors.Add("Каждая коалиция blueprint интриги должна иметь проверяемые основания.");
                if (!scenario.NarrativeFacts.Any(fact => fact.Origin == ScenarioFactOrigin.FactoryFiction) ||
                    !scenario.NarrativeFacts.Any(fact => fact.Origin == ScenarioFactOrigin.GmHypothesis))
                    errors.Add("Blueprint интриги должен явно отделять авторскую ситуацию от гипотезы ведущего.");
                var duplicateBranch = scenario.Branches.GroupBy(branch => $"{branch.FromSceneId}\u001f{branch.ToSceneId}\u001f{branch.ToEndingId}\u001f{branch.Decision}", StringComparer.Ordinal).Any(group => group.Count() > 1);
                if (duplicateBranch) errors.Add("Blueprint интриги содержит повторяющийся переход.");
            }
        }
        if (scenario.Template.Id == "mission" && !string.IsNullOrWhiteSpace(scenario.MissionBlueprintId))
        {
            if (!ScenarioMissionBlueprintFactory.BlueprintIds.Contains(scenario.MissionBlueprintId, StringComparer.Ordinal))
                errors.Add($"Неизвестный blueprint миссии '{scenario.MissionBlueprintId}'.");
            if (reachableEndings.Count < 4) errors.Add("Blueprint миссии должен иметь минимум четыре достижимых финала.");
            if (scenario.Endings.Any(ending => ending.RequiresCombat)) errors.Add("Blueprint миссии не должен требовать обязательного боя.");
            if (scenario.Endings.Select(ending => ending.Outcome).Distinct(StringComparer.Ordinal).Count() < 4)
                errors.Add("Финалы blueprint миссии должны иметь различные последствия.");
            if (!scenario.NarrativeFacts.Any(fact => fact.Origin == ScenarioFactOrigin.FactoryFiction) ||
                !scenario.NarrativeFacts.Any(fact => fact.Origin == ScenarioFactOrigin.GmHypothesis))
                errors.Add("Blueprint миссии должен отделять авторскую ситуацию от гипотезы ведущего.");
        }
        if (scenario.Template.Id == "conflict")
        {
            if (scenario.Characters.Count < 4 || scenario.Characters.Any(role => string.IsNullOrWhiteSpace(role.Concession) || string.IsNullOrWhiteSpace(role.RedLine))) errors.Add("Для личного конфликта нужны минимум четыре роли с уступкой и красной линией.");
            if (scenario.Hypotheses.Count < 3) errors.Add("Для личного конфликта нужны минимум три версии событий.");
            if (reachableEndings.Count < 4) errors.Add("Для личного конфликта нужны минимум четыре достижимых финала.");
            if (scenario.Endings.Where(ending => reachableEndings.Contains(ending.Id)).Select(ending => ending.Outcome).Distinct(StringComparer.Ordinal).Count() < 4) errors.Add("Финалы личного конфликта содержательно не различаются.");
        }
        return new ScenarioValidationResult { Errors = errors };
    }

    private static void AddDuplicateIdentifierErrors(
        IEnumerable<string> identifiers,
        string itemName,
        ICollection<string> errors)
    {
        foreach (var duplicate in identifiers
                     .Where(identifier => !string.IsNullOrWhiteSpace(identifier))
                     .GroupBy(identifier => identifier, StringComparer.Ordinal)
                     .Where(group => group.Count() > 1)
                     .Select(group => group.Key))
        {
            errors.Add($"Обнаружен повторяющийся идентификатор {itemName}: '{duplicate}'.");
        }
    }
}
