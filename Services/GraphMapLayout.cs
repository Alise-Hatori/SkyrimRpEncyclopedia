using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SkyrimRpEncyclopedia.Models;

namespace SkyrimRpEncyclopedia.Services;

public static class GraphMapLayout
{
    // Linear-time stable placement, not a force simulation. Existing map coordinates survive filters/camera changes.
    public static GraphMapDrawing Prepare(GraphMapSnapshot snapshot, CancellationToken token)
    {
        var positions = new Dictionary<GraphEntityKey, Point>();
        var columns = Math.Max(1, (int)Math.Ceiling(Math.Sqrt(snapshot.Nodes.Count)));
        var index = 0;
        foreach (var node in snapshot.Nodes.Values.OrderBy(n => n.Type).ThenBy(n => n.Key.Id))
        { token.ThrowIfCancellationRequested(); positions.Add(node.Key, new(30 + index % columns * 44, 30 + index / columns * 44)); index++; }
        var cells = positions.GroupBy(p => ((int)Math.Floor(p.Value.X / 128), (int)Math.Floor(p.Value.Y / 128)))
            .ToDictionary(g => g.Key, g => g.Select(p => p.Key).ToArray());
        var adjacency = snapshot.Nodes.Keys.ToDictionary(k => k, _ => new List<GraphEdge>());
        foreach (var edge in snapshot.Edges)
        {
            token.ThrowIfCancellationRequested();
            adjacency[edge.Source].Add(edge); if (edge.Target != edge.Source) adjacency[edge.Target].Add(edge);
        }
        var bounds = positions.Count == 0 ? Rect.Empty : new Rect(positions.Values.Aggregate((a, b) => new(Math.Min(a.X,b.X), Math.Min(a.Y,b.Y))),
            positions.Values.Aggregate((a, b) => new(Math.Max(a.X,b.X), Math.Max(a.Y,b.Y))));
        if (!bounds.IsEmpty) bounds.Inflate(12,12);
        var all = snapshot.Nodes.Keys.ToHashSet();
        var geometry = Geometry(snapshot, positions, all, token);
        return new(snapshot, positions, bounds, geometry.Edges, geometry.Nodes, cells, adjacency.ToDictionary(p => p.Key, p => p.Value.ToArray()));
    }
    public static (BitmapSource Edges, Geometry Nodes) Geometry(GraphMapSnapshot snapshot, IReadOnlyDictionary<GraphEntityKey, Point> positions,
        IReadOnlySet<GraphEntityKey> visible, CancellationToken token)
    {
        // Fixed-size overview: ALL edges contribute, but no UI-thread tessellation of 74k lines.
        // Exact directions/endpoints remain in the snapshot and selected-node overlay.
        const int size=1536;
        var pixels=new byte[size*size*4];
        var bounds = positions.Count==0 ? new Rect(0,0,1,1) : new Rect(
            positions.Values.Aggregate((a,b)=>new(Math.Min(a.X,b.X),Math.Min(a.Y,b.Y))),
            positions.Values.Aggregate((a,b)=>new(Math.Max(a.X,b.X),Math.Max(a.Y,b.Y))));
        bounds.Inflate(12,12);
        foreach (var edge in snapshot.Edges)
        {
            token.ThrowIfCancellationRequested(); if (!visible.Contains(edge.Source) || !visible.Contains(edge.Target)) continue;
            var a=positions[edge.Source]; var b=positions[edge.Target];
            var x0=(int)((a.X-bounds.X)/Math.Max(1,bounds.Width)*(size-1)); var y0=(int)((a.Y-bounds.Y)/Math.Max(1,bounds.Height)*(size-1));
            var x1=(int)((b.X-bounds.X)/Math.Max(1,bounds.Width)*(size-1)); var y1=(int)((b.Y-bounds.Y)/Math.Max(1,bounds.Height)*(size-1));
            var dx=Math.Abs(x1-x0); var sx=x0<x1?1:-1; var dy=-Math.Abs(y1-y0); var sy=y0<y1?1:-1; var error=dx+dy;
            while(true)
            {
                var i=(y0*size+x0)*4;
                // Premultiplied slate-blue, bounded opacity in dense regions.
                var alpha=(byte)Math.Min(80,pixels[i+3]+5); pixels[i]=(byte)(alpha*175/255); pixels[i+1]=(byte)(alpha*155/255); pixels[i+2]=(byte)(alpha*125/255); pixels[i+3]=alpha;
                if(x0==x1&&y0==y1) break; var twice=2*error;
                if(twice>=dy) {error+=dy; x0+=sx;} if(twice<=dx) {error+=dx; y0+=sy;}
            }
        }
        token.ThrowIfCancellationRequested();
        var edges=BitmapSource.Create(size,size,96,96,PixelFormats.Pbgra32,null,pixels,size*4); edges.Freeze();
        var nodes = new StreamGeometry();
        using (var c = nodes.Open()) foreach (var key in visible)
        {
            token.ThrowIfCancellationRequested(); var p = positions[key];
            // Small diamonds are a single geometry, not thousands of interactive WPF controls.
            c.BeginFigure(new(p.X,p.Y-5), true, true);
            c.LineTo(new(p.X+5,p.Y), true,false); c.LineTo(new(p.X,p.Y+5),true,false); c.LineTo(new(p.X-5,p.Y),true,false);
        }
        nodes.Freeze(); return (edges,nodes);
    }
}
