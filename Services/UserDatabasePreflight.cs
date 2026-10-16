using System.IO;
using Microsoft.Data.Sqlite;

namespace SkyrimRpEncyclopedia.Services;

/// <summary>Existing files are inspected read-only before any initialization. Never repairs/replaces a file.</summary>
public static class UserDatabasePreflight
{
    public static void ValidateExisting(string path)
    {
        if(Directory.Exists(path)) throw new IOException("Вместо файла базы указан каталог.");
        if (!File.Exists(path)) return; // First creation at a new path remains supported.
        using var c=MvpKnowledgeDatabaseService.OpenReadOnlyConnection(path); c.Open();
        using var cmd=c.CreateCommand(); cmd.CommandTimeout=3;
        cmd.CommandText="SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name IN ('Entities','BuildInfo','CrawlRuns','Pages','PackageInfo')";
        if(Convert.ToInt64(cmd.ExecuteScalar())>0) throw new InvalidDataException("В файле обнаружены таблицы Knowledge/Raw. Он не будет открыт для записи как личная или официальная база.");
        cmd.CommandText="PRAGMA quick_check";
        using(var rows=cmd.ExecuteReader()) while(rows.Read())
            if(rows.GetString(0)!="ok") throw new InvalidDataException("SQLite не подтвердил целостность файла. Автоматическое исправление запрещено.");
        cmd.CommandText="PRAGMA table_info(Articles)";
        var columns=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using(var rows=cmd.ExecuteReader()) while(rows.Read()) columns.Add(rows.GetString(1));
        if(!new[]{"Id","Title","Category","Content"}.All(columns.Contains))
            throw new InvalidDataException("Файл не соответствует пользовательскому/официальному хранилищу статей. Он не будет инициализирован поверх другой базы.");
    }
    public static void ValidateDistinct(params string?[] paths)
    {
        var resolved=paths.Where(p=>!string.IsNullOrWhiteSpace(p)).Select(p=>Path.GetFullPath(p!)).ToArray();
        if(resolved.Distinct(StringComparer.OrdinalIgnoreCase).Count()!=resolved.Length)
            throw new InvalidOperationException("Пути личной, официальной и Knowledge DB должны различаться.");
    }
}
