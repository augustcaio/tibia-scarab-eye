using System;
using System.Drawing;
using System.Windows.Forms;
using TibiaScarabEye.Interop;
using TibiaScarabEye.UI;
using Xunit;
using static TibiaScarabEye.Tests.Desktop.DesktopHarness;

namespace TibiaScarabEye.Tests.Desktop;

[Trait("Category", "Desktop")]
public class SelectorInteractionTests
{
    [WinFormsFact]
    public void SquareMode_ShowsGuidesMovesZoomsPansAndCreatesExactCrop()
    {
        using var source = new SourceWindow();
        using var selector = new Selector(source.Handle);
        ShowOffscreen(selector);

        var squareControl = Field<CheckBox>(selector, "squareOnly");
        Assert.False(squareControl.Checked, "Free rectangular selection is not the default");
        squareControl.Checked = true;

        Move(selector, MouseButtons.None, 300, 200);
        Assert.Equal(Cursors.Cross, selector.Cursor);
        var adorner = Field<SelectionAdorner>(selector, "adorner");
        Assert.True(adorner.Visible && adorner.Owner == selector && adorner.Bounds == selector.RectangleToScreen(selector.ClientRectangle), "Selection guides not above or aligned with preview");
        Assert.Equal(0x80020, Native.GetStyle(adorner.Handle, -20) & 0x80020);
        using (var bitmap = new Bitmap(adorner.Width, adorner.Height))
        {
            adorner.DrawToBitmap(bitmap, new Rectangle(Point.Empty, adorner.Size));
            Assert.Equal(Color.White.ToArgb(), bitmap.GetPixel(300, 208).ToArgb());
            Assert.Equal(Color.White.ToArgb(), bitmap.GetPixel(308, 200).ToArgb());
            Assert.Equal(Color.Magenta.ToArgb(), bitmap.GetPixel(350, 200).ToArgb());
        }

        Drag(selector, MouseButtons.Left, new Point(300, 200), new Point(450, 300));
        Rectangle drawn = Field<Rectangle>(selector, "selection");
        Assert.True(drawn.Width == drawn.Height && drawn.Width > 0, "Mouse drag did not create a square");
        using (var bitmap = new Bitmap(adorner.Width, adorner.Height))
        {
            adorner.DrawToBitmap(bitmap, new Rectangle(Point.Empty, adorner.Size));
            Assert.Equal(Theme.Gold.ToArgb(), bitmap.GetPixel(adorner.Selection.Left, adorner.Selection.Top + 10).ToArgb());
            SaveArtifact(bitmap, "selection-guides.png");
        }

        Move(selector, MouseButtons.None, 350, 230);
        Assert.Equal(Cursors.SizeAll, selector.Cursor);
        Leave(selector);
        Assert.True(!adorner.Pointer.HasValue && selector.Cursor == Cursors.Default && !adorner.Selection.IsEmpty, "Leaving preview did not hide cross or lost selection");

        Drag(selector, MouseButtons.Left, new Point(350, 230), new Point(380, 250));
        Rectangle moved = Field<Rectangle>(selector, "selection");
        Assert.True(moved.Size == drawn.Size && moved.Location != drawn.Location, "Moving selection changed size or failed to move");

        Point anchor = new Point(400, 300);
        Point beforeZoom = (Point)Call(selector, "ToSource", anchor);
        Wheel(selector, anchor.X, anchor.Y, 120);
        Point afterZoom = (Point)Call(selector, "ToSource", anchor);
        Assert.True(Math.Abs(beforeZoom.X - afterZoom.X) <= 1 && Math.Abs(beforeZoom.Y - afterZoom.Y) <= 1, "Zoom lost the pointer anchor");
        Assert.Equal(moved, Field<Rectangle>(selector, "selection"));

        Rectangle beforePan = Field<Rectangle>(selector, "viewport");
        Drag(selector, MouseButtons.Right, new Point(400, 300), new Point(450, 320));
        Rectangle afterPan = Field<Rectangle>(selector, "viewport");
        Assert.True(afterPan.Size == beforePan.Size && afterPan.Location != beforePan.Location, "Zoomed preview did not pan");
        Assert.Equal(moved, Field<Rectangle>(selector, "selection"));
        Drag(selector, MouseButtons.Right, new Point(400, 300), new Point(10000, 10000));
        Assert.Equal(Point.Empty, Field<Rectangle>(selector, "viewport").Location);

        var zoomControl = Field<ComboBox>(selector, "previewZoom");
        zoomControl.SelectedIndex = 3;
        Rectangle magnified = Field<Rectangle>(selector, "viewport");
        Assert.True(magnified.Width < beforePan.Width && magnified.Height < beforePan.Height, "8x zoom did not magnify");
        zoomControl.SelectedIndex = 0;
        Assert.Equal(Point.Empty, Field<Rectangle>(selector, "viewport").Location);
        Assert.Equal(moved, Field<Rectangle>(selector, "selection"));

        zoomControl.SelectedIndex = 2;
        Point zoomStart = (Point)Call(selector, "ToSource", new Point(500, 400));
        Point zoomEnd = (Point)Call(selector, "ToSource", new Point(530, 430));
        Drag(selector, MouseButtons.Left, new Point(500, 400), new Point(530, 430));
        var fullSize = Field<Size>(selector, "sourceSize");
        Assert.Equal(Selector.Square(zoomStart, zoomEnd, fullSize), Field<Rectangle>(selector, "selection"));

        Field<NumericUpDown>(selector, "exactSide").Value = 64;
        Field<NumericUpDown>(selector, "exactX").Value = 17;
        Field<NumericUpDown>(selector, "exactY").Value = 23;
        using (var bitmap = new Bitmap(selector.Width, selector.Height))
        {
            selector.DrawToBitmap(bitmap, new Rectangle(Point.Empty, selector.Size));
            SaveArtifact(bitmap, "selection-preview.png");
        }
        Click(selector, "Criar recorte");
        Assert.NotNull(selector.Result);
        selector.Result.Validate();
        using (var dimensions = new Thumbnail(selector.Handle, source.Handle))
            Assert.Equal(new Rectangle(17, 23, 64, 64), selector.Result.Crop(dimensions.SourceSize));
        Assert.True(selector.Result.Width == 64 && selector.Result.Height == 64, "Initial overlay is not square");
    }

