using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace TibiaScarabEye.UI;

// Desenha, acima da miniatura do jogo no editor de áreas: o contorno de cada overlay, o recorte de origem das áreas
// selecionadas (tracejado), o retângulo que está sendo desenhado ou selecionado e as guias de alinhamento.
internal sealed class EditorAdorner : Adorner {
    internal struct Mark { public Rectangle Rect; public bool Selected, Hidden, Locked; }
    static readonly Color SourceColor=Color.FromArgb(0,210,255), LockedColor=Color.FromArgb(150,200,255), HiddenColor=Color.FromArgb(150,150,142), GuideColor=Color.FromArgb(255,70,160);
    Rectangle preview, drawing, marquee;
    Mark[] marks=new Mark[0];
    Rectangle[] sources=new Rectangle[0];
    int[] verticalGuides=new int[0], horizontalGuides=new int[0];
    internal void Update(Rectangle preview,Mark[] marks,Rectangle[] sources,Rectangle drawing,Rectangle marquee,int[] verticalGuides,int[] horizontalGuides) {
        this.preview=preview; this.marks=marks; this.sources=sources; this.drawing=drawing; this.marquee=marquee;
        this.verticalGuides=verticalGuides; this.horizontalGuides=horizontalGuides; Invalidate();
    }
    protected override void OnPaint(PaintEventArgs e) {
        base.OnPaint(e);
        if(preview.IsEmpty) return;
        var g=e.Graphics;
        g.SetClip(preview);
        foreach(var mark in marks) if(!mark.Selected) DrawMark(g,mark);
        foreach(var mark in marks) if(mark.Selected) DrawMark(g,mark);
        foreach(var source in sources) Outline(g,source,SourceColor,2,4,DashStyle.Dash);
        using(var guide=new Pen(GuideColor)) {
            foreach(int x in verticalGuides) g.DrawLine(guide,x,preview.Top,x,preview.Bottom);
            foreach(int y in horizontalGuides) g.DrawLine(guide,preview.Left,y,preview.Right,y);
        }
        if(!drawing.IsEmpty) Outline(g,drawing,Theme.Gold,3,5,DashStyle.Solid);
        if(!marquee.IsEmpty) Outline(g,marquee,Color.White,1,3,DashStyle.Dash);
    }
    static void DrawMark(Graphics g,Mark mark) {
        Color color=mark.Selected?Theme.Gold:mark.Hidden?HiddenColor:mark.Locked?LockedColor:Color.White;
        Outline(g,mark.Rect,color,mark.Selected?3:2,mark.Selected?5:4,mark.Hidden?DashStyle.Dot:DashStyle.Solid);
    }
    static void Outline(Graphics g,Rectangle rect,Color color,int width,int shadowWidth,DashStyle style) {
        using(var shadow=new Pen(Color.Black,shadowWidth)) using(var border=new Pen(color,width) { DashStyle=style }) {
            g.DrawRectangle(shadow,rect);
            g.DrawRectangle(border,rect);
        }
    }
}
