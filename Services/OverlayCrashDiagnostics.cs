using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;

namespace SkyrimRpEncyclopedia.Services;

internal static class OverlayCrashDiagnostics
{
    private static readonly object Sync = new();

    internal static string LogPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SkyrimRpEncyclopedia",
        "overlay-crash-diagnostics.log");

    internal static void Write(string eventName, Window? window = null, string? details = null)
    {
        try
        {
            var process = Process.GetCurrentProcess();
            var line = new StringBuilder()
                .Append(DateTimeOffset.Now.ToString("O"))
                .Append(" | pid=").Append(process.Id)
                .Append(" | thread=").Append(Environment.CurrentManagedThreadId)
                .Append(" | event=").Append(eventName)
                .Append(" | ").Append(DescribeApplication(window));

            if (!string.IsNullOrWhiteSpace(details))
                line.Append(" | ").Append(details);

            lock (Sync)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
                File.AppendAllText(LogPath, line.AppendLine().ToString());
            }
        }
        catch
        {
            // Diagnostics must never affect the application lifecycle.
        }
    }

    internal static void WriteException(
        string eventName,
        Exception exception,
        bool? isTerminating = null,
        Window? window = null)
    {
        var details = new StringBuilder();
        if (isTerminating.HasValue)
            details.Append("isTerminating=").Append(isTerminating.Value).AppendLine();

        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            details.AppendLine($"{current.GetType().FullName}: {current.Message}");
            details.AppendLine(current.StackTrace ?? "<no managed stack trace>");
            if (current.InnerException is not null)
                details.AppendLine("Inner exception:");
        }

        Write(eventName, window, details.ToString());
    }

    private static string DescribeApplication(Window? window)
    {
        try
        {
            var application = Application.Current;
            var mainWindow = application?.MainWindow;
            return string.Join(", ", new[]
            {
                DescribeWindow("window", window),
                DescribeWindow("main", mainWindow),
                $"windowCount={application?.Windows.Count.ToString() ?? "unavailable"}"
            });
        }
        catch (Exception exception)
        {
            return $"window-state-unavailable={exception.GetType().Name}: {exception.Message}";
        }
    }

    private static string DescribeWindow(string label, Window? window)
    {
        if (window is null)
            return $"{label}=null";

        return $"{label}={window.GetType().Name}; visibility={window.Visibility}; isVisible={window.IsVisible}";
    }
}
