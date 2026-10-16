using System.Windows;
using System.Windows.Media;
using SkyrimRpEncyclopedia.Models;

namespace SkyrimRpEncyclopedia.Views;

/// <summary>A single bounded drawing layer, not one WPF Shape per relation.</summary>
internal sealed class GraphEdgeLayer : FrameworkElement
{
    internal IReadOnlyList<GraphEdge> Edges { get; set; } = Array.Empty<GraphEdge>();
    internal IReadOnlyDictionary<GraphEntityKey, (double X, double Y)> Positions { get; set; } = new Dictionary<GraphEntityKey, (double, double)>();
    private readonly Pen _pen = new(new SolidColorBrush(Color.FromRgb(96, 117, 132)), 1);
    public GraphEdgeLayer() { IsHitTestVisible = false; _pen.Freeze(); }
    protected override void OnRender(DrawingContext dc)
    {
        foreach (var edge in Edges)
        {
            if (!Positions.TryGetValue(edge.Source, out var a) || !Positions.TryGetValue(edge.Target, out var b)) continue;
            var start = new Point(a.X + 80, a.Y + 32); var end = new Point(b.X + 80, b.Y + 32);
            var direction = end - start; if (direction.Length < 1) continue; direction.Normalize();
            start += direction * 28; end -= direction * 28;
            dc.DrawLine(_pen, start, end);
            var side = new Vector(-direction.Y, direction.X);
            dc.DrawLine(_pen, end, end - direction * 8 + side * 4);
            dc.DrawLine(_pen, end, end - direction * 8 - side * 4);
        }
    }
}
