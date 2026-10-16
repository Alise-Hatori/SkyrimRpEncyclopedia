using SkyrimRpEncyclopedia.Services;

namespace SkyrimRpEncyclopedia.Models;

public readonly record struct GraphEntityKey(ArticleCatalogSource SourceKind, int Id)
{
    public static GraphEntityKey FromArticle(Article a) => new(a.IsMvp ? ArticleCatalogSource.Knowledge :
        a.IsOfficial ? ArticleCatalogSource.Official : ArticleCatalogSource.User,
        a.IsMvp ? a.Id - MvpKnowledgeDatabaseService.ArticleIdOffset : a.Id);
}
public readonly record struct GraphEdgeKey(ArticleCatalogSource Store, int Id);
public sealed record GraphNode(GraphEntityKey Key, Article Article);
public sealed record GraphEdge(GraphEdgeKey Key, GraphEntityKey Source, GraphEntityKey Target, string Type, string Description);
public sealed record GraphSearchResult(IReadOnlyList<GraphNode> Nodes, bool Limited, IReadOnlyList<string> Warnings);
public sealed record GraphPage(GraphEntityKey Root, IReadOnlyList<GraphNode> Nodes, IReadOnlyList<GraphEdge> Edges,
    IReadOnlyDictionary<ArticleCatalogSource, int> Cursor, bool HasMore, IReadOnlyList<string> Warnings);
public interface IGraphDataReader
{
    GraphSearchResult Search(string query, IReadOnlyCollection<ArticleCatalogSource> sources, int limit, CancellationToken token);
    GraphPage ReadPage(GraphEntityKey root, IReadOnlyDictionary<ArticleCatalogSource, int> cursor, int neighbors, CancellationToken token);
}
