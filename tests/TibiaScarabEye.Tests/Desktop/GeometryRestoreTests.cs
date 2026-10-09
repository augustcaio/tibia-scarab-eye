using System.Drawing;
using System.Windows.Forms;
using Xunit;

namespace TibiaScarabEye.Tests.Desktop;

[Trait("Category", "Desktop")]
public class GeometryRestoreTests
{
    [WinFormsFact]
    public void Restore_BringsDisconnectedMonitorRectangleBackOnScreen()
    {
        Rectangle restored = Geometry.Restore(new Rectangle(-100000, -100000, 300, 200));

        Assert.True(Screen.FromRectangle(restored).WorkingArea.Contains(restored), "Offscreen restore failed");
    }
}
