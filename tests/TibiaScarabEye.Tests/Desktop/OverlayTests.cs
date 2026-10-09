using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using TibiaScarabEye.Interop;
using TibiaScarabEye.Layouts;
using TibiaScarabEye.UI;
using Xunit;
using static TibiaScarabEye.Tests.Desktop.DesktopHarness;

namespace TibiaScarabEye.Tests.Desktop;

[Trait("Category", "Desktop")]
public class OverlayTests
{
    private const int GwlExStyle = -20, GwlStyle = -16;
    private const int WsExToolWindow = 0x80, WsExTransparent = 0x20, WsChild = 0x40000000;

    [WinFormsFact]
    public void NativeLifecycle_KeepsObsEligibilityAndStableTitleAcrossLockAndDispose()
    {
        using var source = new SourceWindow();
        var spec = new RegionSpec { Name = "Vida e mana", X = .1, Y = .2, W = .3, H = .4, Left = -100, Top = -50, Width = 300, Height = 200, Opacity = 80, ObsId = "stable-test-region" };
        using var overlay = new Overlay(source.Handle, spec);
        overlay.Location = Offscreen;
        overlay.Show();
        Application.DoEvents();
        overlay.Render();
        string obsTitle = overlay.Text;
        Assert.NotEqual(0, Native.GetStyle(overlay.Handle, GwlExStyle) & WsExToolWindow);

        overlay.SetObsMode(true);
        Assert.True(Native.IsWindowVisible(overlay.Handle) && !Native.IsIconic(overlay.Handle) && (Native.GetStyle(overlay.Handle, GwlExStyle) & WsExToolWindow) == 0 && (Native.GetStyle(overlay.Handle, GwlStyle) & WsChild) == 0, "OBS window eligibility failed");
        Assert.True(obsTitle.Contains(spec.Name) && obsTitle.Contains(spec.ObsId), "OBS title not stable or identifiable");

        overlay.CaptureSpec();
        int y = overlay.Spec.Top, h = overlay.Spec.Height;
        overlay.SetLocked(true);
        overlay.CaptureSpec();
        Assert.True(overlay.Spec.Top == y && overlay.Spec.Height == h, "Lock moved content geometry");
        Assert.NotEqual(0, Native.GetStyle(overlay.Handle, GwlExStyle) & WsExTransparent);
        Assert.True((Native.GetStyle(overlay.Handle, GwlExStyle) & WsExToolWindow) == 0 && overlay.Text == obsTitle, "Lock lost OBS eligibility or title");

        overlay.SetLocked(false);
        overlay.CaptureSpec();
        Assert.True(overlay.Spec.Top == y && overlay.Spec.Height == h, "Unlock geometry failed");
        Assert.Equal(0, Native.GetStyle(overlay.Handle, GwlExStyle) & WsExTransparent);

        overlay.SetObsMode(false);
        Assert.NotEqual(0, Native.GetStyle(overlay.Handle, GwlExStyle) & WsExToolWindow);
        overlay.Render();
        overlay.Close();
    }

    public static IEnumerable<object[]> ThinHudSizes() => new[]
    {
        new object[] { new Size(240, 20) },
        new object[] { new Size(400, 24) },
        new object[] { new Size(120, 32) },
        new object[] { new Size(100, 100) },
    };

    [WinFormsTheory]
    [MemberData(nameof(ThinHudSizes))]
    public void RepeatedLockUnlock_PreservesThinHudGeometryAndPosition(Size contentSize)
    {
        using var source = new SourceWindow();
        var thin = new RegionSpec { Name = "Thin HUD regression", X = .1, Y = .1, W = .4, H = .04, Left = 100, Top = 100, Width = contentSize.Width, Height = contentSize.Height, Opacity = 100 };
        using var overlay = new Overlay(source.Handle, thin);
        overlay.Location = Offscreen;
        overlay.Show();
        Application.DoEvents();
        Rectangle original = overlay.Bounds;
        Rectangle content = new Rectangle(original.X, original.Y + Overlay.Bar, original.Width, original.Height - Overlay.Bar);

        for (int cycle = 0; cycle < 8; cycle++)
        {
            overlay.SetLocked(true);
            Assert.True(overlay.Bounds == content, "Lock changed content position/size for " + contentSize + " cycle " + cycle);
            overlay.CaptureSpec();
            Assert.True(overlay.Spec.Width == contentSize.Width && overlay.Spec.Height == contentSize.Height, "Lock changed saved thin HUD size");
            overlay.SetLocked(false);
            Assert.True(overlay.Bounds == original, "Unlock grew or shifted thin HUD " + contentSize + " cycle " + cycle + " expected " + original + " actual " + overlay.Bounds);
        }
        overlay.Close();
    }

