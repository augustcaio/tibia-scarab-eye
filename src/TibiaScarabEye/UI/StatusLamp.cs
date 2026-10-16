using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace TibiaScarabEye.UI;

internal enum LampState { Searching, Reading, Minimized }

// Placa com uma lâmpada que mostra se o Tibia está sendo lido: vermelha enquanto procura, verde lendo, âmbar se a janela
// do jogo está minimizada. O texto diz o que fazer quando não está lendo.
internal sealed class StatusLamp : Control {
    LampState state=LampState.Searching;
    string headline="", detail="";
    public StatusLamp() {
        SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.SupportsTransparentBackColor,true);
        Height=36; Dock=DockStyle.Fill; Margin=Padding.Empty; BackColor=Color.Transparent;
    }
    public LampState State { get { return state; } }
    public string Headline { get { return headline; } }
    public string Detail { get { return detail; } }
    public void Set(LampState newState,string newHeadline,string newDetail) {
        if(state==newState && headline==newHeadline && detail==newDetail) return;
        state=newState; headline=newHeadline; detail=newDetail; Invalidate();
    }
    Color LampColor { get { return state==LampState.Reading?Color.FromArgb(76,201,84):state==LampState.Minimized?Color.FromArgb(234,170,48):Color.FromArgb(206,70,58); } }
    protected override void OnPaint(PaintEventArgs e) {
        var g=e.Graphics;
        g.SmoothingMode=SmoothingMode.AntiAlias;
        var plaque=new Rectangle(0,0,Width-1,Height-1);
        using(var back=new SolidBrush(Color.FromArgb(39,39,37))) g.FillRectangle(back,plaque);
        using(var border=new Pen(Color.FromArgb(79,79,74))) g.DrawRectangle(border,plaque);
        var lamp=new Rectangle(14,Height/2-7,14,14);
        Color color=LampColor;
        using(var halo=new SolidBrush(Color.FromArgb(state==LampState.Reading?70:40,color))) g.FillEllipse(halo,lamp.X-4,lamp.Y-4,lamp.Width+8,lamp.Height+8);
        using(var body=new LinearGradientBrush(lamp,ControlPaint.Light(color,0.5f),ControlPaint.Dark(color,0.1f),90f)) g.FillEllipse(body,lamp);
        using(var rim=new Pen(Color.FromArgb(22,22,21))) g.DrawEllipse(rim,lamp);
        g.SmoothingMode=SmoothingMode.Default;
        int left=lamp.Right+14;
        using(var bold=new Font(Font,FontStyle.Bold)) {
            var size=TextRenderer.MeasureText(g,headline,bold,new Size(int.MaxValue,Height),TextFormatFlags.NoPadding);
            TextRenderer.DrawText(g,headline,bold,new Rectangle(left,0,size.Width+2,Height),Theme.Ink,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.NoPadding);
            TextRenderer.DrawText(g,detail,Font,new Rectangle(left+size.Width+10,0,Width-left-size.Width-20,Height),Theme.Muted,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPadding);
        }
    }
}
