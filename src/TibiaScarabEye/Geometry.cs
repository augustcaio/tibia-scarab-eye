using System;
using System.Collections.Generic;
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
    public enum AlignKind { Left, CenterH, Right, Top, MiddleV, Bottom }
    // Alinha os retângulos entre si, usando como referência o retângulo que envolve todos. O resultado segue a ordem de entrada.
    public static Rectangle[] Align(IList<Rectangle> rects,AlignKind kind) {
        var result=new Rectangle[rects.Count];
        if(rects.Count==0) return result;
        Rectangle bounds=rects[0];
        foreach(var r in rects) bounds=Rectangle.Union(bounds,r);
        for(int i=0;i<result.Length;i++) {
            Rectangle r=rects[i];
            switch(kind) {
                case AlignKind.Left: r.X=bounds.Left; break;
                case AlignKind.CenterH: r.X=bounds.Left+(bounds.Width-r.Width)/2; break;
                case AlignKind.Right: r.X=bounds.Right-r.Width; break;
                case AlignKind.Top: r.Y=bounds.Top; break;
                case AlignKind.MiddleV: r.Y=bounds.Top+(bounds.Height-r.Height)/2; break;
                default: r.Y=bounds.Bottom-r.Height; break;
            }
            result[i]=r;
        }
        return result;
    }
    // Mantém os dois retângulos das pontas e iguala o vão entre todos, na ordem em que aparecem no eixo. Precisa de 3 ou mais.
    public static Rectangle[] Distribute(IList<Rectangle> rects,bool horizontal) {
        var result=new Rectangle[rects.Count];
        rects.CopyTo(result,0);
        if(rects.Count<3) return result;
        var order=new List<int>();
        for(int i=0;i<rects.Count;i++) order.Add(i);
        order.Sort(delegate(int a,int b) { return horizontal?(rects[a].Left+rects[a].Right).CompareTo(rects[b].Left+rects[b].Right):(rects[a].Top+rects[a].Bottom).CompareTo(rects[b].Top+rects[b].Bottom); });
        int start=horizontal?rects[order[0]].Left:rects[order[0]].Top, end=horizontal?rects[order[order.Count-1]].Right:rects[order[order.Count-1]].Bottom, total=0;
        foreach(int i in order) total+=horizontal?rects[i].Width:rects[i].Height;
        double gap=(double)(end-start-total)/(order.Count-1), cursor=start;
        foreach(int i in order) {
            Rectangle r=result[i];
            if(horizontal) { r.X=(int)Math.Round(cursor); cursor+=r.Width+gap; } else { r.Y=(int)Math.Round(cursor); cursor+=r.Height+gap; }
            result[i]=r;
        }
        return result;
    }
    // Encaixa bordas e centros de 'moving' nos de 'others' quando ficam a até 'threshold' pixels. Devolve o deslocamento a aplicar
    // e preenche as posições das guias (x para as verticais, y para as horizontais) que passaram a coincidir.
    public static Point SnapToEdges(Rectangle moving,IEnumerable<Rectangle> others,int threshold,List<int> verticalGuides,List<int> horizontalGuides) {
        int dx=BestOffset(new[]{moving.Left,(moving.Left+moving.Right)/2,moving.Right},others,true,threshold);
        int dy=BestOffset(new[]{moving.Top,(moving.Top+moving.Bottom)/2,moving.Bottom},others,false,threshold);
        Rectangle moved=moving; moved.Offset(dx,dy);
        if(verticalGuides!=null) CollectGuides(new[]{moved.Left,(moved.Left+moved.Right)/2,moved.Right},others,true,verticalGuides);
        if(horizontalGuides!=null) CollectGuides(new[]{moved.Top,(moved.Top+moved.Bottom)/2,moved.Bottom},others,false,horizontalGuides);
        return new Point(dx,dy);
    }
    static int BestOffset(int[] mine,IEnumerable<Rectangle> others,bool horizontal,int threshold) {
        int best=0, bestDistance=threshold+1;
        foreach(var o in others) foreach(int theirs in horizontal?new[]{o.Left,(o.Left+o.Right)/2,o.Right}:new[]{o.Top,(o.Top+o.Bottom)/2,o.Bottom})
            foreach(int m in mine) { int d=theirs-m; if(Math.Abs(d)<bestDistance) { bestDistance=Math.Abs(d); best=d; } }
        return bestDistance>threshold?0:best;
    }
    static void CollectGuides(int[] mine,IEnumerable<Rectangle> others,bool horizontal,List<int> guides) {
        foreach(var o in others) foreach(int theirs in horizontal?new[]{o.Left,(o.Left+o.Right)/2,o.Right}:new[]{o.Top,(o.Top+o.Bottom)/2,o.Bottom})
            foreach(int m in mine) if(m==theirs && !guides.Contains(m)) guides.Add(m);
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
