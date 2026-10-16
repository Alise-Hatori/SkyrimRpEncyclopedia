using SkyrimRpEncyclopedia.Services;
using SkyrimRpEncyclopedia.Models;
using Microsoft.Data.Sqlite;
using System.Text.Json.Nodes;
using System.Text.Json;

if (args.Length == 2 && args[0] == "--rp-rules-only")
{
    RpRulesTests.Run(args[1]);
    return;
}
if (args.Length == 2 && args[0] == "--installed-data-only")
{
    RpRulesTests.VerifyInstalled(args[1]);
    return;
}

if (args.Length == 1 && args[0] == "--map-selection-only")
{
    MapSelectionTests.Run();
    return;
}

if (args.Length == 2 && args[0] == "--prepare-isolated-startup")
{
    ReleasePackageTests.PrepareIsolatedStartup(args[1]);
    return;
}

if (args.Length is 1 or 2 && args[0] == "--release-package-only")
{
    ReleasePackageTests.Run(args.Length == 2 ? args[1] : null);
    return;
}

if (args.Length == 1 && args[0] is "--metadata-resources-only" or "--article-metadata-only")
{
    MetadataWindowResourceTests.Run();
    return;
}

if (args.Length == 1 && args[0] == "--history-only")
{
    RecentHistoryTests.Run();
    return;
}

// Autonomous core can be verified without a database fixture or any temporary files.
if (args.Length == 1 && args[0] == "--graph-atlas-only")
{
    GraphAtlasViewTests.Run();
    GraphDataTests.Run();
    GraphMapTests.Run();
    return;
}

if (args.Length == 1 && args[0] == "--simple-events-only")
{
    RpSimpleEventGeneratorTests.Run();
    RpSimpleEventViewTests.Run();
    return;
}

// Fixture selection must be explicit, before any temporary test data is created.
string? path = Environment.GetEnvironmentVariable("SKYRIM_RP_MVP_DB");
if (string.IsNullOrWhiteSpace(path))
    throw new InvalidOperationException("Set SKYRIM_RP_MVP_DB to the existing fixture database and SKYRIM_RP_TEST_MVP_EXPECTED_ENTITY_COUNT to its independently known expected count (125 or 5579).");
if (!int.TryParse(Environment.GetEnvironmentVariable("SKYRIM_RP_TEST_MVP_EXPECTED_ENTITY_COUNT"), out int expectedMvpEntityCount) || expectedMvpEntityCount <= 0)
    throw new InvalidOperationException("Set SKYRIM_RP_TEST_MVP_EXPECTED_ENTITY_COUNT explicitly to a positive fixture entity count: 125 for the legacy MVP DB or 5579 for the full Knowledge DB. SKYRIM_RP_MVP_DB selects the database; its count is never inferred automatically.");
if (!File.Exists(path))
    throw new InvalidOperationException($"SKYRIM_RP_MVP_DB does not exist: {path}");
Console.WriteLine($"Smoke fixture: database={Path.GetFullPath(path)}, expectedEntities={expectedMvpEntityCount}");

RpSimpleEventGeneratorTests.Run();
RpSimpleEventViewTests.Run();
MapSelectionTests.Run();
GraphAtlasViewTests.Run();
GraphDataTests.Run();
GraphMapTests.Run();
if (expectedMvpEntityCount == 5579)
{
    GraphDataTests.MeasureReal(path);
    GraphMapTests.MeasureReal(path);
}
else
{
    Console.WriteLine("Graph real-fixture measurements skipped for the 125-entity legacy corpus; synthetic graph checks still ran.");
}
NavigationTests.Run();
FiltersHistoryTests.Run();
RecentHistoryTests.Run();
UserDataTests.Run();
ReleaseConfigurationTests.Run();
ReleasePackageTests.Run();
MetadataWindowResourceTests.Run();

var overlaySettingsPath = Path.Combine(Path.GetTempPath(), $"skyrim-rp-overlay-{Guid.NewGuid():N}.json");
var overlaySettingsService = new OverlaySettingsService(overlaySettingsPath);
overlaySettingsService.Save(new OverlaySettings
{
    Left = 120,
    Top = 80,
    Width = 640,
    Height = 420,
    Opacity = 0.72,
    IsMapVisible = true,
    MapHeight = 180,
    IsMiniMapVisible = true,
    MiniMapLeft = 240,
    MiniMapTop = 140,
    MiniMapDiameter = 320,
    MiniMapOpacity = 0.84,
    Theme = OverlayAppearance.FrostMagic,
    FontSize = 18,
    ShowDecorativeBorder = false,
    ShowArticleTitle = false,
    ShowArticleCategory = true,
    ShowArticleContent = false,
    ShowRelatedArticles = true,
    ShowEmbeddedMap = false,
    LayoutMode = OverlayLayoutModes.ArticleOnly,
    IsCompactMode = true,
    IsPositionLocked = true
});
var overlaySettings = overlaySettingsService.Load();
if (overlaySettings.Left != 120 || overlaySettings.Top != 80 || overlaySettings.Width != 640 || overlaySettings.Height != 420 || overlaySettings.Opacity != 0.72 || !overlaySettings.IsMapVisible || overlaySettings.MapHeight != 180 || !overlaySettings.IsMiniMapVisible || overlaySettings.MiniMapLeft != 240 || overlaySettings.MiniMapTop != 140 || overlaySettings.MiniMapDiameter != 320 || overlaySettings.MiniMapOpacity != 0.84 || overlaySettings.Theme != OverlayAppearance.FrostMagic || overlaySettings.FontSize != 18 || overlaySettings.ShowDecorativeBorder || overlaySettings.ShowArticleTitle || !overlaySettings.ShowArticleCategory || overlaySettings.ShowArticleContent || !overlaySettings.ShowRelatedArticles || overlaySettings.ShowEmbeddedMap || overlaySettings.LayoutMode != OverlayLayoutModes.ArticleOnly || !overlaySettings.IsCompactMode || !overlaySettings.IsPositionLocked)
    throw new InvalidOperationException("Overlay settings were not persisted.");
var normalizedOverlaySettings = OverlaySettingsService.Normalize(new OverlaySettings { Width = 0, Height = double.NaN, Opacity = 5, MapHeight = 1000, MiniMapDiameter = 1000, MiniMapOpacity = 0, Theme = "unknown", FontSize = 99 });
if (normalizedOverlaySettings.Width != 320 || normalizedOverlaySettings.Height != 520 || normalizedOverlaySettings.Opacity != 1 || normalizedOverlaySettings.MapHeight != 360 || normalizedOverlaySettings.MiniMapDiameter != 480 || normalizedOverlaySettings.MiniMapOpacity != 0.35 || normalizedOverlaySettings.Theme != OverlayAppearance.NordicFrost || normalizedOverlaySettings.FontSize != 22 || normalizedOverlaySettings.LayoutMode != OverlayLayoutModes.Normal)
    throw new InvalidOperationException("Overlay settings normalization is invalid.");
foreach (var theme in OverlayAppearance.Themes)
{
    overlaySettingsService.Save(new OverlaySettings { Theme = theme, FontSize = 16, ShowDecorativeBorder = false });
    var persistedTheme = overlaySettingsService.Load();
    if (persistedTheme.Theme != theme || persistedTheme.FontSize != 16 || persistedTheme.ShowDecorativeBorder)
        throw new InvalidOperationException($"Overlay appearance theme was not persisted: {theme}.");
}
foreach (var layoutMode in OverlayLayoutModes.All)
{
    overlaySettingsService.Save(new OverlaySettings
    {
        LayoutMode = layoutMode,
        ShowArticleTitle = false,
        ShowArticleCategory = true,
        ShowArticleContent = false,
        ShowRelatedArticles = true,
        ShowEmbeddedMap = false
    });
    var persistedLayout = overlaySettingsService.Load();
    if (persistedLayout.LayoutMode != layoutMode || persistedLayout.ShowArticleTitle ||
        !persistedLayout.ShowArticleCategory || persistedLayout.ShowArticleContent ||
        !persistedLayout.ShowRelatedArticles || persistedLayout.ShowEmbeddedMap)
    {
        throw new InvalidOperationException($"Overlay layout mode was not persisted independently: {layoutMode}.");
    }
}
File.WriteAllText(overlaySettingsPath, "{\"Opacity\":0.75,\"Width\":500}");
var legacyOverlaySettings = overlaySettingsService.Load();
if (legacyOverlaySettings.Opacity != 0.75 || legacyOverlaySettings.Width != 500 || legacyOverlaySettings.Theme != OverlayAppearance.NordicFrost || legacyOverlaySettings.FontSize != 14 || !legacyOverlaySettings.ShowDecorativeBorder || !legacyOverlaySettings.ShowArticleTitle || !legacyOverlaySettings.ShowArticleCategory || !legacyOverlaySettings.ShowArticleContent || !legacyOverlaySettings.ShowRelatedArticles || !legacyOverlaySettings.ShowEmbeddedMap || legacyOverlaySettings.LayoutMode != OverlayLayoutModes.Normal)
    throw new InvalidOperationException("Legacy overlay settings were not loaded with appearance defaults.");
Console.WriteLine("Overlay settings OK: persistence and bounds.");

var selectedOverlayArticle = new Article
{
    Title = "Вайтран",
    Category = "Локации",
    Content = "Город в центральной части Скайрима.",
    SourceUrl = "https://elderscrolls.fandom.com/ru/wiki/%D0%92%D0%B0%D0%B9%D1%82%D1%80%D0%B0%D0%BD"
};
var selectedOverlayContent = OverlayArticleContent.FromArticle(selectedOverlayArticle);
if (selectedOverlayContent.Title != selectedOverlayArticle.Title ||
    selectedOverlayContent.Category != selectedOverlayArticle.Category ||
    selectedOverlayContent.Text != selectedOverlayArticle.Content ||
    string.IsNullOrWhiteSpace(selectedOverlayContent.SourceUrl))
{
    throw new InvalidOperationException("Selected article was not projected for the overlay.");
}
var emptyOverlayContent = OverlayArticleContent.FromArticle(null);
if (emptyOverlayContent.SourceUrl is not null || emptyOverlayContent.Title != "Статья не выбрана")
    throw new InvalidOperationException("Overlay empty-state projection is invalid.");
var articleWithoutText = OverlayArticleContent.FromArticle(new Article
{
    Title = "Пустая статья",
    Content = " ",
    Summary = ""
});
if (articleWithoutText.Text != "Текст статьи отсутствует.")
    throw new InvalidOperationException("Overlay text fallback is invalid.");
var longText = new string('Л', 20_000);
if (OverlayArticleContent.FromArticle(new Article { Content = longText }).Text.Length != longText.Length)
    throw new InvalidOperationException("Overlay article projection truncated long text.");
Console.WriteLine("Overlay article projection OK: selected, empty, missing-text, and long-text states.");

var parameterService = new RpEventParameterService(new Random(20261010));
var fields = parameterService.CreateFields().ToList();
if (fields.Count != 8)
    throw new InvalidOperationException($"Expected 8 RP parameter fields, got {fields.Count}.");

foreach (var field in fields)
{
    field.Value = field.Options.Last();
    if (parameterService.Resolve(field) != field.Options.Last())
        throw new InvalidOperationException($"Preset value was not preserved for {field.Key}.");

    field.Value = $"Пользовательский ввод: {field.Key}";
    if (parameterService.Resolve(field) != field.Value || !field.IsCustomValue)
        throw new InvalidOperationException($"Custom value was not preserved for {field.Key}.");

    field.Value = string.Empty;
    var generated = parameterService.Resolve(field);
    if (string.IsNullOrWhiteSpace(generated) || generated == RpParameterField.RandomValue)
        throw new InvalidOperationException($"Random value was not generated for {field.Key}.");
    if (parameterService.Resolve(field) != generated)
        throw new InvalidOperationException($"Random value was not stable for {field.Key}.");
}

foreach (var field in fields.Where(field => field.IsOptional))
{
    field.Value = RpParameterField.UnspecifiedValue;
    if (parameterService.Resolve(field) != string.Empty)
        throw new InvalidOperationException($"Optional unset value was not retained for {field.Key}.");
}

foreach (var field in fields)
    field.Value = RpParameterField.RandomValue;
var beforeReroll = fields.ToDictionary(field => field.Key, parameterService.Resolve);
var rerolledField = fields[0];
parameterService.Reroll(rerolledField);
var afterReroll = fields.ToDictionary(field => field.Key, parameterService.Resolve);
if (afterReroll[rerolledField.Key] == beforeReroll[rerolledField.Key])
    throw new InvalidOperationException("Partial reroll did not change the selected parameter.");
if (fields.Skip(1).Any(field => afterReroll[field.Key] != beforeReroll[field.Key]))
    throw new InvalidOperationException("Partial reroll changed an unrelated parameter.");

Console.WriteLine("RP parameter fields OK: presets, custom input, optional unset, random mode, and partial reroll.");

var isolatedDatabaseDirectory = Path.Combine(Path.GetTempPath(), $"skyrim-rp-isolated-{Guid.NewGuid():N}");
var isolatedUserDatabase = Path.Combine(isolatedDatabaseDirectory, "user.db");
var isolatedOfficialDatabase = Path.Combine(isolatedDatabaseDirectory, "official.db");
var previousUserDatabase = Environment.GetEnvironmentVariable(DatabaseService.DatabasePathEnvironmentVariable);
var previousOfficialDatabase = Environment.GetEnvironmentVariable(OfficialDatabaseService.DatabasePathEnvironmentVariable);
try
{
    Environment.SetEnvironmentVariable(DatabaseService.DatabasePathEnvironmentVariable, isolatedUserDatabase);
    Environment.SetEnvironmentVariable(OfficialDatabaseService.DatabasePathEnvironmentVariable, isolatedOfficialDatabase);

    var isolatedUserService = new DatabaseService();
    var isolatedOfficialService = new OfficialDatabaseService();
    if (!Path.GetFullPath(isolatedUserDatabase).Equals(isolatedUserService.DatabasePath, StringComparison.OrdinalIgnoreCase) ||
        !Path.GetFullPath(isolatedOfficialDatabase).Equals(isolatedOfficialService.DatabasePath, StringComparison.OrdinalIgnoreCase) ||
        !File.Exists(isolatedUserDatabase) ||
        !File.Exists(isolatedOfficialDatabase))
    {
        throw new InvalidOperationException("Database services did not use the configured isolated paths.");
    }

    if (isolatedUserService.GetArticles().Count != 0 || isolatedOfficialService.GetArticles().Count == 0)
        throw new InvalidOperationException("Isolated databases were not initialized with their expected contents.");

    Console.WriteLine("Database path isolation OK: user and official services use temporary environment-configured files.");
}
finally
{
    Environment.SetEnvironmentVariable(DatabaseService.DatabasePathEnvironmentVariable, previousUserDatabase);
    Environment.SetEnvironmentVariable(OfficialDatabaseService.DatabasePathEnvironmentVariable, previousOfficialDatabase);
    SqliteConnection.ClearAllPools();
    if (Directory.Exists(isolatedDatabaseDirectory))
        Directory.Delete(isolatedDatabaseDirectory, recursive: true);
}

var disabled = new MvpKnowledgeDatabaseService(
    Path.Combine(Path.GetTempPath(), $"skyrim-rp-mvp-{Guid.NewGuid():N}.db"));
if (disabled.GetArticles().Count != 0)
    throw new InvalidOperationException("Adapter must stay inactive when its database path is absent.");

var service = new MvpKnowledgeDatabaseService(path);
DateTime lastWriteBefore = File.GetLastWriteTimeUtc(path);
var articles = service.GetArticles();
if (articles.Count != expectedMvpEntityCount)
    throw new InvalidOperationException($"Expected {expectedMvpEntityCount} MVP entities, got {articles.Count}.");
