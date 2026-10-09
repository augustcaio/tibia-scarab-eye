using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using TibiaScarabEye.Interop;
using TibiaScarabEye.Layouts;
using TibiaScarabEye.UI;
using Xunit;
using static TibiaScarabEye.Tests.Desktop.DesktopHarness;

namespace TibiaScarabEye.Tests.Desktop;

[Trait("Category", "Desktop")]
public class PlannerTests
{
    private const int Cell = Planner.CellPixels;

    // Overlay pronta (janela criada, nao exibida) 100 px dentro do canto da janela de origem.
    private static Overlay NewOverlay(SourceWindow source, string name, int offsetX, int offsetY)
    {
        var window = Native.ThumbnailBounds(source.Handle);
        var spec = new RegionSpec { Name = name, X = .1, Y = .1, W = .2, H = .1, Left = window.Left + offsetX, Top = window.Top + offsetY, Width = 120, Height = 40, Opacity = 100 };
        var overlay = new Overlay(source.Handle, spec);
        overlay.Prepare();
        overlay.Bounds = new Rectangle(spec.Left, spec.Top, spec.Width + 2 * Overlay.Border, spec.Height + 2 * Overlay.Border);
        return overlay;
    }

    // Ponto da tela do planejador sobre o canto superior esquerdo (+ margem) da overlay, e o fator de escala da previa.
    private static Point ViewPoint(Planner planner, Overlay overlay, SourceWindow source, int inset)
    {
        var window = Native.ThumbnailBounds(source.Handle);
        var preview = Field<Rectangle>(planner, "preview");
        double scale = (double)preview.Width / Field<Size>(planner, "sourceSize").Width;
        return new Point(preview.Left + (int)Math.Round((overlay.Left - window.Left) * scale) + inset, preview.Top + (int)Math.Round((overlay.Top - window.Top) * scale) + inset);
    }

    [WinFormsFact]
    public void Dragging_SnapsToTheGridAndApplyMovesTheOverlayOnScreen()
    {
        using var source = new SourceWindow();
        using var overlay = NewOverlay(source, "Vida", 100, 100);
        var window = Native.ThumbnailBounds(source.Handle);
        using var planner = new Planner(source.Handle, new List<Overlay> { overlay }, 0);
        ShowOffscreen(planner);

        Point from = ViewPoint(planner, overlay, source, 4);
        Down(planner, MouseButtons.Left, from.X, from.Y);
        Move(planner, MouseButtons.Left, from.X + 150, from.Y + 90);
        Up(planner, MouseButtons.Left, from.X + 150, from.Y + 90);
        Assert.True(overlay.Left - window.Left == 100 && overlay.Top - window.Top == 100, "A overlay nao pode mudar de lugar antes de aplicar");

        Click(planner, "Aplicar posições");
        int x = overlay.Left - window.Left, y = overlay.Top - window.Top;
        Assert.True(planner.Moved, "Mover e aplicar deve marcar o layout como alterado");
        Assert.True(x % Cell == 0 && y % Cell == 0, $"Posicao {x},{y} fora da grade de {Cell}px");
        Assert.True(x > 100 && y > 100, $"A overlay deveria ter andado para baixo e para a direita, mas esta em {x},{y}");
        Assert.True(overlay.Spec.Left == overlay.Left && overlay.Spec.Top == overlay.Top, "O RegionSpec precisa acompanhar a nova posicao");
        Assert.False(overlay.Visible, "O planejador nao pode mostrar a overlay na tela");
    }

    [WinFormsFact]
    public void Cancel_LeavesTheOverlayWhereItWas()
    {
        using var source = new SourceWindow();
        using var overlay = NewOverlay(source, "Vida", 100, 100);
        Rectangle before = overlay.Bounds;
        using var planner = new Planner(source.Handle, new List<Overlay> { overlay }, 0);
        ShowOffscreen(planner);

        Point from = ViewPoint(planner, overlay, source, 4);
        Down(planner, MouseButtons.Left, from.X, from.Y);
        Move(planner, MouseButtons.Left, from.X + 200, from.Y + 120);
        Up(planner, MouseButtons.Left, from.X + 200, from.Y + 120);
        Click(planner, "Cancelar");

        Assert.Equal(before, overlay.Bounds);
        Assert.False(planner.Moved);
    }

    [WinFormsFact]
    public void Dragging_StaysInsideTheGameWindow()
    {
        using var source = new SourceWindow();
        using var overlay = NewOverlay(source, "Vida", 100, 100);
        var window = Native.ThumbnailBounds(source.Handle);
        using var planner = new Planner(source.Handle, new List<Overlay> { overlay }, 0);
        ShowOffscreen(planner);

        Point from = ViewPoint(planner, overlay, source, 4);
        Down(planner, MouseButtons.Left, from.X, from.Y);
        Move(planner, MouseButtons.Left, from.X + 5000, from.Y + 5000);
        Up(planner, MouseButtons.Left, from.X + 5000, from.Y + 5000);
        Click(planner, "Aplicar posições");

        var inside = new Rectangle(window.Left, window.Top, window.Right - window.Left, window.Bottom - window.Top);
        Assert.True(inside.Contains(overlay.Bounds), $"Overlay {overlay.Bounds} saiu da janela {inside}");
    }

    [WinFormsFact]
    public void ArrowKeys_MoveTheSelectedAreaByOneCell()
    {
        using var source = new SourceWindow();
        using var overlay = NewOverlay(source, "Vida", 96, 96);
        var window = Native.ThumbnailBounds(source.Handle);
        using var planner = new Planner(source.Handle, new List<Overlay> { overlay }, 0);
        ShowOffscreen(planner);

        Message message = default;
        Assert.True((bool)Call(planner, "ProcessCmdKey", message, Keys.Right));
        Assert.True((bool)Call(planner, "ProcessCmdKey", message, Keys.Down));
        Click(planner, "Aplicar posições");

        Assert.Equal(96 + Cell, overlay.Left - window.Left);
        Assert.Equal(96 + Cell, overlay.Top - window.Top);
    }

    [WinFormsFact]
    public void AreaOutsideTheGameWindow_IsBroughtBackInsideWhenApplied()
    {
        using var source = new SourceWindow();
        using var overlay = NewOverlay(source, "Vida", -3000, -3000);
        var window = Native.ThumbnailBounds(source.Handle);
        using var planner = new Planner(source.Handle, new List<Overlay> { overlay }, 0);
        ShowOffscreen(planner);

        Click(planner, "Aplicar posições");

        var inside = new Rectangle(window.Left, window.Top, window.Right - window.Left, window.Bottom - window.Top);
        Assert.True(inside.Contains(overlay.Bounds), $"Overlay {overlay.Bounds} continuou fora da janela {inside}");
    }
}
