using System.Linq;
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
public class AreaEditorTests
{
    private const int Cell = AreaEditor.CellPixels;
    private static readonly int[] SlotBlocks = { 34, 70, 106, 142, 178, 214, 250, 286, 322, 358, 394, 430, 466, 502, 538, 574, 610, 646 };

    // Janela de origem falsa + overlays no mesmo papel que o MainForm tem: o editor recebe a lista e as funcoes de criar/remover.
    private sealed class Rig : IDisposable
    {
        public readonly SourceWindow Source = new SourceWindow();
        public readonly List<Overlay> Overlays = new List<Overlay>();
        public readonly List<RegionSpec> Created = new List<RegionSpec>();
        public AreaEditor Editor;
        public int Changes;

        public Overlay AddExisting(string name, int x, int y)
        {
            var window = Native.ThumbnailBounds(Source.Handle);
            var spec = new RegionSpec { Name = name, X = .1, Y = .1, W = .2, H = .1, Left = window.Left + x, Top = window.Top + y, Width = 120, Height = 40, Opacity = 100 };
            return Create(spec);
        }

        public Rig Open(int selectedIndex = -1)
        {
            Editor = new AreaEditor(Source.Handle, Overlays, Create, Remove, selectedIndex);
            Editor.Changed += delegate { Changes++; };
            ShowOffscreen(Editor);
            return this;
        }

        private Overlay Create(RegionSpec spec)
        {
            Created.Add(spec);
            var overlay = new Overlay(Source.Handle, spec);
            overlay.Prepare();
            overlay.Bounds = new Rectangle(spec.Left, spec.Top, spec.Width + 2 * Overlay.Border, spec.Height + 2 * Overlay.Border);
            Overlays.Add(overlay);
            return overlay;
        }

        private void Remove(Overlay overlay)
        {
            Overlays.Remove(overlay);
            overlay.Dispose();
        }

        public Size SourceSize => Field<Size>(Editor, "sourceSize");

        public Rectangle WindowBounds
        {
            get { var w = Native.ThumbnailBounds(Source.Handle); return new Rectangle(w.Left, w.Top, w.Right - w.Left, w.Bottom - w.Top); }
        }

        // Ponto da tela do editor sobre o pixel (x, y) da janela do jogo, respeitando zoom e deslocamento atuais.
        public Point PointOf(int x, int y)
        {
            var preview = Field<Rectangle>(Editor, "preview");
            var viewport = Field<Rectangle>(Editor, "viewport");
            double scale = (double)preview.Width / viewport.Width;
            return new Point(preview.Left + (int)Math.Round((x - viewport.X) * scale), preview.Top + (int)Math.Round((y - viewport.Y) * scale));
        }

        public void Press(Keys key)
        {
            Message message = default;
            Call(Editor, "ProcessCmdKey", message, key);
        }

        public void Release(Keys key) => Call(Editor, "OnKeyUp", new KeyEventArgs(key));

        public void Dispose()
        {
            Editor?.Dispose();
            foreach (var overlay in Overlays) overlay.Dispose();
            Source.Dispose();
        }
    }

    [WinFormsFact]
    public void DragOnEmptySpace_CreatesAreaAsSlotBlocksAndKeepsItHidden()
    {
        using var rig = new Rig().Open();
        Assert.True(Field<CheckBox>(rig.Editor, "slotSnap").Checked, "O encaixe em slots deve vir ligado");

        Drag(rig.Editor, MouseButtons.Left, rig.PointOf(100, 100), rig.PointOf(250, 160));

        var spec = Assert.Single(rig.Created);
        Rectangle crop = spec.Crop(rig.SourceSize);
        Assert.Contains(crop.Width, SlotBlocks);
        Assert.Contains(crop.Height, SlotBlocks);
        Assert.True(crop.Width > crop.Height, "Cada lado deve ser encaixado de forma independente");
        Assert.True(Math.Abs(crop.X - 100) <= 1 && Math.Abs(crop.Y - 100) <= 1, $"O recorte deve comecar onde o arrasto comecou, mas comecou em {crop.Location}");
        Assert.Equal("Área 1", spec.Name);
        Assert.False(rig.Overlays[0].Visible, "A overlay nao pode aparecer na tela antes do modo jogo");
        Assert.Same(rig.Overlays[0], rig.Editor.SelectedOverlay);
        Assert.True(rig.Changes > 0, "Criar uma area precisa avisar a janela principal");
        Assert.True(rig.WindowBounds.Contains(rig.Overlays[0].Bounds), "A overlay deve nascer dentro da janela do jogo");
        Assert.Equal(spec.Width, rig.Overlays[0].Width - 2 * Overlay.Border);
    }

