using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using SkyrimRpEncyclopedia.Models;

namespace SkyrimRpEncyclopedia.Services;

/// <summary>
/// Retrieves a small, attributable local context and asks a local Ollama model
/// to answer only from that context. It never writes to a Knowledge DB.
/// </summary>
public sealed class KnowledgeAssistantService
{
    private const int MaxSources = 4;
    private readonly IKnowledgeAssistantModel _model;

    public KnowledgeAssistantService(IKnowledgeAssistantModel model)
    {
        _model = model;
    }

    public async Task<KnowledgeAssistantResult> AskAsync(
        string? question,
        IReadOnlyCollection<Article> knowledgeArticles,
        CancellationToken cancellationToken = default)
    {
        string normalizedQuestion = question?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedQuestion))
        {
            return KnowledgeAssistantResult.Insufficient(
                "Введите вопрос, чтобы найти подтверждённые сведения в базе знаний.");
        }

        var sources = FindRelevantArticles(normalizedQuestion, knowledgeArticles);
        if (sources.Count == 0)
        {
            return KnowledgeAssistantResult.Insufficient(
                "В локальной базе не найдено достаточно релевантных статей. Ответ не сформирован, чтобы не выдавать предположения за факты.");
        }

        string prompt = BuildPrompt(normalizedQuestion, sources);
        try
        {
            string answer = await _model.GenerateAsync(prompt, cancellationToken);
            if (string.IsNullOrWhiteSpace(answer))
            {
                return KnowledgeAssistantResult.ModelUnavailable(
                    "Локальная модель вернула пустой ответ. Используйте приведённые источники напрямую.", sources);
            }

            return KnowledgeAssistantResult.Answered(answer.Trim(), sources);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return KnowledgeAssistantResult.ModelUnavailable(
                "Локальная Ollama не ответила вовремя. Используйте приведённые источники напрямую.", sources);
        }
        catch (HttpRequestException)
        {
            return KnowledgeAssistantResult.ModelUnavailable(
                "Локальная Ollama недоступна. Проверьте, что сервис запущен, затем повторите запрос.", sources);
        }
        catch (JsonException)
        {
            return KnowledgeAssistantResult.ModelUnavailable(
                "Ответ локальной Ollama имеет непонятный формат. Источники ниже остаются доступными.", sources);
        }
    }

    public static IReadOnlyList<KnowledgeAssistantSource> FindRelevantArticles(
        string question,
        IReadOnlyCollection<Article> knowledgeArticles)
    {
        var terms = Tokenize(question);
        if (terms.Count == 0)
            return Array.Empty<KnowledgeAssistantSource>();

        return knowledgeArticles
            .Where(article => article.IsMvp &&
                MvpKnowledgeDatabaseService.TryGetSafeSourceUri(article.SourceUrl, out _))
            .Select(article => new { Article = article, Score = Score(article, question, terms) })
            .Where(candidate => candidate.Score > 0)
            .OrderByDescending(candidate => candidate.Score)
            .ThenBy(candidate => candidate.Article.Title, StringComparer.CurrentCultureIgnoreCase)
            .Take(MaxSources)
            .Select(candidate => new KnowledgeAssistantSource(candidate.Article))
            .ToArray();
    }

    internal static string BuildPrompt(string question, IReadOnlyList<KnowledgeAssistantSource> sources)
    {
        var context = new StringBuilder();
        foreach (var source in sources)
        {
            context.AppendLine($"СТАТЬЯ: {source.Title}");
            context.AppendLine($"КАТЕГОРИЯ: {source.Category}");
            context.AppendLine($"URL: {source.SourceUrl}");
            if (!string.IsNullOrWhiteSpace(source.Summary))
                context.AppendLine($"КРАТКО: {TrimForContext(source.Summary, 700)}");
            context.AppendLine($"ТЕКСТ: {TrimForContext(source.Content, 1_500)}");
            context.AppendLine();
        }

        return $"""
            Ты — локальный помощник энциклопедии Skyrim. Отвечай по-русски.
            Используй только сведения из КОНТЕКСТА ниже. Не добавляй факты из памяти,
            не придумывай названия, даты или URL. Если контекста недостаточно, прямо
            скажи, каких сведений в нём нет. Не создавай собственный список источников:
            интерфейс покажет проверенные URL отдельно.

            ВОПРОС: {question}

            КОНТЕКСТ:
            {context}
            """;
    }

    private static int Score(Article article, string question, IReadOnlyList<string> terms)
    {
        int score = article.Title.Contains(question, StringComparison.OrdinalIgnoreCase) ? 20 : 0;
        foreach (string term in terms)
        {
            if (article.Title.Contains(term, StringComparison.OrdinalIgnoreCase)) score += 8;
            if (article.Category.Contains(term, StringComparison.OrdinalIgnoreCase)) score += 5;
            if (article.Tags.Contains(term, StringComparison.OrdinalIgnoreCase)) score += 4;
            if (article.Summary.Contains(term, StringComparison.OrdinalIgnoreCase)) score += 2;
            if (article.Content.Contains(term, StringComparison.OrdinalIgnoreCase)) score += 1;
        }
        return score;
    }

    private static IReadOnlyList<string> Tokenize(string value) =>
        Regex.Matches(value, "[\\p{L}\\p{N}]{2,}")
            .Select(match => match.Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(12)
            .ToArray();

    private static string TrimForContext(string value, int limit) =>
        value.Length <= limit ? value : value[..limit] + "…";
}

