using SkyrimRpEncyclopedia.Models;

namespace SkyrimRpEncyclopedia.Services;

/// <summary>Transactional in-memory page merge; caps never discard cursors of unaccepted data.</summary>
public sealed class GraphExplorerState
{
    public const int MaxNodes = 200, MaxEdges = 600;
    public Dictionary<GraphEntityKey, GraphNode> Nodes { get; } = new();
    public Dictionary<GraphEdgeKey, GraphEdge> Edges { get; } = new();
    public Dictionary<GraphEntityKey, (double X, double Y)> Positions { get; } = new();
    private readonly Dictionary<GraphEntityKey, GraphPage> _pages = new();
    public GraphEntityKey? Center { get; private set; }
    public void Clear() { Nodes.Clear(); Edges.Clear(); Positions.Clear(); _pages.Clear(); Center = null; }
    public void Reset(GraphNode center)
    {
        Clear(); Center = center.Key;
        Nodes[center.Key] = center; Positions[center.Key] = (700, 450);
    }
    public IReadOnlyDictionary<ArticleCatalogSource, int> Cursor(GraphEntityKey root) =>
        _pages.TryGetValue(root, out var page) ? page.Cursor : new Dictionary<ArticleCatalogSource, int>();
    public bool HasMore(GraphEntityKey root) => !_pages.TryGetValue(root, out var page) || page.HasMore;
    public bool TryMerge(GraphPage page)
    {
        if (Nodes.Count + page.Nodes.Select(n => n.Key).Distinct().Count(k => !Nodes.ContainsKey(k)) > MaxNodes ||
            Edges.Count + page.Edges.Select(e => e.Key).Distinct().Count(k => !Edges.ContainsKey(k)) > MaxEdges) return false;
        foreach (var node in page.Nodes)
        {
            Nodes[node.Key] = node;
            if (!Positions.ContainsKey(node.Key))
            {
                var p = Positions.GetValueOrDefault(page.Root, (700d, 450d));
                var slot = Positions.Count;
                var angle = slot * 2.399963229728653;
                var radius = 180 + 100 * Math.Sqrt(slot);
                Positions[node.Key] = (Math.Max(30, p.Item1 + Math.Cos(angle) * radius), Math.Max(30, p.Item2 + Math.Sin(angle) * radius));
            }
        }
        foreach (var edge in page.Edges) Edges[edge.Key] = edge;
        _pages[page.Root] = page;
        return true;
    }
}
