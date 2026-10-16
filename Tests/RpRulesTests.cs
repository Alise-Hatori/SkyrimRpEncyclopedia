using System.Security.Cryptography;
using System.Text.Json;
using SkyrimRpEncyclopedia.Models;
using SkyrimRpEncyclopedia.Services;

static class RpRulesTests
{
    public static void Run(string package)
    {
        var checks = 0;
        void Check(bool value, string message) { checks++; if (!value) throw new InvalidOperationException("RP package: " + message); }
        var before = SHA256.HashData(File.ReadAllBytes(package));
        var temp = Path.Combine(Path.GetTempPath(), "skyrim-rules-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        try
        {
            var user = new DatabaseService(Path.Combine(temp,"user.db"));
            var official = new OfficialDatabaseService(Path.Combine(temp,"official.db"));
            var reader = new ArticleCatalogReader(user.DatabasePath,official.DatabasePath,null,package);
            var rules = reader.ReadSourceMetadata(ArticleCatalogSource.Rules);
            Check(rules.Count==33 && rules.All(a=>a.IsRpRule && a.IsOfficial && !a.IsMvp),"Rules must be a separate readonly source.");
            Check(rules.All(a=>a.Id<0) && rules.Select(a=>a.Id).Distinct().Count()==33,"ID namespace collision.");
            Check(rules.All(a=>!a.IsContentLoaded),"Full texts loaded eagerly.");
            Check(rules.All(a=>!string.IsNullOrWhiteSpace(a.Content)),"An empty text was exported.");
            Check(rules.Sum(a=>a.Content.Length)==97136,"Text length changed.");
            VerifyView(rules); checks++;
            Check(Enumerable.Range(0,5).SelectMany(i=>reader.ReadPage(ArticleCatalogSource.Rules,i*7,7)).Count()==33,"Pagination lost rules.");
            Check(reader.SearchTitles(ArticleCatalogSource.Rules,rules[0].Title,10).Any(a=>a.Id==rules[0].Id),"Cyrillic title search failed.");
            Check(reader.ReadByIds(ArticleCatalogSource.Rules,[4]).Single().Id==-4,"Original ID translation failed.");
            Check(reader.ReadMetadata().Count(a=>a.Category=="Правила RP")==33,"Rules missing from merged catalog.");
            var link = reader.ReadRuleLinks(-5).Single();
            Check(link.SourceArticleId==-5 && link.TargetArticleId==-4 && link.SourceIsOfficial && link.TargetIsOfficial,"Internal relation was not preserved.");
            Check(reader.ReadRuleLinks(-8).Count==0,"Excluded external relation leaked.");
            var original = rules.Single(a=>a.Id==-4);
            var editable = new Article {Title=original.Title,Category=original.Category,Summary=original.Summary,Content=original.Content,Tags=original.Tags,Flags=original.Flags};
            editable.Id=user.AddArticle(editable);
            var merged=reader.ReadMetadata();
            Check(merged.Count(a=>a.Category=="Правила RP")==33 && !merged.Any(a=>a.IsRpRule&&a.Id==-4),"Identical personal copy was duplicated or became uneditable.");
            link=reader.ReadRuleLinks(-5).Single();
            Check(link.TargetArticleId==editable.Id && !link.TargetIsOfficial,"Relation to an identical editable copy broke.");
            editable.Content+="\nЛичное изменение для теста.";
            user.UpdateArticle(editable);
            Check(reader.ReadMetadata().Count(a=>a.Category=="Правила RP")==34,"A distinct personal edit was silently hidden.");
            Check(ArticleFilterRules.SourceOf(original)==ArticleCatalogSource.Rules && ArticleSectionMatcher.Matches("Правила RP",original),"Rules section/filter identity failed.");
            using(var c=MvpKnowledgeDatabaseService.OpenReadOnlyConnection(package))
            {
                c.Open(); using var cmd=c.CreateCommand(); cmd.CommandText="DELETE FROM Articles";
                try {cmd.ExecuteNonQuery();throw new InvalidOperationException("Readonly package accepted writes.");}
                catch(Microsoft.Data.Sqlite.SqliteException e) when(e.SqliteErrorCode==8){checks++;}
            }
            Check(before.SequenceEqual(SHA256.HashData(File.ReadAllBytes(package))),"Package hash changed.");
            Check(RpRulesCatalog.ResolvePath(temp)==Path.Combine(temp,"Data","rp_rules.db"),"Application-relative discovery failed.");
            try { UserDatabasePreflight.ValidateExisting(package); throw new InvalidOperationException("RP package accepted as a writable personal database."); }
            catch(InvalidDataException) { checks++; }
        }
        finally { Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools(); Directory.Delete(temp,true); }
        Console.WriteLine($"RP rules OK: {checks} package/identity/search/pagination/relation/editable-copy/read-only checks.");
    }

    public static void VerifyInstalled(string applicationDirectory)
    {
        // No checkout discovery or user database initialization; used from a copied test host.
        var knowledge=MvpKnowledgeDatabaseService.ResolveDatabasePath(applicationDirectory:applicationDirectory);
        var rulesPath=RpRulesCatalog.ResolvePath(applicationDirectory);
        var reader=new ArticleCatalogReader("", "", knowledge,rulesPath);
        var entities=reader.ReadSourceMetadata(ArticleCatalogSource.Knowledge);
        var rules=reader.ReadSourceMetadata(ArticleCatalogSource.Rules);
        if(entities.Count!=5579 || rules.Count!=33 || rules.Any(a=>string.IsNullOrWhiteSpace(a.Content)))
            throw new InvalidDataException("Installed data counts or texts are invalid.");
        if(reader.ReadRuleLinks(-5).Single().TargetArticleId!=-4 || reader.ReadRuleLinks(-8).Count!=0)
            throw new InvalidDataException("Installed relationships differ from approved export.");
        var found=reader.SearchTitles(ArticleCatalogSource.Rules,rules[0].Title,10);
        if(!found.Any(a=>a.Id==rules[0].Id))throw new InvalidDataException("Installed rule search failed.");
        VerifyView(rules);
        Console.WriteLine(JsonSerializer.Serialize(new {KnowledgeEntities=entities.Count,RpArticles=rules.Count,
            RulesCharacters=rules.Sum(a=>a.Content.Length),RulesSection=rules.Count(a=>ArticleSectionMatcher.Matches("Правила RP",a)),
            KnowledgePath=knowledge,RulesPath=rulesPath,Search=true,InternalLink=true,ExternalLinkExcluded=true,
            ReadOnly=true,CheckoutRequired=false,OffscreenRulesViewChecked=true,VisibleWindowChecked=false}));
    }

    private static void VerifyView(IReadOnlyList<Article> rules)
    {
        Exception? failure=null;
        var thread=new Thread(()=>
        {
            try
            {
                var view=new SkyrimRpEncyclopedia.Views.ArticleCatalogView();
                view.ShowArticles(rules);
                view.Measure(new System.Windows.Size(800,600));
                view.Arrange(new System.Windows.Rect(0,0,800,600)); view.UpdateLayout();
                var list=(System.Windows.Controls.ListBox)view.FindName("CatalogList");
                IEnumerable<System.Windows.DependencyObject> Descendants(System.Windows.DependencyObject parent)
                {
                    yield return parent;
                    for(var i=0;i<System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent);i++)
                        foreach(var child in Descendants(System.Windows.Media.VisualTreeHelper.GetChild(parent,i)))yield return child;
                }
                if(list.Items.Count!=33 || !Descendants(view).OfType<System.Windows.Controls.TextBlock>().Any(t=>t.Text=="RP-ПРАВИЛА · НЕ КАНОН WIKI"))
                    throw new InvalidOperationException("Compiled rules catalog did not display 33 items and a non-canon source badge.");
            }
            catch(Exception error){failure=error;}
            finally {System.Windows.Threading.Dispatcher.CurrentDispatcher.InvokeShutdown();}
        });
        thread.SetApartmentState(ApartmentState.STA); thread.Start();
        if(!thread.Join(20000))throw new TimeoutException("Offscreen rules view test timed out.");
        if(failure is not null)throw new InvalidOperationException("Rules view failed.",failure);
    }
}