public interface IKnowledgeAssistantModel
{
    Task<string> GenerateAsync(string prompt, CancellationToken cancellationToken);
}

public sealed class OllamaKnowledgeModelClient : IKnowledgeAssistantModel
{
    public const string EndpointEnvironmentVariable = "SKYRIM_RP_OLLAMA_ENDPOINT";
    public const string ModelEnvironmentVariable = "SKYRIM_RP_OLLAMA_MODEL";
    public const string DefaultEndpoint = "http://127.0.0.1:11434/api/generate";
    public const string DefaultModel = "qwen3:8b";

    private readonly HttpClient _httpClient;
    private readonly Uri _endpoint;
    private readonly string _model;

    public OllamaKnowledgeModelClient(HttpClient? httpClient = null, string? endpoint = null, string? model = null)
    {
        _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(45) };
        _endpoint = new Uri(endpoint ?? Environment.GetEnvironmentVariable(EndpointEnvironmentVariable) ?? DefaultEndpoint);
        _model = string.IsNullOrWhiteSpace(model)
            ? Environment.GetEnvironmentVariable(ModelEnvironmentVariable) ?? DefaultModel
            : model;
    }

    public async Task<string> GenerateAsync(string prompt, CancellationToken cancellationToken)
    {
        using var response = await _httpClient.PostAsJsonAsync(
            _endpoint,
            new OllamaGenerateRequest(_model, prompt, false),
            cancellationToken);
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<OllamaGenerateResponse>(cancellationToken: cancellationToken);
        return payload?.Response ?? string.Empty;
    }

    private sealed record OllamaGenerateRequest(string Model, string Prompt, bool Stream);
    private sealed record OllamaGenerateResponse(string? Response);
}

public sealed record KnowledgeAssistantSource(Article Article)
{
    public string Title => Article.Title;
    public string Category => Article.Category;
    public string Summary => Article.Summary;
    public string Content => Article.Content;
    public string SourceUrl => Article.SourceUrl;
}

public sealed record KnowledgeAssistantResult(
    string Message,
    IReadOnlyList<KnowledgeAssistantSource> Sources,
    bool IsInsufficientContext,
    bool IsModelUnavailable)
{
    public static KnowledgeAssistantResult Answered(string message, IReadOnlyList<KnowledgeAssistantSource> sources) =>
        new(message, sources, false, false);

    public static KnowledgeAssistantResult Insufficient(string message) =>
        new(message, Array.Empty<KnowledgeAssistantSource>(), true, false);

    public static KnowledgeAssistantResult ModelUnavailable(string message, IReadOnlyList<KnowledgeAssistantSource> sources) =>
        new(message, sources, false, true);
}
