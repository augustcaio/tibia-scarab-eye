using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using TibiaScarabEye.Interop;
using TibiaScarabEye.Layouts;

namespace TibiaScarabEye.UI;

// Editor de áreas: um único lugar para criar e posicionar overlays. Mostra o jogo ao vivo; arrastar no vazio cria uma área
// (o recorte é o retângulo desenhado) e arrastar uma área já criada move a overlay, que só aparece na tela no modo jogo.
// Zoom na roda; segurar Espaço (como no Photoshop), botão do meio ou botão direito e arrastar move a visão.
// As mudanças valem na hora: não há aplicar nem cancelar.
internal sealed class AreaEditor : Form {
    // Encaixe denso e fixo, sem desenhar a grade: 8 px de célula posicionam com precisão sem poluir a prévia.
    internal const int CellPixels=8;
    const int MaxAreas=30;
    static readonly double[] ZoomLevels={1,2,4,8};
    sealed class Item {
        public Overlay Overlay;
        public Thumbnail Thumb;
    }
    enum Gesture { None, Pan, Move, Draw }
    readonly IntPtr source;
    readonly IList<Overlay> overlays;
    readonly Func<RegionSpec,Overlay> create;
    readonly Action<Overlay> remove;
    readonly List<Item> items=new List<Item>();
    readonly EditorAdorner adorner=new EditorAdorner();
    readonly ComboBox name=new ComboBox(), zoomBox=new ComboBox();
    readonly CheckBox squareOnly=new CheckBox(), slotSnap=new CheckBox();
    readonly NumericUpDown exactX=new NumericUpDown(), exactY=new NumericUpDown(), exactW=new NumericUpDown(), exactH=new NumericUpDown();
    readonly Button done, removeButton;
    Thumbnail thumb;
    Size sourceSize;
    Rectangle viewport, preview, drawing;
    double zoomFactor=1;
    Item selected;
    Gesture gesture;
    MouseButtons panButton;
    Point panStart, panOrigin, grabOffset, drawAnchor;
    bool spaceDown, binding;
    public event Action Changed;
    public Overlay SelectedOverlay { get { return selected==null?null:selected.Overlay; } }