    [WinFormsFact]
    public void SlotSnapOff_CreatesTheExactFreeRectangle()
    {
        using var rig = new Rig().Open();
        Field<CheckBox>(rig.Editor, "slotSnap").Checked = false;
        Point from = rig.PointOf(120, 90), to = rig.PointOf(260, 130);
        Point start = (Point)Call(rig.Editor, "ToSource", from), end = (Point)Call(rig.Editor, "ToSource", to);

        Drag(rig.Editor, MouseButtons.Left, from, to);

        Assert.Equal(Rectangle.FromLTRB(start.X, start.Y, end.X, end.Y), Assert.Single(rig.Created).Crop(rig.SourceSize));
    }

    [WinFormsFact]
    public void SquareMode_CreatesASquareOnTheSlotLadder()
    {
        using var rig = new Rig().Open();
        Field<CheckBox>(rig.Editor, "squareOnly").Checked = true;

        Drag(rig.Editor, MouseButtons.Left, rig.PointOf(100, 100), rig.PointOf(250, 130));

        Rectangle crop = Assert.Single(rig.Created).Crop(rig.SourceSize);
        Assert.True(crop.Width == crop.Height && (crop.Width - 34) % 36 == 0, $"Esperava um quadrado na escada de slots, veio {crop.Size}");
    }

    [WinFormsFact]
    public void ClickWithoutDrag_CreatesNothing()
    {
        using var rig = new Rig().Open();

        Drag(rig.Editor, MouseButtons.Left, rig.PointOf(200, 200), rig.PointOf(200, 200));

        Assert.Empty(rig.Created);
    }

    [WinFormsFact]
    public void DraggingAnArea_MovesItSnappedToTheCellImmediately()
    {
        using var rig = new Rig();
        var overlay = rig.AddExisting("Vida", 100, 100);
        rig.Open();
        var window = rig.WindowBounds;

        Point from = rig.PointOf(104, 104);
        Down(rig.Editor, MouseButtons.Left, from.X, from.Y);
        Move(rig.Editor, MouseButtons.Left, from.X + 150, from.Y + 90);
        Up(rig.Editor, MouseButtons.Left, from.X + 150, from.Y + 90);

        int x = overlay.Left - window.Left, y = overlay.Top - window.Top;
        Assert.True(x % Cell == 0 && y % Cell == 0, $"Posicao {x},{y} fora da grade de {Cell}px");
        Assert.True(x > 100 && y > 100, $"A overlay deveria ter andado para baixo e para a direita, mas esta em {x},{y}");
        Assert.True(overlay.Spec.Left == overlay.Left && overlay.Spec.Top == overlay.Top, "O RegionSpec precisa acompanhar a nova posicao");
        Assert.Single(rig.Created); // so a area que ja existia: arrastar uma area nao cria outra
        Assert.True(rig.Changes > 0);
        Assert.False(overlay.Visible);
    }

    [WinFormsFact]
    public void DraggingAnArea_StaysInsideTheGameWindow()
    {
        using var rig = new Rig();
        var overlay = rig.AddExisting("Vida", 100, 100);
        rig.Open();

        Point from = rig.PointOf(104, 104);
        Down(rig.Editor, MouseButtons.Left, from.X, from.Y);
        Move(rig.Editor, MouseButtons.Left, from.X + 5000, from.Y + 5000);
        Up(rig.Editor, MouseButtons.Left, from.X + 5000, from.Y + 5000);

        Assert.True(rig.WindowBounds.Contains(overlay.Bounds), $"Overlay {overlay.Bounds} saiu da janela {rig.WindowBounds}");
    }

