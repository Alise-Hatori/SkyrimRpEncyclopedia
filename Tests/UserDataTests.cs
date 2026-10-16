using System.Security.Cryptography;
using Microsoft.Data.Sqlite;
using SkyrimRpEncyclopedia.Models;
using SkyrimRpEncyclopedia.Services;

static class UserDataTests
{
    public static void Run()
    {
        var checks=0; void Check(bool v,string m){checks++;if(!v)throw new InvalidOperationException("User data: "+m);}
        var directory=Path.Combine(Path.GetTempPath(),"skyrim-data92-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
        var path=Path.Combine(directory,"user.db");
        void Execute(string file,string sql){using var c=new SqliteConnection("Data Source="+file);c.Open();using var cmd=c.CreateCommand();cmd.CommandText=sql;cmd.ExecuteNonQuery();}
        bool Denied(Action operation){try{operation();return false;}catch(Exception e)when(e is SqliteException or IOException or InvalidDataException or InvalidOperationException or ArgumentException){return true;}}
        string Hash(string file){using var stream=new FileStream(file,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete);return Convert.ToHexString(SHA256.HashData(stream));}
        try
        {
            var db=new DatabaseService(path);
            var article=new Article{Title="Личный текст",Content="Сохранённый текст",Category="Места",Tags="Маг, Норд",Flags="Проверено",CreatedAt=DateTime.Now,UpdatedAt=DateTime.Now};
            article.Id=db.AddArticle(article);
            Check(db.GetArticles().Count==1&&db.GetArticles()[0].Content==article.Content,"Create/read failed");
            Check(db.GetTags().Any(t=>t.Name=="Маг")&&db.GetCategories().Any(c=>c.Name=="Места")&&db.GetFlags().Any(f=>f.Name=="Проверено"),"Metadata not synchronized");
            article.Content="Обновлённый текст";db.UpdateArticle(article);Check(db.GetArticles()[0].Content==article.Content,"Update failed");
            var other=new Article{Title="Другой текст",Content="Не менять",Category="Места",Tags="Магия",CreatedAt=DateTime.Now,UpdatedAt=DateTime.Now};other.Id=db.AddArticle(other);
            var tag=db.GetTags().Single(t=>t.Name=="Маг");db.RenameTag(tag.Id,"Чародей");
            Check(db.GetArticles().Single(a=>a.Id==article.Id).Tags=="Чародей, Норд","Rename not propagated");
            Check(db.GetArticles().Single(a=>a.Id==other.Id).Tags=="Магия","Substring/unrelated article changed");
            db.RenameTag(tag.Id,"Чародей");Check(db.GetTags().Count(t=>t.Name=="Чародей")==1,"Repeat rename duplicated");
            Check(Denied(()=>db.RenameTag(-1,"Нет")),"Missing tag silently accepted");
            Execute(path,"CREATE TRIGGER reject_article_change BEFORE UPDATE ON Articles BEGIN SELECT RAISE(ABORT,'test denied'); END");
            Check(Denied(()=>db.RenameTag(tag.Id,"Колдун")),"Injected rename failure ignored");
            Check(db.GetTags().Any(t=>t.Id==tag.Id&&t.Name=="Чародей")&&db.GetArticles().Single(a=>a.Id==article.Id).Tags=="Чародей, Норд","Rename failed to roll back all tables");
            Execute(path,"DROP TRIGGER reject_article_change; CREATE TRIGGER reject_tag_link BEFORE INSERT ON ArticleTags BEGIN SELECT RAISE(ABORT,'test metadata denied'); END");
            var bad=new Article{Title="Не сохранять",Content="Введённый текст",Category="Новая категория",Tags="Новый тег",CreatedAt=DateTime.Now,UpdatedAt=DateTime.Now};
            Check(Denied(()=>db.AddArticle(bad))&&db.GetArticles().Count==2,"Partial article/metadata commit");
            Check(!db.GetTags().Any(t=>t.Name=="Новый тег")&&!db.GetCategories().Any(c=>c.Name=="Новая категория"),"Failed insert left dictionaries");
            var prior=db.GetArticles().Single(a=>a.Id==article.Id);prior.Content="Новый ввод";
            Check(Denied(()=>db.UpdateArticle(prior))&&db.GetArticles().Single(a=>a.Id==article.Id).Content=="Обновлённый текст","Failed update damaged old text");
            Check(prior.Content=="Новый ввод","Failed operation destroyed input model");
            Execute(path,"DROP TRIGGER reject_tag_link");
            Check(Denied(()=>db.AddArticle(new Article{IsMvp=true}))&&Denied(()=>db.UpdateArticle(new Article{Id=article.Id,IsOfficial=true})),"Readonly-source guard absent");
            var category=db.GetCategories().Single(c=>c.Name=="Места");category.Name="Север";db.UpdateCategory(category);
            Check(db.GetArticles().All(a=>a.Category=="Север"),"Category rename not propagated");
            var flag=db.GetFlags().Single(f=>f.Name=="Проверено");flag.Name="Подтверждено";db.UpdateFlag(flag);
            Check(db.GetArticles().Single(a=>a.Id==article.Id).Flags=="Подтверждено","Flag rename not propagated");
            var note=new ArticleNote{ArticleId=article.Id,Content="Заметка",NoteType="Private"};note.Id=db.AddArticleNote(note);
            db.AddArticleNote(new ArticleNote{ArticleId=1000000001,ArticleIsOfficial=true,Content="Личная заметка к Knowledge",NoteType="Private"});
            Check(db.GetAllArticleNotes().Count==2&&db.GetAllArticleNotes().Any(n=>n.ArticleId==1000000001),"Batched/source-qualified notes lost");
            note.Content="Обновлённая заметка";db.UpdateArticleNote(note);Check(db.GetArticleNotes(article.Id,false)[0].Content==note.Content,"Note update failed");
            Execute(path,"CREATE TRIGGER reject_note BEFORE UPDATE ON ArticleNotes BEGIN SELECT RAISE(ABORT,'note denied'); END");
            note.Content="Несохранённый ввод";Check(Denied(()=>db.UpdateArticleNote(note))&&db.GetArticleNotes(article.Id,false)[0].Content=="Обновлённая заметка"&&note.Content=="Несохранённый ввод","Note failure lost persisted/input text");
            Execute(path,"DROP TRIGGER reject_note");
            var ev=new RpEvent{Title="Встреча",EventType="Собрание",GeneratedText="Текст",CreatedAt=DateTime.Now,UpdatedAt=DateTime.Now};ev.Id=db.AddRpEvent(ev);
            ev.GeneratedText="Исправленный текст";db.UpdateRpEvent(ev);db.UpdateRpEvent(ev);Check(db.GetRpEvents().Count==1&&db.GetRpEvents()[0].GeneratedText==ev.GeneratedText,"Repeated event update duplicated/lost text");
            Execute(path,"CREATE TRIGGER reject_event BEFORE UPDATE ON RpEvents BEGIN SELECT RAISE(ABORT,'event denied'); END");
            ev.GeneratedText="Новый ввод";Check(Denied(()=>db.UpdateRpEvent(ev))&&db.GetRpEvents()[0].GeneratedText=="Исправленный текст"&&ev.GeneratedText=="Новый ввод","Event write failure damaged data/input");
            Execute(path,"DROP TRIGGER reject_event");db.DeleteRpEvent(ev.Id);Check(db.GetRpEvents().Count==0,"Event deletion failed");
            db.DeleteArticle(article.Id);Check(db.GetArticles().Count==1&&db.GetArticleNotes(article.Id,false).Count==0,"Article deletion dependencies not atomic");
            var foreign=Path.Combine(directory,"knowledge.db");Execute(foreign,"CREATE TABLE Entities(Id INTEGER PRIMARY KEY); INSERT INTO Entities VALUES(1)");var before=Hash(foreign);
            Check(Denied(()=>new DatabaseService(foreign))&&Hash(foreign)==before,"Foreign DB accepted/mutated by initialization");
            var corrupt=Path.Combine(directory,"corrupt.db");File.WriteAllText(corrupt,"not a sqlite file");before=Hash(corrupt);
            Check(Denied(()=>new DatabaseService(corrupt))&&Hash(corrupt)==before,"Corrupt DB recreated/repaired");
            Check(Denied(()=>new DatabaseService(directory)),"Directory accepted as file");
            Check(Denied(()=>UserDatabasePreflight.ValidateDistinct(path,path)),"Shared writable/readonly path accepted");
        }
        finally{SqliteConnection.ClearAllPools();Directory.Delete(directory,true);}
        Console.WriteLine($"User data 9.2 OK: {checks} temporary-DB checks; CRUD, metadata transactions, injected failures, readonly guards and no repair.");
    }
}
