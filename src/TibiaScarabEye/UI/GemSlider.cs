using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace TibiaScarabEye.UI;

// Controle deslizante com a gema vermelha do Tibia como puxador: trilho fino que se enche de ouro até a gema.
// Mesma interface mínima do TrackBar (Minimum, Maximum, Value, TickFrequency, ValueChanged), com mouse e teclado.
internal sealed class GemSlider : Control {
    const int Pad=12, GemHalfWidth=7, GemHalfHeight=9;
    int minimum, maximum=100, value, tickFrequency=20;
    bool dragging;
    public event EventHandler ValueChanged;
    public GemSlider() {
        SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.Selectable|ControlStyles.StandardClick,true);
        TabStop=true; Height=30; Cursor=Cursors.Hand;
    }
    public int Minimum { get { return minimum; } set { minimum=value; Value=this.value; Invalidate(); } }
    public int Maximum { get { return maximum; } set { maximum=value; Value=this.value; Invalidate(); } }
    public int TickFrequency { get { return tickFrequency; } set { tickFrequency=Math.Max(1,value); Invalidate(); } }
    public int Value {
        get { return value; }
        set {
            int clamped=Math.Max(minimum,Math.Min(maximum,value));
            if(clamped==this.value) return;
            this.value=clamped; Invalidate();
            if(ValueChanged!=null) ValueChanged(this,EventArgs.Empty);
        }
    }
    float ThumbX { get { return Pad+(maximum==minimum?0f:(float)(value-minimum)/(maximum-minimum)*(Width-2*Pad)); } }
    void SetFromX(int x) {
        if(Width<=2*Pad) return;
        Value=minimum+(int)Math.Round((double)(x-Pad)/(Width-2*Pad)*(maximum-minimum));
    }
    protected override bool IsInputKey(Keys keyData) {
        switch(keyData&Keys.KeyCode) { case Keys.Left: case Keys.Right: case Keys.Up: case Keys.Down: case Keys.Home: case Keys.End: case Keys.PageUp: case Keys.PageDown: return true; }
        return base.IsInputKey(keyData);
    }
    protected override void OnKeyDown(KeyEventArgs e) {
        int step=tickFrequency;
        switch(e.KeyCode) {
            case Keys.Left: case Keys.Down: Value-=1; break;
            case Keys.Right: case Keys.Up: Value+=1; break;
            case Keys.PageDown: Value-=step; break;
            case Keys.PageUp: Value+=step; break;
            case Keys.Home: Value=minimum; break;
            case Keys.End: Value=maximum; break;
            default: base.OnKeyDown(e); return;
        }
        e.Handled=true;
    }
    protected override void OnMouseDown(MouseEventArgs e) { if(e.Button!=MouseButtons.Left || !Enabled) return; Focus(); dragging=true; Capture=true; SetFromX(e.X); base.OnMouseDown(e); }
    protected override void OnMouseMove(MouseEventArgs e) { if(dragging) SetFromX(e.X); base.OnMouseMove(e); }
    protected override void OnMouseUp(MouseEventArgs e) { dragging=false; Capture=false; base.OnMouseUp(e); }
    protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
    protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
    protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }
    protected override void OnPaint(PaintEventArgs e) {
        var g=e.Graphics;
        g.Clear(Theme.Background);
        g.SmoothingMode=SmoothingMode.AntiAlias;
        int cy=Height/2; float x=ThumbX;
        var track=new Rectangle(Pad,cy-2,Width-2*Pad,4);
        using(var back=new SolidBrush(Theme.Line)) g.FillRectangle(back,track);
        if(Enabled && x>Pad) using(var fill=new LinearGradientBrush(new RectangleF(Pad,cy-2,Math.Max(1,x-Pad),4),Theme.GoldMid,Theme.Gold,0f)) g.FillRectangle(fill,Pad,cy-2,x-Pad,4);
        using(var tick=new Pen(Theme.Line))
            for(int v=minimum;v<=maximum;v+=tickFrequency) { float tx=Pad+(float)(v-minimum)/Math.Max(1,maximum-minimum)*(Width-2*Pad); g.DrawLine(tick,tx,cy+7,tx,cy+10); }
        var gem=new PointF[]{ new PointF(x-3,cy-GemHalfHeight),new PointF(x+3,cy-GemHalfHeight),new PointF(x+GemHalfWidth,cy-4),new PointF(x+GemHalfWidth,cy+4),new PointF(x+3,cy+GemHalfHeight),new PointF(x-3,cy+GemHalfHeight),new PointF(x-GemHalfWidth,cy+4),new PointF(x-GemHalfWidth,cy-4) };
        Color top=Enabled?Color.FromArgb(240,104,88):Color.FromArgb(84,98,102), bottom=Enabled?Color.FromArgb(142,22,24):Color.FromArgb(52,64,68);
        using(var body=new LinearGradientBrush(new RectangleF(x-GemHalfWidth,cy-GemHalfHeight,GemHalfWidth*2,GemHalfHeight*2),top,bottom,90f)) g.FillPolygon(body,gem);
        using(var rim=new Pen(Enabled?Theme.GoldMid:Theme.Line,1.5f)) g.DrawPolygon(rim,gem);
        if(Enabled) using(var shine=new SolidBrush(Color.FromArgb(200,255,226,214))) g.FillPolygon(shine,new PointF[]{ new PointF(x-4,cy-6),new PointF(x-1,cy-6),new PointF(x-4,cy-2) });
        if(Focused && ShowFocusCues) using(var ring=new Pen(Color.FromArgb(150,Theme.Gold)) { DashStyle=DashStyle.Dot }) g.DrawRectangle(ring,1,1,Width-3,Height-3);
    }
}
