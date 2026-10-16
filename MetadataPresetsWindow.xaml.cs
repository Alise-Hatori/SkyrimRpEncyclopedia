using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using SkyrimRpEncyclopedia.Models;
using SkyrimRpEncyclopedia.Services;

namespace SkyrimRpEncyclopedia;

public sealed class MetadataPresetChoice : INotifyPropertyChanged
{
    public MetadataPreset Preset{get;}
    public ImageSource Image{get;}
    public bool Exists{get;}
    public bool CanSelect=>!Exists;
    public string Group=>Preset.Group;
    public string ExistingLabel=>Exists?"Уже существует — останется без изменений":"Новый элемент";
    private bool _selected;
    public bool IsSelected{get=>_selected;set{_selected=value&&!Exists;PropertyChanged?.Invoke(this,new PropertyChangedEventArgs(nameof(IsSelected)));}}
    public MetadataPresetChoice(MetadataPreset preset,ImageSource image,bool exists){Preset=preset;Image=image;Exists=exists;}
    public event PropertyChangedEventHandler? PropertyChanged;
}
public partial class MetadataPresetsWindow : Window
{
    private readonly MetadataCatalogService _service;private readonly MetadataKind _kind;private readonly Func<string,ImageSource> _image;private readonly Func<Task> _afterAdded;private readonly Action<Exception> _report;
    public ObservableCollection<MetadataPresetChoice> Choices{get;}=new();
    public Task Initialization{get;}
    public string Result=>ResultText.Text;
    private bool _busy,_closed,_writing;
    public MetadataPresetsWindow(MetadataCatalogService service,MetadataKind kind,Func<string,ImageSource> image,Func<Task> afterAdded,Action<Exception> report)
    {
        InitializeComponent();if(kind==MetadataKind.Category)throw new ArgumentException("Наборы доступны для тегов и флагов.");
        _service=service;_kind=kind;_image=image;_afterAdded=afterAdded;_report=report;
        Heading.Text=kind==MetadataKind.Tag?"Готовые теги":"Готовые флаги";
        var view=CollectionViewSource.GetDefaultView(Choices);view.GroupDescriptions.Add(new PropertyGroupDescription(nameof(MetadataPresetChoice.Group)));PresetList.ItemsSource=view;
        Initialization=LoadAsync();
        Closing+=(_,e)=>{if(_writing){e.Cancel=true;ResultText.Text="Дождитесь завершения операции.";}};Closed+=(_,_)=>_closed=true;
    }
    private async Task RefreshChoicesAsync()
    {
        var existing=await Task.Run(()=>_service.ExistingNames(_kind));if(_closed)return;
        foreach(var choice in Choices)choice.PropertyChanged-=ChoiceChanged;
        Choices.Clear();
        foreach(var preset in MetadataPresetCatalog.All.Where(p=>p.Kind==_kind))
        {
            var row=new MetadataPresetChoice(preset,_image(preset.IconId),existing.Contains(MetadataPresetCatalog.NormalizeName(preset.Name)));
            row.PropertyChanged+=ChoiceChanged;Choices.Add(row);
        }
        UpdateCounts();
    }
    private async Task LoadAsync()
    {
        SetBusy(true);ResultText.Text="Чтение пользовательского справочника…";
        try{await RefreshChoicesAsync();ResultText.Text=_kind==MetadataKind.Tag?"Описание и пиктограмма тега показаны как справка каталога: в таблице Tags для них нет полей.":"Назначение флагов статьям остаётся ручным действием.";}
        catch(Exception error){ResultText.Text="Не удалось прочитать справочник. "+error.Message;_report(error);}
        finally{SetBusy(false);}
    }
    private void SetBusy(bool busy){_busy=busy;PresetList.IsEnabled=!busy;SelectAllButton.IsEnabled=!busy;ClearSelectionButton.IsEnabled=!busy;UpdateCounts();}
    private void ChoiceChanged(object? sender,PropertyChangedEventArgs e)=>UpdateCounts();
    private void UpdateCounts()
    {
        CountText.Text=$"Выбрано новых: {Choices.Count(c=>c.IsSelected)} · Уже существует: {Choices.Count(c=>c.Exists)}";
        AddButton.IsEnabled=!_busy&&Choices.Any(c=>c.IsSelected);
    }
    public async Task ApplySelectedAsync()
    {
        if(_busy)return;
        var ids=Choices.Where(c=>c.IsSelected).Select(c=>c.Preset.Id).ToArray();if(ids.Length==0){ResultText.Text="Выберите новые элементы.";return;}
        SetBusy(true);_writing=true;
        try
        {
            var result=await Task.Run(()=>_service.AddPresets(_kind,ids));
            CancelButton.Content="Закрыть";
            ResultText.Text=$"Добавлено: {result.Added}. Совпадений пропущено: {result.Skipped}. Статьям ничего не назначено.";
            try{await RefreshChoicesAsync();await _afterAdded();}
            catch(Exception error){ResultText.Text+=" Набор сохранён, но список не обновился: "+error.Message;_report(error);}
        }
        catch(Exception error){ResultText.Text="Набор не добавлен. Транзакция отменена. "+error.Message;_report(error);}
        finally{_writing=false;SetBusy(false);}
    }
    private async void Add_Click(object sender,RoutedEventArgs e)=>await ApplySelectedAsync();
    private void SelectAll_Click(object sender,RoutedEventArgs e){foreach(var row in Choices)row.IsSelected=true;}
    private void ClearSelection_Click(object sender,RoutedEventArgs e){foreach(var row in Choices)row.IsSelected=false;}
    private void Cancel_Click(object sender,RoutedEventArgs e)=>Close();
}