    [WinFormsFact]
    public void AreaOutsideTheGameWindow_IsBroughtBackInsideOnOpen()
    {
        using var rig = new Rig();
        var overlay = rig.AddExisting("Vida", -3000, -3000);
        rig.Open();

        Assert.True(rig.WindowBounds.Contains(overlay.Bounds), $"Overlay {overlay.Bounds} continuou fora da janela {rig.WindowBounds}");
    }

    [WinFormsFact]
    public void Arrows_MoveTheAreaByACellOrAPixel_AndAltMovesTheCrop()
    {
        using var rig = new Rig();
        var overlay = rig.AddExisting("Vida", 96, 96);
        rig.Open(0);
        var window = rig.WindowBounds;

        rig.Press(Keys.Right);
        rig.Press(Keys.Down);
        Assert.Equal(96 + Cell, overlay.Left - window.Left);
        Assert.Equal(96 + Cell, overlay.Top - window.Top);

        rig.Press(Keys.Right | Keys.Shift);
        Assert.Equal(96 + Cell + 1, overlay.Left - window.Left);

        int cropX = overlay.Spec.Crop(rig.SourceSize).X;
        rig.Press(Keys.Right | Keys.Alt);
        Assert.Equal(cropX + 1, overlay.Spec.Crop(rig.SourceSize).X);
        Assert.Equal(96 + Cell + 1, overlay.Left - window.Left);
    }

    [WinFormsFact]
    public void Delete_RemovesTheSelectedArea()
    {
        using var rig = new Rig();
        rig.AddExisting("Vida", 100, 100);
        rig.AddExisting("Mana", 300, 100);
        rig.Open(0);

        rig.Press(Keys.Delete);

        var remaining = Assert.Single(rig.Overlays);
        Assert.Equal("Mana", remaining.Spec.Name);
        Assert.Null(rig.Editor.SelectedOverlay);
    }

    [WinFormsFact]
    public void RemoveButton_RemovesTheSelectedArea()
    {
        using var rig = new Rig();
        rig.AddExisting("Vida", 100, 100);
        rig.Open(0);

        Click(rig.Editor, "Remover");

        Assert.Empty(rig.Overlays);
    }

    [WinFormsFact]
    public void HoldingSpace_PansTheViewInsteadOfCreatingAnArea()
    {
        using var rig = new Rig().Open();
        Field<ComboBox>(rig.Editor, "zoomBox").SelectedIndex = 2;
        Rectangle before = Field<Rectangle>(rig.Editor, "viewport");
        Assert.True(before.Width < rig.SourceSize.Width, "O zoom de 4x deveria reduzir a area visivel");
        var preview = Field<Rectangle>(rig.Editor, "preview");
        Point center = new Point(preview.Left + preview.Width / 2, preview.Top + preview.Height / 2);

        rig.Press(Keys.Space);
        Assert.True(Field<bool>(rig.Editor, "spaceDown"));
        Drag(rig.Editor, MouseButtons.Left, center, new Point(center.X + 60, center.Y + 40));
        Rectangle panned = Field<Rectangle>(rig.Editor, "viewport");
        Assert.True(panned.Size == before.Size && panned.Location != before.Location, "Espaco + arrastar deveria mover a visao");
        Assert.Empty(rig.Created);

        rig.Release(Keys.Space);
        Assert.False(Field<bool>(rig.Editor, "spaceDown"));
        Drag(rig.Editor, MouseButtons.Left, center, new Point(center.X + 60, center.Y + 40));
        Assert.Single(rig.Created);
        Assert.Equal(panned, Field<Rectangle>(rig.Editor, "viewport"));
    }