if (articles.Any(a => !a.IsMvp || !a.IsOfficial || a.Id < MvpKnowledgeDatabaseService.ArticleIdOffset))
    throw new InvalidOperationException("MVP article origin or collision-safe ID mapping is invalid.");
if (articles.Any(a => !MvpKnowledgeDatabaseService.TryGetSafeSourceUri(a.SourceUrl, out _)))
    throw new InvalidOperationException("An MVP article has no safe source URL.");
if (articles.Any(article => article.AdditionalCategories.Contains(article.Category, StringComparer.OrdinalIgnoreCase)) ||
    articles.Any(article => article.AdditionalCategories.Distinct(StringComparer.OrdinalIgnoreCase).Count() !=
                            article.AdditionalCategories.Count))
{
    throw new InvalidOperationException("MVP additional category projection must not duplicate categories.");
}

var loreArticles = articles.Where(article => ArticleSectionMatcher.Matches("Лор Skyrim", article)).ToList();
var religionArticles = articles.Where(article => ArticleSectionMatcher.Matches("Боги и даэдра", article)).ToList();
var locationArticles = articles.Where(article => ArticleSectionMatcher.Matches("Локации", article)).ToList();
Console.WriteLine($"MVP section candidates: lore={string.Join(" | ", loreArticles.Select(article => article.Title))}; godsAndDaedra={string.Join(" | ", religionArticles.Select(article => article.Title))}");
if (expectedMvpEntityCount == 125 &&
    (loreArticles.Count != 15 || religionArticles.Count != 6 || !religionArticles.Any(article => article.Title == "Талос") ||
    loreArticles.Select(article => article.Id).Distinct().Count() != loreArticles.Count ||
    religionArticles.Select(article => article.Id).Distinct().Count() != religionArticles.Count ||
    loreArticles.Any(article =>
        !article.Category.Equals("Lore", StringComparison.OrdinalIgnoreCase) &&
        !article.AdditionalCategories.Contains("Lore", StringComparer.OrdinalIgnoreCase)) ||
    religionArticles.Any(article => !article.AdditionalCategories.Any(category =>
        category.Equals("Божества", StringComparison.OrdinalIgnoreCase) ||
        category.Equals("Девять Божеств", StringComparison.OrdinalIgnoreCase) ||
        category.Equals("Князья даэдра", StringComparison.OrdinalIgnoreCase)))))
    throw new InvalidOperationException("MVP category projection for lore or religion is incorrect.");
if (expectedMvpEntityCount == 125 && locationArticles.Count != 19)
    throw new InvalidOperationException("Location section projection regressed.");
Console.WriteLine($"MVP section projection OK: lore={loreArticles.Count}, godsAndDaedra={religionArticles.Count}");

var graphTypeCounts = articles.GroupBy(ArticleGraphTypeClassifier.Classify)
    .ToDictionary(group => group.Key, group => group.Count());
if (!graphTypeCounts.TryGetValue(ArticleGraphType.Character, out var characterCount) || characterCount == 0 ||
    !graphTypeCounts.TryGetValue(ArticleGraphType.Place, out var placeCount) || placeCount == 0 ||
    !graphTypeCounts.TryGetValue(ArticleGraphType.Other, out var otherCount) || otherCount == 0)
{
    throw new InvalidOperationException("Graph type projection must expose characters, places, and other articles.");
}
if (graphTypeCounts.Values.Sum() != articles.Count)
    throw new InvalidOperationException("Graph type projection did not classify every MVP article.");
Console.WriteLine($"Graph type projection OK: characters={characterCount}, places={placeCount}, other={otherCount}");

var links = articles.SelectMany(a => service.GetLinksForArticle(a.Id)).ToList();
if (expectedMvpEntityCount == 125 && links.Select(l => l.Id).Distinct().Count() != 1513)
    throw new InvalidOperationException("Expected 1513 unique MVP relations.");
var ids = articles.Select(a => a.Id).ToHashSet();
if (links.Any(l => !ids.Contains(l.SourceArticleId) || !ids.Contains(l.TargetArticleId)))
    throw new InvalidOperationException("An MVP relation points outside the MVP article projection.");

var linkedArticle = articles.First(article => service.GetLinksForArticle(article.Id).Count > 0);
var relatedForOverlay = OverlayRelatedArticleResolver.Resolve(
    linkedArticle,
    service.GetLinksForArticle(linkedArticle.Id),
    articles);
if (relatedForOverlay.Count == 0 || relatedForOverlay.Any(related =>
        related.Article.Id == linkedArticle.Id ||
        !ids.Contains(related.Article.Id)))
{
    throw new InvalidOperationException("Overlay did not resolve real related MVP articles.");
}
if (OverlayRelatedArticleResolver.Resolve(linkedArticle, Array.Empty<ArticleLink>(), articles).Count != 0)
    throw new InvalidOperationException("Overlay empty related-article state is invalid.");
Console.WriteLine($"Overlay related articles OK: source={linkedArticle.Title}, count={relatedForOverlay.Count}.");

if (File.GetLastWriteTimeUtc(path) != lastWriteBefore)
    throw new InvalidOperationException("Read-only adapter unexpectedly changed the MVP database file.");

Console.WriteLine($"MVP adapter OK: articles={articles.Count}, uniqueRelations={links.Select(l => l.Id).Distinct().Count()}");

string? knowledgeDatabasePath = Environment.GetEnvironmentVariable("SKYRIM_RP_TEST_KNOWLEDGE_DB");
if (!string.IsNullOrWhiteSpace(knowledgeDatabasePath))
{
    if (!File.Exists(knowledgeDatabasePath))
        throw new InvalidOperationException("SKYRIM_RP_TEST_KNOWLEDGE_DB does not exist.");

    DateTime knowledgeLastWriteBefore = File.GetLastWriteTimeUtc(knowledgeDatabasePath);
    var knowledgeService = new MvpKnowledgeDatabaseService(knowledgeDatabasePath);
    var knowledgeArticles = knowledgeService.GetArticles();
    if (knowledgeArticles.Count == 0 || knowledgeArticles.Any(article => !article.IsMvp || !article.IsOfficial))
        throw new InvalidOperationException("Knowledge DB was not projected as read-only imported articles.");
    var realKnowledgeSearch = KnowledgeAssistantService.FindRelevantArticles("Кто такой Абелон?", knowledgeArticles);
    if (realKnowledgeSearch.Count == 0 ||
        !realKnowledgeSearch.Any(source => source.Title == "Абелон") ||
        realKnowledgeSearch.Any(source => !MvpKnowledgeDatabaseService.TryGetSafeSourceUri(source.SourceUrl, out _)))
    {
        throw new InvalidOperationException("Knowledge assistant could not retrieve attributable articles from the supplied Knowledge DB.");
    }
    if (File.GetLastWriteTimeUtc(knowledgeDatabasePath) != knowledgeLastWriteBefore)
        throw new InvalidOperationException("Read-only Knowledge DB adapter unexpectedly changed the database file.");

    var realScenarioCatalog = new ScenarioKnowledgeCatalog(knowledgeDatabasePath);
    var realCatalogStatus = realScenarioCatalog.GetStatus();
    var realCatalogCandidates = realScenarioCatalog.Search(new ScenarioKnowledgeSearchRequest { SemanticType = "NPC", Limit = 3 });
    if (!realCatalogStatus.IsCompatible || !realCatalogCandidates.Status.IsCompatible || realCatalogCandidates.Candidates.Count == 0 ||
        realCatalogCandidates.Candidates.Any(candidate => candidate.EntityId <= 0 || string.IsNullOrWhiteSpace(candidate.Title) || string.IsNullOrWhiteSpace(candidate.SourceUrl)))
    {
        throw new InvalidOperationException("Scenario knowledge catalog could not safely query the supplied Knowledge DB.");
    }
    if (File.GetLastWriteTimeUtc(knowledgeDatabasePath) != knowledgeLastWriteBefore)
        throw new InvalidOperationException("Scenario knowledge catalog unexpectedly changed the Knowledge DB.");
    var realEvidence = realScenarioCatalog.GetEvidence(realCatalogCandidates.Candidates[0].EntityId, 800);
    if (realEvidence.Evidence is null || realEvidence.Evidence.EntityId != realCatalogCandidates.Candidates[0].EntityId ||
        realEvidence.Evidence.Fragment.Length > 800 || realEvidence.Evidence.NeedsReview)
        throw new InvalidOperationException("Scenario evidence API could not read a verified real source fragment.");

    Console.WriteLine($"Knowledge DB read-only adapter OK: articles={knowledgeArticles.Count}.");
}

var scenarioCatalogDirectory = Path.Combine(Path.GetTempPath(), $"skyrim-rp-scenario-catalog-{Guid.NewGuid():N}");
ArticleCatalogTests.Run();
ArticleCatalogTests.MeasureKnowledge(path);
ScenarioKnowledgeEvidenceTests.Run();
var scenarioCatalogPath = Path.Combine(scenarioCatalogDirectory, "knowledge.db");
Directory.CreateDirectory(scenarioCatalogDirectory);
try
{
    using (var catalogConnection = new SqliteConnection($"Data Source={scenarioCatalogPath}"))
    {
        catalogConnection.Open();
        using var command = catalogConnection.CreateCommand();
        command.CommandText = """
            CREATE TABLE BuildInfo(Key TEXT PRIMARY KEY, Value TEXT);
            CREATE TABLE Entities(Id INTEGER PRIMARY KEY, TitleRu TEXT, Url TEXT, SemanticType TEXT, PrimaryCategoryRu TEXT,
                                  SummaryRu TEXT, ContentRu TEXT, Confidence REAL, NeedsReview INTEGER);
            CREATE TABLE Categories(Id INTEGER PRIMARY KEY, TitleRu TEXT);
            CREATE TABLE EntityCategories(EntityId INTEGER, CategoryId INTEGER);
            CREATE TABLE Sources(Id INTEGER PRIMARY KEY, EntityId INTEGER, SourceUrl TEXT);
            INSERT INTO BuildInfo VALUES('SchemaVersion', '2.6');
            INSERT INTO Entities VALUES(1, 'Альфа', 'https://example.test/alpha', 'NPC', 'Персонажи', 'Проверенный NPC.', '', 0.95, 0);
            INSERT INTO Entities VALUES(2, 'Бета', 'https://example.test/beta', 'NPC', 'Персонажи', 'Требует проверки.', '', 0.40, 1);
            INSERT INTO Entities VALUES(3, 'Гамма', 'https://example.test/gamma', 'Place', 'Локации', '', 'Текст локации.', 0.90, 0);
            INSERT INTO Categories VALUES(10, 'Персонажи');
            INSERT INTO Categories VALUES(11, 'Локации');
            INSERT INTO EntityCategories VALUES(1, 10);
            INSERT INTO EntityCategories VALUES(2, 10);
            INSERT INTO EntityCategories VALUES(3, 11);
            INSERT INTO Sources VALUES(1, 1, 'https://example.test/source-alpha');
            INSERT INTO Sources VALUES(2, 2, 'https://example.test/source-beta');
            INSERT INTO Sources VALUES(3, 3, 'https://example.test/source-gamma');
            """;
        command.ExecuteNonQuery();
    }
    SqliteConnection.ClearAllPools();
    var scenarioCatalogLastWrite = File.GetLastWriteTimeUtc(scenarioCatalogPath);
    IScenarioKnowledgeCatalog scenarioCatalog = new ScenarioKnowledgeCatalog(scenarioCatalogPath);
    if (scenarioCatalog.GetEvidence(1).Evidence is not { SourceId: 1, RevisionTimestamp: "" })
        throw new InvalidOperationException("Evidence API does not support compatible schemas without optional source metadata.");
    if (!scenarioCatalog.GetStatus().IsCompatible)
        throw new InvalidOperationException("Scenario knowledge catalog rejected a compatible isolated Knowledge DB.");
    var npcCandidates = scenarioCatalog.Search(new ScenarioKnowledgeSearchRequest { SemanticType = "NPC", Limit = 12 });
    if (npcCandidates.Candidates.Count != 1 || npcCandidates.Candidates[0].EntityId != 1 ||
        npcCandidates.Candidates[0].SourceUrl != "https://example.test/source-alpha" ||
        npcCandidates.Candidates[0].NeedsReview || npcCandidates.Candidates[0].Categories.Single() != "Персонажи")
    {
        throw new InvalidOperationException("Scenario knowledge catalog did not preserve verified NPC provenance.");
    }
    var reviewCandidates = scenarioCatalog.Search(new ScenarioKnowledgeSearchRequest { SemanticType = "NPC", IncludeNeedsReview = true, Limit = 1 });
    if (reviewCandidates.Candidates.Count != 1 || reviewCandidates.Candidates[0].Title != "Альфа")
        throw new InvalidOperationException("Scenario knowledge catalog result ordering or limit is unstable.");
    var allNpcCandidates = scenarioCatalog.Search(new ScenarioKnowledgeSearchRequest { SemanticType = "NPC", IncludeNeedsReview = true });
    if (allNpcCandidates.Candidates.Select(candidate => candidate.Title).SequenceEqual(["Альфа", "Бета"]) == false ||
        !allNpcCandidates.Candidates[1].NeedsReview)
    {
        throw new InvalidOperationException("Scenario knowledge catalog did not expose NeedsReview candidates explicitly.");
    }
    var categoryCandidates = scenarioCatalog.Search(new ScenarioKnowledgeSearchRequest { Category = "Локации" });
    var titleCandidates = scenarioCatalog.Search(new ScenarioKnowledgeSearchRequest { Title = "льф" });
    if (categoryCandidates.Candidates.SingleOrDefault()?.EntityId != 3 || titleCandidates.Candidates.SingleOrDefault()?.EntityId != 1 ||
        scenarioCatalog.Search(new ScenarioKnowledgeSearchRequest { Title = "Несуществующий" }).Candidates.Count != 0)
    {
        throw new InvalidOperationException("Scenario knowledge catalog type, category, title, or empty-result search is invalid.");
    }
    if (File.GetLastWriteTimeUtc(scenarioCatalogPath) != scenarioCatalogLastWrite)
        throw new InvalidOperationException("Scenario knowledge catalog unexpectedly changed its source database.");
    var missingCatalog = new ScenarioKnowledgeCatalog(Path.Combine(scenarioCatalogDirectory, "missing.db"));
    if (missingCatalog.GetStatus().IsAvailable || missingCatalog.Search(new ScenarioKnowledgeSearchRequest()).Candidates.Count != 0)
        throw new InvalidOperationException("Scenario knowledge catalog did not handle a missing database safely.");
    var incompatiblePath = Path.Combine(scenarioCatalogDirectory, "incompatible.db");
    using (var incompatibleConnection = new SqliteConnection($"Data Source={incompatiblePath}"))
    {
        incompatibleConnection.Open();
        using var command = incompatibleConnection.CreateCommand();
        command.CommandText = "CREATE TABLE BuildInfo(Key TEXT PRIMARY KEY, Value TEXT); INSERT INTO BuildInfo VALUES('SchemaVersion', '2.5');";
        command.ExecuteNonQuery();
    }
    SqliteConnection.ClearAllPools();
    var incompatibleCatalog = new ScenarioKnowledgeCatalog(incompatiblePath);
    if (incompatibleCatalog.GetStatus().IsCompatible || incompatibleCatalog.Search(new ScenarioKnowledgeSearchRequest()).Candidates.Count != 0)
        throw new InvalidOperationException("Scenario knowledge catalog accepted an incompatible schema.");
    var unreadablePath = Path.Combine(scenarioCatalogDirectory, "unreadable.db");
    File.WriteAllText(unreadablePath, "not a sqlite database");
    var unreadableCatalog = new ScenarioKnowledgeCatalog(unreadablePath);
    if (unreadableCatalog.GetStatus().IsCompatible || unreadableCatalog.Search(new ScenarioKnowledgeSearchRequest()).Candidates.Count != 0)
        throw new InvalidOperationException("Scenario knowledge catalog did not handle a read error safely.");
    Console.WriteLine("Scenario knowledge catalog OK: read-only preflight, targeted search, provenance, review filtering, and safe failures.");
}
finally
{
    SqliteConnection.ClearAllPools();
    if (Directory.Exists(scenarioCatalogDirectory))
        Directory.Delete(scenarioCatalogDirectory, recursive: true);
}