    [WinFormsFact]
    public void EditingRegion_KeepsPositionHandleObsIdentityAndHotkeyVisibility()
    {
        using var source = new SourceWindow();
        var editable = new RegionSpec { Name = "Before edit", X = .1, Y = .1, W = .2, H = .2, Left = 100, Top = 100, Width = 200, Height = 120, Opacity = 80 };
        string temp = Path.Combine(Path.GetTempPath(), "tibiascarabeye-edit-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            using var overlay = new Overlay(source.Handle, editable);
            overlay.Location = Offscreen;
            overlay.Show();
            overlay.SetObsMode(true);
            overlay.SetLocked(true);
            string title = overlay.Text, id = editable.ObsId;
            IntPtr handle = overlay.Handle;
            Point position = overlay.Location;

            using (var editor = new Selector(source.Handle, editable))
            {
                ShowOffscreen(editor);
                using (var dimensions = new Thumbnail(editor.Handle, source.Handle))
                    Assert.Equal(editable.Crop(dimensions.SourceSize), Field<Rectangle>(editor, "selection"));
                Field<NumericUpDown>(editor, "exactSide").Value = 160;
                Field<NumericUpDown>(editor, "exactHeight").Value = 40;
                Field<ComboBox>(editor, "name").Text = "Edited region";
                Assert.True(editable.Name == "Before edit" && editable.W == .2, "Editor changed original before save");
                Click(editor, "Salvar alterações");
                Assert.NotNull(editor.Result);
                overlay.UpdateRegion(editor.Result);
                editor.Close();
            }
            Assert.True(editable.Name == "Edited region" && editable.ObsId == id && overlay.Handle == handle && overlay.Text == title && overlay.Location == position, "Editing lost position, HWND or OBS identity");

            var editedLayout = new Layout();
            editedLayout.Regions.Add(editable);
            editedLayout.Save(temp);
            using (var reloaded = new Overlay(source.Handle, Layout.Load(temp).Regions[0]))
                Assert.Equal(title, reloaded.Text);
            Assert.True(editable.Width == 200 && editable.Height == 50 && editable.Opacity == 80, "Editing lost scale, aspect or opacity");

            using (var bitmap = new Bitmap(overlay.Width, overlay.Height))
            {
                overlay.DrawToBitmap(bitmap, new Rectangle(Point.Empty, overlay.Size));
                Assert.Equal(Color.FromArgb(133, 133, 126).ToArgb(), bitmap.GetPixel(0, 0).ToArgb());
                SaveArtifact(bitmap, "overlay-border-preview.png");
            }

            using (var main = new MainForm())
            {
                SetField(main, "source", source.Handle);
                Field<List<Overlay>>(main, "overlays").Add(overlay);
                SetField(main, "locked", true); // as overlays so aparecem no modo jogo
                const int WmHotkey = 0x312, HideShowHotkeyId = 2;
                void PressHotkey() => Call(main, "WndProc", Message.Create(main.Handle, WmHotkey, new IntPtr(HideShowHotkeyId), IntPtr.Zero));
                PressHotkey();
                Call(main, "TickSource");
                Call(main, "TickSource");
                Assert.False(overlay.Visible, "Timer restored an intentionally hidden overlay");
                PressHotkey();
                Assert.True(overlay.Visible && overlay.Location == position && overlay.Handle == handle, "Show hotkey lost visibility, geometry or HWND");
                main.Close();
            }
            overlay.Close();
        }
        finally
        {
            if (File.Exists(temp)) File.Delete(temp);
        }
    }
}
