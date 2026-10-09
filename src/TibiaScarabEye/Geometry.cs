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
    // Quadrado com origem em 'anchor' na direção de 'end', limitado a uma área que começa em (0,0). Vazio se não houve arrasto.
    public static Rectangle Square(Point anchor,Point end,Size size) {
        int dx=end.X-anchor.X,dy=end.Y-anchor.Y;
        int side=Math.Max(Math.Abs(dx),Math.Abs(dy));
        side=Math.Min(side,Math.Min(dx<0?anchor.X:size.Width-anchor.X,dy<0?anchor.Y:size.Height-anchor.Y));
        return side<=0?Rectangle.Empty:new Rectangle(dx<0?anchor.X-side:anchor.X,dy<0?anchor.Y-side:anchor.Y,side,side);
    }
    // Seleção em blocos de slots (34, 70, 106 px...): cada lado pula para o tamanho de slots mais próximo que cabe até a borda.
    public static Rectangle SlotBlock(Point anchor,Point end,Size size,bool square) {
        int dx=end.X-anchor.X, dy=end.Y-anchor.Y;
        if(dx==0 && dy==0) return Rectangle.Empty;
        int roomX=dx<0?anchor.X:size.Width-anchor.X, roomY=dy<0?anchor.Y:size.Height-anchor.Y, w, h;
        if(square) w=h=SnapToSlots(Math.Max(Math.Abs(dx),Math.Abs(dy)),Math.Min(roomX,roomY));
        else { w=SnapToSlots(Math.Abs(dx),roomX); h=SnapToSlots(Math.Abs(dy),roomY); }
        return new Rectangle(dx<0?anchor.X-w:anchor.X,dy<0?anchor.Y-h:anchor.Y,w,h);
    }
    // Slot do Tibia (action bar, equipamento, container): 34 px, com passo de 36 px entre slots da action bar.
    public const int SlotSize=34, SlotPitch=36;
    // Tamanhos de blocos de slots vizinhos da action bar: 34, 70, 106 px... O terceiro, 106, é a largura do minimapa.
    // Escolhe o mais próximo de 'length' que caiba em 'max'.
    public static int SnapToSlots(int length,int max) {
        int n=Math.Max(0,(int)Math.Round((double)(length-SlotSize)/SlotPitch,MidpointRounding.AwayFromZero));
        int size=SlotSize+n*SlotPitch;
        while(size>max && n>0) size=SlotSize+--n*SlotPitch;
        return Math.Max(1,Math.Min(size,max));
    }
    // Leva o canto superior esquerdo à linha de grade mais próxima; a grade começa no canto da janela do jogo.
    public static Point SnapToGrid(Point location,int cell) {
        if(cell<1) return location;
        return new Point((int)Math.Round((double)location.X/cell)*cell,(int)Math.Round((double)location.Y/cell)*cell);
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
