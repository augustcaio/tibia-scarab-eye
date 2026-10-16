using System;
using System.Drawing;
using System.Windows.Forms;

namespace TibiaScarabEye.UI;

// Interruptor ligado/desligado no estilo da interface: trilho afundado, botão de pedra com a mesma moldura biselada dos botões,
// e o dourado do tema quando está ligado. Clique ou Espaço alternam.
internal sealed class AlertSwitch : Control {
    bool isChecked, hover;
    public event EventHandler CheckedChanged;
    public AlertSwitch() {
        SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.SupportsTransparentBackColor|ControlStyles.Selectable|ControlStyles.StandardClick,true);
        BackColor=Color.Transparent; Cursor=Cursors.Hand; TabStop=true; Margin=Padding.Empty;
        AccessibleRole=AccessibleRole.CheckButton;
    }
    public bool Checked {
        get { return isChecked; }
        set { if(isChecked==value) return; isChecked=value; Invalidate(); if(CheckedChanged!=null) CheckedChanged(this,EventArgs.Empty); }
    }
    protected override Size DefaultSize { get { return new Size(LogicalToDeviceUnits(58),LogicalToDeviceUnits(26)); } }
    public override Size GetPreferredSize(Size proposedSize) { return DefaultSize; }

    public void PerformClick() { OnClick(EventArgs.Empty); }
    protected override void OnClick(EventArgs e) { Checked=!Checked; base.OnClick(e); }
    protected override bool IsInputKey(Keys keyData) { return keyData==Keys.Space || base.IsInputKey(keyData); }
    protected override void OnKeyUp(KeyEventArgs e) { if(e.KeyCode==Keys.Space) Checked=!Checked; base.OnKeyUp(e); }
    protected override void OnMouseEnter(EventArgs e) { hover=true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hover=false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
    protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
    protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }

    protected override void OnPaint(PaintEventArgs e) {
        var g=e.Graphics;
        var track=new Rectangle(0,0,Width,Height);
        using(var fill=new SolidBrush(isChecked?Color.FromArgb(70,62,34):Color.FromArgb(39,39,37))) g.FillRectangle(fill,track);
        // Trilho afundado: o inverso da moldura dos botões (sombra em cima e à esquerda, brilho embaixo e à direita).
        using(var shade=new Pen(Color.FromArgb(22,22,21))) using(var glint=new Pen(Color.FromArgb(106,106,100))) {
            g.DrawLine(shade,0,0,Width-1,0); g.DrawLine(shade,0,0,0,Height-1);
            g.DrawLine(glint,0,Height-1,Width-1,Height-1); g.DrawLine(glint,Width-1,0,Width-1,Height-1);
        }
        int gap=LogicalToDeviceUnits(3), knobWidth=Width/2-gap;
        var knob=new Rectangle(isChecked?Width-gap-knobWidth:gap,gap,knobWidth,Height-2*gap);
        using(var body=new SolidBrush(hover?Color.FromArgb(88,88,83):Theme.Surface)) g.FillRectangle(body,knob);
        Theme.Frame(g,knob);
        var label=isChecked?new Rectangle(gap,0,Width-knobWidth-2*gap,Height):new Rectangle(knob.Right,0,Width-knob.Right-gap,Height);
        using(var font=new Font(Font.FontFamily,Font.Size-1,FontStyle.Bold))
            TextRenderer.DrawText(g,isChecked?"ON":"OFF",font,label,isChecked?Theme.Gold:Theme.Muted,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.NoPadding);
        if(Focused) ControlPaint.DrawFocusRectangle(g,Rectangle.Inflate(track,-2,-2),Theme.Ink,Color.Transparent);
    }
}
