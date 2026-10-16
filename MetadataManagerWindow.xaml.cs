using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using SkyrimRpEncyclopedia.Models;
using SkyrimRpEncyclopedia.Services;

namespace SkyrimRpEncyclopedia;

public sealed record MetadataIconOption(string Id,string Title,string StoredValue,ImageSource Image);
public sealed record MetadataListItem(MetadataEntry Entry,ImageSource Image,string Hint)
{
    public string Name=>Entry.Name;
    public bool IsSystem=>Entry.IsSystem;
}
public sealed class MetadataUiCommand : ICommand
{
    private readonly Func<Task> _action; private readonly Func<bool> _allowed;
    public MetadataUiCommand(Func<Task> action,Func<bool> allowed){_action=action;_allowed=allowed;}
    public bool CanExecute(object? p)=>_allowed();
    public async void Execute(object? p){if(CanExecute(p))await _action();}
    public event EventHandler? CanExecuteChanged{add=>CommandManager.RequerySuggested+=value;remove=>CommandManager.RequerySuggested-=value;}
}
public sealed class MetadataTabState : INotifyPropertyChanged,IDisposable
{
    private readonly MetadataCatalogService _service;
    private readonly Func<string,bool> _confirm;
    private readonly Action<Exception> _report;
    private readonly Func<string,ImageSource> _image;
    private readonly IReadOnlyList<MetadataIconOption> _icons;
    private readonly DispatcherTimer _timer;
    private MetadataListItem? _selected;
    private int? _savedId;
    private string _name="",_description="",_color="#9DBBD1",_search="",_status="",_baseline="";
    private MetadataIconOption? _icon;
    private bool _busy,_resetting,_disposed,_restoreQueued;
    public bool IsWriting {get;private set;}
    public MetadataKind Kind{get;}
    public string Title=>Kind switch{MetadataKind.Category=>"Категории",MetadataKind.Tag=>"Теги",_=>"Флаги"};
    public string Subtitle=>Kind switch{MetadataKind.Category=>"Разделы вашего архива",MetadataKind.Tag=>"Предметные метки для поиска",_=>"Состояние и особенности материала"};
    public ObservableCollection<MetadataListItem> Rows{get;}=new();
    public ICollectionView RowsView{get;}
    public ObservableCollection<MetadataIconOption> IconOptions{get;}=new();
    public bool IsFlag=>Kind==MetadataKind.Flag;
    public bool HasDescription=>Kind!=MetadataKind.Tag;
    public bool HasPresets=>Kind!=MetadataKind.Category;
    public bool CanEdit=>!IsBusy;
    public bool IsBusy{get=>_busy;private set{_busy=value;Changed();Changed(nameof(CanEdit));CommandManager.InvalidateRequerySuggested();}}
    public int Count=>Rows.Count;
    public string CountLabel=>$"{Title}  ·  {Count}";
    public string VisibleLabel=>$"Показано: {RowsView.Cast<object>().Count()} из {Count}";
    public bool IsEmpty=>RowsView.IsEmpty;
    public string EmptyTitle=>Rows.Count==0?"Здесь начинается ваш архив":"Совпадений не найдено";
    public string EmptyHint=>Rows.Count==0?"Создайте первый элемент"+(HasPresets?" или выберите готовый набор.":"."):"Попробуйте другое название или описание.";
    public string ModeLabel=>_selected is null&&!_savedId.HasValue?"НОВЫЙ ЭЛЕМЕНТ":_selected?.IsSystem==true?"СИСТЕМНЫЙ ЭЛЕМЕНТ":"РЕДАКТИРОВАНИЕ";
    public string StorageHint=>Kind==MetadataKind.Tag?"Описание и пиктограмма тега — подсказки каталога, не поля базы. Произвольный значок не сохраняется."
        :Kind==MetadataKind.Category?"Описание сохраняется. Пиктограмма определяется названием; отдельного поля значка нет."
        :"Значок и описание сохраняются в существующих полях флага. Метка не назначается статьям автоматически.";
    public string Status{get=>_status;set{_status=value;Changed();}}
    public string Name{get=>_name;set{if(_name==value)return;_name=value;DraftChanged();}}
    public string Description{get=>_description;set{if(_description==value)return;_description=value;DraftChanged();}}
    public string Color{get=>_color;set{if(_color==value)return;_color=value;DraftChanged();}}
    public MetadataIconOption? SelectedIcon{get=>_icon;set{if(_resetting||value is null||!IconOptions.Contains(value)||ReferenceEquals(_icon,value))return;_icon=value;DraftChanged();}}
    public ImageSource PreviewImage=>IsFlag&&SelectedIcon is not null?SelectedIcon.Image:_image(MetadataPresetCatalog.IconFor(Kind,Name));
    public string PreviewTitle=>string.IsNullOrWhiteSpace(Name)?"Название элемента":Name;
    public string PreviewDescription=>HasDescription?(string.IsNullOrWhiteSpace(Description)?"Описание поможет выбрать подходящую метку.":Description)
        :MetadataPresetCatalog.Find(Kind,Name)?.Description??"Личная метка для поиска. Описание тега не хранится в этой схеме.";
    public bool IsDirty=>Snapshot()!=_baseline;
    public MetadataListItem? SelectedRow{get=>_selected is not null&&RowsView.Contains(_selected)?_selected:null;set=>TrySelect(value);}
    public string SearchText{get=>_search;set{_search=value;Changed();_timer.Stop();_timer.Start();}}
    public ICommand NewCommand{get;}
    public ICommand SaveCommand{get;}
    public ICommand CancelCommand{get;}
    public ICommand DeleteCommand{get;}
    public ICommand PresetsCommand{get;}
    public MetadataTabState(MetadataKind kind,MetadataCatalogService service,Func<string,bool> confirm,Action<Exception> report,
        Func<string,ImageSource> image,IReadOnlyList<MetadataIconOption> icons,Func<MetadataKind,Task> presets)
    {
        Kind=kind;_service=service;_confirm=confirm;_report=report;_image=image;_icons=icons;
        RowsView=CollectionViewSource.GetDefaultView(Rows);
        RowsView.Filter=item=>item is MetadataListItem row&&(string.IsNullOrWhiteSpace(SearchText)||row.Name.Contains(SearchText.Trim(),StringComparison.OrdinalIgnoreCase)||row.Hint.Contains(SearchText.Trim(),StringComparison.OrdinalIgnoreCase));
        _timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(160)};_timer.Tick+=(_,_)=>ApplySearch();
        NewCommand=new MetadataUiCommand(()=>{NewDraft();return Task.CompletedTask;},()=>!IsBusy);
        SaveCommand=new MetadataUiCommand(SaveAsync,()=>!IsBusy&&IsDirty&&!string.IsNullOrWhiteSpace(Name));
        CancelCommand=new MetadataUiCommand(()=>{CancelDraft();return Task.CompletedTask;},()=>!IsBusy&&IsDirty);
        DeleteCommand=new MetadataUiCommand(DeleteAsync,()=>!IsBusy&&_selected is {IsSystem:false});
        PresetsCommand=new MetadataUiCommand(()=>presets(Kind),()=>!IsBusy&&HasPresets);
        ResetDraft(null);
    }
    public void ApplySearch(){_timer.Stop();RowsView.Refresh();Changed(nameof(IsEmpty));Changed(nameof(VisibleLabel));Changed(nameof(EmptyTitle));Changed(nameof(EmptyHint));Changed(nameof(SelectedRow));}
    public bool CanLeave()=>!IsBusy&&(!IsDirty||_confirm("Есть несохранённые изменения. Отменить их и перейти?"));
    public bool TrySelect(MetadataListItem? row)
    {
        if(_resetting||ReferenceEquals(_selected,row))return true;
        if(row is not null&&(row.Entry.Kind!=Kind||!Rows.Contains(row))){RestoreSelection();return false;}
        if(row is null&&_selected is not null&&!RowsView.Contains(_selected))return true;
        if(!CanLeave()){RestoreSelection();return false;}
        ResetDraft(row);return true;
    }
    public bool NewDraft(){if(!CanLeave())return false;ResetDraft(null);return true;}
    public void CancelDraft()=>ResetDraft(_selected);
    private void ResetDraft(MetadataListItem? row)
    {
        _resetting=true;_selected=row;_savedId=row?.Entry.Id;_name=row?.Entry.Name??"";_description=row?.Entry.Description??"";_color=row?.Entry.Color??"#9DBBD1";
        IconOptions.Clear();foreach(var icon in _icons)IconOptions.Add(icon);
        var stored=row?.Entry.Icon;_icon=_icons.FirstOrDefault(i=>i.StoredValue==stored);
        if(IsFlag&&row is not null&&_icon is null){_icon=new("legacy","Существующий значок — без изменения",stored??"",row.Image);IconOptions.Insert(0,_icon);}
        _icon??=_icons.First(i=>i.Id=="shield");_baseline=Snapshot();_resetting=false;
        foreach(var name in new[]{nameof(Name),nameof(Description),nameof(Color),nameof(SelectedIcon),nameof(SelectedRow),nameof(IsDirty),nameof(PreviewTitle),nameof(PreviewDescription),nameof(PreviewImage),nameof(ModeLabel)})Changed(name);
        CommandManager.InvalidateRequerySuggested();
    }
    private string Snapshot()=>string.Join("\u001f",Name,Description,Color,IsFlag?SelectedIcon?.StoredValue:"");
    private void DraftChanged([CallerMemberName]string? property=null)
    {if(!_resetting){Changed(property);Changed(nameof(IsDirty));Changed(nameof(PreviewTitle));Changed(nameof(PreviewDescription));Changed(nameof(PreviewImage));CommandManager.InvalidateRequerySuggested();}}
    private void RestoreSelection()
    {
        if(_restoreQueued||_disposed)return;_restoreQueued=true;
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.DataBind,new Action(()=>{_restoreQueued=false;if(!_disposed)Changed(nameof(SelectedRow));}));
    }
    public async Task ReloadAsync(int? selectId=null)
    {
        IsBusy=true;
        try{await ReadRowsAsync(selectId??_selected?.Entry.Id);}
        catch(Exception error){Fail("Не удалось прочитать метаданные",error);}
        finally{IsBusy=false;}
    }
    private async Task ReadRowsAsync(int? selectId)
    {
        var entries=await Task.Run(()=>_service.Read(Kind));if(_disposed)return;
        var projected=new List<MetadataListItem>();
        foreach(var item in entries)
        {
            var hint=item.Description;
            if(Kind==MetadataKind.Tag)hint=MetadataPresetCatalog.Find(Kind,item.Name)?.Description??"Личный тег";
            projected.Add(new(item,_image(MetadataPresetCatalog.IconFor(Kind,item.Name,item.Icon)),hint));
        }
        _resetting=true;
        try{Rows.Clear();foreach(var row in projected)Rows.Add(row);}
        finally{_resetting=false;}
        ResetDraft(Rows.FirstOrDefault(r=>r.Entry.Id==selectId));ApplySearch();Changed(nameof(Count));Changed(nameof(CountLabel));
    }
    public async Task SaveAsync()
    {
        if(IsBusy||!IsDirty)return;
        var source=_selected?.Entry;
        var candidate=new MetadataEntry(Kind,source?.Id??_savedId??0,Name,HasDescription?Description:"",IsFlag?SelectedIcon?.StoredValue??"":"",IsFlag?Color:"#808080",source?.IsSystem??false,source?.SortOrder??Rows.Count);
        IsBusy=true;IsWriting=true;
        try
        {
            if(IsFlag&&!string.IsNullOrWhiteSpace(Color)&&Color!=source?.Color)_=ColorConverter.ConvertFromString(Color);
            var id=await Task.Run(()=>_service.Save(candidate));_savedId=id;_baseline=Snapshot();Changed(nameof(IsDirty));Changed(nameof(ModeLabel));Status="Сохранено.";
            try{await ReadRowsAsync(id);}catch(Exception error){Fail("Запись сохранена, но список не обновился. Не создавайте её повторно",error);}
        }
        catch(Exception error){Fail("Не удалось сохранить. Ввод оставлен в форме",error);}
        finally{IsWriting=false;IsBusy=false;}
    }
    public async Task DeleteAsync()
    {
        if(IsBusy||_selected is null)return;
        if(_selected.IsSystem){Status="Системный элемент нельзя удалить.";return;}
        if(!_confirm("Удалить выбранный пользовательский элемент из справочника?"))return;
        var entry=_selected.Entry;IsBusy=true;IsWriting=true;
        try{await Task.Run(()=>_service.Delete(entry));await ReadRowsAsync(null);Status="Элемент удалён.";}
        catch(Exception error){Fail("Не удалось удалить элемент",error);}
        finally{IsWriting=false;IsBusy=false;}
    }
    private void Fail(string message,Exception error){Status=message+". "+error.Message;_report(error);}
    private void Changed([CallerMemberName]string? name=null)=>PropertyChanged?.Invoke(this,new PropertyChangedEventArgs(name));
    public event PropertyChangedEventHandler? PropertyChanged;
    public void Dispose(){_disposed=true;_timer.Stop();}
}
public partial class MetadataManagerWindow : Window
{
    public IReadOnlyList<MetadataTabState> Tabs{get;}
    public Task Initialization{get;}
    private bool _ready,_switching;private int _previousTab;
    private readonly MetadataCatalogService _service;private readonly Func<string,bool> _confirm;private readonly Action<Exception> _report;
    public MetadataManagerWindow(DatabaseService database,Func<string,bool>? confirm=null,Action<Exception>? report=null)
    {
        InitializeComponent();_service=new(database);
        _confirm=confirm??(message=>MessageBox.Show(this,message,"Управление метаданными",MessageBoxButton.YesNo,MessageBoxImage.Question)==MessageBoxResult.Yes);
        _report=report??(error=>OverlayCrashDiagnostics.WriteException("Metadata.Operation.Failed",error,window:this));
        var titles=new[]{"Пергамент","Крепость","Персонаж","Клинки","Руна","Божество","Компас","Лупа","Весы","Щит","Маска","Замок","Проверка","Звезда","Песочные часы","Кубик","Перо","Карта","Алхимия","Корона","Существо","Диалог"};
        var icons=MetadataPresetCatalog.IconIds.Select((id,i)=>new MetadataIconOption(id,titles[i],"metadata:"+id,ResolveIcon(id))).ToArray();
        Tabs=Enum.GetValues<MetadataKind>().Select(kind=>new MetadataTabState(kind,_service,_confirm,_report,ResolveIcon,icons,OpenPresetsAsync)).ToArray();
        DataContext=this;MetadataTabs.SelectedIndex=0;_ready=true;Initialization=Task.WhenAll(Tabs.Select(tab=>tab.ReloadAsync()));
        Closing+=(_,e)=>{if(Tabs.Any(t=>t.IsWriting)){e.Cancel=true;return;}if(Tabs.Any(t=>t.IsDirty)&&!_confirm("Закрыть окно и отменить несохранённые изменения?"))e.Cancel=true;};
        Closed+=(_,_)=>{foreach(var tab in Tabs)tab.Dispose();};
    }
    internal ImageSource ResolveIcon(string id)=>(ImageSource)FindResource("MetadataIcon_"+id);
    private void MetadataTabs_SelectionChanged(object sender,System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if(!_ready||_switching||!ReferenceEquals(e.Source,MetadataTabs))return;
        var next=MetadataTabs.SelectedIndex;if(next==_previousTab)return;
        if(Tabs.Any(t=>t.IsBusy)||!Tabs[_previousTab].CanLeave()){_switching=true;MetadataTabs.SelectedIndex=_previousTab;_switching=false;return;}
        Tabs[_previousTab].CancelDraft();_previousTab=next;
    }
    internal async Task OpenPresetsAsync(MetadataKind kind)
    {
        var tab=Tabs.First(t=>t.Kind==kind);if(!tab.CanLeave())return;tab.CancelDraft();
        var dialog=new MetadataPresetsWindow(_service,kind,ResolveIcon,()=>tab.ReloadAsync(),_report){Owner=this};dialog.ShowDialog();await Task.CompletedTask;
    }
    private void Close_Click(object sender,RoutedEventArgs e)=>Close();
}