    [WinFormsFact]
    public void MiddleAndRightButtons_AlsoPan()
    {
        using var rig = new Rig().Open();
        Field<ComboBox>(rig.Editor, "zoomBox").SelectedIndex = 1;
        var preview = Field<Rectangle>(rig.Editor, "preview");
        Point center = new Point(preview.Left + preview.Width / 2, preview.Top + preview.Height / 2);

        Rectangle before = Field<Rectangle>(rig.Editor, "viewport");
        Drag(rig.Editor, MouseButtons.Middle, center, new Point(center.X + 30, center.Y + 20));
        Rectangle afterMiddle = Field<Rectangle>(rig.Editor, "viewport");
        Drag(rig.Editor, MouseButtons.Right, center, new Point(center.X - 20, center.Y - 10));

        Assert.NotEqual(before.Location, afterMiddle.Location);
        Assert.NotEqual(afterMiddle.Location, Field<Rectangle>(rig.Editor, "viewport").Location);
        Assert.Empty(rig.Created);
    }

    [WinFormsFact]
    public void Wheel_ZoomsAroundThePointer()
    {
        using var rig = new Rig().Open();
        var preview = Field<Rectangle>(rig.Editor, "preview");
        Point anchor = new Point(preview.Left + preview.Width / 3, preview.Top + preview.Height / 3);
        Point beforeZoom = (Point)Call(rig.Editor, "ToSource", anchor);
        Rectangle fit = Field<Rectangle>(rig.Editor, "viewport");

        Wheel(rig.Editor, anchor.X, anchor.Y, 120);

        Point afterZoom = (Point)Call(rig.Editor, "ToSource", anchor);
        Assert.True(Field<Rectangle>(rig.Editor, "viewport").Width < fit.Width, "A roda deveria aproximar");
        Assert.True(Math.Abs(beforeZoom.X - afterZoom.X) <= 1 && Math.Abs(beforeZoom.Y - afterZoom.Y) <= 1, "O zoom perdeu o ponto sob o mouse");
    }

    [WinFormsFact]
    public void CropFields_EditTheSelectedAreaWithoutLosingItsIdentity()
    {
        using var rig = new Rig();
        var overlay = rig.AddExisting("Vida", 100, 100);
        rig.Open(0);
        IntPtr handle = overlay.Handle;
        string title = overlay.Text, id = overlay.Spec.ObsId;
        Point position = overlay.Location;

        Field<NumericUpDown>(rig.Editor, "exactW").Value = 100;
        Field<NumericUpDown>(rig.Editor, "exactH").Value = 40;
        Field<NumericUpDown>(rig.Editor, "exactX").Value = 17;
        Field<NumericUpDown>(rig.Editor, "exactY").Value = 23;

        Assert.Equal(new Rectangle(17, 23, 100, 40), overlay.Spec.Crop(rig.SourceSize));
        Assert.True(overlay.Handle == handle && overlay.Text == title && overlay.Spec.ObsId == id && overlay.Location == position, "Editar o recorte perdeu posicao, HWND ou identidade no OBS");
        Assert.True(rig.Changes > 0);
    }

    [WinFormsFact]
    public void NameBox_RenamesTheSelectedArea()
    {
        using var rig = new Rig();
        var overlay = rig.AddExisting("Vida", 100, 100);
        rig.Open(0);

        Field<ComboBox>(rig.Editor, "name").Text = "Minimapa";

        Assert.Equal("Minimapa", overlay.Spec.Name);
    }

    [WinFormsFact]
    public void CreatedAreasGetUniqueNames()
    {
        using var rig = new Rig().Open();

        Drag(rig.Editor, MouseButtons.Left, rig.PointOf(50, 50), rig.PointOf(100, 90));
        Drag(rig.Editor, MouseButtons.Left, rig.PointOf(300, 300), rig.PointOf(350, 340));

        Assert.Equal(new[] { "Área 1", "Área 2" }, rig.Created.ConvertAll(s => s.Name));
    }

    // ----- Camadas, selecao multipla, alinhamento e desfazer -----

    private static int Selected(AreaEditor editor) => Field<System.Collections.IList>(editor, "selection").Count;

    private static Rig TwoAreas(out Overlay bottom, out Overlay top)
    {
        var rig = new Rig();
        bottom = rig.AddExisting("Vida", 100, 100);
        top = rig.AddExisting("Mana", 300, 200);
        rig.Open();
        return rig;
    }

