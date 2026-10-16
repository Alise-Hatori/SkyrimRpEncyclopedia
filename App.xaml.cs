using System.Configuration;
using System.Data;
using System.Windows;
using System.Windows.Threading;
using SkyrimRpEncyclopedia.Services;

namespace SkyrimRpEncyclopedia;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    public App()
    {
        DispatcherUnhandledException += App_DispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += AppDomain_UnhandledException;
        TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
        Startup += (_, _) => OverlayCrashDiagnostics.Write("Application.Startup");
        Exit += (_, _) => OverlayCrashDiagnostics.Write("Application.Exit");
    }

    private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        OverlayCrashDiagnostics.WriteException("Application.DispatcherUnhandledException", e.Exception);
        MessageBox.Show("Приложение не может безопасно продолжить работу.\n\n"+e.Exception.Message+
            "\n\nПодробности: "+OverlayCrashDiagnostics.LogPath+"\nБазы не удаляются и не восстанавливаются автоматически.","Ошибка приложения",MessageBoxButton.OK,MessageBoxImage.Error);
        e.Handled=true;
        Shutdown(-1);
    }

    private static void AppDomain_UnhandledException(object? sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception exception)
            OverlayCrashDiagnostics.WriteException("AppDomain.UnhandledException", exception, e.IsTerminating);
        else
            OverlayCrashDiagnostics.Write("AppDomain.UnhandledException", details: $"isTerminating={e.IsTerminating}; exceptionObject={e.ExceptionObject}");
    }

    private static void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e) =>
        OverlayCrashDiagnostics.WriteException("TaskScheduler.UnobservedTaskException", e.Exception);
}

