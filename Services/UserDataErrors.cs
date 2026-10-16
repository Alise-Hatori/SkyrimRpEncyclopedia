using System.Windows;

namespace SkyrimRpEncyclopedia.Services;

internal static class UserDataErrors
{
    internal static void Report(string operation,Exception error,Window? owner=null,bool inputRetained=true)
    {
        OverlayCrashDiagnostics.WriteException(operation,error,window:owner);
        var message="Операция не выполнена. "+(inputRetained?"Введённые данные сохранены в форме.":"")+"\n\n"+error.Message+"\n\nАвтоматический повтор и восстановление базы не выполнялись.";
        if(owner is null) MessageBox.Show(message,"Ошибка записи",MessageBoxButton.OK,MessageBoxImage.Error);
        else MessageBox.Show(owner,message,"Ошибка записи",MessageBoxButton.OK,MessageBoxImage.Error);
    }
}
