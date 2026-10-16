using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;

namespace SkyrimRpEncyclopedia.Services;

public class SkyrimUespCatalogService
{
    private readonly HttpClient _httpClient;

    public SkyrimUespCatalogService()
    {
        _httpClient =
            new HttpClient();

        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
            "SkyrimRpEncyclopedia/1.0");
    }

    public async Task<List<string>> GetCategoryPages(
    string category)
{
    if (string.IsNullOrWhiteSpace(category))
        throw new ArgumentException(
            "Категория UESP не указана.");

    string url =
        "https://en.uesp.net/wiki/Category:" +
        Uri.EscapeDataString(category.Trim());

    string html =
        await _httpClient.GetStringAsync(url);

    var urls =
        ExtractPageUrls(
            html);

    return urls;
}

    public async Task<List<string>> GetSubcategories(
        string category)
    {
        if (string.IsNullOrWhiteSpace(category))
            throw new ArgumentException(
                "Категория UESP не указана.");

        string url =
            "https://en.uesp.net/wiki/Category:" +
            Uri.EscapeDataString(category.Trim());

        string html =
            await _httpClient.GetStringAsync(url);

        return ExtractCategoryNames(
            html);
    }

    private static List<string> ExtractPageUrls(
        string html)
    {
        var urls =
            new List<string>();

        if (string.IsNullOrWhiteSpace(html))
            return urls;

        var matches =
            Regex.Matches(
                html,
                @"href\s*=\s*[""'](/wiki/Skyrim:[^""'#?]+)[""']",
                RegexOptions.IgnoreCase);

        foreach (Match match in matches)
        {
            if (!match.Success)
                continue;

            string path =
                WebUtility.HtmlDecode(
                    match.Groups[1].Value);

            string url =
                "https://en.uesp.net" +
                path;

            if (!urls.Contains(
                    url,
                    StringComparer.OrdinalIgnoreCase))
            {
                urls.Add(url);
            }
        }

        return urls;
    }

    private static List<string> ExtractCategoryNames(
        string html)
    {
        var categories =
            new List<string>();

        if (string.IsNullOrWhiteSpace(html))
            return categories;

        var matches =
            Regex.Matches(
                html,
                @"href\s*=\s*[""']/wiki/Category:([^""'#?]+)[""']",
                RegexOptions.IgnoreCase);

        foreach (Match match in matches)
        {
            if (!match.Success)
                continue;

            string name =
                WebUtility.HtmlDecode(
                    match.Groups[1].Value);

            name =
                Uri.UnescapeDataString(
                    name);

            if (string.IsNullOrWhiteSpace(name))
                continue;

            if (!categories.Contains(
                    name,
                    StringComparer.OrdinalIgnoreCase))
            {
                categories.Add(name);
            }
        }

        return categories;
    }
}