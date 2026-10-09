using System;
using System.Drawing;
using System.Windows.Forms;
using TibiaScarabEye.Interop;
using TibiaScarabEye.Layouts;

namespace TibiaScarabEye.UI;

internal sealed class Selector : Form {
    readonly IntPtr source;
    Thumbnail thumb;
    Rectangle preview, selection;
    Rectangle viewport;
    readonly ComboBox previewZoom=new ComboBox();
    readonly double[] zoomLevels={1,2,4,8};
    double zoomFactor=1;
    bool panning, panMode;
    MouseButtons panButton;
    readonly CheckBox squareOnly=new CheckBox();
    readonly Button panTool;
    Point panStart, panOrigin;
    Point start;
    Point moveOffset;
    Size sourceSize;
    bool dragging, moving, binding;
    readonly NumericUpDown exactX=new NumericUpDown(), exactY=new NumericUpDown(), exactSide=new NumericUpDown(), exactHeight=new NumericUpDown();
    readonly SelectionAdorner adorner=new SelectionAdorner();
    Point? pointer;
    readonly Button accept;
    readonly ComboBox name;
    public RegionSpec Result;
    public Selector(IntPtr src,RegionSpec initial=null) {
        source=src; Theme.Apply(this); Text="Selecionar área • Tibia Scarab Eye";
        Size=new Size(1060,740); MinimumSize=new Size(850,480); StartPosition=FormStartPosition.CenterParent;
        Controls.Add(Theme.Label("Desenhe uma área livre. Arraste dentro dela para mover; ajuste os pixels abaixo.",20,16,980,28,false));
        Controls.Add(Theme.Label("Nome da área",20,52,115,28,true));
        name=new ComboBox { Left=135,Top=49,Width=260,DropDownStyle=ComboBoxStyle.DropDown,MaxLength=80 };
        name.Items.AddRange(new object[]{"Vida e mana","Battle list","Cooldowns","Minimapa","Chat","Área personalizada"}); name.SelectedIndex=0; Controls.Add(name);
        squareOnly.Text="Manter quadrado"; squareOnly.SetBounds(420,49,220,30); Controls.Add(squareOnly);
        squareOnly.CheckedChanged+=delegate { if(!selection.IsEmpty) ReadExact(); };
        Controls.Add(Theme.Label("Zoom da prévia",20,91,125,25,true));
        previewZoom.SetBounds(145,87,95,30); previewZoom.DropDownStyle=ComboBoxStyle.DropDownList;
        previewZoom.Items.AddRange(new object[]{"1× (ajustar)","2×","4×","8×"}); previewZoom.SelectedIndex=0; Controls.Add(previewZoom);
        previewZoom.SelectedIndexChanged+=delegate { if(thumb!=null) SetZoom(zoomLevels[previewZoom.SelectedIndex],new Point(preview.Left+preview.Width/2,preview.Top+preview.Height/2)); };
        panTool=Theme.Button("Mover (pan)",258,83,145,false); Controls.Add(panTool);
        panTool.Click+=delegate { panMode=!panMode; panTool.Text=panMode?"Voltar à seleção":"Mover (pan)"; panTool.BackColor=panMode?Theme.Gold:Theme.Surface; panTool.ForeColor=panMode?Theme.Background:Theme.Ink; UpdatePointer(PointToClient(MousePosition)); };
        Controls.Add(Theme.Label("Roda: zoom • Direito/meio + arraste: pan",420,91,590,25,true));
        accept=Theme.Button("Criar recorte",20,0,160,true); accept.Enabled=false; accept.Anchor=AnchorStyles.Bottom|AnchorStyles.Left;
        accept.Click+=delegate { Commit(); }; Controls.Add(accept);
        var cancel=Theme.Button("Cancelar",194,0,120,false); cancel.Anchor=accept.Anchor; cancel.Click+=delegate { DialogResult=DialogResult.Cancel; }; Controls.Add(cancel); CancelButton=cancel;
        var fields=new NumericUpDown[]{exactX,exactY,exactSide,exactHeight};
        string[] labels={"X","Y","Largura","Altura"};
        for(int i=0;i<fields.Length;i++) {
            var label=Theme.Label(labels[i],20+i*190,ClientSize.Height-93,66,26,true); label.Anchor=AnchorStyles.Bottom|AnchorStyles.Left; Controls.Add(label);
            fields[i].SetBounds(86+i*190,ClientSize.Height-96,110,30); fields[i].Anchor=label.Anchor; fields[i].Maximum=100000; fields[i].Enabled=false;
            fields[i].ValueChanged+=delegate(object sender,EventArgs e) {
                if(!binding && sender==exactHeight && squareOnly.Checked) { binding=true; exactSide.Value=exactHeight.Value; binding=false; }
                ReadExact();
            }; Controls.Add(fields[i]);
        }
        LocationChanged+=delegate { PositionAdorner(); };
        Resize+=delegate { accept.Top=ClientSize.Height-50; cancel.Top=accept.Top; UpdatePreview(); Mark(); };
        Shown+=delegate { try { thumb=new Thumbnail(Handle,source); accept.Top=ClientSize.Height-50; cancel.Top=accept.Top; UpdatePreview(); if(initial!=null) { selection=initial.Crop(sourceSize); name.Text=initial.Name; SyncExact(); Mark(); Text="Editar área • Tibia Scarab Eye"; accept.Text="Salvar alterações"; } PositionAdorner(); adorner.Show(this); } catch(Exception ex) { MessageBox.Show(this,"Não foi possível abrir a prévia.\n"+ex.Message); DialogResult=DialogResult.Cancel; } };
        MouseDown+=delegate(object s,MouseEventArgs e) {
            if(!dragging && !panning && preview.Contains(e.Location) && (e.Button==MouseButtons.Right || e.Button==MouseButtons.Middle || (panMode && e.Button==MouseButtons.Left))) { panning=true; panButton=e.Button; panStart=e.Location; panOrigin=viewport.Location; Capture=true; UpdatePointer(e.Location); return; }
            if(panning || dragging) return;
            if(e.Button!=MouseButtons.Left || !preview.Contains(e.Location)) return;
            start=ToSource(e.Location); moving=selection.Contains(start);
            moveOffset=new Point(start.X-selection.X,start.Y-selection.Y);
            if(!moving) selection=Rectangle.Empty;
            dragging=true; Capture=true; UpdatePointer(e.Location); Mark();
        };
        MouseMove+=delegate(object s,MouseEventArgs e) { if(panning) PanTo(e.Location); else if(dragging) DragTo(e.Location); UpdatePointer(e.Location); };
        MouseUp+=delegate(object s,MouseEventArgs e) {
            if(e.Button==panButton && panning) { PanTo(e.Location); panning=false; Capture=false; UpdatePointer(e.Location); return; }
            if(e.Button!=MouseButtons.Left || !dragging) return; DragTo(e.Location); dragging=false; Capture=false; UpdatePointer(e.Location);
        };
        MouseWheel+=delegate(object s,MouseEventArgs e) {
            if(!preview.Contains(e.Location) || dragging || panning || e.Delta==0) return;
            int index=Math.Max(0,Math.Min(zoomLevels.Length-1,previewZoom.SelectedIndex+(e.Delta>0?1:-1)));
            SetZoom(zoomLevels[index],e.Location);
            // The selection handler is a no-op when the zoom already matches.
            previewZoom.SelectedIndex=index; UpdatePointer(e.Location);
        };
        MouseLeave+=delegate { if(!dragging && !panning) { pointer=null; Cursor=Cursors.Default; Mark(); } };
        MouseCaptureChanged+=delegate { if(!Capture) { dragging=false; panning=false; } };
    }
    void UpdatePreview() {
        if(thumb==null) return;
        Size current=thumb.SourceSize;
        if(current!=sourceSize) { sourceSize=current; viewport=new Rectangle(Point.Empty,current); zoomFactor=1; previewZoom.SelectedIndex=0; selection=Rectangle.Empty; SyncExact(); }
        preview=Geometry.Fit(viewport.Size,PreviewArea());
        thumb.Draw(viewport,preview);
        exactX.Enabled=exactY.Enabled=exactSide.Enabled=exactHeight.Enabled=true;
        pointer=null; PositionAdorner();
    }
    Rectangle PreviewArea() { return new Rectangle(20,130,ClientSize.Width-40,ClientSize.Height-248); }
    void SetZoom(double factor,Point anchor) {
        if(factor==zoomFactor || thumb==null || dragging || panning) return;
        Point pixel=ToSource(anchor);
        double fx=(double)(anchor.X-preview.X)/preview.Width,fy=(double)(anchor.Y-preview.Y)/preview.Height;
        int w=Math.Max(1,(int)Math.Round(sourceSize.Width/factor)),h=Math.Max(1,(int)Math.Round(sourceSize.Height/factor));
        viewport=new Rectangle(Math.Max(0,Math.Min(sourceSize.Width-w,pixel.X-(int)Math.Round(fx*w))),Math.Max(0,Math.Min(sourceSize.Height-h,pixel.Y-(int)Math.Round(fy*h))),w,h);
        zoomFactor=factor; UpdatePreview(); Mark();
    }
    void PanTo(Point p) {
        viewport.Location=new Point(Math.Max(0,Math.Min(sourceSize.Width-viewport.Width,panOrigin.X-(int)Math.Round((double)(p.X-panStart.X)*viewport.Width/preview.Width))),Math.Max(0,Math.Min(sourceSize.Height-viewport.Height,panOrigin.Y-(int)Math.Round((double)(p.Y-panStart.Y)*viewport.Height/preview.Height))));
        thumb.Draw(viewport,preview); Mark();
    }
    void PositionAdorner() { if(IsHandleCreated && !adorner.IsDisposed) adorner.Bounds=RectangleToScreen(ClientRectangle); }
    void UpdatePointer(Point p) {
        bool inside=preview.Contains(p);
        pointer=!panning && !panMode && (inside || dragging)?(Point?)new Point(Math.Max(preview.Left,Math.Min(preview.Right-1,p.X)),Math.Max(preview.Top,Math.Min(preview.Bottom-1,p.Y))):null;
        Cursor=panning || (panMode && inside)?Cursors.Hand:inside || dragging?((dragging && moving) || (!dragging && selection.Contains(ToSource(p)))?Cursors.SizeAll:Cursors.Cross):Cursors.Default;
        Mark();
    }
    Point ToSource(Point p) {
        return new Point(Math.Max(viewport.Left,Math.Min(viewport.Right,viewport.Left+(int)Math.Round((double)(p.X-preview.X)*viewport.Width/preview.Width))),Math.Max(viewport.Top,Math.Min(viewport.Bottom,viewport.Top+(int)Math.Round((double)(p.Y-preview.Y)*viewport.Height/preview.Height))));
    }
    internal static Rectangle Square(Point anchor,Point end,Size size) {
        int dx=end.X-anchor.X,dy=end.Y-anchor.Y;
        int side=Math.Max(Math.Abs(dx),Math.Abs(dy));
        side=Math.Min(side,Math.Min(dx<0?anchor.X:size.Width-anchor.X,dy<0?anchor.Y:size.Height-anchor.Y));
        return side<=0?Rectangle.Empty:new Rectangle(dx<0?anchor.X-side:anchor.X,dy<0?anchor.Y-side:anchor.Y,side,side);
    }
    void DragTo(Point p) {
        Point end=ToSource(p);
        if(moving) selection.Location=new Point(Math.Max(0,Math.Min(sourceSize.Width-selection.Width,end.X-moveOffset.X)),Math.Max(0,Math.Min(sourceSize.Height-selection.Height,end.Y-moveOffset.Y)));
        else selection=squareOnly.Checked?Square(start,end,sourceSize):Rectangle.FromLTRB(Math.Min(start.X,end.X),Math.Min(start.Y,end.Y),Math.Max(start.X,end.X),Math.Max(start.Y,end.Y));
        SyncExact(); Mark();
    }
    void ReadExact() {
        if(binding || sourceSize.Width<1 || sourceSize.Height<1) return;
        int w=Math.Max(1,Math.Min((int)exactSide.Value,sourceSize.Width));
        int h=Math.Max(1,Math.Min((int)exactHeight.Value,sourceSize.Height));
        if(squareOnly.Checked) w=h=Math.Min(w,sourceSize.Height);
        selection=new Rectangle(Math.Min((int)exactX.Value,sourceSize.Width-w),Math.Min((int)exactY.Value,sourceSize.Height-h),w,h);
        SyncExact(); Mark();
    }
    void SyncExact() {
        binding=true; exactX.Value=selection.X; exactY.Value=selection.Y; exactSide.Value=selection.Width; exactHeight.Value=selection.Height; binding=false;
    }
    void Mark() {
        accept.Enabled=selection.Width>0 && selection.Height>0;
        Rectangle mark=Rectangle.Empty;
        if(!selection.IsEmpty && viewport.Width>0 && viewport.Height>0) mark=Rectangle.FromLTRB(preview.Left+(int)Math.Round((double)(selection.Left-viewport.Left)*preview.Width/viewport.Width),preview.Top+(int)Math.Round((double)(selection.Top-viewport.Top)*preview.Height/viewport.Height),preview.Left+(int)Math.Round((double)(selection.Right-viewport.Left)*preview.Width/viewport.Width),preview.Top+(int)Math.Round((double)(selection.Bottom-viewport.Top)*preview.Height/viewport.Height));
        adorner.UpdateGuides(preview,mark,pointer);
    }
    void Commit() {
        if(string.IsNullOrWhiteSpace(name.Text)) { MessageBox.Show(this,"Dê um nome para esta área."); return; }
        if(selection.Width<1 || selection.Height<1) return;
        Result=new RegionSpec { Name=name.Text.Trim(), X=(double)selection.X/sourceSize.Width,Y=(double)selection.Y/sourceSize.Height,W=(double)selection.Width/sourceSize.Width,H=(double)selection.Height/sourceSize.Height,Left=80,Top=80,Opacity=100 };
        Result.Width=Math.Max(32,selection.Width); Result.Height=Math.Max(20,selection.Height);
        DialogResult=DialogResult.OK;
    }
    protected override void OnFormClosed(FormClosedEventArgs e) { adorner.Close(); if(thumb!=null) thumb.Dispose(); base.OnFormClosed(e); }
    protected override void Dispose(bool disposing) { if(disposing) adorner.Dispose(); base.Dispose(disposing); }
}
