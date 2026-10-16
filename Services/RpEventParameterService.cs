using SkyrimRpEncyclopedia.Models;

namespace SkyrimRpEncyclopedia.Services;

public sealed class RpEventParameterService
{
    private readonly Random _random;

    public RpEventParameterService(Random? random = null)
    {
        _random = random ?? Random.Shared;
    }

    public IReadOnlyList<RpParameterField> CreateFields()
    {
        return
        [
            Create("eventType", "Тип события", false,
                "⚔ Задание", "🕵 Расследование", "🐉 Охота", "🛡 Оборона",
                "⚔ Засада", "💰 Поиск предмета", "👤 Встреча", "🏛 Политическое событие",
                "☠ Покушение", "🎭 Свободное событие"),
            Create("location", "Локация", false,
                "Вайтран", "Солитьюд", "Рифтен", "Виндхельм", "Маркарт", "Хелген",
                "Драконий Предел", "Предел", "Рифт", "Владение Хьялмарк"),
            Create("faction", "Фракция", true,
                "Имперский легион", "Братья Бури", "Соратники", "Тёмное Братство",
                "Гильдия воров", "Коллегия Винтерхолда", "Стража Вайтрана"),
            Create("characters", "Участники", true,
                "Жители города", "Стража", "Наёмники", "Путешественники", "Искатели приключений"),
            Create("objective", "Цель", false,
                "защитить поселение", "раскрыть тайну", "найти пропавшего человека",
                "вернуть ценный предмет", "остановить угрозу", "доставить важное сообщение"),
            Create("conditions", "Условия", true,
                "до рассвета", "без лишнего шума", "под прикрытием непогоды",
                "не привлекая стражу", "в присутствии свидетелей"),
            Create("reward", "Награда", true,
                "золото", "репутация фракции", "доступ к информации", "редкий предмет", "услуга"),
            Create("extra", "Дополнительные детали", true,
                "неожиданный свидетель", "конфликт интересов", "тайный срок", "ложный след")
        ];
    }

    public string Resolve(RpParameterField field)
    {
        return field.Resolve(() => ChooseRandom(field));
    }

    public void Reroll(RpParameterField field)
    {
        var previous = Resolve(field);
        field.Reroll(ChooseRandom(field, previous));
    }

    private string ChooseRandom(RpParameterField field, string? exclude = null)
    {
        var candidates = field.Options
            .Where(option => option != RpParameterField.RandomValue && option != RpParameterField.UnspecifiedValue)
            .ToList();

        var alternatives = candidates
            .Where(option => option != exclude)
            .ToList();
        if (alternatives.Count > 0)
            candidates = alternatives;

        if (candidates.Count == 0)
            return string.Empty;

        return candidates[_random.Next(candidates.Count)];
    }

    private static RpParameterField Create(string key, string label, bool isOptional, params string[] values)
    {
        var options = new List<string> { RpParameterField.RandomValue };
        if (isOptional)
            options.Add(RpParameterField.UnspecifiedValue);
        options.AddRange(values);

        return new RpParameterField
        {
            Key = key,
            Label = label,
            IsOptional = isOptional,
            Options = options
        };
    }
}
