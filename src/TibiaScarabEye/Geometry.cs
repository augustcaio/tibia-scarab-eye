using System;
using System.Drawing;
using System.Windows.Forms;

namespace TibiaScarabEye;

internal static class Geometry {
    public static Rectangle Fit(Size source, Rectangle area) {
        if(source.Width<1 || source.Height<1 || area.Width<1 || area.Height<1) return Rectangle.Empty;
        double scale=Math.Min((double)area.Width/source.Width,(double)area.Height/source.Height);
        int w=Math.Max(1,(int)(source.Width*scale)), h=Math.Max(1,(int)(source.Height*scale));
        return new Rectangle(area.Left+(area.Width-w)/2,area.Top+(area.Height-h)/2,w,h);
    }
    // Leva o canto superior esquerdo à linha de grade mais próxima; a grade começa no canto da área do mapa.
    public static Point SnapToGrid(Point location,Rectangle grid,int cell) {
        if(cell<1 || grid.IsEmpty) return location;
        return new Point(grid.Left+(int)Math.Round((double)(location.X-grid.Left)/cell)*cell,grid.Top+(int)Math.Round((double)(location.Y-grid.Top)/cell)*cell);
    }
    // Mantém o retângulo dentro de uma área que começa em (0,0); se for maior que ela, fica no canto superior esquerdo.
    public static Rectangle ClampInside(Rectangle rect,Size bounds) {
        return new Rectangle(Math.Max(0,Math.Min(rect.X,bounds.Width-rect.Width)),Math.Max(0,Math.Min(rect.Y,bounds.Height-rect.Height)),rect.Width,rect.Height);
    }
    public static Rectangle Restore(Rectangle requested) {
        // Keep at least the entire small overlay inside a currently connected monitor.
        var screen=Screen.FromRectangle(requested).WorkingArea;
        int w=Math.Min(requested.Width,screen.Width), h=Math.Min(requested.Height,screen.Height);
        return new Rectangle(Math.Max(screen.Left,Math.Min(requested.Left,screen.Right-w)),Math.Max(screen.Top,Math.Min(requested.Top,screen.Bottom-h)),w,h);
    }
}
