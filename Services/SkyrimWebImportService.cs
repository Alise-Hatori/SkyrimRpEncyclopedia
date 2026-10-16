using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using SkyrimRpEncyclopedia.Models;

namespace SkyrimRpEncyclopedia.Services;

public class SkyrimWebImportService
{
    private readonly HttpClient _httpClient;

    public SkyrimWebImportService()
    {
        _httpClient =
            new HttpClient();

        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
            "SkyrimRpEncyclopedia/1.0");
    }

    public async Task<ImportedArticle?> ImportPage(
        string url,
        string category)
    {
        if (string.IsNullOrWhiteSpace(url))
            throw new ArgumentException(
                "URL страницы не указан.");

        if (string.IsNullOrWhiteSpace(category))
            throw new ArgumentException(
                "Категория статьи не указана.");

        string html =
            await _httpClient.GetStringAsync(url);

        return ParsePage(
            html,
            url,
            category);
    }

    private ImportedArticle? ParsePage(
        string html,
        string url,
        string category)
    {
        if (string.IsNullOrWhiteSpace(html))
            return null;

        string title =
            ExtractTitle(html);

        if (string.IsNullOrWhiteSpace(title))
            return null;

        string articleHtml =
            ExtractArticleBlock(html);

        if (string.IsNullOrWhiteSpace(articleHtml))
            return null;

        string content =
            ExtractArticleText(articleHtml);

        if (string.IsNullOrWhiteSpace(content))
            return null;

        string summary =
            CreateSummary(content);

        return new ImportedArticle
        {
            Title = title,

            Category =
                category.Trim(),

            Summary =
                summary,

            Content =
                content,

            Tags = new List<string>
            {
                category.Trim()
            },

            IsOfficial = false,

            SourceTitle =
                $"UESP — {title}",

            SourceUrl =
                url,

            SourceType =
                "Website"
        };
    }

    private static string ExtractTitle(
        string html)
    {
        var match =
            Regex.Match(
                html,
                @"<h1[^>]*>(.*?)</h1>",
                RegexOptions.IgnoreCase |
                RegexOptions.Singleline);

        if (!match.Success)
        {
            match =
                Regex.Match(
                    html,
                    @"<title[^>]*>(.*?)</title>",
                    RegexOptions.IgnoreCase |
                    RegexOptions.Singleline);
        }

        if (!match.Success)
            return "";

        return CleanHtml(
            match.Groups[1].Value);
    }

    private static string ExtractArticleBlock(
        string html)
    {
        string startMarker =
            "<div id=\"mw-content-text\"";

        int start =
            html.IndexOf(
                startMarker,
                StringComparison.OrdinalIgnoreCase);

        if (start < 0)
            return "";

        string[] endMarkers =
        {
            "<div class=\"printfooter\"",
            "<div id=\"catlinks\"",
            "<div class=\"mw-hidden-catlinks\""
        };

        int end = -1;

        foreach (string marker in endMarkers)
        {
            int position =
                html.IndexOf(
                    marker,
                    start,
                    StringComparison.OrdinalIgnoreCase);

            if (position >= 0 &&
                (end < 0 || position < end))
            {
                end = position;
            }
        }

        if (end < 0)
        {
            end =
                html.IndexOf(
                    "</body>",
                    start,
                    StringComparison.OrdinalIgnoreCase);
        }

        if (end < 0 || end <= start)
            return "";

        return html[
            start..
            end];
    }

    private static string ExtractArticleText(
        string html)
    {
        string text =
            html;

        text =
            Regex.Replace(
                text,
                @"<script\b[^>]*>.*?</script>",
                "",
                RegexOptions.IgnoreCase |
                RegexOptions.Singleline);

        text =
            Regex.Replace(
                text,
                @"<style\b[^>]*>.*?</style>",
                "",
                RegexOptions.IgnoreCase |
                RegexOptions.Singleline);

        text =
            Regex.Replace(
                text,
                @"<table\b[^>]*>.*?</table>",
                "",
                RegexOptions.IgnoreCase |
                RegexOptions.Singleline);

        text =
            Regex.Replace(
                text,
                @"<nav\b[^>]*>.*?</nav>",
                "",
                RegexOptions.IgnoreCase |
                RegexOptions.Singleline);

        text =
            Regex.Replace(
                text,
                @"<div[^>]+class\s*=\s*[""'][^""']*(?:navbox|metadata|infobox|toc)[^""']*[""'][^>]*>.*?</div>",
                "",
                RegexOptions.IgnoreCase |
                RegexOptions.Singleline);

        text =
            Regex.Replace(
                text,
                @"<h[1-6][^>]*>",
                "\n\n",
                RegexOptions.IgnoreCase);

        text =
            Regex.Replace(
                text,
                @"</h[1-6]>",
                "\n\n",
                RegexOptions.IgnoreCase);

        text =
            Regex.Replace(
                text,
                @"<p[^>]*>",
                "\n",
                RegexOptions.IgnoreCase);

        text =
            Regex.Replace(
                text,
                @"</p>",
                "\n",
                RegexOptions.IgnoreCase);

        text =
            Regex.Replace(
                text,
                @"<li[^>]*>",
                "\n• ",
                RegexOptions.IgnoreCase);

        text =
            Regex.Replace(
                text,
                @"</li>",
                "\n",
                RegexOptions.IgnoreCase);

        text =
            Regex.Replace(
                text,
                @"<br\s*/?>",
                "\n",
                RegexOptions.IgnoreCase);

        text =
            Regex.Replace(
                text,
                @"<[^>]+>",
                " ");

        text =
            WebUtility.HtmlDecode(
                text);

        text =
            Regex.Replace(
                text,
                @"[ \t]+",
                " ");

        text =
            Regex.Replace(
                text,
                @"\n\s*\n\s*\n+",
                "\n\n");

        return text.Trim();
    }

    private static string CreateSummary(
        string text)
    {
        string[] paragraphs =
            text
                .Split(
                    new[]
                    {
                        "\r\n\r\n",
                        "\n\n"
                    },
                    StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .Where(x => x.Length >= 80)
                .ToArray();

        foreach (string paragraph in paragraphs)
        {
            string clean =
                Regex.Replace(
                    paragraph,
                    @"^(Origin|Government|Inns|Shops and Services|Guilds and Temples)\s*",
                    "",
                    RegexOptions.IgnoreCase);

            clean =
                clean.Trim();

            if (clean.Length < 80)
                continue;

            if (clean.Length <= 500)
                return clean;

            int position =
                clean.IndexOf(
                    '.',
                    200);

            if (position > 0 &&
                position < 500)
            {
                return clean[
                    ..(position + 1)]
                    .Trim();
            }

            return clean[
                ..500]
                .Trim();
        }

        return text.Length <= 500
            ? text
            : text[..500].Trim();
    }

    private static string CleanHtml(
        string value)
    {
        string result =
            Regex.Replace(
                value,
                @"<[^>]+>",
                "");

        result =
            WebUtility.HtmlDecode(
                result);

        return result.Trim();
    }
}