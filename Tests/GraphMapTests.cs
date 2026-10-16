using System.Diagnostics;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Data.Sqlite;
using SkyrimRpEncyclopedia.Models;
using SkyrimRpEncyclopedia.Services;
using SkyrimRpEncyclopedia.Views;

static class GraphMapTests
{
    public static void Run()
    {
        var checks=0;
        void Check(bool value,string message) { checks++; if(!value) throw new InvalidOperationException("Full map: "+message); }
        var folder=Path.Combine(Path.GetTempPath(),"skyrim-atlas83-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder);
        var path=Path.Combine(folder,"knowledge.db");
        try
        {
            using(var c=new SqliteConnection("Data Source="+path))
            {
                c.Open(); using var cmd=c.CreateCommand(); cmd.CommandText="""
                    CREATE TABLE Entities(Id INTEGER PRIMARY KEY,TitleRu TEXT,PrimaryCategoryRu TEXT,TypeRu TEXT,SummaryRu TEXT,ContentRu TEXT,SemanticType TEXT,NeedsReview INTEGER,CreatedAt TEXT,Url TEXT);
                    CREATE TABLE Relations(Id INTEGER PRIMARY KEY,SourceEntityId INTEGER,TargetEntityId INTEGER,RelationType TEXT,Evidence TEXT);
                    CREATE TABLE Categories(Id INTEGER PRIMARY KEY,TitleRu TEXT); CREATE TABLE EntityCategories(EntityId INTEGER,CategoryId INTEGER);
                    CREATE TABLE Sources(Id INTEGER PRIMARY KEY,EntityId INTEGER,SourceUrl TEXT);
                    WITH RECURSIVE n(x) AS (VALUES(1) UNION ALL SELECT x+1 FROM n WHERE x<701)
                    INSERT INTO Entities SELECT x,'Персонаж '||x,'Персонажи','NPC','','SECRET FULL TEXT','NPC',0,'2026-01-01','https://example.test/'||x FROM n;
                    WITH RECURSIVE n(x) AS (VALUES(1) UNION ALL SELECT x+1 FROM n WHERE x<2200)
                    INSERT INTO Relations SELECT x,(x%701)+1,((x+1)%701)+1,'Related','Evidence' FROM n;
                    """; cmd.ExecuteNonQuery();
            }
            SqliteConnection.ClearAllPools(); var hash=Hash(path);
            var reader=new GraphMapReader(path); var reports=new List<GraphMapProgress>();
            var snapshot=reader.Read(default,new ImmediateProgress(p=>reports.Add(p)));
            Check(snapshot.Nodes.Count==701 && snapshot.Edges.Count==2200,"Full load retained explorer caps or skipped a batch.");
            Check(reports.Count>=4 && reports[^1].Nodes==701 && reports[^1].Edges==2200,"Progress missing batches/counts.");
            Check(snapshot.Nodes.Keys.Distinct().Count()==701 && snapshot.Edges.Select(e=>e.Key).Distinct().Count()==2200,"Duplicate keys.");
            Check(snapshot.Edges.All(e=>snapshot.Nodes.ContainsKey(e.Source)&&snapshot.Nodes.ContainsKey(e.Target)),"Unknown endpoints.");
            Check(snapshot.Edges[0].Source.Id==2 && snapshot.Edges[0].Target.Id==3 && snapshot.Edges.All(e=>e.Type=="Related"),"Direction/type changed.");
            Check(!typeof(GraphMapNode).GetProperties().Any(p=>p.Name.Contains("Content")||p.PropertyType==typeof(Article)),"Full node retains content/article.");
            var selected=reader.ReadArticle(new(ArticleCatalogSource.Knowledge,1),default);
            Check(selected.IsMvp && selected.Id==1000000001 && !selected.IsContentLoaded,"Article selection wrong source or eager text.");
            var drawing=GraphMapLayout.Prepare(snapshot,default);
            Check(drawing.Positions.Count==701 && drawing.EdgeImage.IsFrozen && drawing.NodeGeometry.IsFrozen,"Drawing not frozen/complete.");
            Check(drawing.Positions.SequenceEqual(GraphMapLayout.Prepare(snapshot,default).Positions),"Placement not stable.");
            using(var cancel=new CancellationTokenSource())
            {
                var cancelled=false;
                try { reader.Read(cancel.Token,new ImmediateProgress(_=>cancel.Cancel())); } catch(OperationCanceledException) { cancelled=true; }
                Check(cancelled,"Mid-batch cancellation ignored.");
            }
            var absent=path+".absent"; var rejected=false;
            try { new GraphMapReader(absent).Read(default); } catch(FileNotFoundException) { rejected=true; }
            Check(rejected&&!File.Exists(absent),"Missing source created file.");
            CameraTests(Check); UiTests(reader,drawing,Check);
            Check(Hash(path)==hash,"Read changed temporary fixture.");
            using(var c=new SqliteConnection("Data Source="+path))
            {
                c.Open(); using var cmd=c.CreateCommand();
                cmd.CommandText="INSERT INTO Categories VALUES(1,'Князья даэдра'); INSERT INTO EntityCategories VALUES(1,1)"; cmd.ExecuteNonQuery();
            }
            Check(reader.Read(default).Nodes[new(ArticleCatalogSource.Knowledge,1)].Type==ArticleGraphType.Religion,"Full map differs from shared additional-category classifier.");
            using(var c=new SqliteConnection("Data Source="+path))
            { c.Open(); using var cmd=c.CreateCommand(); cmd.CommandText="INSERT INTO Relations VALUES(9999,1,99999,'Related','')"; cmd.ExecuteNonQuery(); }
            rejected=false; try { reader.Read(default); } catch(InvalidDataException) { rejected=true; }
            Check(rejected,"Orphan accepted as complete map.");
        }
        finally { SqliteConnection.ClearAllPools(); Directory.Delete(folder,true); }
        Console.WriteLine($"Graph full map/camera OK: {checks} checks (unshown WPF control, no live visual acceptance).");
    }
    private static void CameraTests(Action<bool,string> check)
    {
        var camera=new GraphCamera(); var anchor=new Point(137,219); var world=camera.ToWorld(anchor);
        camera.Zoom(2,anchor); check((camera.ToWorld(anchor)-world).Length<1e-8,"Cursor anchor moved.");
        camera.Zoom(.001,anchor); check(camera.Scale==.1,"Minimum scale."); camera.Zoom(50,anchor); check(camera.Scale==4,"Maximum scale.");
        camera.Zoom(double.NaN,anchor); camera.Zoom(double.PositiveInfinity,anchor); camera.Zoom(0,anchor); check(camera.Scale==4,"Nonfinite scale accepted.");
        var before=camera.ToWorld(new(400,300)); camera.Resize(new(800,600),new(1000,700)); check((before-camera.ToWorld(new(500,350))).Length<1e-8,"Resize loses center.");
        camera.Pan(new(10,-20)); check(double.IsFinite(camera.Offset.X),"Pan invalid.");
        camera.Fit(new(0,0,1000,700),new(800,600)); var a=camera.Matrix.Transform(new Point(0,0)); var b=camera.Matrix.Transform(new Point(1000,700));
        check(a.X>=19&&a.Y>=19&&b.X<=781&&b.Y<=581,"Fit bounds outside viewport.");
        camera.Fit(Rect.Empty,new(800,600)); check(double.IsFinite(camera.Scale),"Empty fit invalid.");
    }
    private static void UiTests(IGraphMapReader reader,GraphMapDrawing drawing,Action<bool,string> check)
    {
        Sta(()=>
        {
            var view=new GraphView(new EmptyExplorer(),reader); Layout(view);
            var key=new GraphEntityKey(ArticleCatalogSource.User,7);
            view.Explorer.Reset(new(key,new Article{Id=7,Title="Личный узел"}));
            var positions=view.Explorer.Positions.ToDictionary(p=>p.Key,p=>p.Value);
            Finish(view.SetFullModeAsync(true)); Layout(view);
            check(view.FullDrawing?.Snapshot.Edges.Count==2200,"UI did not publish complete map.");
            check(view.Camera.Scale<1,"First full map was not fitted after a collapsed viewport.");
            check(!((CheckBox)view.FindName("ShowOfficialCheckBox")).IsEnabled,"Full mode implies unsupported source merging.");
            check(((Grid)view.FindName("GraphViewport")).Children.Count==2,"Thousands of full-map controls.");
            var layer=((Grid)view.FindName("GraphViewport")).Children.OfType<GraphMapLayer>().Single();
            var first=drawing.Positions.First(); check(layer.Hit(view.Camera.Matrix.Transform(first.Value))==first.Key,"Spatial hit at fitted scale wrong.");
            foreach(var zoom in new[]{.1,1d,4d})
            { view.ZoomAt(zoom,new(200,150)); check(layer.Hit(view.Camera.Matrix.Transform(first.Value))==first.Key,"Spatial hit changes with scale."); }
            Finish(view.SearchAsync("ПЕРСОНАЖ 12")); check(((ListBox)view.FindName("SearchResults")).Items.Count>0,"Cyrillic full-map search failed.");
            Finish(view.SelectMapNodeAsync(first.Key)); Article? opened=null; view.ArticleSelected+=a=>opened=a;
            Click(view,"OpenArticleButton"); check(opened is {IsMvp:true}&&GraphEntityKey.FromArticle(opened)==first.Key,"Navigation wrong ID/source.");
            Click(view,"ZoomResetButton"); check(view.Camera.Scale==1,"100% button failed.");
            Click(view,"ZoomInButton"); check(Math.Abs(view.Camera.Scale-1.2)<1e-8,"Plus button failed.");
            Click(view,"ZoomOutButton"); check(Math.Abs(view.Camera.Scale-1)<1e-8,"Minus button failed.");
            Click(view,"FitButton"); check(view.Camera.Scale>=.1&&view.Camera.Scale<=4,"Fit button failed.");
            var selectedKey=layer.Selected; view.Camera.Pan(new(33,-21)); view.ApplyCamera();
            check(layer.Selected==selectedKey,"Pan loses selection.");
            var center=view.Camera.ToWorld(new(viewportWidth(view)/2,viewportHeight(view)/2));
            view.Measure(new Size(1100,760)); view.Arrange(new Rect(0,0,1100,760)); view.UpdateLayout();
            check((view.Camera.ToWorld(new(viewportWidth(view)/2,viewportHeight(view)/2))-center).Length<1e-6,"Actual viewport resize changes world center.");
            var controls=((Grid)view.FindName("GraphViewport")).Children.Count;
            for(var i=0;i<3;i++) { view.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent)); view.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent)); }
            var viewport=(Grid)view.FindName("GraphViewport"); var scale=view.Camera.Scale;
            viewport.RaiseEvent(new MouseWheelEventArgs(Mouse.PrimaryDevice,0,120){RoutedEvent=UIElement.PreviewMouseWheelEvent});
            check(Math.Abs(view.Camera.Scale-Math.Min(4,scale*1.2))<1e-8,"Wheel duplicated/missing after reentry.");
            check(viewport.Children.Count==controls,"Reentry duplicates visuals.");
            ((CheckBox)view.FindName("ShowCharactersCheckBox")).IsChecked=false; Finish(view.ApplyFullFiltersAsync());
            check(layer.VisibleKeys.Count==0,"Full map filter ignored.");
            ((CheckBox)view.FindName("ShowCharactersCheckBox")).IsChecked=true; Finish(view.ApplyFullFiltersAsync());
            check(layer.VisibleKeys.Count==701&&positions.All(p=>view.Explorer.Positions[p.Key]==p.Value),"Filters lose positions/data.");
            var map=view.FullDrawing; Finish(view.SetFullModeAsync(false));
            check(view.Explorer.Nodes.Count==1&&view.Explorer.Nodes.ContainsKey(key),"Exploration lost on mode switch.");
            check(((CheckBox)view.FindName("ShowOfficialCheckBox")).IsEnabled,"Exploration source filter not restored.");
            Finish(view.SetFullModeAsync(true)); check(ReferenceEquals(map,view.FullDrawing),"Reenter rereads/copies map.");

