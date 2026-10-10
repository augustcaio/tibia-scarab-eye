using System;
using System.Drawing;
using System.Windows.Forms;
using TibiaScarabEye.UI;
using Xunit;
using static TibiaScarabEye.Tests.Desktop.DesktopHarness;

namespace TibiaScarabEye.Tests.Desktop;

[Trait("Category", "Desktop")]
public class ThemedControlsTests
{
    [WinFormsFact]
    public void GemSlider_ClampsTheValueAndRaisesTheEventOnlyOnChange()
    {
        using var slider = new GemSlider { Minimum = 20, Maximum = 100, Value = 100 };
        int raised = 0;
        slider.ValueChanged += delegate { raised++; };

        slider.Value = 500;
        Assert.Equal(100, slider.Value);
        Assert.Equal(0, raised);

        slider.Value = 5;
        Assert.Equal(20, slider.Value);
        Assert.Equal(1, raised);
    }

    [WinFormsFact]
    public void GemSlider_FollowsTheMouseAndTheKeyboard()
    {
        using var form = new Form { Size = new Size(300, 120) };
        var slider = new GemSlider { Minimum = 0, Maximum = 100, Value = 0, Width = 212, Location = new Point(0, 0) };
        form.Controls.Add(slider);
        ShowOffscreen(form);

        Down(slider, MouseButtons.Left, slider.Width / 2, 10);
        Up(slider, MouseButtons.Left, slider.Width / 2, 10);
        Assert.InRange(slider.Value, 48, 52);

        Call(slider, "OnKeyDown", new KeyEventArgs(Keys.End));
        Assert.Equal(100, slider.Value);
        Call(slider, "OnKeyDown", new KeyEventArgs(Keys.Left));
        Assert.Equal(99, slider.Value);
        Call(slider, "OnKeyDown", new KeyEventArgs(Keys.PageDown));
        Assert.Equal(79, slider.Value);
    }

    [WinFormsFact]
    public void DisabledSlider_IgnoresTheMouse()
    {
        using var form = new Form { Size = new Size(300, 120) };
        var slider = new GemSlider { Minimum = 0, Maximum = 100, Value = 10, Width = 212, Enabled = false };
        form.Controls.Add(slider);
        ShowOffscreen(form);

        Down(slider, MouseButtons.Left, 190, 10);

        Assert.Equal(10, slider.Value);
    }

    [WinFormsFact]
    public void ThemedButton_PaintsPrimaryInGoldAndSecondaryOnTheRaisedSurface()
    {
        using var form = new Form { Size = new Size(300, 140), BackColor = Theme.Background };
        var primary = new ThemedButton { Text = "Principal", Primary = true, Size = new Size(120, 32), Location = new Point(10, 10) };
        var secondary = new ThemedButton { Text = "Secundario", Size = new Size(120, 32), Location = new Point(10, 60) };
        form.Controls.Add(primary); form.Controls.Add(secondary);
        ShowOffscreen(form);

        using var p = new Bitmap(primary.Width, primary.Height);
        primary.DrawToBitmap(p, new Rectangle(Point.Empty, primary.Size));
        using var s = new Bitmap(secondary.Width, secondary.Height);
        secondary.DrawToBitmap(s, new Rectangle(Point.Empty, secondary.Size));

        Color gold = p.GetPixel(8, 16), raised = s.GetPixel(8, 16);
        Assert.True(gold.R > 180 && gold.G > 110, $"O botao principal deveria ser dourado, mas o pixel foi {gold}");
        Assert.True(Math.Abs(raised.R - Theme.Raised.R) <= 3 && Math.Abs(raised.G - Theme.Raised.G) <= 3, $"O botao secundario deveria usar a superficie elevada, mas o pixel foi {raised}");
    }
}
