using System.Collections;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Microsoft.Data.Sqlite;
using SkyrimRpEncyclopedia;
using SkyrimRpEncyclopedia.Services;

static class MetadataWindowResourceTests
{
    public static void Run()
    {
        Exception? failure = null;
        var checks = 0;
        var thread = new Thread(() =>
        {
            var folder = Path.Combine(Path.GetTempPath(), "skyrim-metadata-resources-" + Guid.NewGuid().ToString("N"));
            Application? application = null;
            MetadataManagerWindow? window = null;
            try
            {
                SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
                void Check(bool condition, string message)
                {
                    checks++;
                    if (!condition) throw new InvalidOperationException("Metadata window resources: " + message);
                }
                Directory.CreateDirectory(folder);
                var db = new DatabaseService(Path.Combine(folder, "user.db"));
                string Hash()
                {
                    using var stream = new FileStream(db.DatabasePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                    return Convert.ToHexString(SHA256.HashData(stream));
                }
                var before = Hash();
                application = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
                // Exact application-level resource scope, without App startup, MainWindow or network.
                foreach (var dictionary in new[] { "DarkSkyrim/Colors.xaml", "DarkSkyrim/Icons.xaml", "DarkSkyrim/Controls.xaml", "NordicArchive/Visuals.xaml" })
                    application.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/Assets/Theme/" + dictionary, UriKind.Relative) });

                window = new MetadataManagerWindow(db); // Real compiled InitializeComponent; never Show/ShowDialog.
                Wait(window.Initialization);
                Check(!window.IsVisible, "Test must not show a window.");
                var root = new DirectoryInfo(AppContext.BaseDirectory);
                while (root is not null && !File.Exists(Path.Combine(root.FullName, "MetadataManagerWindow.xaml"))) root = root.Parent;
                if (root is null) throw new DirectoryNotFoundException("Metadata resource test requires checkout.");
                var xaml = File.ReadAllText(Path.Combine(root.FullName, "MetadataManagerWindow.xaml"));
                foreach (var key in Regex.Matches(xaml, @"\{(?:Static|Dynamic)Resource\s+([^}]+)\}").Select(m => m.Groups[1].Value).Distinct())
                    Check(window.TryFindResource(key) is not null, "Unresolved window resource: " + key);
                var danger = (Style)window.FindResource("NordDangerButton");
                var dialogDanger = (Style)window.FindResource("DialogDangerButton");
                Check(ReferenceEquals(dialogDanger.BasedOn, danger), "Dialog danger must reuse the existing danger style.");
                Check(danger.BasedOn == (Style)window.FindResource("NordActionButton"), "Existing action-button base changed.");
                var deleteButtons=0;
                var tabs=(TabControl)window.FindName("MetadataTabs");
                for(var index=0;index<3;index++)
                {
                    tabs.SelectedIndex=index;
                    var content=(FrameworkElement)window.Content;
                    content.Measure(new Size(1240,800));content.Arrange(new Rect(0,0,1240,800));content.UpdateLayout();
                    var buttons=VisualDescendants(content).OfType<Button>().Where(b=>b.Content as string=="Удалить").ToArray();
                    Check(buttons.Length==1,"Each catalog tab must retain a delete button.");deleteButtons+=buttons.Length;
                    foreach(var button in buttons)
                        Check(ReferenceEquals(button.Style,dialogDanger)&&button.MinWidth==100&&button.Height==36,"Delete style or established geometry changed.");
                }
                Check(deleteButtons==3,"All metadata tabs must retain delete buttons.");
                Check(Hash() == before, "Window construction changed temporary database contents.");
                ArticleMetadataSelectionTests.Run();
                MetadataCatalogTests.Run();
            }
            catch (Exception error) { failure = error; }
            finally
            {
                window?.Close();
                application?.Shutdown();
                SqliteConnection.ClearAllPools();
                if (Directory.Exists(folder)) Directory.Delete(folder, true);
                Dispatcher.CurrentDispatcher.InvokeShutdown();
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null) throw new InvalidOperationException("Metadata resource regression", failure);
        Console.WriteLine($"Metadata window resources OK: {checks} checks; real compiled window without showing, all referenced keys, existing danger style, three delete buttons and unchanged temporary DB.");
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        foreach (var child in LogicalTreeHelper.GetChildren(parent))
            if (child is DependencyObject dependency)
            {
                yield return dependency;
                foreach (var nested in Descendants(dependency)) yield return nested;
            }
    }

    internal static void Wait(Task task)
    {
        var deadline=DateTime.UtcNow.AddSeconds(25);
        while(!task.IsCompleted)
        {
            if(DateTime.UtcNow>deadline)throw new TimeoutException("Metadata UI operation exceeded 25 seconds.");
            var frame=new DispatcherFrame();
            Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background,new Action(()=>frame.Continue=false));
            Dispatcher.PushFrame(frame);Thread.Sleep(1);
        }
        task.GetAwaiter().GetResult();
    }
    private static IEnumerable<DependencyObject> VisualDescendants(DependencyObject parent)
    {
        for(var index=0;index<System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent);index++)
        {
            var child=System.Windows.Media.VisualTreeHelper.GetChild(parent,index);yield return child;
            foreach(var nested in VisualDescendants(child))yield return nested;
        }
    }
}
