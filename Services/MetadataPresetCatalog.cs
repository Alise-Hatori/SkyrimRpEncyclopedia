using System.Text;
using System.Text.RegularExpressions;
using SkyrimRpEncyclopedia.Models;

namespace SkyrimRpEncyclopedia.Services;

public static class MetadataPresetCatalog
{
    public static string NormalizeName(string value) => Regex.Replace(value.Normalize(NormalizationForm.FormC).Trim(), @"\s+", " ").ToUpperInvariant();
    public static IReadOnlyList<MetadataPreset> All { get; } = Array.AsReadOnly(new[]
    {
        new MetadataPreset("tag-location", MetadataKind.Tag, "Мир и лор", "Локация", "Места и их окружение.", "fortress"),
        new MetadataPreset("tag-character", MetadataKind.Tag, "Мир и лор", "Персонаж", "Сведения о персонаже.", "person"),
        new MetadataPreset("tag-faction", MetadataKind.Tag, "Мир и лор", "Фракция", "Объединения и организации.", "blades"),
        new MetadataPreset("tag-history", MetadataKind.Tag, "Мир и лор", "История", "История, хроники и документы.", "parchment"),
        new MetadataPreset("tag-religion", MetadataKind.Tag, "Мир и лор", "Религия и Даэдра", "Верования, культы и Даэдра.", "divinity"),
        new MetadataPreset("tag-item", MetadataKind.Tag, "Мир и лор", "Предмет", "Предметы и их игровое применение.", "alchemy"),
        new MetadataPreset("tag-adventure", MetadataKind.Tag, "Игровое применение", "Приключение", "Материал для исследования и путешествия.", "compass"),
        new MetadataPreset("tag-investigation", MetadataKind.Tag, "Игровое применение", "Расследование", "Материал для поиска и сопоставления сведений.", "search"),
        new MetadataPreset("tag-negotiation", MetadataKind.Tag, "Игровое применение", "Переговоры", "Предмет обсуждения и договорённостей.", "scales"),
        new MetadataPreset("tag-politics", MetadataKind.Tag, "Игровое применение", "Политика", "Власть и общественные интересы.", "crown"),
        new MetadataPreset("tag-rules", MetadataKind.Tag, "Игровое применение", "Правила RP", "Правила сообщества, задаваемые пользователем.", "shield"),
        new MetadataPreset("tag-hook", MetadataKind.Tag, "Игровое применение", "Сюжетный крючок", "Отправная точка для свободного отыгрыша.", "mask"),
        new MetadataPreset("tag-geography", MetadataKind.Tag, "Дополнительные категории", "География", "Регионы, дороги и ориентиры.", "map"),
        new MetadataPreset("tag-government", MetadataKind.Tag, "Дополнительные категории", "Политическое устройство", "Устройство власти и управления.", "crown"),
        new MetadataPreset("tag-magic", MetadataKind.Tag, "Дополнительные категории", "Магия", "Магические знания и практики.", "rune"),
        new MetadataPreset("tag-creatures", MetadataKind.Tag, "Дополнительные категории", "Существа", "Сведения о существах.", "dragon"),
        new MetadataPreset("tag-rumors", MetadataKind.Tag, "Дополнительные категории", "Диалоги и слухи", "Реплики и слухи; метка не подтверждает их истинность.", "speech"),
        new MetadataPreset("tag-personal", MetadataKind.Tag, "Дополнительные категории", "Личные материалы", "Авторские материалы пользователя.", "quill"),
        new MetadataPreset("flag-canon", MetadataKind.Flag, "Состояние материала", "Канон подтверждён", "Назначается вручную после проверки источников, не автоматически по происхождению статьи.", "parchment"),
        new MetadataPreset("flag-approval", MetadataKind.Flag, "Состояние материала", "Требует согласования", "Необходимо согласование с участниками или ведущим.", "scales"),
        new MetadataPreset("flag-review", MetadataKind.Flag, "Состояние материала", "Требует проверки", "Сведения требуют дополнительной проверки.", "question"),
        new MetadataPreset("flag-interpretation", MetadataKind.Flag, "Состояние материала", "Авторская интерпретация", "Авторская трактовка, не подтверждённый канонический факт.", "rune"),
        new MetadataPreset("flag-ready", MetadataKind.Flag, "Состояние материала", "Готово к отыгрышу", "Материал подготовлен пользователем для игры.", "dice"),
        new MetadataPreset("flag-hidden", MetadataKind.Flag, "Состояние материала", "Скрытая информация", "Пометка ведущего; сама по себе не ограничивает доступ к статье.", "lock"),
        new MetadataPreset("flag-important", MetadataKind.Flag, "Состояние материала", "Важный материал", "Материал требует особого внимания.", "star"),
        new MetadataPreset("flag-time", MetadataKind.Flag, "Состояние материала", "Зависит от времени", "Актуальность или применение ограничены временем.", "hourglass")
    });

    private static readonly IReadOnlyDictionary<(MetadataKind Kind,string Name),MetadataPreset> ByName =
        All.ToDictionary(p=>(p.Kind,NormalizeName(p.Name)));
    public static MetadataPreset? Find(MetadataKind kind,string name) => ByName.TryGetValue((kind,NormalizeName(name)),out var preset)?preset:null;

    public static string IconFor(MetadataKind kind, string name, string storedIcon = "")
    {
        var key = storedIcon.StartsWith("metadata:", StringComparison.Ordinal) ? storedIcon[9..] : "";
        if (IconIds.Contains(key)) return key;
        var preset = Find(kind,name);
        if (preset is not null) return preset.IconId;
        if (kind == MetadataKind.Category)
        {
            var text = name.ToLowerInvariant();
            if (text.Contains("локац")) return "fortress";
            if (text.Contains("персонаж")) return "person";
            if (text.Contains("фракц")) return "blades";
            if (text.Contains("маги")) return "rune";
            if (text.Contains("бог") || text.Contains("даэдр")) return "divinity";
            if (text.Contains("правил")) return "shield";
        }
        return kind == MetadataKind.Flag ? "shield" : "parchment";
    }

    public static IReadOnlyList<string> IconIds { get; } = new[] { "parchment", "fortress", "person", "blades", "rune", "divinity", "compass", "search", "scales", "shield", "mask", "lock", "question", "star", "hourglass", "dice", "quill", "map", "alchemy", "crown", "dragon", "speech" };
}