var selectorCandidates = new[]
{
    new ScenarioKnowledgeCandidate { EntityId = 1, Title = "Альфа-персонаж", SemanticType = "NPC", PrimaryCategory = "Персонажи (Skyrim)", Categories = ["Персонажи (Skyrim)"], SourceUrl = "https://example.test/npc", ContextExcerpt = "Канонический фрагмент NPC.", Confidence = 0.95 },
    new ScenarioKnowledgeCandidate { EntityId = 2, Title = "Бета-проверка", SemanticType = "NPC", PrimaryCategory = "Персонажи (Skyrim)", Categories = ["Персонажи (Skyrim)"], SourceUrl = "https://example.test/review", ContextExcerpt = "Не использовать без проверки.", Confidence = 0.2, NeedsReview = true },
    new ScenarioKnowledgeCandidate { EntityId = 3, Title = "Гильдия", SemanticType = "Faction", PrimaryCategory = "Фракции (Skyrim)", Categories = ["Фракции (Skyrim)"], SourceUrl = "https://example.test/faction", ContextExcerpt = "Канонический фрагмент фракции.", Confidence = 0.9 },
    new ScenarioKnowledgeCandidate { EntityId = 4, Title = "Древний свиток", SemanticType = "Book", PrimaryCategory = "Квестовые предметы (Skyrim)", Categories = ["Квестовые предметы (Skyrim)"], SourceUrl = "https://example.test/book", ContextExcerpt = "Канонический фрагмент книги.", Confidence = 0.9 },
    new ScenarioKnowledgeCandidate { EntityId = 5, Title = "Еловый посох", SemanticType = "Item", PrimaryCategory = "Предметы (Skyrim)", Categories = ["Предметы (Skyrim)"], SourceUrl = "https://example.test/item", ContextExcerpt = "Канонический фрагмент предмета.", Confidence = 0.9 },
    new ScenarioKnowledgeCandidate { EntityId = 6, Title = "Железный меч", SemanticType = "Weapon", PrimaryCategory = "Оружие (Skyrim)", Categories = ["Оружие (Skyrim)"], SourceUrl = "https://example.test/weapon", ContextExcerpt = "Канонический фрагмент оружия.", Confidence = 0.9 },
    new ScenarioKnowledgeCandidate { EntityId = 7, Title = "Застава", SemanticType = "Place", PrimaryCategory = "Локации (Skyrim)", Categories = ["Локации (Skyrim)"], SourceUrl = "https://example.test/place", ContextExcerpt = "Канонический фрагмент локации.", Confidence = 0.9 },
    new ScenarioKnowledgeCandidate { EntityId = 8, Title = "Испытание", SemanticType = "Quest", PrimaryCategory = "Квесты (Skyrim)", Categories = ["Квесты (Skyrim)"], SourceUrl = "https://example.test/quest", ContextExcerpt = "Канонический фрагмент квеста.", Confidence = 0.9 },
    new ScenarioKnowledgeCandidate { EntityId = 9, Title = "Кожаный доспех", SemanticType = "Armor", PrimaryCategory = "Предметы (Skyrim)", Categories = ["Предметы (Skyrim)"], SourceUrl = "https://example.test/armor", ContextExcerpt = "Канонический фрагмент доспеха.", Confidence = 0.9 },
    new ScenarioKnowledgeCandidate { EntityId = 10, Title = "Лидия", SemanticType = "NPC", PrimaryCategory = "Персонажи (Skyrim)", Categories = ["Персонажи (Skyrim)"], SourceUrl = "https://example.test/lydia", ContextExcerpt = "Канонический фрагмент NPC.", Confidence = 0.9 },
    new ScenarioKnowledgeCandidate { EntityId = 11, Title = "Мьол Львица", SemanticType = "NPC", PrimaryCategory = "Персонажи (Skyrim)", Categories = ["Персонажи (Skyrim)"], SourceUrl = "https://example.test/mjoll", ContextExcerpt = "Канонический фрагмент NPC.", Confidence = 0.9 },
    new ScenarioKnowledgeCandidate { EntityId = 12, Title = "Назим", SemanticType = "NPC", PrimaryCategory = "Персонажи (Skyrim)", Categories = ["Персонажи (Skyrim)"], SourceUrl = "https://example.test/nazeem", ContextExcerpt = "Канонический фрагмент NPC.", Confidence = 0.9 },
    new ScenarioKnowledgeCandidate { EntityId = 13, Title = "Орден", SemanticType = "Faction", PrimaryCategory = "Фракции (Skyrim)", Categories = ["Фракции (Skyrim)"], SourceUrl = "https://example.test/order", ContextExcerpt = "Канонический фрагмент фракции.", Confidence = 0.9 },
    new ScenarioKnowledgeCandidate { EntityId = 14, Title = "Пещера", SemanticType = "Place", PrimaryCategory = "Локации (Skyrim)", Categories = ["Локации (Skyrim)"], SourceUrl = "https://example.test/cave", ContextExcerpt = "Канонический фрагмент локации.", Confidence = 0.9 }
};
var recordingCatalog = new RecordingScenarioKnowledgeCatalog(selectorCandidates);
var anchorSelector = new ScenarioCanonicalAnchorSelector(recordingCatalog);
var expectedTemplateIds = new[] { "negotiation", "investigation", "mission", "intrigue", "conflict" };
if (!ScenarioCanonicalAnchorSelector.Rules.Select(rule => rule.TemplateId).OrderBy(id => id).SequenceEqual(expectedTemplateIds.OrderBy(id => id)))
    throw new InvalidOperationException("Canonical anchor selector does not define rules for every scenario template.");
foreach (var templateId in expectedTemplateIds)
{
    var selection = anchorSelector.FindCandidates(templateId);
    if (!selection.CatalogStatus.IsCompatible || selection.Anchors.Count == 0 ||
        selection.Anchors.Any(anchor => anchor.Status != ScenarioCanonicalAnchorStatus.Candidate || anchor.Origin != ScenarioFactOrigin.CanonicalKnowledge || anchor.NeedsReview || string.IsNullOrWhiteSpace(anchor.Purpose) || string.IsNullOrWhiteSpace(anchor.SourceUrl)) ||
        selection.Anchors.Select(anchor => anchor.EntityId).Distinct().Count() != selection.Anchors.Count)
    {
        throw new InvalidOperationException($"Canonical anchor selection is invalid for template {templateId}.");
    }
    var replay = anchorSelector.FindCandidates(templateId);
    if (!selection.Anchors.Select(anchor => $"{anchor.EntityId}/{anchor.Purpose}").SequenceEqual(replay.Anchors.Select(anchor => $"{anchor.EntityId}/{anchor.Purpose}")))
        throw new InvalidOperationException($"Canonical anchor selection is not deterministic for template {templateId}.");
}
if (!recordingCatalog.Requests.Any(request => !string.IsNullOrWhiteSpace(request.Category)) ||
    recordingCatalog.Requests.Any(request => request.IncludeNeedsReview))
{
    throw new InvalidOperationException("Canonical anchor selector did not apply category preferences or review exclusion explicitly.");
}
var canonicalAnchor = anchorSelector.FindCandidates("conflict").Anchors.First();
if (canonicalAnchor.EntityId <= 0 || canonicalAnchor.SourceUrl != selectorCandidates.Single(candidate => candidate.EntityId == canonicalAnchor.EntityId).SourceUrl ||
    canonicalAnchor.ContextExcerpt.Contains("мотив", StringComparison.OrdinalIgnoreCase))
{
    throw new InvalidOperationException("Canonical provenance was not preserved or fictionalized by the selector.");
}
var emptyAnchorSelection = new ScenarioCanonicalAnchorSelector(new RecordingScenarioKnowledgeCatalog([])).FindCandidates("investigation");
if (emptyAnchorSelection.Anchors.Count != 0 || !emptyAnchorSelection.Diagnostic.Contains("нет подходящих", StringComparison.OrdinalIgnoreCase))
    throw new InvalidOperationException("Canonical anchor selector did not report an empty candidate set safely.");
var singleAnchorCandidate = selectorCandidates.Single(candidate => candidate.EntityId == 1);
var singleAnchorSelector = new ScenarioCanonicalAnchorSelector(new RecordingScenarioKnowledgeCatalog([singleAnchorCandidate]));
var singleSuggestion = singleAnchorSelector.SuggestCandidates("conflict", 17);
var singleSuggestionReplay = singleAnchorSelector.SuggestCandidates("conflict", 99);
if (singleSuggestion.Anchors.Count != 1 || singleSuggestion.Anchors[0].EntityId != singleAnchorCandidate.EntityId ||
    !singleSuggestion.Anchors.Select(anchor => anchor.EntityId).SequenceEqual(singleSuggestionReplay.Anchors.Select(anchor => anchor.EntityId)))
{
    throw new InvalidOperationException("Canonical anchor selector did not handle a singleton compatible pool predictably.");
}
var unavailableAnchorSelection = new ScenarioCanonicalAnchorSelector(new RecordingScenarioKnowledgeCatalog([], compatible: false)).FindCandidates("conflict");
if (unavailableAnchorSelection.Anchors.Count != 0 || unavailableAnchorSelection.CatalogStatus.IsCompatible ||
    new ScenarioGeneratorService().Generate(new ScenarioGenerationOptions { Genre = "Личный конфликт", ParticipantCount = 4, DurationMinutes = 90, Seed = 42 }).Scenario is null)
{
    throw new InvalidOperationException("Unavailable catalog affected autonomous scenario generation.");
}
var suggestionOne = anchorSelector.SuggestCandidates("conflict", 17);
var suggestionReplay = anchorSelector.SuggestCandidates("conflict", 17);
var suggestionSignature = suggestionOne.Anchors.Select(anchor => $"{anchor.EntityId}/{anchor.Purpose}").ToArray();
if (suggestionOne.Anchors.Any(anchor => anchor.Status != ScenarioCanonicalAnchorStatus.Proposed) ||
    !suggestionSignature.SequenceEqual(suggestionReplay.Anchors.Select(anchor => $"{anchor.EntityId}/{anchor.Purpose}")))
{
    throw new InvalidOperationException("Seeded canonical proposals are not reproducible or are incorrectly confirmed.");
}
var distinctSuggestion = Enumerable.Range(18, 64)
    .Select(seed => anchorSelector.SuggestCandidates("conflict", seed))
    .FirstOrDefault(result => !suggestionSignature.SequenceEqual(result.Anchors.Select(anchor => $"{anchor.EntityId}/{anchor.Purpose}")));
if (distinctSuggestion is null)
    throw new InvalidOperationException("Different seeds did not vary a sufficiently large compatible candidate set.");
var reorderedSelector = new ScenarioCanonicalAnchorSelector(new RecordingScenarioKnowledgeCatalog(selectorCandidates.Reverse().ToArray()));
if (!suggestionSignature.SequenceEqual(reorderedSelector.SuggestCandidates("conflict", 17).Anchors.Select(anchor => $"{anchor.EntityId}/{anchor.Purpose}")))
    throw new InvalidOperationException("Seeded canonical proposals depend on source result order.");
var autonomousConflict = new ScenarioGeneratorService().Generate(new ScenarioGenerationOptions { Genre = "Личный конфликт", ParticipantCount = 4, DurationMinutes = 120, Difficulty = "Высокая", Seed = 991 }).Scenario!;
var selectedAnchor = ScenarioCanonicalAnchorSelector.WithStatus(suggestionOne.Anchors[0], ScenarioCanonicalAnchorStatus.Selected);
var rejectedAnchor = ScenarioCanonicalAnchorSelector.WithStatus(suggestionOne.Anchors[1], ScenarioCanonicalAnchorStatus.Rejected);
var fictionalAnchor = new ScenarioCanonicalAnchor { EntityId = 999, Title = "Фабричная деталь", Status = ScenarioCanonicalAnchorStatus.Selected, Origin = ScenarioFactOrigin.FactoryFiction };
var anchoredConflict = ScenarioCanonicalAnchorApplicator.Apply(autonomousConflict, [selectedAnchor, rejectedAnchor, fictionalAnchor]);
if (anchoredConflict.CanonicalAnchors.Count != 1 || anchoredConflict.CanonicalAnchors[0].EntityId != selectedAnchor.EntityId ||
    anchoredConflict.CanonicalAnchors[0].SourceUrl != selectedAnchor.SourceUrl ||
    anchoredConflict.CanonicalAnchors[0].Status != ScenarioCanonicalAnchorStatus.Selected ||
    anchoredConflict.Scenes.Select(scene => scene.Id).SequenceEqual(autonomousConflict.Scenes.Select(scene => scene.Id)) == false ||
    anchoredConflict.Branches.Select(branch => $"{branch.FromSceneId}/{branch.ToSceneId}/{branch.ToEndingId}").SequenceEqual(autonomousConflict.Branches.Select(branch => $"{branch.FromSceneId}/{branch.ToSceneId}/{branch.ToEndingId}")) == false ||
    anchoredConflict.Endings.Select(ending => ending.Id).SequenceEqual(autonomousConflict.Endings.Select(ending => ending.Id)) == false)
{
    throw new InvalidOperationException("Applying canonical anchors changed factory drama or retained an unconfirmed/non-canonical anchor.");
}
if (ScenarioCanonicalAnchorApplicator.Apply(autonomousConflict, [rejectedAnchor]).CanonicalAnchors.Count != 0)
    throw new InvalidOperationException("Unconfirmed canonical anchors were applied to a scenario.");
var formattedScenario = ScenarioTextFormatter.FormatScenario(anchoredConflict);
if (!formattedScenario.Contains("Канонические справочные опоры:", StringComparison.Ordinal) ||
    !formattedScenario.Contains(selectedAnchor.SourceUrl, StringComparison.Ordinal) ||
    !formattedScenario.Contains("Сцены:", StringComparison.Ordinal) ||
    !formattedScenario.Contains("Финалы:", StringComparison.Ordinal))
{
    throw new InvalidOperationException("Copy-friendly scenario text lost headings, anchors, or structure.");
}
Console.WriteLine("Scenario canonical selector OK: five template rules, provenance, deterministic limits, review exclusion, and autonomous fallback.");