    [WinFormsFact]
    public void LayerList_ShowsTheTopLayerFirstAndFollowsTheSelection()
    {
        using var rig = TwoAreas(out var bottom, out var top);
        var list = Field<LayerList>(rig.Editor, "layerList");

        Assert.Equal(new[] { "Mana", "Vida" }, new[] { ((RegionSpec)list.Items[0]).Name, ((RegionSpec)list.Items[1]).Name });

        list.SetSelected(1, true); // a linha de baixo da lista e a camada de baixo
        Assert.Same(bottom, rig.Editor.SelectedOverlay);
        Assert.Equal(1, Selected(rig.Editor));

        Point click = rig.PointOf(304, 204);
        Down(rig.Editor, MouseButtons.Left, click.X, click.Y);
        Up(rig.Editor, MouseButtons.Left, click.X, click.Y);
        Assert.Same(top, rig.Editor.SelectedOverlay);
        Assert.Equal(new List<int> { 0 }, list.SelectedIndices.Cast<int>().ToList());
    }

    [WinFormsFact]
    public void HidingALayer_IsStoredInTheSpecAndCanBeUndone()
    {
        using var rig = TwoAreas(out var bottom, out var top);

        Call(rig.Editor, "ToggleLayer", 0, true); // linha 0 = camada de cima

        Assert.True(top.Spec.Hidden);
        Assert.False(bottom.Spec.Hidden);
        rig.Press(Keys.Control | Keys.Z);
        Assert.False(top.Spec.Hidden);
        rig.Press(Keys.Control | Keys.Y);
        Assert.True(top.Spec.Hidden);
    }

    [WinFormsFact]
    public void LockedLayer_DoesNotMoveNorGetDeleted()
    {
        using var rig = new Rig();
        var overlay = rig.AddExisting("Vida", 96, 96);
        rig.Open(0);
        var window = rig.WindowBounds;

        Call(rig.Editor, "ToggleLayer", 0, false);
        Assert.True(overlay.Spec.Locked);

        rig.Press(Keys.Right);
        Point from = rig.PointOf(100, 100);
        Down(rig.Editor, MouseButtons.Left, from.X, from.Y);
        Move(rig.Editor, MouseButtons.Left, from.X + 80, from.Y + 40);
        Up(rig.Editor, MouseButtons.Left, from.X + 80, from.Y + 40);
        rig.Press(Keys.Delete);

        Assert.Equal(96, overlay.Left - window.Left);
        Assert.Single(rig.Overlays);
    }

    [WinFormsFact]
    public void RaiseAndLower_ReorderTheLayersAndUndoRestoresTheOrder()
    {
        using var rig = TwoAreas(out var bottom, out var top);
        Field<LayerList>(rig.Editor, "layerList").SetSelected(1, true); // camada de baixo

        Click(rig.Editor, "Subir");
        Assert.Equal(new[] { top, bottom }, rig.Overlays);

        Click(rig.Editor, "Descer");
        Assert.Equal(new[] { bottom, top }, rig.Overlays);

        Click(rig.Editor, "Subir");
        rig.Press(Keys.Control | Keys.Z);
        Assert.Equal(new[] { bottom, top }, rig.Overlays);
    }

    [WinFormsFact]
    public void SelectAll_ThenArrowsMoveEveryAreaAndDeleteRemovesThem_UndoBringsThemBackWithTheSameIdentity()
    {
        using var rig = TwoAreas(out var bottom, out var top);
        string idBottom = bottom.Spec.ObsId, idTop = top.Spec.ObsId;
        int x1 = bottom.Left, x2 = top.Left;

        rig.Press(Keys.Control | Keys.A);
        Assert.Equal(2, Selected(rig.Editor));
        rig.Press(Keys.Right);
        Assert.Equal(new[] { x1 + Cell, x2 + Cell }, new[] { bottom.Left, top.Left });

        rig.Press(Keys.Delete);
        Assert.Empty(rig.Overlays);
        rig.Press(Keys.Control | Keys.Z);
        Assert.Equal(new[] { idBottom, idTop }, rig.Overlays.ConvertAll(o => o.Spec.ObsId));
        Assert.Equal(new[] { x1 + Cell, x2 + Cell }, rig.Overlays.ConvertAll(o => o.Left));
        Assert.Equal(new[] { "Vida", "Mana" }, rig.Overlays.ConvertAll(o => o.Spec.Name));
        rig.Press(Keys.Control | Keys.Y);
        Assert.Empty(rig.Overlays);
    }