    public AreaEditor(IntPtr src,IList<Overlay> list,Func<RegionSpec,Overlay> createArea,Action<Overlay> removeArea,int selectedIndex) {
        source=src; overlays=list; create=createArea; remove=removeArea;
        Theme.Apply(this); Text="Editor de áreas • Tibia Scarab Eye"; KeyPreview=true;
        Size=new Size(1100,780); MinimumSize=new Size(900,560); StartPosition=FormStartPosition.CenterParent;
        Controls.Add(Theme.Label("Arraste no jogo para criar uma área. Arraste uma área para posicioná-la. Ela só aparece na tela no modo jogo.",20,14,1040,24,false));
        Controls.Add(Theme.Label("Nome",20,53,55,24,true));
        name.SetBounds(75,49,250,30); name.DropDownStyle=ComboBoxStyle.DropDown; name.MaxLength=80; name.Enabled=false;
        name.Items.AddRange(new object[]{"Vida e mana","Battle list","Cooldowns","Minimapa","Chat","Área personalizada"}); Controls.Add(name);
        squareOnly.Text="Manter quadrado"; squareOnly.SetBounds(345,49,170,30); Controls.Add(squareOnly);
        slotSnap.Text="Encaixar em slots do Tibia (34 · 70 · 106 px…)"; slotSnap.Checked=true; slotSnap.SetBounds(525,49,420,30); Controls.Add(slotSnap);
        Controls.Add(Theme.Label("Zoom",20,91,50,25,true));
        zoomBox.SetBounds(75,87,120,30); zoomBox.DropDownStyle=ComboBoxStyle.DropDownList;
        zoomBox.Items.AddRange(new object[]{"1× (ajustar)","2×","4×","8×"}); zoomBox.SelectedIndex=0; Controls.Add(zoomBox);
        Controls.Add(Theme.Label("Roda: zoom • Espaço (ou botão do meio/direito) + arrastar: mover a visão • Setas: mover a área • Alt + setas: mover o recorte • Delete: remover",215,91,840,25,true));
        done=Theme.Button("Concluir",20,0,160,true); done.Anchor=AnchorStyles.Bottom|AnchorStyles.Left; done.Click+=delegate { Close(); }; Controls.Add(done); CancelButton=done;
        removeButton=Theme.Button("Remover área",194,0,150,false); removeButton.Anchor=done.Anchor; removeButton.Enabled=false; removeButton.Click+=delegate { RemoveSelected(); }; Controls.Add(removeButton);
        var fields=new NumericUpDown[]{exactX,exactY,exactW,exactH};
        string[] labels={"Recorte X","Y","Largura","Altura"};
        for(int i=0;i<fields.Length;i++) {
            var label=Theme.Label(labels[i],20+i*190,ClientSize.Height-93,i==0?70:66,26,true); label.Anchor=AnchorStyles.Bottom|AnchorStyles.Left; Controls.Add(label);
            fields[i].SetBounds((i==0?96:86)+i*190,ClientSize.Height-96,110,30); fields[i].Anchor=label.Anchor; fields[i].Maximum=100000; fields[i].Enabled=false;
            fields[i].ValueChanged+=delegate { ApplyFields(); }; Controls.Add(fields[i]);
        }
        name.TextChanged+=delegate { Rename(); };
        zoomBox.SelectedIndexChanged+=delegate { if(thumb!=null) SetZoom(ZoomLevels[zoomBox.SelectedIndex],new Point(preview.Left+preview.Width/2,preview.Top+preview.Height/2)); };
        LocationChanged+=delegate { PositionAdorner(); };
        Resize+=delegate { LayoutBottom(); UpdateView(); };
        Deactivate+=delegate { spaceDown=false; };
        Shown+=delegate {
            try {
                thumb=new Thumbnail(Handle,source); sourceSize=thumb.SourceSize; viewport=new Rectangle(Point.Empty,sourceSize);
                LoadItems(selectedIndex); SyncFields(); LayoutBottom(); UpdateView(); adorner.Show(this); ActiveControl=null;
            } catch(Exception ex) { MessageBox.Show(this,"Não foi possível abrir a prévia.\n"+ex.Message); DialogResult=DialogResult.Cancel; }
        };
        MouseDown+=delegate(object s,MouseEventArgs e) { BeginGesture(e); };
        MouseMove+=delegate(object s,MouseEventArgs e) { Continue(e.Location); UpdateCursor(e.Location); };
        MouseUp+=delegate(object s,MouseEventArgs e) { EndGesture(e); };
        MouseWheel+=delegate(object s,MouseEventArgs e) {
            if(thumb==null || !preview.Contains(e.Location) || gesture!=Gesture.None || e.Delta==0) return;
            int index=Math.Max(0,Math.Min(ZoomLevels.Length-1,zoomBox.SelectedIndex+(e.Delta>0?1:-1)));
            SetZoom(ZoomLevels[index],e.Location);
            // O handler da combo não faz nada quando o zoom já é o pedido.
            zoomBox.SelectedIndex=index; UpdateCursor(e.Location);
        };
        MouseCaptureChanged+=delegate { if(!Capture) gesture=Gesture.None; };
    }

    void LayoutBottom() { done.Top=ClientSize.Height-50; removeButton.Top=done.Top; }
    Rectangle PreviewArea() { return new Rectangle(20,124,Math.Max(1,ClientSize.Width-40),Math.Max(1,ClientSize.Height-124-112)); }
    double ViewScale { get { return preview.Width>0 && viewport.Width>0?(double)preview.Width/viewport.Width:1; } }
    Rectangle ToView(Rectangle r) {
        double s=ViewScale;
        return Rectangle.FromLTRB(preview.Left+(int)Math.Round((r.Left-viewport.Left)*s),preview.Top+(int)Math.Round((r.Top-viewport.Top)*s),preview.Left+(int)Math.Round((r.Right-viewport.Left)*s),preview.Top+(int)Math.Round((r.Bottom-viewport.Top)*s));
    }
    Point ToSource(Point p) {
        double s=ViewScale;
        return new Point(Math.Max(viewport.Left,Math.Min(viewport.Right,viewport.Left+(int)Math.Round((p.X-preview.X)/s))),Math.Max(viewport.Top,Math.Min(viewport.Bottom,viewport.Top+(int)Math.Round((p.Y-preview.Y)/s))));
    }
    Rectangle CropOf(Item item) { return item.Overlay.Spec.Crop(sourceSize); }
    // Posição da overlay em coordenadas da janela de origem (a mesma origem da miniatura DWM).
    Rectangle RectOf(Overlay overlay) {
        var window=Native.ThumbnailBounds(source);
        return new Rectangle(overlay.Left-window.Left,overlay.Top-window.Top,overlay.Width,overlay.Height);
    }
    void SetRect(Overlay overlay,Rectangle rect) {
        var window=Native.ThumbnailBounds(source);
        overlay.Bounds=new Rectangle(window.Left+rect.X,window.Top+rect.Y,rect.Width,rect.Height);
        overlay.CaptureSpec();
    }
    void KeepInside(Overlay overlay) {
        Rectangle rect=RectOf(overlay), inside=Geometry.ClampInside(rect,sourceSize);
        if(inside!=rect) SetRect(overlay,inside);
    }
    void RaiseChanged() { if(Changed!=null) Changed(); }

