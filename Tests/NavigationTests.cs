using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Xml.Linq;
using SkyrimRpEncyclopedia.Models;
using SkyrimRpEncyclopedia.Views;

static class NavigationTests
{
    public static void Run()
    {
        var checks=0;
        void Check(bool condition,string message) { checks++; if(!condition) throw new InvalidOperationException("Navigation: "+message); }
        var folder=new DirectoryInfo(AppContext.BaseDirectory);
        while(folder is not null && !File.Exists(Path.Combine(folder.FullName,"MainWindow.xaml.cs"))) folder=folder.Parent;
        if(folder is null) throw new DirectoryNotFoundException("Navigation source contract requires checkout.");
        var source=File.ReadAllText(Path.Combine(folder.FullName,"MainWindow.xaml.cs"));
        var xaml=XDocument.Load(Path.Combine(folder.FullName,"MainWindow.xaml"));
        XNamespace wpf="http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        // These are source-contract checks, not claims about a visible MainWindow.
        foreach(var handler in xaml.Descendants(wpf+"Button").Select(b=>(string?)b.Attribute("Click")).Where(h=>h is not null).Distinct())
            Check(Regex.IsMatch(source,@"\bvoid\s+"+Regex.Escape(handler!)+@"\s*\("),"Missing menu handler: "+handler);
        string Method(string name)
        {
            var m=Regex.Match(source,@"\bprivate\s+(?:async\s+)?void\s+"+name+@"\s*\(");
            if(!m.Success) throw new InvalidOperationException(name+" missing");
            var start=source.IndexOf('{',m.Index); var depth=1; var end=start+1;
            while(end<source.Length && depth>0) { if(source[end]=='{') depth++; else if(source[end]=='}') depth--; end++; }
            return source[start..end];
        }
        foreach(var route in new[]{"Events_Click","EventsLibrary_Click","Generator_Click","Notes_Click"})
            Check(Method(route).Contains("ShowMainContent();"),route+" bypasses shared container transition.");
        var transition=Method("ShowMainContent");
        Check(transition.Contains("WelcomeContainer.Visibility = Visibility.Collapsed")&&transition.Contains("Visibility.Visible")&&transition.Contains("ClearArticlesPanel();"),"Shared transition incomplete.");
        Check(Method("RenderArticle").Contains("restoreContext: true"),"Article back loses catalogue context.");
        Check(Method("ShowArticles").Contains("PageTitle.Text = _catalogTitle")&&Method("ShowArticles").Contains("RestoreScrollOffset"),"Title/scroll not restored.");
        Check(Method("SearchBox_TextChanged").Contains("_restoringCatalog"),"Restored query starts unwanted search.");
        var history=Method("RecentArticles_Click");
        Check(history.Contains("catch (Exception error)")&&history.Contains("History.Read.Failed")&&history.Contains("ShowArticles(new List<Article>())")&&history.Contains("return;"),"History read failure must be caught locally, logged and clear stale results.");
        Check(history.Contains("История временно недоступна")&&history.IndexOf("Не удалось прочитать историю",StringComparison.Ordinal)>history.IndexOf("ShowArticles(new List<Article>())",StringComparison.Ordinal),"History error must not be overwritten by the generic empty-list message.");
        Check(Method("Notes_Click").Contains("Task.Run")&&Method("Notes_Click").Contains("GetAllArticleNotes"),"Notes route must use background batch read.");
        Check(Method("EventsLibrary_Click").Contains("EventsLibraryView")&&Method("EventsLibrary_Click").Contains("EditRequested"),"Event library editing not connected.");
        var editor=File.ReadAllText(Path.Combine(folder.FullName,"ArticleEditorWindow.xaml.cs"));
        Check(editor.Contains("catch(Exception error) { UserDataErrors.Report(\"Article.Save.Failed\",error,this);return; }")&&editor.IndexOf("Article.Save.Failed",StringComparison.Ordinal)<editor.IndexOf("DialogResult = true",StringComparison.Ordinal),"Failed article save closes editor.");
        var note=File.ReadAllText(Path.Combine(folder.FullName,"ArticleNotesWindow.xaml.cs"));
        Check(note.Contains("Note.Save.Failed\",error,this);return;")&&note.Contains("Note.Delete.Failed\",error,this);return;"),"Note errors must preserve form.");
        var builder=File.ReadAllText(Path.Combine(folder.FullName,"Views","EventBuilderView.xaml.cs"));
        Check(builder.Contains("if(_editingEvent is null) candidate.Id=_database.AddRpEvent(candidate); else _database.UpdateRpEvent(candidate);")&&builder.Contains("_editingEvent=candidate"),"Event save must retain identity for next update.");
        Check(builder.Contains("catch(Exception error) { _reportError(error);return; }")&&builder.Contains("_editingEvent=null; SaveButton.Content"),"Event error/new-draft guard missing.");
        Check(builder.Contains("_lastValues[key] = value;")&&builder.Contains("ResultBox.Text = rpEvent.GeneratedText;"),"Editing must retain saved parameters/text without forced regeneration.");

        Exception? failure=null;
        var thread=new Thread(()=>
        {
            try
            {
                var view=new ArticleCatalogView();
                var rows=Enumerable.Range(1,100).Select(i=>new Article{Id=i,Title="Статья "+i,Category="Локации"}).ToArray();
                view.ShowArticles(rows); view.Measure(new Size(650,400)); view.Arrange(new Rect(0,0,650,400)); view.UpdateLayout();
                view.RestoreScrollOffset(500); Drain(); view.UpdateLayout();
                Check(view.CaptureScrollOffset()>0,"Catalogue scroll restore failed.");
                var position=view.CaptureScrollOffset();
                view.ShowArticles([]); view.ShowArticles(rows); view.RestoreScrollOffset(position); Drain(); view.UpdateLayout();
                Check(Math.Abs(view.CaptureScrollOffset()-position)<2,"Catalogue return loses scroll.");
                view.RestoreScrollOffset(700); view.ShowArticles([]); Drain(); view.UpdateLayout();
                Check(view.CaptureScrollOffset()==0,"Stale scroll request changed new list.");
            }
            catch(Exception e){failure=e;}
            finally {Dispatcher.CurrentDispatcher.InvokeShutdown();}
        });
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); thread.Join(); if(failure is not null) throw new InvalidOperationException("Navigation control test",failure);
        Console.WriteLine($"Navigation 9.0 OK: {checks} source-contract/offscreen catalogue checks; no visible MainWindow.");
    }
    private static void Drain()
    {
        var frame=new DispatcherFrame(); Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background,new Action(()=>frame.Continue=false)); Dispatcher.PushFrame(frame);
    }
}
