using Microsoft.Data.Sqlite;
using SkyrimRpEncyclopedia.Models;
using SkyrimRpEncyclopedia.Services;

static class FiltersHistoryTests
{
    public static void Run()
    {
        var checks=0; void Check(bool v,string m){checks++;if(!v)throw new InvalidOperationException("Filters/history: "+m);}
        var user=new Article{Id=1,Category="Места",Tags="Маг, Норд",Flags="Важное; Проверено"};
        user.DeferContent(()=>throw new InvalidOperationException("Filter must not read text"));
        var knowledge=new Article{Id=1000000001,IsOfficial=true,IsMvp=true,Category="Места",AdditionalCategories=["Север"],Tags="Магия",Flags="Требует проверки"};
        var official=new Article{Id=1,IsOfficial=true,Category="Места",Tags="Маг"};
        ArticleFilterValue Tag(string s)=>new(ArticleCatalogSource.User,s);
        Check(ArticleFilterRules.Matches(user,new(null,null,[Tag("Маг"),Tag("НОРД")],[])),"AND/case failed");
        Check(!ArticleFilterRules.Matches(user,new(null,null,[Tag("Маг"),Tag("Нет")],[])),"Any substituted for all");
        Check(!ArticleFilterRules.Matches(user,new(null,null,[Tag("Ма")],[])),"Substring matched");
        Check(!ArticleFilterRules.Matches(knowledge,new(null,null,[Tag("Маг")],[])),"Source/title merged");
        Check(ArticleFilterRules.Options(new[]{user,knowledge,official},ArticleFilterRules.Categories).Count==4,"Source categories merged");
        Check(ArticleFilterRules.Matches(knowledge,new(ArticleCatalogSource.Knowledge,new(ArticleCatalogSource.Knowledge,"Север"),[],[])),"Additional category not available");
        Check(!ArticleFilterRules.Matches(user,new(ArticleCatalogSource.Knowledge,null,[],[])),"Source group ignored");
        Check(ArticleFilterRules.Matches(user,new(null,null,[Tag("Маг")],[Tag("Важное"),Tag("Проверено")])),"Independent groups not AND");
        Check(!ArticleFilterRules.Matches(user,new(null,null,[Tag("Маг")],[Tag("Важное"),Tag("Нет")])),"Flags not AND");
        Check(!user.IsContentLoaded,"Metadata filter loaded text");
        var folder=Path.Combine(Path.GetTempPath(),"skyrim-filters91-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder);
        try
        {
            var path=Path.Combine(folder,"user.db"); var db=new DatabaseService(path);
            Check(db.GetRecentArticles().Count==0,"History not empty");
            var time=new DateTime(2026,1,1,12,0,0,DateTimeKind.Utc);
            db.RecordArticleView(1,false,time); db.RecordArticleView(1,true,time.AddSeconds(1)); db.RecordArticleView(1000000001,true,time.AddSeconds(2));
            var rows=db.GetRecentArticles(); Check(rows.Count==3,"Same IDs from sources merged");
            Check(rows[0].ArticleId==1000000001&&rows[1].ArticleIsOfficial,"History ordering/source failed");
            db.RecordArticleView(1,false,time.AddSeconds(3)); rows=db.GetRecentArticles();
            Check(rows.Count==3&&rows[0].ArticleId==1&&!rows[0].ArticleIsOfficial,"Repeat duplicated/not reordered");
            using(var c=new SqliteConnection("Data Source="+path))
            {c.Open();using var cmd=c.CreateCommand();cmd.CommandText="CREATE TRIGGER fail_history BEFORE INSERT ON ViewingHistory BEGIN SELECT RAISE(ABORT,'history denied'); END";cmd.ExecuteNonQuery();}
            var denied=false;try{db.RecordArticleView(2,false,time);}catch(SqliteException){denied=true;}
            Check(denied&&db.GetRecentArticles().Count==3,"History failed write changed records");
        }
        finally{SqliteConnection.ClearAllPools();Directory.Delete(folder,true);}
        Console.WriteLine($"Filters/history 9.1 OK: {checks} checks; source-qualified values, exact AND matching, history and failed-write preservation.");
    }
}