    void LoadItems(int selectedIndex) {
        for(int i=0;i<overlays.Count;i++) {
            KeepInside(overlays[i]);
            items.Add(new Item { Overlay=overlays[i], Thumb=new Thumbnail(Handle,source) });
            if(i==selectedIndex) selected=items[i];
        }
    }
    void UpdateView() {
        if(thumb==null) return;
        Size current=thumb.SourceSize;
        if(current!=sourceSize) { sourceSize=current; viewport=new Rectangle(Point.Empty,current); zoomFactor=1; zoomBox.SelectedIndex=0; }
        preview=Geometry.Fit(viewport.Size,PreviewArea());
        thumb.Draw(viewport,preview);
        foreach(var item in items) DrawItem(item);
        Mark(); PositionAdorner();
    }
    // A miniatura da área segue o mesmo encaixe que a overlay usa na tela, dentro da borda dela, e é cortada ao que cabe na prévia.
    void DrawItem(Item item) {
        Rectangle view=ToView(RectOf(item.Overlay)), crop=CropOf(item);
        int border=Math.Max(1,(int)Math.Round(Overlay.Border*ViewScale));
        Rectangle dest=Geometry.Fit(crop.Size,Rectangle.Inflate(view,-border,-border)), visible=Rectangle.Intersect(dest,preview);
        if(dest.IsEmpty || visible.Width<1 || visible.Height<1) { item.Thumb.Hide(); return; }
        double sx=(double)crop.Width/dest.Width, sy=(double)crop.Height/dest.Height;
        item.Thumb.Draw(Rectangle.FromLTRB(crop.Left+(int)Math.Round((visible.Left-dest.Left)*sx),crop.Top+(int)Math.Round((visible.Top-dest.Top)*sy),crop.Left+(int)Math.Round((visible.Right-dest.Left)*sx),crop.Top+(int)Math.Round((visible.Bottom-dest.Top)*sy)),visible);
    }
    void Mark() {
        var marks=new EditorAdorner.Mark[items.Count];
        for(int i=0;i<marks.Length;i++) marks[i]=new EditorAdorner.Mark { Rect=ToView(RectOf(items[i].Overlay)), Selected=items[i]==selected };
        adorner.Update(preview,marks,selected!=null?ToView(CropOf(selected)):Rectangle.Empty,drawing.IsEmpty?Rectangle.Empty:ToView(drawing));
    }
    void PositionAdorner() { if(IsHandleCreated && !adorner.IsDisposed) adorner.Bounds=RectangleToScreen(ClientRectangle); }

    void SetZoom(double factor,Point anchor) {
        if(factor==zoomFactor || thumb==null || gesture!=Gesture.None) return;
        Point pixel=ToSource(anchor);
        double fx=(double)(anchor.X-preview.X)/preview.Width, fy=(double)(anchor.Y-preview.Y)/preview.Height;
        int w=Math.Max(1,(int)Math.Round(sourceSize.Width/factor)), h=Math.Max(1,(int)Math.Round(sourceSize.Height/factor));
        viewport=new Rectangle(Math.Max(0,Math.Min(sourceSize.Width-w,pixel.X-(int)Math.Round(fx*w))),Math.Max(0,Math.Min(sourceSize.Height-h,pixel.Y-(int)Math.Round(fy*h))),w,h);
        zoomFactor=factor; UpdateView();
    }
    void PanTo(Point p) {
        viewport.Location=new Point(Math.Max(0,Math.Min(sourceSize.Width-viewport.Width,panOrigin.X-(int)Math.Round((p.X-panStart.X)/ViewScale))),Math.Max(0,Math.Min(sourceSize.Height-viewport.Height,panOrigin.Y-(int)Math.Round((p.Y-panStart.Y)/ViewScale))));
        UpdateView();
    }