var scenarioExportDirectory = Path.Combine(Path.GetTempPath(), $"skyrim-rp-scenario-export-{Guid.NewGuid():N}");
Directory.CreateDirectory(scenarioExportDirectory);
try
{
    var scenarioExportPath = Path.Combine(scenarioExportDirectory, "confirmed-scenario.json");
    var scenarioExportService = new ScenarioExportService();
    var needsReviewAnchor = new ScenarioCanonicalAnchor
    {
        EntityId = 998,
        Title = "Требует проверки",
        SemanticType = "NPC",
        Categories = ["Персонажи"],
        SourceUrl = "https://example.invalid/review",
        ContextExcerpt = "Не должен быть экспортирован.",
        NeedsReview = true,
        Confidence = 0.5,
        Purpose = "Тест",
        Status = ScenarioCanonicalAnchorStatus.Selected,
        Origin = ScenarioFactOrigin.CanonicalKnowledge
    };
    var exportScenario = ScenarioCanonicalAnchorApplicator.Apply(anchoredConflict, [selectedAnchor, needsReviewAnchor]);
    var savedScenario = scenarioExportService.Save(scenarioExportPath, exportScenario);
    if (!savedScenario.IsSuccess || !File.Exists(scenarioExportPath))
        throw new InvalidOperationException("Confirmed scenario could not be exported.");

    var loadedScenario = scenarioExportService.Load(scenarioExportPath);
    if (!loadedScenario.IsSuccess || loadedScenario.Scenario is null)
        throw new InvalidOperationException("Confirmed scenario could not be restored.");
    var restoredAnchor = loadedScenario.Scenario.CanonicalAnchors.SingleOrDefault();
    if (restoredAnchor is null || restoredAnchor.EntityId != selectedAnchor.EntityId ||
        restoredAnchor.Title != selectedAnchor.Title || restoredAnchor.SemanticType != selectedAnchor.SemanticType ||
        !restoredAnchor.Categories.SequenceEqual(selectedAnchor.Categories) || restoredAnchor.SourceUrl != selectedAnchor.SourceUrl ||
        restoredAnchor.ContextExcerpt != selectedAnchor.ContextExcerpt || restoredAnchor.Confidence != selectedAnchor.Confidence ||
        restoredAnchor.NeedsReview || restoredAnchor.Purpose != selectedAnchor.Purpose ||
        restoredAnchor.Origin != ScenarioFactOrigin.CanonicalKnowledge || restoredAnchor.Status != ScenarioCanonicalAnchorStatus.Selected)
    {
        throw new InvalidOperationException("Scenario export did not preserve confirmed canonical anchor provenance.");
    }
    if (!loadedScenario.Scenario.Scenes.Select(scene => scene.Id).SequenceEqual(exportScenario.Scenes.Select(scene => scene.Id)) ||
        !loadedScenario.Scenario.Clues.Select(clue => clue.Id).SequenceEqual(exportScenario.Clues.Select(clue => clue.Id)) ||
        !loadedScenario.Scenario.Branches.Select(branch => $"{branch.FromSceneId}/{branch.ToSceneId}/{branch.ToEndingId}").SequenceEqual(exportScenario.Branches.Select(branch => $"{branch.FromSceneId}/{branch.ToSceneId}/{branch.ToEndingId}")) ||
        !loadedScenario.Scenario.Characters.Select(role => role.Name).SequenceEqual(exportScenario.Characters.Select(role => role.Name)) ||
        !loadedScenario.Scenario.Endings.Select(ending => ending.Id).SequenceEqual(exportScenario.Endings.Select(ending => ending.Id)))
    {
        throw new InvalidOperationException("Scenario export altered the factory graph.");
    }
    if (ScenarioCanonicalAnchorApplicator.Apply(exportScenario, [selectedAnchor]).CanonicalAnchors.Count != 1)
        throw new InvalidOperationException("Repeated application duplicated a canonical anchor.");

    var legacyPath = Path.Combine(scenarioExportDirectory, "legacy-scenario.json");
    var legacyJson = JsonNode.Parse(File.ReadAllText(scenarioExportPath))!.AsObject();
    legacyJson.Remove("CanonicalAnchors");
    File.WriteAllText(legacyPath, legacyJson.ToJsonString());
    var legacyScenario = scenarioExportService.Load(legacyPath);
    if (!legacyScenario.IsSuccess || legacyScenario.Scenario is null || legacyScenario.Scenario.CanonicalAnchors.Count != 0)
        throw new InvalidOperationException("Legacy scenario without CanonicalAnchors did not open safely.");

    var malformedPath = Path.Combine(scenarioExportDirectory, "malformed-scenario.json");
    File.WriteAllText(malformedPath, "{ not valid JSON");
    if (scenarioExportService.Load(malformedPath).IsSuccess)
        throw new InvalidOperationException("Malformed scenario export was accepted.");

    var incompletePath = Path.Combine(scenarioExportDirectory, "incomplete-scenario.json");
    File.WriteAllText(incompletePath, "{}");
    if (scenarioExportService.Load(incompletePath).IsSuccess)
        throw new InvalidOperationException("Incomplete scenario export was accepted.");

    var unavailableCatalogForExport = new RecordingScenarioKnowledgeCatalog(Array.Empty<ScenarioKnowledgeCandidate>(), compatible: false);
    if (unavailableCatalogForExport.GetStatus().IsCompatible)
        throw new InvalidOperationException("Unavailable catalog fixture is unexpectedly compatible.");
    var offlineExportPath = Path.Combine(scenarioExportDirectory, "offline-scenario.json");
    var offlineSave = scenarioExportService.Save(offlineExportPath, loadedScenario.Scenario);
    var offlineLoad = scenarioExportService.Load(offlineExportPath);
    if (!offlineSave.IsSuccess || !offlineLoad.IsSuccess || offlineLoad.Scenario is null ||
        offlineLoad.Scenario.CanonicalAnchors.Single().SourceUrl != selectedAnchor.SourceUrl)
    {
        throw new InvalidOperationException("Scenario export or restore unexpectedly depended on Knowledge DB availability.");
    }
}
finally
{
    if (Directory.Exists(scenarioExportDirectory))
        Directory.Delete(scenarioExportDirectory, true);
}
Console.WriteLine("Scenario export OK: round-trip, provenance, legacy compatibility, duplicate prevention, and safe failures.");

var assistantArticles = new List<Article>
{
    new()
    {
        Id = MvpKnowledgeDatabaseService.ArticleIdOffset + 1,
        IsMvp = true,
        Title = "Абелон",
        Category = "Персонажи",
        Summary = "Житель Данстара.",
        Content = "Абелон живёт в Данстаре.",
        SourceUrl = "https://elderscrolls.fandom.com/ru/wiki/%D0%90%D0%B1%D0%B5%D0%BB%D0%BE%D0%BD"
    },
    new()
    {
        Id = MvpKnowledgeDatabaseService.ArticleIdOffset + 2,
        IsMvp = true,
        Title = "Несвязанная статья",
        Category = "Предметы",
        Content = "Этот текст описывает учебный предмет.",
        SourceUrl = "https://elderscrolls.fandom.com/ru/wiki/%D0%9F%D1%80%D0%B5%D0%B4%D0%BC%D0%B5%D1%82"
    }
};
var recordingModel = new RecordingKnowledgeAssistantModel("Абелон — житель Данстара.");
var assistant = new KnowledgeAssistantService(recordingModel);
var assistantResult = await assistant.AskAsync("Кто такой Абелон?", assistantArticles);
if (assistantResult.IsInsufficientContext || assistantResult.IsModelUnavailable ||
    assistantResult.Sources.Count != 1 || assistantResult.Sources[0].Title != "Абелон" ||
    assistantResult.Sources[0].SourceUrl != assistantArticles[0].SourceUrl ||
    recordingModel.Prompts.Count != 1 ||
    !recordingModel.Prompts[0].Contains(assistantArticles[0].SourceUrl, StringComparison.Ordinal) ||
    recordingModel.Prompts[0].Contains("Несвязанная статья", StringComparison.Ordinal))
{
    throw new InvalidOperationException("Knowledge assistant did not restrict context to relevant, attributable articles.");
}

var noContextResult = await assistant.AskAsync("Несуществующий термин qzxv", assistantArticles);
if (!noContextResult.IsInsufficientContext || noContextResult.Sources.Count != 0 || recordingModel.Prompts.Count != 1)
    throw new InvalidOperationException("Knowledge assistant called a model without sufficient local context.");

var unavailableAssistant = new KnowledgeAssistantService(
    new RecordingKnowledgeAssistantModel(null, new HttpRequestException("Ollama unavailable")));
var unavailableResult = await unavailableAssistant.AskAsync("Кто такой Абелон?", assistantArticles);
if (!unavailableResult.IsModelUnavailable || unavailableResult.Sources.Count != 1)
    throw new InvalidOperationException("Knowledge assistant did not handle unavailable Ollama safely.");

Console.WriteLine("Knowledge assistant OK: local retrieval, attributable context, insufficient-context and Ollama-unavailable paths.");

var scenarioGenerator = new ScenarioGeneratorService();
foreach (var template in scenarioGenerator.GetTemplates())
{
    var generated = scenarioGenerator.Generate(new ScenarioGenerationOptions
    {
        Genre = template.Genre,
        ParticipantCount = template.MinParticipants,
        DurationMinutes = template.MinDurationMinutes,
        Tone = "Напряжённый",
        Difficulty = "Средняя",
        Seed = 42000 + template.MinParticipants,
        AllowMandatoryCombat = false,
    });
    if (generated.Scenario is null || !generated.Validation.IsValid || generated.Scenario.Template.Id != template.Id)
        throw new InvalidOperationException($"Scenario template {template.Id} did not generate a valid scenario.");
}
var deterministicOptions = new ScenarioGenerationOptions { Genre = "Переговоры", ParticipantCount = 3, DurationMinutes = 90, Tone = "Напряжённый", Difficulty = "Средняя", Seed = 314159 };
var firstScenario = scenarioGenerator.Generate(deterministicOptions).Scenario!;
var secondScenario = scenarioGenerator.Generate(deterministicOptions).Scenario!;
if (firstScenario.Title != secondScenario.Title || firstScenario.Hook != secondScenario.Hook || firstScenario.GmNotes.SequenceEqual(secondScenario.GmNotes) == false || firstScenario.Characters.Select(role => role.Secret).SequenceEqual(secondScenario.Characters.Select(role => role.Secret)) == false || firstScenario.Branches.Select(branch => $"{branch.FromSceneId}/{branch.ToSceneId}/{branch.ToEndingId}/{branch.Condition}/{string.Join(",", branch.RequiredClueIds)}").SequenceEqual(secondScenario.Branches.Select(branch => $"{branch.FromSceneId}/{branch.ToSceneId}/{branch.ToEndingId}/{branch.Condition}/{string.Join(",", branch.RequiredClueIds)}")) == false)
    throw new InvalidOperationException("Fixed seed did not reproduce the scenario.");

var wagonOptions = new ScenarioGenerationOptions { Genre = "Переговоры", ParticipantCount = 4, DurationMinutes = 120, Tone = "Мрачный", Difficulty = "Высокая", Seed = 7788 };
var wagon = scenarioGenerator.GenerateLegacyNegotiation(wagonOptions).Scenario!;
if (!ScenarioValidator.Validate(wagon).IsValid || wagon.Endings.Count < 3 || wagon.Branches.Count(branch => !string.IsNullOrWhiteSpace(branch.ToEndingId)) < 3)
    throw new InvalidOperationException("Last Wagon does not contain multiple graph-linked endings.");
if (wagon.Characters.Count(role => role.IsKeySecretHolder && !string.IsNullOrWhiteSpace(role.Secret)) != 1)
    throw new InvalidOperationException("Last Wagon did not assign exactly one key secret holder.");
if (wagon.Hook.Contains("личный ящик", StringComparison.OrdinalIgnoreCase) || wagon.PublicBriefing.Contains("помощник сам добавил", StringComparison.OrdinalIgnoreCase))
    throw new InvalidOperationException("Last Wagon disclosed the secret in public information.");
if (wagon.Clues.Any(clue => !wagon.Scenes.Any(scene => scene.ClueIds.Contains(clue.Id))) || wagon.Branches.Where(branch => branch.RequiredClueIds.Count > 0).Any(branch => branch.RequiredClueIds.Any(clueId => !wagon.Clues.Any(clue => clue.Id == clueId))))
    throw new InvalidOperationException("Last Wagon contains a decorative or untraceable clue.");
var lighter = scenarioGenerator.GenerateLegacyNegotiation(new ScenarioGenerationOptions { Genre = "Переговоры", ParticipantCount = 4, DurationMinutes = 60, Tone = "Дипломатичный", Difficulty = "Низкая", Seed = 7788 }).Scenario!;
if (lighter.Hook == wagon.Hook || lighter.Clues.Count >= wagon.Clues.Count || lighter.Scenes.Count >= wagon.Scenes.Count)
    throw new InvalidOperationException("Tone, difficulty, or duration did not materially affect Last Wagon.");
var stressWagon = scenarioGenerator.GenerateLegacyNegotiation(new ScenarioGenerationOptions { Genre = "Переговоры", ParticipantCount = 4, DurationMinutes = 180, Tone = "Напряжённый", Difficulty = "Высокая", Seed = 7788 }).Scenario!;
var noDeal = stressWagon.Endings.Single(ending => ending.Id == "no-deal");
if (!stressWagon.Branches.Any(branch => branch.FromSceneId == "inspection" && branch.ToSceneId == "terms" && branch.RequiredClueIds.Count == 0) ||
    !stressWagon.Branches.Any(branch => branch.FromSceneId == "terms" && branch.ToEndingId == "no-deal") ||
    !stressWagon.Branches.Any(branch => branch.FromSceneId == "deadline" && branch.ToEndingId == "no-deal") ||
    noDeal.Outcome.Contains("уезжает", StringComparison.OrdinalIgnoreCase) || noDeal.Outcome.Contains("соглашение достигнуто", StringComparison.OrdinalIgnoreCase))
    throw new InvalidOperationException("Last Wagon does not preserve a truthful continuation after failed negotiations or missed clues.");
if (!stressWagon.GmNotes.Any(note => note.Contains("не объявляйте согласие", StringComparison.OrdinalIgnoreCase)))
    throw new InvalidOperationException("Last Wagon does not tell the GM how to handle an unmodelled player plan without inventing consent.");

var negotiationGenerator = new ScenarioGeneratorService(new RecordingScenarioKnowledgeCatalog(selectorCandidates.Concat(
    [new ScenarioKnowledgeCandidate { EntityId = 9100, Title = "Непроверенное место", SemanticType = "Place", PrimaryCategory = "Локации (Skyrim)", NeedsReview = true, SourceUrl = "https://example.test/review" }]).ToArray()));
var negotiationSamples = Enumerable.Range(1, 96).Select(seed => negotiationGenerator.Generate(new ScenarioGenerationOptions
{
    Genre = "Переговоры", ParticipantCount = 4, DurationMinutes = 180, Tone = "Дипломатичный", Difficulty = "Высокая", Seed = seed
}).Scenario ?? throw new InvalidOperationException($"Negotiation failed at seed {seed}.")).ToArray();
if (!negotiationSamples.Select(s => s.NegotiationBlueprintId).Distinct().Order().SequenceEqual(ScenarioNegotiationBlueprintFactory.BlueprintIds.Order()))
    throw new InvalidOperationException("Negotiation seeds do not cover every blueprint.");
var negotiationRepresentatives = negotiationSamples.GroupBy(s => s.NegotiationBlueprintId).Select(g => g.First()).ToArray();
if (negotiationRepresentatives.Select(s => string.Join(";", s.Scenes.Select(scene =>
    $"{scene.ClueIds.Count}/{s.Branches.Count(b => b.FromSceneId == scene.Id && b.ToSceneId != "")}/{s.Branches.Count(b => b.FromSceneId == scene.Id && b.ToEndingId != "")}").Order())).Distinct().Count() != 3)
    throw new InvalidOperationException("Negotiation blueprints differ only by names.");
