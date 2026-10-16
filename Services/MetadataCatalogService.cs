using Microsoft.Data.Sqlite;
using SkyrimRpEncyclopedia.Models;

namespace SkyrimRpEncyclopedia.Services;

/// <summary>Only explicit metadata actions; no startup seeding or article assignment.</summary>
public sealed class MetadataCatalogService
{
    private readonly DatabaseService _database;
    public MetadataCatalogService(DatabaseService database) => _database = database;

    public IReadOnlyList<MetadataEntry> Read(MetadataKind kind) => kind switch
    {
        MetadataKind.Category => _database.GetCategories().Select(c => new MetadataEntry(kind, c.Id, c.Name, c.Description, IsSystem:c.IsSystem, SortOrder:c.SortOrder)).ToArray(),
        MetadataKind.Tag => _database.GetTags().Select(t => new MetadataEntry(kind,t.Id,t.Name)).ToArray(),
        _ => _database.GetFlags().Select(f => new MetadataEntry(kind,f.Id,f.Name,f.Description,f.Icon,f.Color,f.IsSystem)).ToArray()
    };

    public int Save(MetadataEntry draft)
    {
        if (string.IsNullOrWhiteSpace(draft.Name)) throw new ArgumentException("Введите название.");
        if (draft.Kind != MetadataKind.Category && draft.Name.IndexOfAny([',',';','\r','\n']) >= 0)
            throw new ArgumentException("Название одной метки не должно содержать разделители.");
        switch (draft.Kind)
        {
            case MetadataKind.Category:
                var category = new ContentCategory { Id=draft.Id,Name=draft.Name,Description=draft.Description,IsSystem=draft.IsSystem,SortOrder=draft.SortOrder };
                if(draft.Id==0) return _database.AddCategory(category); _database.UpdateCategory(category); break;
            case MetadataKind.Tag:
                if(draft.Id==0) return _database.AddTag(draft.Name); _database.RenameTag(draft.Id,draft.Name); break;
            case MetadataKind.Flag:
                var flag = new ContentFlag { Id=draft.Id,Name=draft.Name,Description=draft.Description,Icon=draft.Icon,Color=draft.Color,IsSystem=draft.IsSystem };
                if(draft.Id==0) return _database.AddFlag(flag); _database.UpdateFlag(flag); break;
            default: throw new ArgumentOutOfRangeException(nameof(draft));
        }
        return draft.Id;
    }

    public void Delete(MetadataEntry item)
    {
        if(item.IsSystem) throw new InvalidOperationException("Системный элемент нельзя удалить.");
        if(item.Kind==MetadataKind.Tag) { _database.DeleteTag(item.Id); return; }
        using var connection=OpenWrite(); using var command=connection.CreateCommand();
        command.CommandText=$"DELETE FROM {(item.Kind==MetadataKind.Category ? "Categories" : "Flags")} WHERE Id=$id AND IsSystem=0";
        command.Parameters.AddWithValue("$id",item.Id);
        if(command.ExecuteNonQuery()!=1) throw new InvalidOperationException("Элемент отсутствует или защищён от удаления.");
    }

    private SqliteConnection OpenWrite()
    {
        var c=new SqliteConnection(new SqliteConnectionStringBuilder { DataSource=_database.DatabasePath,Mode=SqliteOpenMode.ReadWrite }.ToString());
        c.Open(); return c;
    }

    public IReadOnlySet<string> ExistingNames(MetadataKind kind)
    {
        using var connection=MvpKnowledgeDatabaseService.OpenReadOnlyConnection(_database.DatabasePath); connection.Open();
        using var command=connection.CreateCommand();
        command.CommandText=$"SELECT Name FROM {(kind==MetadataKind.Tag ? "Tags" : kind==MetadataKind.Flag ? "Flags" : throw new ArgumentException("Наборы доступны для тегов и флагов."))}";
        using var rows=command.ExecuteReader(); var names=new HashSet<string>(StringComparer.Ordinal);
        while(rows.Read()) names.Add(MetadataPresetCatalog.NormalizeName(rows.GetString(0)));
        return names;
    }

    public MetadataPresetResult AddPresets(MetadataKind kind,IReadOnlyCollection<string> selectedIds)
    {
        if(kind==MetadataKind.Category) throw new ArgumentException("Готовые наборы не создают категории.");
        var candidates=MetadataPresetCatalog.All.Where(p=>p.Kind==kind && selectedIds.Contains(p.Id)).ToArray();
        if(candidates.Length!=selectedIds.Distinct().Count()) throw new ArgumentException("Набор содержит неизвестные элементы.");
        using var connection=OpenWrite(); using var transaction=connection.BeginTransaction();
        using var read=connection.CreateCommand(); read.Transaction=transaction;
        read.CommandText=$"SELECT Name FROM {(kind==MetadataKind.Tag ? "Tags" : "Flags")}";
        var names=new HashSet<string>(StringComparer.Ordinal);
        using(var rows=read.ExecuteReader()) while(rows.Read()) names.Add(MetadataPresetCatalog.NormalizeName(rows.GetString(0)));
        var added=0; var skipped=0;
        foreach(var preset in candidates)
        {
            if(!names.Add(MetadataPresetCatalog.NormalizeName(preset.Name))) { skipped++; continue; }
            using var command=connection.CreateCommand(); command.Transaction=transaction;
            command.CommandText=kind==MetadataKind.Tag
                ? "INSERT INTO Tags(Name,CreatedAt) VALUES($name,$date)"
                : "INSERT INTO Flags(Name,Icon,Color,Description,IsSystem) VALUES($name,$icon,'#9DBBD1',$description,0)";
            command.Parameters.AddWithValue("$name",preset.Name);
            if(kind==MetadataKind.Tag) command.Parameters.AddWithValue("$date",DateTime.Now.ToString("O"));
            else { command.Parameters.AddWithValue("$icon","metadata:"+preset.IconId); command.Parameters.AddWithValue("$description",preset.Description); }
            command.ExecuteNonQuery(); added++;
        }
        transaction.Commit();
        return new MetadataPresetResult(added,skipped);
    }
}
