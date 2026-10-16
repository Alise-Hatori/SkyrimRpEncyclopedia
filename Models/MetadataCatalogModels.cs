namespace SkyrimRpEncyclopedia.Models;

public enum MetadataKind { Category, Tag, Flag }

public sealed record MetadataEntry(MetadataKind Kind, int Id, string Name, string Description = "",
    string Icon = "", string Color = "#808080", bool IsSystem = false, int SortOrder = 0);

public sealed record MetadataPreset(string Id, MetadataKind Kind, string Group, string Name, string Description, string IconId);
public sealed record MetadataPresetResult(int Added, int Skipped);