    [WinFormsFact]
    public void ShiftDragOnEmptySpace_SelectsTheAreasInsideTheRectangle()
    {
        using var rig = TwoAreas(out _, out _);
        SetField(rig.Editor, "modifierKeys", (Func<Keys>)(() => Keys.Shift));

        Drag(rig.Editor, MouseButtons.Left, rig.PointOf(60, 60), rig.PointOf(480, 300));

        Assert.Equal(2, Selected(rig.Editor));
        Assert.Equal(2, rig.Created.Count); // so as duas que ja existiam: o retangulo seleciona, nao cria
    }

    [WinFormsFact]
    public void DraggingOneOfSeveralSelectedAreas_MovesTheWholeGroupByTheSameDelta()
    {
        using var rig = TwoAreas(out var bottom, out var top);
        rig.Press(Keys.Control | Keys.A);
        int dxBefore = top.Left - bottom.Left, dyBefore = top.Top - bottom.Top;
        int x = bottom.Left;

        Point from = rig.PointOf(104, 104);
        Down(rig.Editor, MouseButtons.Left, from.X, from.Y);
        Move(rig.Editor, MouseButtons.Left, from.X + 60, from.Y + 30);
        Up(rig.Editor, MouseButtons.Left, from.X + 60, from.Y + 30);

        Assert.True(bottom.Left > x, "O grupo deveria ter andado");
        Assert.Equal(dxBefore, top.Left - bottom.Left);
        Assert.Equal(dyBefore, top.Top - bottom.Top);
        Assert.Equal(2, Selected(rig.Editor));
    }

    [WinFormsFact]
    public void Align_PutsAreasOnTheSameEdgeAndDistributeEvensTheGaps()
    {
        using var rig = new Rig();
        var a = rig.AddExisting("A", 100, 100);
        var b = rig.AddExisting("B", 220, 160);
        var c = rig.AddExisting("C", 480, 60);
        rig.Open();
        rig.Press(Keys.Control | Keys.A);

        Click(rig.Editor, "Esq.");
        Assert.True(a.Left == b.Left && b.Left == c.Left, "Alinhar a esquerda deveria igualar o X");
        Click(rig.Editor, "Topo");
        Assert.True(a.Top == b.Top && b.Top == c.Top, "Alinhar ao topo deveria igualar o Y");

        rig.Press(Keys.Control | Keys.Z); // desfaz o topo
        Assert.NotEqual(a.Top, c.Top);

        // Afasta as tres na horizontal e distribui: os vaos precisam ficar iguais.
        Call(rig.Editor, "Place", Call(rig.Editor, "Find", a.Spec.ObsId), new Rectangle(0, 100, a.Width, a.Height));
        Call(rig.Editor, "Place", Call(rig.Editor, "Find", b.Spec.ObsId), new Rectangle(100, 100, b.Width, b.Height));
        Call(rig.Editor, "Place", Call(rig.Editor, "Find", c.Spec.ObsId), new Rectangle(400, 100, c.Width, c.Height));
        Click(rig.Editor, "Dist. H");
        int gap1 = b.Left - a.Bounds.Right, gap2 = c.Left - b.Bounds.Right;
        Assert.True(Math.Abs(gap1 - gap2) <= 1, $"Vaos desiguais: {gap1} e {gap2}");
        Assert.Equal(0, a.Left - rig.WindowBounds.Left);
    }

    [WinFormsFact]
    public void AlignButtons_AreDisabledWithoutEnoughAreasSelected()
    {
        using var rig = TwoAreas(out _, out _);
        var align = Field<Button[]>(rig.Editor, "alignButtons");

        Assert.All(align, b => Assert.False(b.Enabled));
        rig.Press(Keys.Control | Keys.A);
        Assert.All(align, b => Assert.True(b.Enabled));
        Assert.False(Field<Button>(rig.Editor, "distributeH").Enabled, "Distribuir pede 3 ou mais areas");
    }