    [WinFormsFact]
    public void FreeMode_KeepsIndependentWidthHeightAndPansWithToolOrMiddleButton()
    {
        using var source = new SourceWindow();
        using var selector = new Selector(source.Handle);
        ShowOffscreen(selector);

        Drag(selector, MouseButtons.Left, new Point(300, 200), new Point(450, 260));
        Rectangle free = Field<Rectangle>(selector, "selection");
        Assert.True(free.Width > free.Height && free.Height > 0, "Free drag constrained the rectangle");

        Field<ComboBox>(selector, "previewZoom").SelectedIndex = 1;
        Rectangle before = Field<Rectangle>(selector, "viewport");
        var panTool = Field<Button>(selector, "panTool");
        panTool.PerformClick();
        Drag(selector, MouseButtons.Left, new Point(400, 300), new Point(450, 320));
        Assert.True(Field<Rectangle>(selector, "viewport") != before && Field<Rectangle>(selector, "selection") == free, "Pan tool altered selection or failed to navigate");
        panTool.PerformClick();

        before = Field<Rectangle>(selector, "viewport");
        Drag(selector, MouseButtons.Middle, new Point(400, 300), new Point(420, 320));
        Assert.True(Field<Rectangle>(selector, "viewport") != before && Field<Rectangle>(selector, "selection") == free, "Middle-button pan altered selection or failed");

        Field<NumericUpDown>(selector, "exactSide").Value = 120;
        Field<NumericUpDown>(selector, "exactHeight").Value = 40;
        Field<NumericUpDown>(selector, "exactX").Value = 17;
        Field<NumericUpDown>(selector, "exactY").Value = 23;
        using (var bitmap = new Bitmap(selector.Width, selector.Height))
        {
            selector.DrawToBitmap(bitmap, new Rectangle(Point.Empty, selector.Size));
            SaveArtifact(bitmap, "selection-preview.png");
        }
        Click(selector, "Criar recorte");
        using (var dimensions = new Thumbnail(selector.Handle, source.Handle))
            Assert.Equal(new Rectangle(17, 23, 120, 40), selector.Result.Crop(dimensions.SourceSize));
        selector.Result.Validate();
    }
}
