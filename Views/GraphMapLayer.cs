using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SkyrimRpEncyclopedia.Models;
using SkyrimRpEncyclopedia.Services;

namespace SkyrimRpEncyclopedia.Views;

/// <summary>One retained drawing surface. Frozen batched geometry, spatial hit testing, bounded viewport labels.</summary>
internal sealed class GraphMapLayer : FrameworkElement
{
    internal GraphMapDrawing? Drawing { get; set; }
    internal IReadOnlySet<GraphEntityKey> VisibleKeys { get; set; } = new HashSet<GraphEntityKey>();
    internal BitmapSource? Edges { get; set; }
    internal Geometry? Nodes { get; set; }
    internal GraphCamera Camera { get; set; } = new();
    internal GraphEntityKey? Selected { get; set; }
    private readonly Typeface _font = new("Segoe UI");
    internal GraphEntityKey? Hit(Point screen)
    {
        if (Drawing is null) return null;
        var world = Camera.ToWorld(screen); var radius = Math.Max(7,8 / Camera.Scale);
        var nearest = radius * radius; GraphEntityKey? result = null;
        for (var x=(int)Math.Floor((world.X-radius)/128); x<=(int)Math.Floor((world.X+radius)/128); x++)
        for (var y=(int)Math.Floor((world.Y-radius)/128); y<=(int)Math.Floor((world.Y+radius)/128); y++)
        {
            if (!Drawing.Cells.TryGetValue((x,y),out var cell)) continue;
            foreach (var key in cell)
            { if (!VisibleKeys.Contains(key)) continue; var delta=Drawing.Positions[key]-world; if (delta.LengthSquared <= nearest) { nearest=delta.LengthSquared; result=key; } }
        }
        return result;
    }
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc); if (Drawing is null) return;
        dc.PushTransform(new MatrixTransform(Camera.Matrix));
        if(Edges is not null && !Drawing.Bounds.IsEmpty) dc.DrawImage(Edges,Drawing.Bounds);
        dc.DrawGeometry(Brushes.LightSlateGray, null, Nodes);
        if (Selected is GraphEntityKey selected && VisibleKeys.Contains(selected))
        {
            var selectedPen = new Pen(Brushes.Goldenrod, 1.4/Camera.Scale);
            foreach (var edge in Drawing.Adjacency[selected])
                if (VisibleKeys.Contains(edge.Source) && VisibleKeys.Contains(edge.Target)) dc.DrawLine(selectedPen, Drawing.Positions[edge.Source], Drawing.Positions[edge.Target]);
            dc.DrawEllipse(Brushes.Goldenrod, selectedPen, Drawing.Positions[selected],7,7);
        }
        // No labels at distant zoom. At closer zoom draw at most 80 visible labels, never one TextBlock per node.
        if (Camera.Scale >= .65)
        {
            var a=Camera.ToWorld(new(0,0)); var b=Camera.ToWorld(new(ActualWidth,ActualHeight)); var count=0;
            var labels=new List<Rect>();
            for(var x=(int)Math.Floor(a.X/128); x<=(int)Math.Floor(b.X/128) && count<80; x++)
            for(var y=(int)Math.Floor(a.Y/128); y<=(int)Math.Floor(b.Y/128) && count<80; y++)
            {
                if (!Drawing.Cells.TryGetValue((x,y),out var cell)) continue;
                foreach(var key in cell)
                {
                    var p=Drawing.Positions[key]; if (!VisibleKeys.Contains(key) || p.X<a.X || p.Y<a.Y || p.X>b.X || p.Y>b.Y) continue;
                    var labelBounds=new Rect(p.X+8,p.Y-6,130,16);
                    if(labels.Any(r=>r.IntersectsWith(labelBounds))) continue;
                    var text=new FormattedText(Drawing.Snapshot.Nodes[key].Title,CultureInfo.CurrentUICulture,FlowDirection.LeftToRight,_font,10,Brushes.LightGray,VisualTreeHelper.GetDpi(this).PixelsPerDip)
                        { MaxTextWidth=130, MaxLineCount=1, Trimming=TextTrimming.CharacterEllipsis };
                    dc.DrawText(text,new(p.X+8,p.Y-6)); labels.Add(labelBounds); if(++count>=80) break;
                }
            }
        }
        dc.Pop();
    }
}
