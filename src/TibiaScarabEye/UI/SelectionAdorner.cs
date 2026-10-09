using System.Drawing;
using System.Windows.Forms;

namespace TibiaScarabEye.UI;

// Separate owned window keeps the guides above DWM's live thumbnail surface.
// Layered + transparent styles pass mouse input to the selector underneath.
internal sealed class SelectionAdorner : Form {
    internal Rectangle Preview { get; private set; }
    internal Rectangle Selection { get; private set; }
    internal Point? Pointer { get; private set; }
    internal SelectionAdorner() {
        FormBorderStyle=FormBorderStyle.None; ShowInTaskbar=false;
        StartPosition=FormStartPosition.Manual; AutoScaleMode=AutoScaleMode.None;
        BackColor=Color.Magenta; TransparencyKey=Color.Magenta; DoubleBuffered=true;
    }
    protected override bool ShowWithoutActivation { get { return true; } }
    protected override CreateParams CreateParams { get { var p=base.CreateParams; p.ExStyle|=0x80000|0x20|0x80|0x08000000; return p; } }
    internal void UpdateGuides(Rectangle preview,Rectangle selection,Point? pointer) {
        Preview=preview; Selection=selection; Pointer=pointer; Invalidate();
    }
    protected override void OnPaint(PaintEventArgs e) {
        base.OnPaint(e);
        if(Preview.IsEmpty) return;
        e.Graphics.SetClip(Preview);
        if(!Selection.IsEmpty) {
            using(var shadow=new Pen(Color.Black,5)) using(var border=new Pen(Theme.Gold,3)) {
                e.Graphics.DrawRectangle(shadow,Selection);
                e.Graphics.DrawRectangle(border,Selection);
            }
        }
        if(Pointer.HasValue) {
            Point p=Pointer.Value;
            using(var shadow=new Pen(Color.Black,3)) using(var light=new Pen(Color.White,1)) {
                e.Graphics.DrawLine(shadow,p.X-12,p.Y,p.X+12,p.Y);
                e.Graphics.DrawLine(shadow,p.X,p.Y-12,p.X,p.Y+12);
                e.Graphics.DrawLine(light,p.X-12,p.Y,p.X+12,p.Y);
                e.Graphics.DrawLine(light,p.X,p.Y-12,p.X,p.Y+12);
            }
        }
    }
}
