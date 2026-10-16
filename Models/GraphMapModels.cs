using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SkyrimRpEncyclopedia.Services;

namespace SkyrimRpEncyclopedia.Models;

// Deliberately no Article, Content, ContentRu or deferred text loader in the full-map snapshot.
public sealed record GraphMapNode(GraphEntityKey Key, string Title, string Category, ArticleGraphType Type);
public sealed record GraphMapProgress(int Nodes, int Edges, long TotalNodes, long TotalEdges, string Phase);
public sealed record GraphMapSnapshot(IReadOnlyDictionary<GraphEntityKey, GraphMapNode> Nodes,
    IReadOnlyList<GraphEdge> Edges, long ExpectedNodes, long ExpectedEdges);
public sealed record GraphMapDrawing(GraphMapSnapshot Snapshot,
    IReadOnlyDictionary<GraphEntityKey, Point> Positions, Rect Bounds,
    BitmapSource EdgeImage, Geometry NodeGeometry,
    IReadOnlyDictionary<(int X, int Y), GraphEntityKey[]> Cells,
    IReadOnlyDictionary<GraphEntityKey, GraphEdge[]> Adjacency);
public interface IGraphMapReader
{
    GraphMapSnapshot Read(CancellationToken token, IProgress<GraphMapProgress>? progress = null);
    Article ReadArticle(GraphEntityKey key, CancellationToken token);
}