foreach (var s in negotiationSamples)
{
    if (!ScenarioValidator.Validate(s).IsValid || JsonSerializer.Serialize(s) != JsonSerializer.Serialize(negotiationGenerator.Generate(s.Options).Scenario) ||
        s.Characters.Any(r => string.IsNullOrWhiteSpace(r.Concession) || string.IsNullOrWhiteSpace(r.RedLine)) ||
        s.Characters.Any(r => r.IsKeySecretHolder) || s.Endings.Any(e => e.RequiresCombat) ||
        s.NarrativeFacts.Any(f => f.Origin == ScenarioFactOrigin.CanonicalKnowledge) ||
        s.CanonicalAnchors.Any(a => a.NeedsReview || a.Status != ScenarioCanonicalAnchorStatus.Proposed))
        throw new InvalidOperationException("Negotiation replay, graph, role positions or provenance is invalid.");
}
var negotiationExportDirectory = Path.Combine(Path.GetTempPath(), $"skyrim-rp-negotiation-{Guid.NewGuid():N}");
Directory.CreateDirectory(negotiationExportDirectory);
try
{
    var exporter = new ScenarioExportService();
    foreach (var s in negotiationRepresentatives)
    {
        var requiredId = s.Branches.First(b => b.RequiredClueIds.Count > 0).RequiredClueIds[0];
        var damaged = JsonNode.Parse(JsonSerializer.Serialize(s))!.AsObject();
        var cluesNode = damaged["Clues"]!.AsArray();
        cluesNode.Remove(cluesNode.Single(c => c!["Id"]!.GetValue<string>() == requiredId));
        if (!ScenarioValidator.Validate(JsonSerializer.Deserialize<GeneratedScenario>(damaged.ToJsonString())!).Errors.Any(e => e.Contains(requiredId, StringComparison.Ordinal)))
            throw new InvalidOperationException("Negotiation validator accepted missing mandatory evidence.");
        foreach (var badId in new[] { "", "unknown-blueprint" })
        {
            var badJson = JsonNode.Parse(JsonSerializer.Serialize(s))!.AsObject();
            badJson["NegotiationBlueprintId"] = badId;
            var bad = JsonSerializer.Deserialize<GeneratedScenario>(badJson.ToJsonString())!;
            if (ScenarioValidator.Validate(bad).IsValid || bad.Template.Id != "negotiation")
                throw new InvalidOperationException("Invalid negotiation ID was accepted or replaced by another genre.");
        }
        var target = Path.Combine(negotiationExportDirectory, s.NegotiationBlueprintId + ".json");
        if (!exporter.Save(target, s).IsSuccess) throw new InvalidOperationException("Negotiation export failed.");
        var loaded = exporter.Load(target);
        var expectedCaption = s.NegotiationBlueprintId switch
        {
            "hostage-exchange" => "Право выйти", "border-agreement" => "Проход по двум подписям", _ => "Остаток по двум актам"
        };
        if (ScenarioTextFormatter.GetDisplayName(s) != expectedCaption ||
            !ScenarioTextFormatter.FormatScenario(s).Contains("Каркас: " + expectedCaption + ".", StringComparison.Ordinal))
            throw new InvalidOperationException("Negotiation caption still uses the legacy template.");
        var viewState = new ScenarioGeneratorState();
        viewState.Accept(wagon);
        if (!viewState.TryAcceptLoad(loaded) || viewState.Current != loaded.Scenario || viewState.LastSeed != s.Seed ||
            !loaded.HasSavedSeed || new[] { "Genre", "Seed", "ParticipantCount", "DurationMinutes", "Tone", "Difficulty" }
                .Any(field => !loaded.SavedOptionFields.Contains(field, StringComparer.OrdinalIgnoreCase)) ||
            JsonSerializer.Serialize(loaded.Scenario!.Options) != JsonSerializer.Serialize(s.Options))
            throw new InvalidOperationException("Loaded scenario did not preserve saved generation parameters.");
        var failedPath = Path.Combine(negotiationExportDirectory, "broken.json");
        File.WriteAllText(failedPath, "{broken");
        var acceptedBeforeFailure = viewState.Current;
        if (viewState.TryAcceptLoad(exporter.Load(failedPath)) || viewState.Current != acceptedBeforeFailure || viewState.LastSeed != s.Seed)
            throw new InvalidOperationException("Failed JSON load replaced the accepted scenario.");
        viewState.Clear(); // Same operation used by the genre-change handler.
        if (viewState.Current is not null || viewState.LastSeed.HasValue)
            throw new InvalidOperationException("Genre change retained a stale scenario or replay seed.");
        var partialJson = JsonNode.Parse(File.ReadAllText(target))!.AsObject();
        partialJson["Options"]!.AsObject().Remove("Tone");
        var partialPath = Path.Combine(negotiationExportDirectory, "partial-options.json");
        File.WriteAllText(partialPath, partialJson.ToJsonString());
        var partialLoad = exporter.Load(partialPath);
        if (!partialLoad.IsSuccess || partialLoad.SavedOptionFields.Contains("Tone", StringComparer.OrdinalIgnoreCase) ||
            !viewState.TryAcceptLoad(partialLoad) || viewState.LastSeed.HasValue)
            throw new InvalidOperationException("Missing JSON parameters were inferred or enabled ambiguous replay.");
        viewState.UpdateCurrent(ScenarioCanonicalAnchorApplicator.Apply(viewState.Current!, [selectedAnchor]));
        if (viewState.LastSeed.HasValue)
            throw new InvalidOperationException("Applying anchors enabled replay with incomplete JSON parameters.");
        var expected = JsonNode.Parse(JsonSerializer.Serialize(s))!.AsObject();
        expected["CanonicalAnchors"] = new JsonArray();
        if (!loaded.IsSuccess || loaded.Scenario is null || !JsonNode.DeepEquals(expected, JsonNode.Parse(JsonSerializer.Serialize(loaded.Scenario))))
            throw new InvalidOperationException("Negotiation JSON lost content or confirmed Proposed anchors.");
        if (ScenarioCanonicalAnchorApplicator.Apply(s, [selectedAnchor]).NegotiationBlueprintId != s.NegotiationBlueprintId ||
            !ScenarioTextFormatter.FormatScenario(s).Contains("Blueprint переговоров: " + s.NegotiationBlueprintId, StringComparison.Ordinal))
            throw new InvalidOperationException("Negotiation ID lost in formatting or anchor application.");
        var fallback = new ScenarioGeneratorService().Generate(s.Options).Scenario!;
        if (!ScenarioValidator.Validate(fallback).IsValid || fallback.CanonicalAnchors.Count != 0 || !fallback.GenerationDiagnostic.Contains("Автоном", StringComparison.Ordinal))
            throw new InvalidOperationException("Negotiation autonomous fallback failed.");
        var threshold = s.NegotiationBlueprintId switch { "hostage-exchange" => 120, "border-agreement" => 150, _ => 90 };
        GeneratedScenario ParameterScenario(int duration, int count, string difficulty, string tone) => negotiationGenerator.Generate(new ScenarioGenerationOptions
        { Genre = "Переговоры", Seed = s.Seed, DurationMinutes = duration, ParticipantCount = count, Difficulty = difficulty, Tone = tone }).Scenario
            ?? throw new InvalidOperationException("Negotiation parameter boundary failed.");
        var beforeThreshold = ParameterScenario(threshold - 1, 4, "Средняя", "Напряжённый");
        var atThreshold = ParameterScenario(threshold, 4, "Средняя", "Напряжённый");
        if (atThreshold.Scenes.Count != beforeThreshold.Scenes.Count + 1 || atThreshold.Endings.Count != beforeThreshold.Endings.Count + 1)
            throw new InvalidOperationException("Negotiation duration threshold has no structural effect.");
        var hard = ParameterScenario(180, 4, "Высокая", "Напряжённый");
        var medium = ParameterScenario(180, 4, "Средняя", "Напряжённый");
        var easy = ParameterScenario(180, 4, "Низкая", "Напряжённый");
        if (hard.Clues.Count != medium.Clues.Count + 1 || medium.Clues.Count != easy.Clues.Count ||
            !hard.Branches.Any(b => b.RequiredClueIds.Count > medium.Branches.Single(m => m.FromSceneId == b.FromSceneId && m.ToSceneId == b.ToSceneId && m.ToEndingId == b.ToEndingId).RequiredClueIds.Count))
            throw new InvalidOperationException("Negotiation difficulty does not change evidence requirements.");
        foreach (var tone in new[] { "Мрачный", "Дипломатичный", "Приключенческий" })
        {
            var toned = ParameterScenario(180, 4, "Средняя", tone);
            if (toned.Hook == medium.Hook || JsonSerializer.Serialize(toned.Branches) != JsonSerializer.Serialize(medium.Branches))
                throw new InvalidOperationException("Negotiation tone must change atmosphere, not the graph.");
        }
        foreach (var count in new[] { 2, 6 })
        foreach (var duration in new[] { 60, 180 })
        {
            var boundary = ParameterScenario(duration, count, "Высокая", "Напряжённый");
            if (!ScenarioValidator.Validate(boundary).IsValid || boundary.Characters.Count != count ||
                (s.NegotiationBlueprintId == "hostage-exchange" && boundary.Characters.All(r => r.Name != "Ида")))
                throw new InvalidOperationException("Negotiation player bounds lost roles or the held person's agency.");
        }
        if (s.NegotiationBlueprintId == "hostage-exchange" && s.CanonicalAnchors.Any(a => a.SemanticType != "Place"))
            throw new InvalidOperationException("Exchange treated canonical objects as persons.");
    }
    foreach (var duration in new[] { 89, 90, 149, 150 })
    {
        var legacy = scenarioGenerator.GenerateLegacyNegotiation(new ScenarioGenerationOptions
        { Genre = "Переговоры", ParticipantCount = 2, DurationMinutes = duration, Difficulty = "Высокая", Seed = 7788 }).Scenario!;
        if (!ScenarioValidator.Validate(legacy).IsValid || legacy.Scenes.Any(s => s.Id == "alternative") != (duration >= 90) ||
            legacy.Scenes.Any(s => s.Id == "deadline") != (duration >= 150))
            throw new InvalidOperationException("Legacy wagon duration boundary regressed.");
    }
    var legacyJson = JsonNode.Parse(JsonSerializer.Serialize(wagon))!.AsObject();
    legacyJson.Remove("NegotiationBlueprintId");
    var legacyPath = Path.Combine(negotiationExportDirectory, "legacy.json");
    File.WriteAllText(legacyPath, legacyJson.ToJsonString());
    var legacyLoaded = exporter.Load(legacyPath);
    if (!legacyLoaded.IsSuccess || legacyLoaded.Scenario?.NegotiationBlueprintId != "")
        throw new InvalidOperationException("Legacy wagon JSON is incompatible.");
    var legacyViewState = new ScenarioGeneratorState();
    if (ScenarioTextFormatter.GetDisplayName(wagon) != "Последняя повозка до заката" ||
        !legacyViewState.TryAcceptLoad(legacyLoaded) || !legacyViewState.TryAcceptLoad(exporter.Load(legacyPath)) ||
        legacyViewState.Current?.NegotiationBlueprintId != "" || legacyViewState.LastSeed != wagon.Seed)
        throw new InvalidOperationException("Repeated legacy load changed the caption, blueprint or saved seed.");
    foreach (var r in legacyJson["Characters"]!.AsArray()) r!["IsKeySecretHolder"] = false;
    if (ScenarioValidator.Validate(JsonSerializer.Deserialize<GeneratedScenario>(legacyJson.ToJsonString())!).IsValid)
        throw new InvalidOperationException("Legacy key-secret invariant was weakened.");
}
finally { Directory.Delete(negotiationExportDirectory, recursive: true); }
Console.WriteLine("Negotiation blueprints OK: 96 seeds, graph variation, evidence, role agency, boundaries, provenance and legacy wagon.");

var knowledgeInvestigationGenerator = new ScenarioGeneratorService(recordingCatalog);
var investigationSeeds = Enumerable.Range(1, 96).ToArray();
var investigationBlueprints = investigationSeeds
    .Select(seed => knowledgeInvestigationGenerator.Generate(new ScenarioGenerationOptions
    {
        Genre = "Расследование", ParticipantCount = 4, DurationMinutes = 150, Tone = "Дипломатичный", Difficulty = "Высокая", Seed = seed
    }).Scenario!)
    .ToArray();
if (investigationBlueprints.Select(scenario => scenario.InvestigationBlueprintId).Distinct(StringComparer.Ordinal).Count() != ScenarioInvestigationBlueprintFactory.BlueprintIds.Count ||
    investigationBlueprints.Select(scenario => scenario.InvestigationBlueprintId).Distinct(StringComparer.Ordinal).Except(ScenarioInvestigationBlueprintFactory.BlueprintIds).Any())
{
    throw new InvalidOperationException("Investigation pilot did not select multiple declared blueprints across fixed seeds.");
}
var firstBlueprintScenario = investigationBlueprints.First();
var firstBlueprintReplay = knowledgeInvestigationGenerator.Generate(firstBlueprintScenario.Options).Scenario!;
if (JsonSerializer.Serialize(firstBlueprintScenario) != JsonSerializer.Serialize(firstBlueprintReplay) ||
    firstBlueprintScenario.CanonicalAnchors.Any(anchor => anchor.NeedsReview || anchor.Origin != ScenarioFactOrigin.CanonicalKnowledge ||
                                                         anchor.Status != ScenarioCanonicalAnchorStatus.Proposed || string.IsNullOrWhiteSpace(anchor.SourceUrl)) ||
    firstBlueprintScenario.NarrativeFacts.Any(fact => fact.Origin == ScenarioFactOrigin.CanonicalKnowledge))
{
    throw new InvalidOperationException("Investigation blueprint lost deterministic, review-safe canonical provenance.");
}
var structurallyDifferentInvestigations = investigationBlueprints
    .GroupBy(scenario => scenario.InvestigationBlueprintId)
    .Select(group => group.First())
    .Select(scenario => $"{scenario.InvestigationBlueprintId}|{scenario.Hook}|{string.Join(',', scenario.Scenes.Select(scene => scene.Id))}|{string.Join(',', scenario.Endings.Select(ending => ending.Id))}")
    .Distinct(StringComparer.Ordinal)
    .Count();
if (structurallyDifferentInvestigations < 2 || !investigationBlueprints.All(scenario => ScenarioValidator.Validate(scenario).IsValid))
    throw new InvalidOperationException("Investigation pilot varied only labels or produced an invalid graph.");
var fallbackInvestigation = new ScenarioGeneratorService(new RecordingScenarioKnowledgeCatalog([])).Generate(new ScenarioGenerationOptions
{
    Genre = "Расследование", ParticipantCount = 4, DurationMinutes = 150, Seed = 7
}).Scenario!;
if (fallbackInvestigation.CanonicalAnchors.Count != 0 || !ScenarioValidator.Validate(fallbackInvestigation).IsValid)
    throw new InvalidOperationException("Investigation fallback did not remain autonomous when Knowledge DB candidates were unavailable.");

var investigationOptions = new ScenarioGenerationOptions { Genre = "Расследование", ParticipantCount = 4, DurationMinutes = 150, Tone = "Дипломатичный", Difficulty = "Высокая", Seed = 2401 };
var investigation = scenarioGenerator.Generate(investigationOptions).Scenario!;
if (!ScenarioValidator.Validate(investigation).IsValid || investigation.Hypotheses.Count < 3 || investigation.Clues.Count < 4 ||
    investigation.Clues.Any(clue => clue.SupportsHypothesisIds.Count == 0 && clue.ContradictsHypothesisIds.Count == 0) ||
    investigation.Clues.Any(clue => !investigation.Scenes.Any(scene => scene.ClueIds.Contains(clue.Id))) ||
    investigation.Endings.Select(ending => ending.Outcome).Distinct(StringComparer.Ordinal).Count() < 4)
    throw new InvalidOperationException("Investigation blueprint does not provide traceable hypotheses, clues, and distinct outcomes.");
if (string.IsNullOrWhiteSpace(investigation.InvestigationBlueprintId) ||
    !investigation.NarrativeFacts.Any(fact => fact.Origin == ScenarioFactOrigin.FactoryFiction) ||
    !investigation.NarrativeFacts.Any(fact => fact.Origin == ScenarioFactOrigin.GmHypothesis) ||
    investigation.PublicBriefing.Contains("Авторская истина", StringComparison.OrdinalIgnoreCase))
    throw new InvalidOperationException("Investigation blueprint did not separate public information from author fiction and GM truth.");
