using System.Drawing;
using System.Windows.Forms;

namespace TibiaScarabEye.UI;

// Separate owned window keeps the guides above DWM's live thumbnail surface.
// Layered + transparent styles pass mouse input to the selector underneath.
internal abstract class Adorner : Form {
    protected Adorner() {
        FormBorderStyle=FormBorderStyle.None; ShowInTaskbar=false;
        StartPosition=FormStartPosition.Manual; AutoScaleMode=AutoScaleMode.None;
        DoubleBuffered=true;
    }
    protected override bool ShowWithoutActivation { get { return true; } }
    protected override CreateParams CreateParams { get { var p=base.CreateParams; p.ExStyle|=0x80000|0x20|0x80|0x08000000; return p; } }
}