    // A última área da lista fica por cima na prévia, então é a primeira a receber o clique.
    Item ItemAt(Point p) {
        for(int i=items.Count-1;i>=0;i--) if(ToView(RectOf(items[i].Overlay)).Contains(p)) return items[i];
        return null;
    }
    void Select(Item item) { selected=item; SyncFields(); Mark(); }
    void BeginGesture(MouseEventArgs e) {
        if(thumb==null || gesture!=Gesture.None || !preview.Contains(e.Location)) return;
        ActiveControl=null; // as teclas (setas, Delete, Espaço) passam a valer para a prévia, não para o campo que tinha o foco
        if(spaceDown || e.Button==MouseButtons.Middle || e.Button==MouseButtons.Right) {
            gesture=Gesture.Pan; panButton=e.Button; panStart=e.Location; panOrigin=viewport.Location; Capture=true; UpdateCursor(e.Location); return;
        }
        if(e.Button!=MouseButtons.Left) return;
        var hit=ItemAt(e.Location);
        if(hit!=null) {
            Select(hit); gesture=Gesture.Move;
            Point at=ToSource(e.Location); Rectangle r=RectOf(hit.Overlay);
            grabOffset=new Point(at.X-r.X,at.Y-r.Y);
        } else { Select(null); gesture=Gesture.Draw; drawAnchor=ToSource(e.Location); drawing=Rectangle.Empty; }
        Capture=true; UpdateCursor(e.Location);
    }
    void Continue(Point p) {
        if(gesture==Gesture.Pan) PanTo(p);
        else if(gesture==Gesture.Move) {
            Point at=ToSource(p), target=new Point(at.X-grabOffset.X,at.Y-grabOffset.Y);
            if((ModifierKeys&Keys.Alt)==0) target=Geometry.SnapToGrid(target,CellPixels);
            Place(selected,new Rectangle(target,RectOf(selected.Overlay).Size));
        } else if(gesture==Gesture.Draw) {
            Point end=ToSource(p);
            drawing=slotSnap.Checked?Geometry.SlotBlock(drawAnchor,end,sourceSize,squareOnly.Checked)
                :squareOnly.Checked?Geometry.Square(drawAnchor,end,sourceSize)
                :Rectangle.FromLTRB(Math.Min(drawAnchor.X,end.X),Math.Min(drawAnchor.Y,end.Y),Math.Max(drawAnchor.X,end.X),Math.Max(drawAnchor.Y,end.Y));
            Mark();
        }
    }
    void EndGesture(MouseEventArgs e) {
        if(gesture==Gesture.None) return;
        if(gesture==Gesture.Pan) { if(e.Button!=panButton) return; PanTo(e.Location); }
        else if(e.Button!=MouseButtons.Left) return;
        else {
            Continue(e.Location);
            if(gesture==Gesture.Move) RaiseChanged();
            else if(!drawing.IsEmpty) { var rect=drawing; drawing=Rectangle.Empty; CreateArea(rect); }
        }
        gesture=Gesture.None; drawing=Rectangle.Empty; Capture=false; Mark(); UpdateCursor(e.Location);
    }
    void UpdateCursor(Point p) {
        bool inside=preview.Contains(p);
        Cursor=gesture==Gesture.Pan || (inside && spaceDown)?Cursors.Hand
            :gesture==Gesture.Move || (gesture==Gesture.None && inside && ItemAt(p)!=null)?Cursors.SizeAll
            :inside || gesture==Gesture.Draw?Cursors.Cross:Cursors.Default;
    }
    void Place(Item item,Rectangle rect) {
        SetRect(item.Overlay,Geometry.ClampInside(rect,sourceSize));
        DrawItem(item); SyncFields(); Mark();
    }

    // Nova área: o recorte é o retângulo desenhado, em tamanho real (1:1); a overlay nasce ao lado do recorte, escondida.
    void CreateArea(Rectangle crop) {
        if(overlays.Count>=MaxAreas) { MessageBox.Show(this,"O protótipo permite até 30 áreas por layout."); return; }
        var window=Native.ThumbnailBounds(source);
        var spec=new RegionSpec { Name=NextName(), X=(double)crop.X/sourceSize.Width, Y=(double)crop.Y/sourceSize.Height, W=(double)crop.Width/sourceSize.Width, H=(double)crop.Height/sourceSize.Height, Width=Math.Max(32,crop.Width), Height=Math.Max(20,crop.Height), Opacity=100 };
        var size=new Size(spec.Width+2*Overlay.Border,spec.Height+2*Overlay.Border);
        int x=crop.Right+8+size.Width<=sourceSize.Width?crop.Right+8:crop.Left-8-size.Width;
        var dest=Geometry.ClampInside(new Rectangle(x,crop.Top,size.Width,size.Height),sourceSize);
        spec.Left=window.Left+dest.X; spec.Top=window.Top+dest.Y;
        var overlay=create(spec);
        var item=new Item { Overlay=overlay, Thumb=new Thumbnail(Handle,source) };
        items.Add(item); KeepInside(overlay); DrawItem(item); Select(item); RaiseChanged();
    }
    string NextName() {
        for(int n=overlays.Count+1;;n++) {
            string candidate="Área "+n; bool taken=false;
            foreach(var overlay in overlays) if(overlay.Spec.Name==candidate) taken=true;
            if(!taken) return candidate;
        }
    }
    void RemoveSelected() {
        if(selected==null) return;
        var item=selected; selected=null;
        remove(item.Overlay); item.Thumb.Dispose(); items.Remove(item);
        SyncFields(); Mark(); RaiseChanged();
    }
    void Rename() {
        if(binding || selected==null) return;
        string text=name.Text.Trim();
        if(text.Length==0) return;
        selected.Overlay.Spec.Name=text; RaiseChanged();
    }