var evidenceBoundEnding = investigation.Branches.First(branch => !string.IsNullOrWhiteSpace(branch.ToEndingId) && branch.RequiredClueIds.Count > 0);
var investigationReplay = scenarioGenerator.Generate(investigationOptions).Scenario!;
if (!investigation.Characters.Select(role => $"{role.Name}/{role.Secret}").SequenceEqual(investigationReplay.Characters.Select(role => $"{role.Name}/{role.Secret}")) ||
    !investigation.Clues.Select(clue => $"{clue.Id}/{string.Join(',', clue.SupportsHypothesisIds)}/{string.Join(',', clue.ContradictsHypothesisIds)}").SequenceEqual(investigationReplay.Clues.Select(clue => $"{clue.Id}/{string.Join(',', clue.SupportsHypothesisIds)}/{string.Join(',', clue.ContradictsHypothesisIds)}")) ||
    !investigation.Branches.Select(branch => $"{branch.FromSceneId}/{branch.ToSceneId}/{branch.ToEndingId}/{string.Join(',', branch.RequiredClueIds)}").SequenceEqual(investigationReplay.Branches.Select(branch => $"{branch.FromSceneId}/{branch.ToSceneId}/{branch.ToEndingId}/{string.Join(',', branch.RequiredClueIds)}")) ||
    !investigation.Endings.Select(ending => ending.Outcome).SequenceEqual(investigationReplay.Endings.Select(ending => ending.Outcome)))
    throw new InvalidOperationException("Fixed seed did not reproduce Ash on the Seal completely.");
var missingInvestigationProof = new GeneratedScenario
{
    Title = investigation.Title, Seed = investigation.Seed, Options = investigation.Options, Template = investigation.Template, Hook = investigation.Hook, ConflictCause = investigation.ConflictCause,
    StartSceneId = investigation.StartSceneId, PublicBriefing = investigation.PublicBriefing, KnownFacts = investigation.KnownFacts, GmNotes = investigation.GmNotes, Hypotheses = investigation.Hypotheses,
    Characters = investigation.Characters, Clues = investigation.Clues.Where(clue => clue.Id != evidenceBoundEnding.RequiredClueIds[0]).ToArray(), Scenes = investigation.Scenes,
    Branches = investigation.Branches, Endings = investigation.Endings,
};
if (!ScenarioValidator.Validate(missingInvestigationProof).Errors.Any(error => error.Contains(evidenceBoundEnding.RequiredClueIds[0], StringComparison.Ordinal)))
    throw new InvalidOperationException("Scenario validator did not reject an investigation ending with missing required proof.");
var unreachableInvestigation = new GeneratedScenario
{
    Title = investigation.Title, Seed = investigation.Seed, Options = investigation.Options, Template = investigation.Template, Hook = investigation.Hook, ConflictCause = investigation.ConflictCause,
    StartSceneId = investigation.StartSceneId, PublicBriefing = investigation.PublicBriefing, KnownFacts = investigation.KnownFacts, GmNotes = investigation.GmNotes, Hypotheses = investigation.Hypotheses,
    Characters = investigation.Characters, Clues = investigation.Clues, Scenes = investigation.Scenes, Branches = investigation.Branches,
    Endings = investigation.Endings.Concat([new ScenarioEnding { Id = "unreachable-investigation-final", Title = "Недостижимый", Outcome = "Тестовый исход." }]).ToArray(),
};
if (!ScenarioValidator.Validate(unreachableInvestigation).Errors.Any(error => error.Contains("unreachable-investigation-final", StringComparison.Ordinal)))
    throw new InvalidOperationException("Scenario validator did not report an unreachable investigation ending.");
var missionOptions = new ScenarioGenerationOptions { Genre = "Опасное поручение", ParticipantCount = 4, DurationMinutes = 150, Tone = "Приключенческий", Difficulty = "Высокая", Seed = 813 };
var mission = scenarioGenerator.Generate(missionOptions).Scenario!;
if (!ScenarioValidator.Validate(mission).IsValid || mission.Characters.Select(role => role.Role).Distinct(StringComparer.Ordinal).Count() < 4 ||
    !mission.Branches.Any(branch => branch.ToSceneId == "narrows" && branch.RequiredClueIds.Contains("guide-map")) ||
    !mission.Branches.Any(branch => branch.ToSceneId == "ridge" && branch.RequiredClueIds.Contains("supply-list")) ||
    !mission.Branches.Any(branch => branch.ToSceneId == "escort" && branch.RequiredClueIds.Contains("hunter-token")) ||
    mission.Endings.Count < 4 || mission.Endings.Any(ending => ending.RequiresCombat))
    throw new InvalidOperationException("Mountain Pass does not provide distinct non-mandatory-combat routes and roles.");
if (!mission.Endings.Single(ending => ending.Id == "return-with-cargo").Outcome.Contains("не доставлен", StringComparison.OrdinalIgnoreCase) ||
    !mission.Endings.Single(ending => ending.Id == "rescue-delay").Outcome.Contains("откладывается", StringComparison.OrdinalIgnoreCase) ||
    !mission.Endings.Single(ending => ending.Id == "timely-delivery").Outcome.Contains("вовремя", StringComparison.OrdinalIgnoreCase))
    throw new InvalidOperationException("Mountain Pass endings do not preserve truthful delivery and rescue consequences.");
var shortMission = scenarioGenerator.Generate(new ScenarioGenerationOptions { Genre = "Опасное поручение", ParticipantCount = 4, DurationMinutes = 60, Tone = "Напряжённый", Difficulty = "Низкая", Seed = 813 }).Scenario!;
if (shortMission.Scenes.Count >= mission.Scenes.Count || shortMission.Clues.Count >= mission.Clues.Count || shortMission.Branches.Any(branch => branch.ToEndingId == "partial-delivery"))
    throw new InvalidOperationException("Mountain Pass duration or difficulty did not materially change expedition structure.");
var missionReplay = scenarioGenerator.Generate(missionOptions).Scenario!;
if (!mission.Characters.Select(role => $"{role.Name}/{role.Secret}").SequenceEqual(missionReplay.Characters.Select(role => $"{role.Name}/{role.Secret}")) ||
    !mission.Clues.Select(clue => clue.Id).SequenceEqual(missionReplay.Clues.Select(clue => clue.Id)) ||
    !mission.Branches.Select(branch => $"{branch.FromSceneId}/{branch.ToSceneId}/{branch.ToEndingId}/{string.Join(',', branch.RequiredClueIds)}").SequenceEqual(missionReplay.Branches.Select(branch => $"{branch.FromSceneId}/{branch.ToSceneId}/{branch.ToEndingId}/{string.Join(',', branch.RequiredClueIds)}")))
    throw new InvalidOperationException("Fixed seed did not reproduce Mountain Pass completely.");
var missingMountainProof = new GeneratedScenario
{
    Title = mission.Title, Seed = mission.Seed, Options = mission.Options, Template = mission.Template, Hook = mission.Hook, ConflictCause = mission.ConflictCause,
    StartSceneId = mission.StartSceneId, PublicBriefing = mission.PublicBriefing, KnownFacts = mission.KnownFacts, GmNotes = mission.GmNotes,
    Characters = mission.Characters, Clues = mission.Clues.Where(clue => clue.Id != "smuggler-mark").ToArray(), Scenes = mission.Scenes, Branches = mission.Branches, Endings = mission.Endings,
};
if (!ScenarioValidator.Validate(missingMountainProof).Errors.Any(error => error.Contains("smuggler-mark", StringComparison.Ordinal)))
    throw new InvalidOperationException("Scenario validator did not reject a high-difficulty mountain route with missing preparation evidence.");
var unreachableMission = new GeneratedScenario
{
    Title = mission.Title, Seed = mission.Seed, Options = mission.Options, Template = mission.Template, Hook = mission.Hook, ConflictCause = mission.ConflictCause,
    StartSceneId = mission.StartSceneId, PublicBriefing = mission.PublicBriefing, KnownFacts = mission.KnownFacts, GmNotes = mission.GmNotes,
    Characters = mission.Characters, Clues = mission.Clues, Scenes = mission.Scenes, Branches = mission.Branches,
    Endings = mission.Endings.Concat([new ScenarioEnding { Id = "unreachable-mission-ending", Title = "Недостижимый", Outcome = "Тестовый исход." }]).ToArray(),
};
if (!ScenarioValidator.Validate(unreachableMission).Errors.Any(error => error.Contains("unreachable-mission-ending", StringComparison.Ordinal)))
    throw new InvalidOperationException("Scenario validator did not report an unreachable Mountain Pass ending.");
var missionBlueprintGenerator = new ScenarioGeneratorService(new RecordingScenarioKnowledgeCatalog(selectorCandidates.Concat(
    [new ScenarioKnowledgeCandidate { EntityId = 9001, Title = "Непроверенный маршрут", SemanticType = "Place", PrimaryCategory = "Локации (Skyrim)", SourceUrl = "https://example.test/review-route", NeedsReview = true }]).ToArray()));
var missionBlueprintScenarios = Enumerable.Range(1, 96).Select(seed => missionBlueprintGenerator.Generate(new ScenarioGenerationOptions
{
    Genre = "Опасное поручение", ParticipantCount = 4, DurationMinutes = 150,
    Tone = "Приключенческий", Difficulty = "Высокая", Seed = seed
}).Scenario ?? throw new InvalidOperationException($"Mission blueprint failed at seed {seed}.")).ToArray();
if (!missionBlueprintScenarios.Select(s => s.MissionBlueprintId).Distinct().Order().SequenceEqual(ScenarioMissionBlueprintFactory.BlueprintIds.Order()) ||
    missionBlueprintScenarios.Any(s => !ScenarioValidator.Validate(s).IsValid || s.Endings.Any(e => e.RequiresCombat) ||
        s.CanonicalAnchors.Any(a => a.NeedsReview || a.Status != ScenarioCanonicalAnchorStatus.Proposed || string.IsNullOrWhiteSpace(a.SourceUrl))) ||
    !missionBlueprintScenarios.Any(s => s.CanonicalAnchors.Count > 0))
    throw new InvalidOperationException("Mission blueprints lost coverage, graph validity, safe anchors or optional-combat policy.");
var missionRepresentatives = missionBlueprintScenarios.GroupBy(s => s.MissionBlueprintId).Select(g => g.First()).ToArray();
if (missionRepresentatives.Select(s => string.Join(',', s.Scenes.Select(scene => scene.Id))).Distinct().Count() != 3 ||
    missionRepresentatives.Select(s => string.Join(',', s.Endings.Select(ending => ending.Id))).Distinct().Count() != 3)
    throw new InvalidOperationException("Mission blueprints reuse the same structural identifiers.");
// Ignore names and IDs: a mere rename must not satisfy structural variation.
if (missionRepresentatives.Select(s => string.Join(";", s.Scenes.Select(scene =>
        $"{scene.ClueIds.Count}/{s.Branches.Count(b => b.FromSceneId == scene.Id && !string.IsNullOrWhiteSpace(b.ToSceneId))}/{s.Branches.Count(b => b.FromSceneId == scene.Id && !string.IsNullOrWhiteSpace(b.ToEndingId))}").Order(StringComparer.Ordinal))).Distinct().Count() != 3)
    throw new InvalidOperationException("Mission blueprint graphs differ only by labels.");
