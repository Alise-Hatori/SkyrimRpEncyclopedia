using System.Security.Cryptography;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Data.Sqlite;
using SkyrimRpEncyclopedia;
using SkyrimRpEncyclopedia.Models;
using SkyrimRpEncyclopedia.Services;

static class MetadataCatalogTests
{
    public static void Run()
    {
        var checks=0;void Check(bool ok,string text){checks++;if(!ok)throw new InvalidOperationException("Metadata catalog: "+text);}
        void Wait(Task task)=>MetadataWindowResourceTests.Wait(task);
        var folder=Path.Combine(Path.GetTempPath(),"skyrim-metadata90-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
        var windows=new List<Window>();
        string Hash(string file){using var stream=new FileStream(file,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete);return Convert.ToHexString(SHA256.HashData(stream));}
        void Execute(string file,string sql){using var c=new SqliteConnection(new SqliteConnectionStringBuilder{DataSource=file}.ToString());c.Open();using var cmd=c.CreateCommand();cmd.CommandText=sql;cmd.ExecuteNonQuery();}
        bool Denied(Action action){try{action();return false;}catch(Exception e)when(e is SqliteException or InvalidOperationException or ArgumentException){return true;}}
        try
        {
            Check(MetadataPresetCatalog.All.Count(p=>p.Kind==MetadataKind.Tag)==18&&MetadataPresetCatalog.All.Count(p=>p.Kind==MetadataKind.Flag)==8,"Catalog counts differ.");
            Check(MetadataPresetCatalog.All.Select(p=>p.Id).Distinct().Count()==26,"Preset IDs duplicate.");
            Check(MetadataPresetCatalog.All.Where(p=>p.Kind==MetadataKind.Tag).Select(p=>p.Group).Distinct().Count()==3,"Tag grouping lost.");
            var db=new DatabaseService(Path.Combine(folder,"user.db"));var service=new MetadataCatalogService(db);
            var confirm=true;var errors=new List<Exception>();var before=Hash(db.DatabasePath);
            var window=new MetadataManagerWindow(db,_=>confirm,errors.Add);windows.Add(window);Wait(window.Initialization);
            Check(window.Tabs.Count==3&&window.Tabs.All(t=>t.Count==0&&!t.IsDirty)&&errors.Count==0,"Empty window state wrong.");
            Check(Hash(db.DatabasePath)==before,"Opening catalog seeded/wrote DB.");
            var closingRead=new MetadataManagerWindow(db,_=>true,errors.Add);closingRead.Close();Wait(closingRead.Initialization);
            Check(!Application.Current.Windows.Cast<Window>().Contains(closingRead),"Read-only initialization blocked closing.");
            foreach(var id in MetadataPresetCatalog.IconIds)
            {
                var image=window.ResolveIcon(id);Check(image is DrawingImage&&image.Width>0&&image.Height>0,"Icon missing/empty: "+id);
            }
            var cats=window.Tabs[0];var tags=window.Tabs[1];var flags=window.Tabs[2];
            Check(!cats.SaveCommand.CanExecute(null)&&!cats.DeleteCommand.CanExecute(null),"Empty form actions enabled.");
            cats.Name="Тестовая локация";cats.Description="Первое описание";Wait(cats.SaveAsync());
            Check(db.GetCategories().Count==1&&cats.SelectedRow?.Name=="Тестовая локация"&&!cats.IsDirty,$"Category save/select failed: count={db.GetCategories().Count}, selected={cats.SelectedRow?.Name}, dirty={cats.IsDirty}, status={cats.Status}");
            cats.Description="Второе описание";Wait(cats.SaveAsync());
            Check(db.GetCategories().Single().Description=="Второе описание","Category update failed.");
            cats.Name="Не сохранять";cats.CancelDraft();Check(cats.Name=="Тестовая локация"&&!cats.IsDirty,"Cancel lost original snapshot.");
            tags.Name="Метка поиска";Wait(tags.SaveAsync());Check(db.GetTags().Single().Name=="Метка поиска","Tag create failed.");
            tags.Name="Переименованная метка";Wait(tags.SaveAsync());Check(db.GetTags().Count==1&&db.GetTags()[0].Name==tags.Name,"Tag update duplicated.");
            tags.SearchText="НЕСУЩЕСТВУЕТ";tags.ApplySearch();Check(tags.IsEmpty&&tags.Count==1&&tags.EmptyTitle.Contains("Совпадений"),"No-match search state wrong.");
            tags.SearchText="переИМЕНОВАННАЯ";tags.ApplySearch();Check(!tags.IsEmpty,"Cyrillic case-insensitive search failed.");
            tags.SearchText="";tags.ApplySearch();tags.NewDraft();
            var tabControl=(TabControl)window.FindName("MetadataTabs");tabControl.SelectedIndex=1;
            tags.Name="Черновик другой вкладки";confirm=false;tabControl.SelectedIndex=2;
            Check(tabControl.SelectedIndex==1&&tags.Name=="Черновик другой вкладки","Dirty tab guard lost draft.");
            Check(!tags.NewDraft()&&tags.IsDirty,"Dirty new action did not ask permission.");
            confirm=true;tabControl.SelectedIndex=2;Check(!tags.IsDirty&&db.GetTags().Count==1,"Switch saved discarded draft.");
            flags.Name="Личный статус";flags.Description="Описание статуса";flags.SelectedIcon=flags.IconOptions.Single(i=>i.Id=="lock");Wait(flags.SaveAsync());
            var savedFlag=db.GetFlags().Single();Check(savedFlag.Icon=="metadata:lock"&&savedFlag.Description=="Описание статуса","Flag icon/description not persisted.");
            Check(!savedFlag.DisplayName.Contains("metadata:"),"Resource token leaked into legacy metadata picker.");
            Check(new ContentFlag{Name="Старый",Icon="X"}.DisplayName.StartsWith("X "),"Legacy icon display changed.");
            Wait(flags.ReloadAsync(savedFlag.Id));Check(flags.SelectedIcon?.Id=="lock","Stored icon not restored.");
            var sysCategory=db.AddCategory(new ContentCategory{Name="Системная",IsSystem=true});
            var sysFlag=db.AddFlag(new ContentFlag{Name="Защищённый",Icon="старое",Color="#123456",Description="Не менять",IsSystem=true});
            Wait(cats.ReloadAsync(sysCategory));Wait(flags.ReloadAsync(sysFlag));
            Check(!cats.DeleteCommand.CanExecute(null)&&!flags.DeleteCommand.CanExecute(null),"System delete enabled.");
            var previousFlag=flags.SelectedRow;
            Check(!flags.TrySelect(cats.Rows.First())&&ReferenceEquals(flags.SelectedRow,previousFlag),"Foreign-kind row could replace flag editor state.");
            Wait(cats.DeleteAsync());Wait(flags.DeleteAsync());
            Check(db.GetCategories().Any(c=>c.Id==sysCategory)&&db.GetFlags().Any(f=>f.Id==sysFlag),"System item deleted.");
            Check(Denied(()=>service.Delete(new MetadataEntry(MetadataKind.Flag,sysFlag,"Защищённый"))),"Stale row bypassed system protection.");
            flags.Description="Изменено вручную";Wait(flags.SaveAsync());
            Check(db.GetFlags().Single(f=>f.Id==sysFlag).Icon=="старое","Editing description overwrote legacy icon.");
            Wait(cats.ReloadAsync(db.GetCategories().Single(c=>!c.IsSystem).Id));Wait(cats.DeleteAsync());
            Wait(tags.ReloadAsync());tags.TrySelect(tags.Rows.Single());Wait(tags.DeleteAsync());
            Check(db.GetCategories().Count==1&&db.GetTags().Count==0,"Allowed deletion failed.");
            flags.NewDraft();flags.Name="Ошибка записи";flags.Description="Ввод сохранится";
            Execute(db.DatabasePath,"CREATE TRIGGER fail_metadata BEFORE INSERT ON Flags BEGIN SELECT RAISE(ABORT,'metadata denied'); END");
            var count=db.GetFlags().Count;Wait(flags.SaveAsync());
            Check(flags.IsDirty&&flags.Name=="Ошибка записи"&&flags.Description=="Ввод сохранится"&&db.GetFlags().Count==count&&errors.Count>0,"Write failure lost draft/partially committed.");
            Execute(db.DatabasePath,"DROP TRIGGER fail_metadata");flags.CancelDraft();
            var postDb=new DatabaseService(Path.Combine(folder,"postcommit.db"));var failImage=true;
            using(var post=new MetadataTabState(MetadataKind.Tag,new MetadataCatalogService(postDb),_=>true,errors.Add,
                id=>failImage?throw new InvalidOperationException("render denied"):window.ResolveIcon(id),flags.IconOptions.ToArray(),_=>Task.CompletedTask))
            {
                post.Name="Сохранено без списка";Wait(post.SaveAsync());
                Check(postDb.GetTags().Count==1&&!post.IsDirty&&post.Status.Contains("Запись сохранена"),"Post-commit failure reported as failed save.");
                failImage=false;post.Name="Обновлено по сохранённому Id";Wait(post.SaveAsync());
                Check(postDb.GetTags().Count==1&&postDb.GetTags()[0].Name==post.Name,"Post-commit refresh failure caused duplicate insert.");
            }

            var bulkDb=new DatabaseService(Path.Combine(folder,"bulk.db"));var bulk=new MetadataCatalogService(bulkDb);
            bulkDb.AddTag("  локация  ");
            bulkDb.AddFlag(new ContentFlag{Name="  Канон   подтверждён ",Description="Собственное описание",Icon="OLD",Color="#112233"});
            Execute(bulkDb.DatabasePath,"UPDATE Flags SET Name='  Канон   подтверждён '"); // Legacy fixture retains surrounding spaces.
            var article=new Article{Title="Без назначения",Category="Тест",Content="Исходный текст",Tags="",Flags="",CreatedAt=DateTime.Now,UpdatedAt=DateTime.Now};bulkDb.AddArticle(article);
            var tagIds=MetadataPresetCatalog.All.Where(p=>p.Kind==MetadataKind.Tag).Select(p=>p.Id).ToArray();
            var flagIds=MetadataPresetCatalog.All.Where(p=>p.Kind==MetadataKind.Flag).Select(p=>p.Id).ToArray();
            var added=bulk.AddPresets(MetadataKind.Tag,tagIds);Check(added.Added==17&&added.Skipped==1,"Normalized existing tag not skipped.");
            var addedFlags=bulk.AddPresets(MetadataKind.Flag,flagIds);Check(addedFlags.Added==7&&addedFlags.Skipped==1,"Normalized existing flag not skipped.");
            var retained=bulkDb.GetFlags().Single(f=>f.Icon=="OLD");
            Check(retained.Name=="  Канон   подтверждён "&&retained.Color=="#112233"&&retained.Description=="Собственное описание","Preset overwrote existing fields.");
            Check(bulk.AddPresets(MetadataKind.Tag,tagIds).Added==0&&bulk.AddPresets(MetadataKind.Flag,flagIds).Added==0,"Repeated presets duplicate.");
            var original=bulkDb.GetArticles().Single();Check(original.Tags==""&&original.Flags==""&&original.Content=="Исходный текст","Presets assigned metadata to article.");
            Check(bulkDb.GetTags().Count==18&&bulkDb.GetFlags().Count==8,"Final preset counts incorrect.");

            var atomicDb=new DatabaseService(Path.Combine(folder,"atomic.db"));var atomic=new MetadataCatalogService(atomicDb);
            var dialog=new MetadataPresetsWindow(atomic,MetadataKind.Flag,window.ResolveIcon,()=>Task.CompletedTask,errors.Add);windows.Add(dialog);Wait(dialog.Initialization);
            before=Hash(atomicDb.DatabasePath);
            Check(dialog.Choices.Count==8&&dialog.Choices.All(c=>!c.IsSelected),"Opening dialog selected/wrote items.");
            dialog.Close();Check(Hash(atomicDb.DatabasePath)==before,"Cancel dialog wrote DB.");
            var active=new MetadataPresetsWindow(atomic,MetadataKind.Flag,window.ResolveIcon,()=>Task.CompletedTask,errors.Add);windows.Add(active);Wait(active.Initialization);
            foreach(var choice in active.Choices)choice.IsSelected=true;
            Execute(atomicDb.DatabasePath,"CREATE TRIGGER fail_batch BEFORE INSERT ON Flags WHEN NEW.Name='Требует проверки' BEGIN SELECT RAISE(ABORT,'batch denied'); END");
            Wait(active.ApplySelectedAsync());Check(atomicDb.GetFlags().Count==0&&active.Choices.All(c=>c.IsSelected)&&active.Result.Contains("Транзакция"),"Batch not rolled back or selection lost.");
            Execute(atomicDb.DatabasePath,"DROP TRIGGER fail_batch");Wait(active.ApplySelectedAsync());
            Check(atomicDb.GetFlags().Count==8&&active.Choices.All(c=>c.Exists&&!c.IsSelected)&&active.Result.Contains("Добавлено: 8"),"Dialog commit/preview failed.");
            var subset=new MetadataPresetsWindow(atomic,MetadataKind.Tag,window.ResolveIcon,()=>Task.CompletedTask,errors.Add);windows.Add(subset);Wait(subset.Initialization);
            subset.Choices[0].IsSelected=true;subset.Choices[4].IsSelected=true;Wait(subset.ApplySelectedAsync());
            Check(atomicDb.GetTags().Count==2,"Subset dialog added unselected tags.");
            var official=new OfficialDatabaseService(Path.Combine(folder,"official.db"));
            Check(official.GetArticles().Count>0&&db.GetFlags().Count==count,"User/official compatibility regression.");

            Wait(window.Tabs[1].ReloadAsync());
            var preview=Environment.GetEnvironmentVariable("SKYRIM_RP_METADATA_PREVIEW_DIR");
            var showcase=new MetadataManagerWindow(bulkDb,_=>true,errors.Add);windows.Add(showcase);Wait(showcase.Initialization);
            var showcaseTabs=(TabControl)showcase.FindName("MetadataTabs");
            if(!string.IsNullOrWhiteSpace(preview))Directory.CreateDirectory(preview);
            for(var i=0;i<3;i++)
            {
                showcaseTabs.SelectedIndex=i;showcase.Tabs[i].TrySelect(showcase.Tabs[i].Rows.FirstOrDefault());
                var content=(FrameworkElement)showcase.Content;content.Measure(new Size(1240,860));content.Arrange(new Rect(0,0,1240,860));content.UpdateLayout();
                if(!string.IsNullOrWhiteSpace(preview))SavePreview(showcase,Path.Combine(preview,new[]{"categories.png","tags.png","flags.png"}[i]));
                if(i==2)
                {
                    var combo=VisualDescendants((DependencyObject)showcase.Content).OfType<ComboBox>().Single();
                    Check(ReferenceEquals(combo.SelectedItem,showcase.Tabs[i].SelectedIcon),$"Icon selection lost on tab reuse: target={combo.SelectedItem}, source={showcase.Tabs[i].SelectedIcon?.Title}");
                    Check(VisualDescendants(combo).OfType<TextBlock>().Any(t=>t.Text==showcase.Tabs[i].SelectedIcon?.Title),"Selected icon caption is blank.");
                }
            }
            var minimum=(FrameworkElement)showcase.Content;minimum.Measure(new Size(960,700));minimum.Arrange(new Rect(0,0,960,700));minimum.UpdateLayout();
            var actions=VisualDescendants(minimum).OfType<Button>().Where(b=>b.ActualWidth>0&&b.Visibility==Visibility.Visible).ToArray();
            Check(actions.Length>=5&&actions.All(b=>{var r=b.TransformToAncestor(minimum).TransformBounds(new Rect(b.RenderSize));return r.Left>=-1&&r.Right<=minimum.ActualWidth+1&&r.Top>=-1&&r.Bottom<=minimum.ActualHeight+1;}),"Actions overflow minimum window layout.");
            if(!string.IsNullOrWhiteSpace(preview)){SavePreview(subset,Path.Combine(preview,"preset-tags.png"));SavePreview(showcase,Path.Combine(preview,"minimum-layout.png"));}
            Check(windows.All(w=>!w.IsVisible),"Tests must not show windows.");
            Console.WriteLine($"Metadata catalog 9.0 OK: {checks} assertions; UI state/dirty guards/CRUD/system protection, 18+8 presets, idempotency, rollback, resources and unchanged article assignments. No visible window.");
        }
        finally
        {
            foreach(var window in windows)
            {
                if(window is MetadataManagerWindow owner)foreach(var tab in owner.Tabs)tab.CancelDraft();
                window.Close();
            }
            SqliteConnection.ClearAllPools();Directory.Delete(folder,true);
        }
    }

    private static void SavePreview(Window window,string file)
    {
        var width=window.Width;var height=window.Height;
        var content=(FrameworkElement)window.Content;content.Measure(new Size(width,height));content.Arrange(new Rect(0,0,width,height));content.UpdateLayout();
        var bitmap=new RenderTargetBitmap((int)width,(int)height,96,96,PixelFormats.Pbgra32);
        var backdrop=new DrawingVisual();using(var dc=backdrop.RenderOpen())dc.DrawRectangle(window.Background,null,new Rect(0,0,width,height));
        bitmap.Render(backdrop);bitmap.Render(content);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream=File.Create(file);encoder.Save(stream);
    }
    private static IEnumerable<DependencyObject> VisualDescendants(DependencyObject parent)
    {
        for(var i=0;i<VisualTreeHelper.GetChildrenCount(parent);i++)
        {var child=VisualTreeHelper.GetChild(parent,i);yield return child;foreach(var nested in VisualDescendants(child))yield return nested;}
    }
}
