using System.IO;
using Microsoft.Web.WebView2.Core;

namespace SkyrimRpEncyclopedia.Services;

internal static class MapWebView2Environment
{
    internal const string UserDataFolderDescription =
        "%LOCALAPPDATA%\\SkyrimRpEncyclopedia\\WebView2";

    private static readonly string UserDataFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SkyrimRpEncyclopedia",
        "WebView2");

    private static readonly Lazy<Task<CoreWebView2Environment>> EnvironmentTask =
        new(CreateAsync);

    internal static Task<CoreWebView2Environment> GetAsync() => EnvironmentTask.Value;

    private static Task<CoreWebView2Environment> CreateAsync()
    {
        Directory.CreateDirectory(UserDataFolder);
        return CoreWebView2Environment.CreateAsync(userDataFolder: UserDataFolder);
    }
}
