using System.Drawing;
using System.Windows.Forms;

namespace TibiaScarabEye.UI;

// Cores do menu de contexto no tema escuro do programa.
internal sealed class DarkMenuColors : ProfessionalColorTable {
    static readonly Color Back=Color.FromArgb(39,39,37), Hot=Color.FromArgb(79,79,74), Edge=Color.FromArgb(22,22,21);
    public override Color ToolStripDropDownBackground { get { return Back; } }
    public override Color ImageMarginGradientBegin { get { return Back; } }
    public override Color ImageMarginGradientMiddle { get { return Back; } }
    public override Color ImageMarginGradientEnd { get { return Back; } }
    public override Color MenuBorder { get { return Edge; } }
    public override Color MenuItemBorder { get { return Color.FromArgb(133,133,126); } }
    public override Color MenuItemSelected { get { return Hot; } }
    public override Color MenuItemSelectedGradientBegin { get { return Hot; } }
    public override Color MenuItemSelectedGradientEnd { get { return Hot; } }
    public override Color SeparatorDark { get { return Hot; } }
    public override Color SeparatorLight { get { return Back; } }
}
