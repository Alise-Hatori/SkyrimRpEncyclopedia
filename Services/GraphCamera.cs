using System.Windows;
using System.Windows.Media;

namespace SkyrimRpEncyclopedia.Services;

/// <summary>Screen = world * Scale + Offset. No ScrollViewer/layout scaling involved.</summary>
public sealed class GraphCamera
{
    public double Scale { get; private set; } = 1;
    public Vector Offset { get; private set; }
    public Matrix Matrix => new(Scale, 0, 0, Scale, Offset.X, Offset.Y);
    public Point ToWorld(Point screen) => new((screen.X - Offset.X) / Scale, (screen.Y - Offset.Y) / Scale);
    public void Zoom(double scale, Point anchor)
    {
        if (!double.IsFinite(scale) || scale <= 0 || !Finite(anchor)) return;
        var world = ToWorld(anchor); var bounded = Math.Clamp(scale, .1, 4);
        var offset = new Vector(anchor.X - world.X * bounded, anchor.Y - world.Y * bounded);
        if (!Finite(offset)) return;
        Scale = bounded; Offset = offset;
    }
    public void Pan(Vector delta) { var offset=Offset+delta; if (Finite(offset)) Offset=offset; }
    public void Center(Point world, Size viewport)
    {
        if (!Finite(world) || !Valid(viewport)) return;
        var offset=new Vector(viewport.Width / 2 - world.X * Scale, viewport.Height / 2 - world.Y * Scale);
        if(Finite(offset)) Offset=offset;
    }
    public void Fit(Rect bounds, Size viewport)
    {
        if (bounds.IsEmpty || !Valid(viewport) || !Finite(bounds.TopLeft) || !double.IsFinite(bounds.Width) || !double.IsFinite(bounds.Height)) return;
        Scale = Math.Clamp(Math.Min(Math.Max(1, viewport.Width - 40) / Math.Max(1, bounds.Width), Math.Max(1, viewport.Height - 40) / Math.Max(1, bounds.Height)), .1, 4);
        Center(new(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2), viewport);
    }
    public void Resize(Size oldSize, Size newSize)
    { if (Valid(oldSize) && Valid(newSize)) Center(ToWorld(new(oldSize.Width / 2, oldSize.Height / 2)), newSize); }
    private static bool Finite(Point p) => double.IsFinite(p.X) && double.IsFinite(p.Y);
    private static bool Finite(Vector p) => double.IsFinite(p.X) && double.IsFinite(p.Y);
    private static bool Valid(Size s) => s.Width > 0 && s.Height > 0 && double.IsFinite(s.Width) && double.IsFinite(s.Height);
}