            var delayed=new DelayedMap(reader); var lateView=new GraphView(null,delayed); Layout(lateView);
            var pending=lateView.SetFullModeAsync(true); if(!delayed.Started.Wait(TimeSpan.FromSeconds(3))) throw new TimeoutException();
            lateView.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent)); delayed.Release.Set(); Finish(pending);
            check(lateView.FullDrawing is null,"Unloaded view applied stale full map.");
            delayed=new DelayedMap(reader); lateView=new GraphView(null,delayed); Layout(lateView);
            pending=lateView.SetFullModeAsync(true); if(!delayed.Started.Wait(TimeSpan.FromSeconds(3))) throw new TimeoutException();
            Click(lateView,"CancelButton"); delayed.Release.Set(); Finish(pending);
            check(lateView.FullDrawing is null,"Cancelled map published partial state.");
            delayed=new DelayedMap(reader); lateView=new GraphView(null,delayed); Layout(lateView);
            pending=lateView.SetFullModeAsync(true); if(!delayed.Started.Wait(TimeSpan.FromSeconds(3))) throw new TimeoutException();
            Finish(lateView.SetFullModeAsync(false)); delayed.Release.Set(); Finish(pending);
            check(lateView.FullDrawing is null,"Old map replaced exploration after switch.");
        });
        static double viewportWidth(GraphView v)=>((Grid)v.FindName("GraphViewport")).ActualWidth;
        static double viewportHeight(GraphView v)=>((Grid)v.FindName("GraphViewport")).ActualHeight;
    }
    public static void MeasureReal(string path)
    {
        var hash=Hash(path); var reader=new GraphMapReader(path); var allocated=GC.GetTotalAllocatedBytes(true); var memory=GC.GetTotalMemory(true);
        using var process=Process.GetCurrentProcess(); process.Refresh(); var privateBefore=process.PrivateMemorySize64;
        var watch=Stopwatch.StartNew(); var snapshot=reader.Read(default); var readMs=watch.Elapsed.TotalMilliseconds;
        watch.Restart(); var drawing=GraphMapLayout.Prepare(snapshot,default); var layoutMs=watch.Elapsed.TotalMilliseconds;
        using(var c=MvpKnowledgeDatabaseService.OpenReadOnlyConnection(path))
        { c.Open(); using var cmd=c.CreateCommand(); cmd.CommandText="SELECT (SELECT COUNT(*) FROM Entities),(SELECT COUNT(*) FROM Relations)";
          using var r=cmd.ExecuteReader(); r.Read(); if(r.GetInt64(0)!=snapshot.Nodes.Count||r.GetInt64(1)!=snapshot.Edges.Count) throw new InvalidOperationException("Real COUNT mismatch."); }
        var retained=GC.GetTotalMemory(true)-memory; var totalAllocated=GC.GetTotalAllocatedBytes(true)-allocated;
        Console.WriteLine($"Full map real: {snapshot.Nodes.Count} nodes, {snapshot.Edges.Count} edges; read={readMs:F1}ms, layout+geometry={layoutMs:F1}ms, retained managed delta={retained/1048576d:F2}MiB, allocated delta={totalAllocated/1048576d:F2}MiB.");
        Sta(()=>
        {
            var layer=new GraphMapLayer{Drawing=drawing,VisibleKeys=snapshot.Nodes.Keys.ToHashSet(),Edges=drawing.EdgeImage,Nodes=drawing.NodeGeometry};
            layer.Measure(new Size(900,600)); layer.Arrange(new Rect(0,0,900,600)); layer.Camera.Fit(drawing.Bounds,new(900,600));
            var target=new RenderTargetBitmap(900,600,96,96,PixelFormats.Pbgra32);
            watch.Restart(); target.Render(layer); var firstMs=watch.Elapsed.TotalMilliseconds;
            watch.Restart(); for(var i=0;i<5;i++) { layer.Camera.Zoom(layer.Camera.Scale*1.08,new(450,300)); layer.Camera.Pan(new(8,4)); layer.InvalidateVisual(); layer.UpdateLayout(); target.Render(layer); }
            Console.WriteLine($"Full map offscreen render: first={firstMs:F1}ms, five pan/zoom renders mean={watch.Elapsed.TotalMilliseconds/5:F1}ms. NOT a live window/FPS measurement.");
        });
        process.Refresh(); Console.WriteLine($"Full map process private-memory delta (includes offscreen WPF/native buffers): {(process.PrivateMemorySize64-privateBefore)/1048576d:F2}MiB; not isolated peak/live-window memory.");
        if(Hash(path)!=hash) throw new InvalidOperationException("Real DB hash changed.");
    }
    private static string Hash(string path) { using var f=File.OpenRead(path); return Convert.ToHexString(SHA256.HashData(f)); }
    private static void Click(GraphView v,string name)=>((Button)v.FindName(name)).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    private static void Layout(GraphView v) { v.Measure(new Size(1280,800)); v.Arrange(new Rect(0,0,1280,800)); v.UpdateLayout(); }
    private static void Finish(Task task)
    {
        var frame=new DispatcherFrame(); var watch=Stopwatch.StartNew();
        var timer=new DispatcherTimer(TimeSpan.FromMilliseconds(5),DispatcherPriority.Background,(_,_)=>{if(task.IsCompleted||watch.ElapsedMilliseconds>15000) frame.Continue=false;},Dispatcher.CurrentDispatcher);
        timer.Start(); Dispatcher.PushFrame(frame); timer.Stop(); if(!task.IsCompleted) throw new TimeoutException("Full-map UI request"); task.GetAwaiter().GetResult();
    }
    private static void Sta(Action action)
    {
        Exception? failure=null; var thread=new Thread(()=>{try { SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext()); action(); }
            catch(Exception e){failure=e;} finally{Dispatcher.CurrentDispatcher.InvokeShutdown();}});
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); thread.Join(); if(failure is not null) throw new InvalidOperationException("Map STA",failure);
    }
    private sealed class ImmediateProgress(Action<GraphMapProgress> callback):IProgress<GraphMapProgress> { public void Report(GraphMapProgress value)=>callback(value); }
    private sealed class DelayedMap(IGraphMapReader inner):IGraphMapReader
    {
        public ManualResetEventSlim Started {get;}=new(); public ManualResetEventSlim Release {get;}=new();
        public GraphMapSnapshot Read(CancellationToken token,IProgress<GraphMapProgress>? progress=null)
        { Started.Set(); if(!Release.Wait(TimeSpan.FromSeconds(5))) throw new TimeoutException(); return inner.Read(default); }
        public Article ReadArticle(GraphEntityKey key,CancellationToken token)=>inner.ReadArticle(key,token);
    }
    private sealed class EmptyExplorer:IGraphDataReader
    {
        public GraphSearchResult Search(string q,IReadOnlyCollection<ArticleCatalogSource>s,int l,CancellationToken t)=>new([],false,[]);
        public GraphPage ReadPage(GraphEntityKey root,IReadOnlyDictionary<ArticleCatalogSource,int> cursor,int neighbors,CancellationToken token)=>new(root,[],[],cursor,false,[]);
    }
}