    // Os campos mostram e editam o recorte de origem (pixels do jogo) da área selecionada.
    void SyncFields() {
        binding=true;
        bool has=selected!=null;
        name.Enabled=removeButton.Enabled=exactX.Enabled=exactY.Enabled=exactW.Enabled=exactH.Enabled=has;
        name.Text=has?selected.Overlay.Spec.Name:"";
        Rectangle crop=has?CropOf(selected):Rectangle.Empty;
        exactX.Value=crop.X; exactY.Value=crop.Y; exactW.Value=crop.Width; exactH.Value=crop.Height;
        binding=false;
    }
    void ApplyFields() {
        if(binding || selected==null) return;
        ApplyCrop(new Rectangle((int)exactX.Value,(int)exactY.Value,(int)exactW.Value,(int)exactH.Value));
    }
    void ApplyCrop(Rectangle crop) {
        int w=Math.Max(1,Math.Min(crop.Width,sourceSize.Width)), h=Math.Max(1,Math.Min(crop.Height,sourceSize.Height));
        crop=new Rectangle(Math.Max(0,Math.Min(crop.X,sourceSize.Width-w)),Math.Max(0,Math.Min(crop.Y,sourceSize.Height-h)),w,h);
        var spec=selected.Overlay.Spec;
        selected.Overlay.UpdateRegion(new RegionSpec { Name=spec.Name, X=(double)crop.X/sourceSize.Width, Y=(double)crop.Y/sourceSize.Height, W=(double)crop.Width/sourceSize.Width, H=(double)crop.Height/sourceSize.Height, Width=spec.Width, Height=spec.Height, Opacity=spec.Opacity });
        KeepInside(selected.Overlay); DrawItem(selected); SyncFields(); Mark(); RaiseChanged();
    }

    protected override bool ProcessCmdKey(ref Message msg,Keys keyData) {
        // Em campos de texto as teclas são do campo (Espaço digita, setas movem o cursor).
        if(ActiveControl is ComboBox || ActiveControl is NumericUpDown) return base.ProcessCmdKey(ref msg,keyData);
        Keys key=keyData&Keys.KeyCode;
        bool shift=(keyData&Keys.Shift)!=0, alt=(keyData&Keys.Alt)!=0;
        if(key==Keys.Space) { spaceDown=true; UpdateCursor(PointToClient(MousePosition)); return true; }
        if(key==Keys.Delete && selected!=null) { RemoveSelected(); return true; }
        int dx=0, dy=0;
        switch(key) {
            case Keys.Left: dx=-1; break;
            case Keys.Right: dx=1; break;
            case Keys.Up: dy=-1; break;
            case Keys.Down: dy=1; break;
            default: return base.ProcessCmdKey(ref msg,keyData);
        }
        if(selected==null) return base.ProcessCmdKey(ref msg,keyData);
        if(alt) { int step=shift?10:1; Rectangle crop=CropOf(selected); ApplyCrop(new Rectangle(crop.X+dx*step,crop.Y+dy*step,crop.Width,crop.Height)); }
        else { int step=shift?1:CellPixels; Rectangle rect=RectOf(selected.Overlay); Place(selected,new Rectangle(rect.X+dx*step,rect.Y+dy*step,rect.Width,rect.Height)); RaiseChanged(); }
        return true;
    }
    protected override void OnKeyUp(KeyEventArgs e) {
        if(e.KeyCode==Keys.Space) { spaceDown=false; UpdateCursor(PointToClient(MousePosition)); }
        base.OnKeyUp(e);
    }
    protected override void OnFormClosed(FormClosedEventArgs e) {
        adorner.Close();
        foreach(var item in items) item.Thumb.Dispose();
        if(thumb!=null) thumb.Dispose();
        base.OnFormClosed(e);
    }
    protected override void Dispose(bool disposing) { if(disposing) adorner.Dispose(); base.Dispose(disposing); }
}