foreach (var s in missionBlueprintScenarios)
{
    if (JsonSerializer.Serialize(s) != JsonSerializer.Serialize(missionBlueprintGenerator.Generate(s.Options).Scenario))
        throw new InvalidOperationException("Mission seed replay differs in content or anchor order.");
}
var missionExportDirectory = Path.Combine(Path.GetTempPath(), $"skyrim-rp-mission-{Guid.NewGuid():N}");
Directory.CreateDirectory(missionExportDirectory);
try
{
    var exporter = new ScenarioExportService();
    foreach (var s in missionRepresentatives)
    {
        var clueId = s.Branches.First(branch => branch.RequiredClueIds.Count > 0).RequiredClueIds[0];
        var damaged = JsonNode.Parse(JsonSerializer.Serialize(s))!.AsObject();
        var damagedClues = damaged["Clues"]!.AsArray();
        damagedClues.Remove(damagedClues.Single(clue => clue!["Id"]!.GetValue<string>() == clueId));
        var invalid = JsonSerializer.Deserialize<GeneratedScenario>(damaged.ToJsonString())!;
        if (!ScenarioValidator.Validate(invalid).Errors.Any(error => error.Contains(clueId, StringComparison.Ordinal)))
            throw new InvalidOperationException("Mission validator accepted a deleted mandatory clue.");
        var exportPath = Path.Combine(missionExportDirectory, s.MissionBlueprintId + ".json");
        var saved = exporter.Save(exportPath, s);
        var loaded = exporter.Load(exportPath);
        if (!saved.IsSuccess || !loaded.IsSuccess || loaded.Scenario is null ||
            loaded.Scenario.MissionBlueprintId != s.MissionBlueprintId || loaded.Scenario.CanonicalAnchors.Count != 0 ||
            !loaded.Scenario.NarrativeFacts.Select(f => $"{f.Id}/{f.Origin}/{f.Text}").SequenceEqual(s.NarrativeFacts.Select(f => $"{f.Id}/{f.Origin}/{f.Text}")))
            throw new InvalidOperationException("Mission export lost provenance or promoted Proposed anchors.");
        var expectedSnapshot = JsonNode.Parse(JsonSerializer.Serialize(s))!.AsObject();
        expectedSnapshot["CanonicalAnchors"] = new JsonArray();
        if (!JsonNode.DeepEquals(expectedSnapshot, JsonNode.Parse(JsonSerializer.Serialize(loaded.Scenario))))
            throw new InvalidOperationException("Mission JSON round-trip changed content other than unconfirmed anchors.");
        var applied = ScenarioCanonicalAnchorApplicator.Apply(s, [selectedAnchor]);
        if (applied.MissionBlueprintId != s.MissionBlueprintId || !ScenarioTextFormatter.FormatScenario(s).Contains("Blueprint миссии: " + s.MissionBlueprintId, StringComparison.Ordinal))
            throw new InvalidOperationException("Mission ID was lost in application or copy formatting.");
        var fallback = new ScenarioGeneratorService(new RecordingScenarioKnowledgeCatalog(Array.Empty<ScenarioKnowledgeCandidate>())).Generate(s.Options).Scenario!;
        if (!ScenarioValidator.Validate(fallback).IsValid || fallback.CanonicalAnchors.Count != 0 || !fallback.GenerationDiagnostic.Contains("автоном", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Mission empty-catalog fallback is invalid or silent.");
        foreach (var duration in new[] { 60, 119, 120, 180 })
        foreach (var difficulty in new[] { "Низкая", "Средняя", "Высокая" })
        foreach (var count in new[] { 4, 5, 6 })
        {
            var parameterResult = missionBlueprintGenerator.Generate(new ScenarioGenerationOptions
            {
                Genre = s.Options.Genre, Seed = s.Seed, DurationMinutes = duration,
                Difficulty = difficulty, ParticipantCount = count, Tone = s.Options.Tone
            });
            if (parameterResult.Scenario is null || !parameterResult.Validation.IsValid ||
                parameterResult.Scenario.MissionBlueprintId != s.MissionBlueprintId || parameterResult.Scenario.Characters.Count != count)
                throw new InvalidOperationException("Mission parameter boundary broke a blueprint.");
        }
    }
    var legacyMission = ScenarioGeneratorService.BuildMountainPass(missionOptions, scenarioGenerator.GetTemplateByGenre(missionOptions.Genre)!);
    var legacyMissionJson = JsonNode.Parse(JsonSerializer.Serialize(legacyMission))!.AsObject();
    legacyMissionJson.Remove("MissionBlueprintId");
    var legacyMissionPath = Path.Combine(missionExportDirectory, "legacy-mission.json");
    File.WriteAllText(legacyMissionPath, legacyMissionJson.ToJsonString());
    var restoredLegacyMission = exporter.Load(legacyMissionPath);
    if (!restoredLegacyMission.IsSuccess || restoredLegacyMission.Scenario is null || restoredLegacyMission.Scenario.MissionBlueprintId != "" ||
        ScenarioTextFormatter.FormatScenario(restoredLegacyMission.Scenario).Contains("Blueprint миссии:", StringComparison.Ordinal))
        throw new InvalidOperationException("Legacy mission JSON without MissionBlueprintId is incompatible.");
}
finally
{
    Directory.Delete(missionExportDirectory, recursive: true);
}
Console.WriteLine("Mission blueprints OK: 96 seeds, three graphs, evidence, parameter boundaries, provenance, anchors and legacy JSON.");
var intrigueOptions = new ScenarioGenerationOptions { Genre = "Фракционная интрига", ParticipantCount = 4, DurationMinutes = 180, Tone = "Дипломатичный", Difficulty = "Высокая", Seed = 991 };
var intrigueGenerator = new ScenarioGeneratorService(recordingCatalog);
var intrigueScenarios = Enumerable.Range(1, 96).Select(seed => intrigueGenerator.Generate(new ScenarioGenerationOptions
{
    Genre = "Фракционная интрига", ParticipantCount = 4, DurationMinutes = 180, Tone = "Дипломатичный", Difficulty = "Высокая", Seed = seed
}).Scenario!).ToArray();
if (intrigueScenarios.Select(scenario => scenario.IntrigueBlueprintId).Distinct(StringComparer.Ordinal).Count() != ScenarioIntrigueBlueprintFactory.BlueprintIds.Count ||
    intrigueScenarios.Select(scenario => scenario.IntrigueBlueprintId).Distinct(StringComparer.Ordinal).Except(ScenarioIntrigueBlueprintFactory.BlueprintIds).Any() ||
    !intrigueScenarios.All(scenario => ScenarioValidator.Validate(scenario).IsValid))
    throw new InvalidOperationException("Intrigue pilot did not generate every declared valid blueprint across fixed seeds.");
var council = intrigueGenerator.Generate(intrigueOptions).Scenario!;
if (council.Characters.Count < 4 || council.Characters.Any(role => string.IsNullOrWhiteSpace(role.Concession) || string.IsNullOrWhiteSpace(role.RedLine)) ||
    council.Coalitions.Count < 3 || council.Coalitions.Any(coalition => coalition.Members.Count < 2 || coalition.RequiredClueIds.Count == 0 || coalition.Members.Any(member => !council.Characters.Any(role => role.Name == member))) ||
    council.Endings.Count < 4 || council.Endings.Select(ending => ending.Outcome).Distinct(StringComparer.Ordinal).Count() < 4 ||
    council.NarrativeFacts.All(fact => fact.Origin != ScenarioFactOrigin.FactoryFiction) || council.NarrativeFacts.All(fact => fact.Origin != ScenarioFactOrigin.GmHypothesis) ||
    council.CanonicalAnchors.Any(anchor => anchor.NeedsReview || anchor.Status != ScenarioCanonicalAnchorStatus.Proposed || string.IsNullOrWhiteSpace(anchor.SourceUrl)))
    throw new InvalidOperationException("Intrigue blueprint does not preserve roles, coalitions, provenance, or review-safe anchors.");
var councilReplay = intrigueGenerator.Generate(intrigueOptions).Scenario!;
if (JsonSerializer.Serialize(council) != JsonSerializer.Serialize(councilReplay))
    throw new InvalidOperationException("Fixed seed did not reproduce an intrigue blueprint completely.");
var intrigueStructures = intrigueScenarios.GroupBy(scenario => scenario.IntrigueBlueprintId).Select(group => group.First())
    .Select(scenario => $"{scenario.Hook}|{string.Join(',', scenario.Scenes.Select(scene => scene.Id))}|{string.Join(',', scenario.Coalitions.Select(coalition => coalition.Id))}|{string.Join(',', scenario.Endings.Select(ending => ending.Id))}")
    .Distinct(StringComparer.Ordinal).Count();
if (intrigueStructures != ScenarioIntrigueBlueprintFactory.BlueprintIds.Count)
    throw new InvalidOperationException("Intrigue blueprints vary only identifiers rather than political structure.");
var requiredCoalitionClue = council.Coalitions.First().RequiredClueIds.First();
var missingCouncilEvidence = new GeneratedScenario
{
    Title = council.Title, Seed = council.Seed, Options = council.Options, Template = council.Template, Hook = council.Hook, ConflictCause = council.ConflictCause, IntrigueBlueprintId = council.IntrigueBlueprintId,
    StartSceneId = council.StartSceneId, PublicBriefing = council.PublicBriefing, KnownFacts = council.KnownFacts, NarrativeFacts = council.NarrativeFacts, GmNotes = council.GmNotes, Coalitions = council.Coalitions,
    Characters = council.Characters, Clues = council.Clues.Where(clue => clue.Id != requiredCoalitionClue).ToArray(), Scenes = council.Scenes, Branches = council.Branches, Endings = council.Endings, CanonicalAnchors = council.CanonicalAnchors
};
if (!ScenarioValidator.Validate(missingCouncilEvidence).Errors.Any(error => error.Contains(requiredCoalitionClue, StringComparison.Ordinal)))
    throw new InvalidOperationException("Scenario validator did not reject an intrigue coalition with missing evidence.");
var missingCoalitionRole = new GeneratedScenario
{
    Title = council.Title, Seed = council.Seed, Options = council.Options, Template = council.Template, Hook = council.Hook, ConflictCause = council.ConflictCause, IntrigueBlueprintId = council.IntrigueBlueprintId,
    StartSceneId = council.StartSceneId, PublicBriefing = council.PublicBriefing, KnownFacts = council.KnownFacts, NarrativeFacts = council.NarrativeFacts, GmNotes = council.GmNotes,
    Coalitions = council.Coalitions.Select((coalition, index) => index == 0 ? new ScenarioCoalition { Id = coalition.Id, Name = coalition.Name, Members = ["Несуществующая роль", coalition.Members[1]], Concession = coalition.Concession, Cost = coalition.Cost, RequiredClueIds = coalition.RequiredClueIds } : coalition).ToArray(),
    Characters = council.Characters, Clues = council.Clues, Scenes = council.Scenes, Branches = council.Branches, Endings = council.Endings, CanonicalAnchors = council.CanonicalAnchors
};
if (!ScenarioValidator.Validate(missingCoalitionRole).Errors.Any(error => error.Contains("Несуществующая роль", StringComparison.Ordinal)))
    throw new InvalidOperationException("Scenario validator did not reject an intrigue coalition with a missing role.");
var missingIntrigueScene = new GeneratedScenario
{
    Title = council.Title, Seed = council.Seed, Options = council.Options, Template = council.Template, Hook = council.Hook, ConflictCause = council.ConflictCause, IntrigueBlueprintId = council.IntrigueBlueprintId,
    StartSceneId = council.StartSceneId, PublicBriefing = council.PublicBriefing, KnownFacts = council.KnownFacts, NarrativeFacts = council.NarrativeFacts, GmNotes = council.GmNotes,
    Coalitions = council.Coalitions, Characters = council.Characters, Clues = council.Clues, Scenes = council.Scenes,
    Branches = council.Branches.Concat([new ScenarioBranch { FromSceneId = "opening", ToSceneId = "missing-intrigue-scene", Decision = "Тестовый переход", Condition = "Тестовое условие." }]).ToArray(),
    Endings = council.Endings, CanonicalAnchors = council.CanonicalAnchors
};
if (!ScenarioValidator.Validate(missingIntrigueScene).Errors.Any(error => error.Contains("missing-intrigue-scene", StringComparison.Ordinal)))
    throw new InvalidOperationException("Scenario validator did not reject an intrigue transition to a missing scene.");
var autonomousIntrigue = new ScenarioGeneratorService(new RecordingScenarioKnowledgeCatalog(Array.Empty<ScenarioKnowledgeCandidate>())).Generate(intrigueOptions).Scenario!;
if (autonomousIntrigue.CanonicalAnchors.Count != 0 || !autonomousIntrigue.GenerationDiagnostic.Contains("автоном", StringComparison.OrdinalIgnoreCase) || !ScenarioValidator.Validate(autonomousIntrigue).IsValid)
    throw new InvalidOperationException("Intrigue factory did not retain the diagnostic autonomous fallback for an empty catalog.");
var transparentIntrigueText = ScenarioTextFormatter.FormatScenario(council);
if (!transparentIntrigueText.Contains("Blueprint интриги: " + council.IntrigueBlueprintId, StringComparison.Ordinal) ||
    !transparentIntrigueText.Contains("Происхождение фактов:", StringComparison.Ordinal) ||
    !transparentIntrigueText.Contains("Вымышленный исходный факт сценария", StringComparison.Ordinal) ||
    !transparentIntrigueText.Contains("Гипотеза ведущего, не подтверждённая истина", StringComparison.Ordinal) ||
    !transparentIntrigueText.Contains("Обязательные улики:", StringComparison.Ordinal) ||
    !transparentIntrigueText.Contains("Связанные улики:", StringComparison.Ordinal) ||
    !transparentIntrigueText.Contains("Условия достижения:", StringComparison.Ordinal))
{
    throw new InvalidOperationException("Copy-friendly intrigue text does not expose blueprint provenance and scenario conditions.");
}
var oldFriendOptions = new ScenarioGenerationOptions { Genre = "Личный конфликт", ParticipantCount = 4, DurationMinutes = 120, Tone = "Мрачный", Difficulty = "Высокая", Seed = 1776 };
var oldFriend = scenarioGenerator.Generate(oldFriendOptions).Scenario!;
if (!ScenarioValidator.Validate(oldFriend).IsValid || oldFriend.Characters.Count < 4 || oldFriend.Characters.Any(role => string.IsNullOrWhiteSpace(role.Concession) || string.IsNullOrWhiteSpace(role.RedLine)) ||
    oldFriend.Hypotheses.Count < 3 || oldFriend.Endings.Count < 4 || oldFriend.Endings.Select(ending => ending.Outcome).Distinct(StringComparer.Ordinal).Count() < 4 ||
    !oldFriend.Branches.Any(branch => branch.ToEndingId == "hide-ledger") || !oldFriend.Branches.Any(branch => branch.ToEndingId == "fair-compromise") || !oldFriend.Branches.Any(branch => branch.ToEndingId == "refuse-honestly"))
    throw new InvalidOperationException("Old Friend's Debt does not provide functional roles and meaningful alternatives.");
if (oldFriend.Hook.Contains("Несса отвела масло", StringComparison.OrdinalIgnoreCase) || oldFriend.PublicBriefing.Contains("Несса отвела масло", StringComparison.OrdinalIgnoreCase) ||
    !oldFriend.GmNotes.Any(note => note.Contains("Объективная истина", StringComparison.OrdinalIgnoreCase)) ||
    !oldFriend.GmNotes.Any(note => note.Contains("не автоматическое согласие", StringComparison.OrdinalIgnoreCase)))
    throw new InvalidOperationException("Old Friend's Debt leaked objective truth or treated the old debt as automatic consent.");
var fairCompromise = oldFriend.Branches.Single(branch => branch.ToEndingId == "fair-compromise");
if (!fairCompromise.RequiredClueIds.Contains("witness-statement") || !oldFriend.Endings.Single(ending => ending.Id == "refuse-honestly").Outcome.Contains("не берут книгу", StringComparison.OrdinalIgnoreCase))
    throw new InvalidOperationException("Old Friend's Debt does not preserve high-difficulty proof or a truthful refusal outcome.");
var shortOldFriend = scenarioGenerator.Generate(new ScenarioGenerationOptions { Genre = "Личный конфликт", ParticipantCount = 4, DurationMinutes = 60, Tone = "Дипломатичный", Difficulty = "Низкая", Seed = 1776 }).Scenario!;
if (shortOldFriend.Scenes.Count >= oldFriend.Scenes.Count || shortOldFriend.Clues.Count >= oldFriend.Clues.Count || shortOldFriend.Scenes.Any(scene => scene.Id == "temple"))
    throw new InvalidOperationException("Old Friend's Debt duration or difficulty did not materially change the scenario structure.");
var oldFriendReplay = scenarioGenerator.Generate(oldFriendOptions).Scenario!;
if (!oldFriend.Characters.Select(role => $"{role.Name}/{role.Secret}/{role.Concession}/{role.RedLine}").SequenceEqual(oldFriendReplay.Characters.Select(role => $"{role.Name}/{role.Secret}/{role.Concession}/{role.RedLine}")) ||
    !oldFriend.Clues.Select(clue => clue.Id).SequenceEqual(oldFriendReplay.Clues.Select(clue => clue.Id)) ||
    !oldFriend.Branches.Select(branch => $"{branch.FromSceneId}/{branch.ToSceneId}/{branch.ToEndingId}/{string.Join(',', branch.RequiredClueIds)}").SequenceEqual(oldFriendReplay.Branches.Select(branch => $"{branch.FromSceneId}/{branch.ToSceneId}/{branch.ToEndingId}/{string.Join(',', branch.RequiredClueIds)}")))
    throw new InvalidOperationException("Fixed seed did not reproduce Old Friend's Debt completely.");
var missingOldFriendProof = new GeneratedScenario
{
    Title = oldFriend.Title, Seed = oldFriend.Seed, Options = oldFriend.Options, Template = oldFriend.Template, Hook = oldFriend.Hook, ConflictCause = oldFriend.ConflictCause,
    StartSceneId = oldFriend.StartSceneId, PublicBriefing = oldFriend.PublicBriefing, KnownFacts = oldFriend.KnownFacts, GmNotes = oldFriend.GmNotes, Hypotheses = oldFriend.Hypotheses,
    Characters = oldFriend.Characters, Clues = oldFriend.Clues.Where(clue => clue.Id != "witness-statement").ToArray(), Scenes = oldFriend.Scenes, Branches = oldFriend.Branches, Endings = oldFriend.Endings,
};
if (!ScenarioValidator.Validate(missingOldFriendProof).Errors.Any(error => error.Contains("witness-statement", StringComparison.Ordinal)))
    throw new InvalidOperationException("Scenario validator did not reject an Old Friend compromise with missing required proof.");
var unreachableOldFriend = new GeneratedScenario
{
    Title = oldFriend.Title, Seed = oldFriend.Seed, Options = oldFriend.Options, Template = oldFriend.Template, Hook = oldFriend.Hook, ConflictCause = oldFriend.ConflictCause,
    StartSceneId = oldFriend.StartSceneId, PublicBriefing = oldFriend.PublicBriefing, KnownFacts = oldFriend.KnownFacts, GmNotes = oldFriend.GmNotes, Hypotheses = oldFriend.Hypotheses,
    Characters = oldFriend.Characters, Clues = oldFriend.Clues, Scenes = oldFriend.Scenes, Branches = oldFriend.Branches,
    Endings = oldFriend.Endings.Concat([new ScenarioEnding { Id = "unreachable-old-friend-ending", Title = "Недостижимый", Outcome = "Тестовый исход." }]).ToArray(),
};
if (!ScenarioValidator.Validate(unreachableOldFriend).Errors.Any(error => error.Contains("unreachable-old-friend-ending", StringComparison.Ordinal)))
    throw new InvalidOperationException("Scenario validator did not report an unreachable Old Friend ending.");
var unreachableWagon = new GeneratedScenario
{
    Title = wagon.Title, Seed = wagon.Seed, Options = wagon.Options, Template = wagon.Template, Hook = wagon.Hook, ConflictCause = wagon.ConflictCause,
    StartSceneId = wagon.StartSceneId, PublicBriefing = wagon.PublicBriefing, KnownFacts = wagon.KnownFacts, GmNotes = wagon.GmNotes,
    Characters = wagon.Characters, Clues = wagon.Clues, Scenes = wagon.Scenes, Branches = wagon.Branches,
    Endings = wagon.Endings.Concat([new ScenarioEnding { Id = "unreachable-final", Title = "Недостижимый", Outcome = "Тестовый финал." }]).ToArray(),
};
if (!ScenarioValidator.Validate(unreachableWagon).Errors.Any(error => error.Contains("unreachable-final", StringComparison.Ordinal)))
    throw new InvalidOperationException("Scenario validator did not report an unreachable ending.");
var brokenBranchWagon = new GeneratedScenario
{
    Title = wagon.Title, Seed = wagon.Seed, Options = wagon.Options, Template = wagon.Template, Hook = wagon.Hook, ConflictCause = wagon.ConflictCause,
    StartSceneId = wagon.StartSceneId, PublicBriefing = wagon.PublicBriefing, KnownFacts = wagon.KnownFacts, GmNotes = wagon.GmNotes,
    Characters = wagon.Characters, Clues = wagon.Clues, Scenes = wagon.Scenes,
    Branches = wagon.Branches.Concat([new ScenarioBranch { FromSceneId = "opening", ToSceneId = "missing-node", Condition = "искусственная ошибка" }]).ToArray(), Endings = wagon.Endings,
};
if (!ScenarioValidator.Validate(brokenBranchWagon).Errors.Any(error => error.Contains("missing-node", StringComparison.Ordinal)))
    throw new InvalidOperationException("Scenario validator did not report a missing branch node.");
var unavailableClueWagon = new GeneratedScenario
{
    Title = wagon.Title, Seed = wagon.Seed, Options = wagon.Options, Template = wagon.Template, Hook = wagon.Hook, ConflictCause = wagon.ConflictCause,
    StartSceneId = wagon.StartSceneId, PublicBriefing = wagon.PublicBriefing, KnownFacts = wagon.KnownFacts, GmNotes = wagon.GmNotes,
    Characters = wagon.Characters, Clues = wagon.Clues, Scenes = wagon.Scenes,
    Branches = wagon.Branches.Concat([new ScenarioBranch { FromSceneId = "opening", ToEndingId = "truth-exposed", Condition = "искусственное условие", RequiredClueIds = ["not-a-clue"] }]).ToArray(), Endings = wagon.Endings,
};
if (!ScenarioValidator.Validate(unavailableClueWagon).Errors.Any(error => error.Contains("not-a-clue", StringComparison.Ordinal)))
    throw new InvalidOperationException("Scenario validator did not report an unavailable required clue.");
var undiscoverableClueWagon = new GeneratedScenario
{
    Title = wagon.Title, Seed = wagon.Seed, Options = wagon.Options, Template = wagon.Template, Hook = wagon.Hook, ConflictCause = wagon.ConflictCause,
    StartSceneId = wagon.StartSceneId, PublicBriefing = wagon.PublicBriefing, KnownFacts = wagon.KnownFacts, GmNotes = wagon.GmNotes,
    Characters = wagon.Characters, Clues = wagon.Clues.Concat([new ScenarioClue { Id = "sealed-clue", Text = "Недоступная заметка.", KnownFact = "существует только в тесте", Reveals = "ничего" }]).ToArray(), Scenes = wagon.Scenes,
    Branches = wagon.Branches.Concat([new ScenarioBranch { FromSceneId = "opening", ToEndingId = "truth-exposed", Condition = "искусственное условие", RequiredClueIds = ["sealed-clue"] }]).ToArray(), Endings = wagon.Endings,
};
if (!ScenarioValidator.Validate(undiscoverableClueWagon).Errors.Any(error => error.Contains("sealed-clue", StringComparison.Ordinal)))
    throw new InvalidOperationException("Scenario validator did not report a required clue that no reachable scene provides.");
var unreachableAssignedClueWagon = new GeneratedScenario
{
    Title = wagon.Title, Seed = wagon.Seed, Options = wagon.Options, Template = wagon.Template, Hook = wagon.Hook, ConflictCause = wagon.ConflictCause,
    StartSceneId = wagon.StartSceneId, PublicBriefing = wagon.PublicBriefing, KnownFacts = wagon.KnownFacts, GmNotes = wagon.GmNotes,
    Characters = wagon.Characters,
    Clues = wagon.Clues.Concat([new ScenarioClue { Id = "unreachable-clue", Text = "Недостижимая заметка.", KnownFact = "Тест", Reveals = "Тест" }]).ToArray(),
    Scenes = wagon.Scenes.Concat([new ScenarioScene { Id = "unreachable-scene", Title = "Изолированная сцена", Purpose = "Тест", ClueIds = ["unreachable-clue"] }]).ToArray(),
    Branches = wagon.Branches.Concat([new ScenarioBranch { FromSceneId = "opening", ToSceneId = "terms", Decision = "Тест", Condition = "Тест", RequiredClueIds = ["unreachable-clue"] }]).ToArray(),
    Endings = wagon.Endings,
};
if (!ScenarioValidator.Validate(unreachableAssignedClueWagon).Errors.Any(error => error.Contains("unreachable-clue", StringComparison.Ordinal)))
    throw new InvalidOperationException("Scenario validator did not report a branch requiring a clue from an unreachable scene.");
var duplicateIdsWagon = new GeneratedScenario
{
    Title = wagon.Title, Seed = wagon.Seed, Options = wagon.Options, Template = wagon.Template, Hook = wagon.Hook, ConflictCause = wagon.ConflictCause,
    StartSceneId = wagon.StartSceneId, PublicBriefing = wagon.PublicBriefing, KnownFacts = wagon.KnownFacts, GmNotes = wagon.GmNotes,
    Characters = wagon.Characters,
    Clues = wagon.Clues.Concat([new ScenarioClue { Id = "invoice", Text = "Дубликат.", KnownFact = "Тест", Reveals = "Тест" }]).ToArray(),
    Scenes = wagon.Scenes.Concat([new ScenarioScene { Id = "opening", Title = "Дубликат", Purpose = "Тест" }]).ToArray(),
    Branches = wagon.Branches,
    Endings = wagon.Endings.Concat([new ScenarioEnding { Id = "no-deal", Title = "Дубликат", Outcome = "Тест" }]).ToArray(),
};
var duplicateIdErrors = ScenarioValidator.Validate(duplicateIdsWagon).Errors;
if (!duplicateIdErrors.Any(error => error.Contains("сцены", StringComparison.Ordinal)) ||
    !duplicateIdErrors.Any(error => error.Contains("улики", StringComparison.Ordinal)) ||
    !duplicateIdErrors.Any(error => error.Contains("финала", StringComparison.Ordinal)))
{
    throw new InvalidOperationException("Scenario validator did not report duplicate graph identifiers.");
}

var invalidScenario = new GeneratedScenario
{
    Options = deterministicOptions,
    Template = new ScenarioTemplate { MinDurationMinutes = 60, MaxDurationMinutes = 120 },
    ConflictCause = "",
    Characters = [new ScenarioCharacter { Goal = "", Motive = "" }],
    Scenes = [new ScenarioScene { Id = "opening" }],
    Branches = [new ScenarioBranch { FromSceneId = "opening", ToSceneId = "missing", Condition = "ошибка" }],
    Endings = [new ScenarioEnding { IsReachable = false, RequiresCombat = true }],
};
var invalidValidation = ScenarioValidator.Validate(invalidScenario);
if (invalidValidation.IsValid || invalidValidation.Errors.Count < 4)
    throw new InvalidOperationException("Scenario validator did not detect invalid roles, ending, branch, conflict and combat.");
var emptySceneIdScenario = new GeneratedScenario
{
    Title = wagon.Title, Seed = wagon.Seed, Options = wagon.Options, Template = wagon.Template, Hook = wagon.Hook, ConflictCause = wagon.ConflictCause,
    StartSceneId = wagon.StartSceneId, PublicBriefing = wagon.PublicBriefing, KnownFacts = wagon.KnownFacts, GmNotes = wagon.GmNotes,
    Characters = wagon.Characters, Clues = wagon.Clues, Branches = wagon.Branches, Endings = wagon.Endings,
    Scenes = wagon.Scenes.Select((scene, index) => index == 0
        ? new ScenarioScene { Id = "", Title = scene.Title, Purpose = scene.Purpose, ClueIds = scene.ClueIds }
        : scene).ToArray()
};
if (!ScenarioValidator.Validate(emptySceneIdScenario).Errors.Any(error => error.Contains("У сцены отсутствует идентификатор", StringComparison.Ordinal)))
    throw new InvalidOperationException("Scenario validator accepted a scene without an identifier.");

var allFactoryExportDirectory = Path.Combine(Path.GetTempPath(), $"skyrim-rp-factory-export-{Guid.NewGuid():N}");
Directory.CreateDirectory(allFactoryExportDirectory);
try
{
    var factoryExportService = new ScenarioExportService();
    var factoryScenarios = new[] { wagon, investigation, mission, council, oldFriend };
    foreach (var factoryScenario in factoryScenarios)
    {
        var factoryExportPath = Path.Combine(allFactoryExportDirectory, factoryScenario.Template.Id + ".json");
        var saved = factoryExportService.Save(factoryExportPath, factoryScenario);
        var restored = factoryExportService.Load(factoryExportPath);
        if (!saved.IsSuccess || !restored.IsSuccess || restored.Scenario is null ||
            restored.Scenario.Template.Id != factoryScenario.Template.Id ||
            restored.Scenario.Seed != factoryScenario.Seed ||
            restored.Scenario.InvestigationBlueprintId != factoryScenario.InvestigationBlueprintId ||
            restored.Scenario.IntrigueBlueprintId != factoryScenario.IntrigueBlueprintId ||
            restored.Scenario.MissionBlueprintId != factoryScenario.MissionBlueprintId ||
            restored.Scenario.NegotiationBlueprintId != factoryScenario.NegotiationBlueprintId ||
            !restored.Scenario.NarrativeFacts.Select(fact => $"{fact.Id}/{fact.Origin}/{fact.SourceEntityId}/{fact.SourceUrl}").SequenceEqual(factoryScenario.NarrativeFacts.Select(fact => $"{fact.Id}/{fact.Origin}/{fact.SourceEntityId}/{fact.SourceUrl}")) ||
            !restored.Scenario.Characters.Select(character => character.Name).SequenceEqual(factoryScenario.Characters.Select(character => character.Name)) ||
            !restored.Scenario.Clues.Select(clue => clue.Id).SequenceEqual(factoryScenario.Clues.Select(clue => clue.Id)) ||
            !restored.Scenario.Scenes.Select(scene => scene.Id).SequenceEqual(factoryScenario.Scenes.Select(scene => scene.Id)) ||
            !restored.Scenario.Branches.Select(branch => $"{branch.FromSceneId}/{branch.ToSceneId}/{branch.ToEndingId}").SequenceEqual(factoryScenario.Branches.Select(branch => $"{branch.FromSceneId}/{branch.ToSceneId}/{branch.ToEndingId}")) ||
            !restored.Scenario.Endings.Select(ending => ending.Id).SequenceEqual(factoryScenario.Endings.Select(ending => ending.Id)))
        {
            throw new InvalidOperationException($"Scenario export round-trip changed factory '{factoryScenario.Template.Id}'.");
        }
    }
}
finally
{
    if (Directory.Exists(allFactoryExportDirectory))
        Directory.Delete(allFactoryExportDirectory, true);
}
var impossible = scenarioGenerator.Generate(new ScenarioGenerationOptions { Genre = "Переговоры", ParticipantCount = 1, DurationMinutes = 5, Seed = 1 });
if (impossible.Scenario is not null || impossible.Validation.IsValid)
    throw new InvalidOperationException("Scenario generator did not fail safely for impossible options.");
var deterministicFactoryOptions = new[]
{
    wagonOptions,
    investigationOptions,
    missionOptions,
    intrigueOptions,
    oldFriendOptions
};
foreach (var options in deterministicFactoryOptions)
{
    var selectedTemplate = scenarioGenerator.GetTemplateByGenre(options.Genre);
    if (selectedTemplate is null || selectedTemplate.Id != scenarioGenerator.Generate(options).Scenario?.Template.Id)
        throw new InvalidOperationException($"Genre '{options.Genre}' did not select its expected factory.");
    var first = scenarioGenerator.Generate(options).Scenario;
    var replay = scenarioGenerator.Generate(options).Scenario;
    if (first is null || replay is null || JsonSerializer.Serialize(first) != JsonSerializer.Serialize(replay))
        throw new InvalidOperationException($"Fixed seed did not reproduce factory '{options.Genre}'.");
}
if (scenarioGenerator.GetTemplateByGenre("") is not null ||
    scenarioGenerator.Generate(new ScenarioGenerationOptions { Genre = "unknown", ParticipantCount = 4, DurationMinutes = 90, Seed = 1 }).Scenario is not null)
{
    throw new InvalidOperationException("Unknown genre was silently replaced by a factory.");
}
Console.WriteLine("Scenario generator OK: five templates, nonlinear Last Wagon, graph validation, secret isolation, meaningful options and fixed seed.");

sealed class RecordingScenarioKnowledgeCatalog : IScenarioKnowledgeCatalog
{
    public ScenarioKnowledgeEvidenceResult GetEvidence(int entityId, int maxLength = 1200) =>
        new() { Status = GetStatus(), Reason = "not_found", Diagnostic = "Каталог тестовых кандидатов не содержит текстов свидетельств." };
    private readonly IReadOnlyList<ScenarioKnowledgeCandidate> _candidates;
    private readonly bool _compatible;

    public RecordingScenarioKnowledgeCatalog(
        IReadOnlyList<ScenarioKnowledgeCandidate> candidates,
        bool compatible = true)
    {
        _candidates = candidates;
        _compatible = compatible;
    }

    public List<ScenarioKnowledgeSearchRequest> Requests { get; } = new();

    public ScenarioKnowledgeCatalogStatus GetStatus() => new()
    {
        IsAvailable = _compatible,
        IsCompatible = _compatible,
        Diagnostic = _compatible ? "Тестовый каталог доступен." : "Тестовый каталог недоступен.",
        SchemaVersion = _compatible ? ScenarioKnowledgeCatalog.ExpectedSchemaVersion : ""
    };

    public ScenarioKnowledgeSearchResult Search(ScenarioKnowledgeSearchRequest request)
    {
        Requests.Add(request);
        var status = GetStatus();
        if (!status.IsCompatible)
            return new ScenarioKnowledgeSearchResult { Status = status };

        var candidates = _candidates
            .Where(candidate => string.IsNullOrWhiteSpace(request.SemanticType) || candidate.SemanticType == request.SemanticType)
            .Where(candidate => string.IsNullOrWhiteSpace(request.Category) || candidate.PrimaryCategory == request.Category || candidate.Categories.Contains(request.Category))
            .Where(candidate => request.IncludeNeedsReview || !candidate.NeedsReview)
            .OrderBy(candidate => candidate.Title, StringComparer.Ordinal)
            .ThenBy(candidate => candidate.EntityId)
            .Take(request.Limit)
            .ToArray();
        return new ScenarioKnowledgeSearchResult { Status = status, Candidates = candidates };
    }
}

sealed class RecordingKnowledgeAssistantModel : IKnowledgeAssistantModel
{
    private readonly string? _answer;
    private readonly Exception? _failure;

    public RecordingKnowledgeAssistantModel(string? answer, Exception? failure = null)
    {
        _answer = answer;
        _failure = failure;
    }

    public List<string> Prompts { get; } = new();

    public Task<string> GenerateAsync(string prompt, CancellationToken cancellationToken)
    {
        Prompts.Add(prompt);
        if (_failure is not null)
            throw _failure;
        return Task.FromResult(_answer ?? string.Empty);
    }
}
