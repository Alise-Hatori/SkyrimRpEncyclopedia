using SkyrimRpEncyclopedia.Services;

static class ReleaseConfigurationTests
{
    public static void Run()
    {
        var checks=0;
        void Check(bool condition,string message){checks++;if(!condition)throw new InvalidOperationException("Release configuration: "+message);}
        var old=Environment.GetEnvironmentVariable(MvpKnowledgeDatabaseService.DatabasePathEnvironmentVariable);
        var folder=Path.Combine(Path.GetTempPath(),"skyrim-release93-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            Environment.SetEnvironmentVariable(MvpKnowledgeDatabaseService.DatabasePathEnvironmentVariable,null);
            Check(MvpKnowledgeDatabaseService.ResolveDatabasePath(applicationDirectory:folder) is null,"Missing database must not be created or replaced.");
            Directory.CreateDirectory(Path.Combine(folder,"Data"));
            var local=Path.Combine(folder,"Data","knowledge.db");
            File.WriteAllText(local,"Temporary path fixture, not a corpus");
            Check(MvpKnowledgeDatabaseService.ResolveDatabasePath(applicationDirectory:folder)==local,"Stable application-relative path not selected.");
            var configured=Path.Combine(folder,"configured.db");
            Environment.SetEnvironmentVariable(MvpKnowledgeDatabaseService.DatabasePathEnvironmentVariable,configured);
            Check(MvpKnowledgeDatabaseService.ResolveDatabasePath(applicationDirectory:folder)==configured,"Missing configured file must not silently select another corpus.");
            var explicitPath=Path.Combine(folder,"explicit.db");
            Check(MvpKnowledgeDatabaseService.ResolveDatabasePath(explicitPath,folder)==explicitPath,"Explicit path must override environment.");
            using var connection=MvpKnowledgeDatabaseService.OpenReadOnlyConnection(local);
            Check(new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder(connection.ConnectionString).Mode==Microsoft.Data.Sqlite.SqliteOpenMode.ReadOnly,"Knowledge connection is not readonly.");
            var root=new DirectoryInfo(AppContext.BaseDirectory);
            while(root is not null&&!File.Exists(Path.Combine(root.FullName,"MainWindow.xaml.cs")))root=root.Parent;
            if(root is null)throw new DirectoryNotFoundException("Release source contract requires checkout.");
            Check(File.ReadAllText(Path.Combine(root.FullName,"Views","ScenarioGeneratorView.xaml.cs")).Contains("DifficultyBox.SelectedItem = \"Средняя\""),"Difficulty default must match selectable string.");
        }
        finally
        {
            Environment.SetEnvironmentVariable(MvpKnowledgeDatabaseService.DatabasePathEnvironmentVariable,old);
            Directory.Delete(folder,true);
        }
        Console.WriteLine($"Release configuration 9.3 OK: {checks} path precedence, no replacement, readonly connection and initial difficulty checks.");
    }
}