    [WinFormsFact]
    public void UndoAndRedo_CoverCreateMoveAndCropEdits()
    {
        using var rig = new Rig().Open();

        Drag(rig.Editor, MouseButtons.Left, rig.PointOf(100, 100), rig.PointOf(250, 160));
        var overlay = Assert.Single(rig.Overlays);
        string id = overlay.Spec.ObsId;

        rig.Press(Keys.Control | Keys.Z);
        Assert.Empty(rig.Overlays);
        rig.Press(Keys.Control | Keys.Y);
        var again = Assert.Single(rig.Overlays);
        Assert.Equal(id, again.Spec.ObsId);

        int x = again.Left;
        rig.Press(Keys.Right);
        Assert.Equal(x + Cell, again.Left);
        rig.Press(Keys.Control | Keys.Z);
        Assert.Equal(x, again.Left);

        int width = again.Spec.Crop(rig.SourceSize).Width;
        Field<NumericUpDown>(rig.Editor, "exactW").Value = width + 10;
        Assert.Equal(width + 10, again.Spec.Crop(rig.SourceSize).Width);
        rig.Press(Keys.Control | Keys.Z);
        Assert.Equal(width, again.Spec.Crop(rig.SourceSize).Width);
    }

    [WinFormsFact]
    public void UndoButtons_FollowTheHistory()
    {
        using var rig = new Rig();
        rig.AddExisting("Vida", 96, 96);
        rig.Open(0);
        var undo = Field<Button>(rig.Editor, "undoButton");
        var redo = Field<Button>(rig.Editor, "redoButton");

        Assert.False(undo.Enabled);
        rig.Press(Keys.Right);
        Assert.True(undo.Enabled && !redo.Enabled);
        undo.PerformClick();
        Assert.True(!undo.Enabled && redo.Enabled);
    }

    [WinFormsFact]
    public void Duplicate_CopiesTheSelectedAreasWithNewIdentity()
    {
        using var rig = new Rig();
        var original = rig.AddExisting("Vida", 96, 96);
        rig.Open(0);
        var window = rig.WindowBounds;

        rig.Press(Keys.Control | Keys.D);

        Assert.Equal(2, rig.Overlays.Count);
        var copy = rig.Overlays[1];
        Assert.Equal("Vida cópia", copy.Spec.Name);
        Assert.NotEqual(original.Spec.ObsId, copy.Spec.ObsId);
        Assert.Equal(original.Spec.Crop(rig.SourceSize), copy.Spec.Crop(rig.SourceSize));
        Assert.Equal(96 + Cell, copy.Left - window.Left);
        Assert.Same(copy, rig.Editor.SelectedOverlay);
        Assert.False(copy.Visible);
    }

    [WinFormsFact]
    public void OpacityBox_AppliesToEverySelectedArea()
    {
        using var rig = TwoAreas(out var bottom, out var top);
        rig.Press(Keys.Control | Keys.A);

        Field<NumericUpDown>(rig.Editor, "opacityBox").Value = 60;

        Assert.Equal(new[] { 60, 60 }, new[] { bottom.Spec.Opacity, top.Spec.Opacity });
        rig.Press(Keys.Control | Keys.Z);
        Assert.Equal(new[] { 100, 100 }, new[] { bottom.Spec.Opacity, top.Spec.Opacity });
    }

    [WinFormsFact]
    public void DraggingNearAnotherArea_SnapsItsEdgeToIt()
    {
        using var rig = new Rig();
        var fixedArea = rig.AddExisting("Fixa", 300, 100);
        var moving = rig.AddExisting("Movel", 100, 100);
        rig.Open();
        int fixedTop = fixedArea.Top;

        Point from = rig.PointOf(104, 104);
        Down(rig.Editor, MouseButtons.Left, from.X, from.Y);
        Move(rig.Editor, MouseButtons.Left, from.X + 120, from.Y + 2); // ficou a ~2 px da altura da outra
        Up(rig.Editor, MouseButtons.Left, from.X + 120, from.Y + 2);

        Assert.Equal(fixedTop, moving.Top);
    }
}
