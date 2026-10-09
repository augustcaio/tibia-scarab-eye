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
// O painel de camadas mostra a ordem (de cima para baixo), esconde e trava áreas; há seleção múltipla, alinhamento,
// guias inteligentes, duplicar e desfazer/refazer. As mudanças valem na hora: não há aplicar nem cancelar.
internal sealed class AreaEditor : Form {
    // Encaixe denso e fixo, sem desenhar a grade: 8 px de célula posicionam com precisão sem poluir a prévia.
    internal const int CellPixels=8;
    const int MaxAreas=30, PanelWidth=250, GuidePixels=6, PanelRows=7, RowHeight=34;
    static readonly double[] ZoomLevels={1,2,4,8};
    sealed class Item {
        public Overlay Overlay;
        public Thumbnail Thumb;
        public RegionSpec Spec { get { return Overlay.Spec; } }
    }
    enum Gesture { None, Pan, Move, Draw, Marquee }
    readonly IntPtr source;
    readonly IList<Overlay> overlays;
    readonly Func<RegionSpec,Overlay> create;
    readonly Action<Overlay> remove;
    readonly List<Item> items=new List<Item>();      // mesma ordem de 'overlays': de baixo para cima
    readonly List<Item> selection=new List<Item>();
    readonly EditorAdorner adorner=new EditorAdorner();
    readonly EditorHistory history=new EditorHistory();
    readonly ComboBox name=new ComboBox(), zoomBox=new ComboBox();
    readonly CheckBox squareOnly=new CheckBox(), slotSnap=new CheckBox();
    readonly NumericUpDown exactX=new NumericUpDown(), exactY=new NumericUpDown(), exactW=new NumericUpDown(), exactH=new NumericUpDown(), opacityBox=new NumericUpDown();
    readonly LayerList layerList=new LayerList();
    readonly Label layersLabel, opacityLabel, pixelLabel;
    readonly Button done, undoButton, redoButton, upButton, downButton, duplicateButton, removeButton;
    readonly Button[] alignButtons=new Button[6];
    readonly Button distributeH, distributeV;
    readonly ToolTip tips=new ToolTip();
    // Em um campo à parte para os testes poderem simular Ctrl/Shift/Alt, que não dá para pressionar de verdade na bancada.
    Func<Keys> modifierKeys=delegate { return Control.ModifierKeys; };
    Thumbnail thumb;
    Size sourceSize;
    Rectangle viewport, preview, drawing, marquee;
    double zoomFactor=1;
    Item primary;
    Gesture gesture;
    MouseButtons panButton;
    Point panStart, panOrigin, grabOffset, drawAnchor, marqueeAnchor;
    Dictionary<Item,Rectangle> moveStarts=new Dictionary<Item,Rectangle>();
    Rectangle moveUnion;
    EditorHistory.Snapshot gestureBefore, renameBefore;
    List<string> marqueeBase=new List<string>();
    readonly List<int> verticalGuides=new List<int>(), horizontalGuides=new List<int>();
    bool spaceDown, binding;
    public event Action Changed;
    public Overlay SelectedOverlay { get { return primary==null?null:primary.Overlay; } }

    public AreaEditor(IntPtr src,IList<Overlay> list,Func<RegionSpec,Overlay> createArea,Action<Overlay> removeArea,int selectedIndex) {
        source=src; overlays=list; create=createArea; remove=removeArea;
        Theme.Apply(this); Text="Editor de áreas • Tibia Scarab Eye"; KeyPreview=true;
        Size=new Size(1200,830); MinimumSize=new Size(1000,660); StartPosition=FormStartPosition.CenterParent;
        Controls.Add(Theme.Label("Arraste no jogo para criar uma área. Arraste uma área para posicioná-la (Shift + arrastar no vazio seleciona várias). Ela só aparece na tela no modo jogo.",20,14,1140,24,false));
        Controls.Add(Theme.Label("Nome",20,53,55,24,true));
        name.SetBounds(75,49,250,30); name.DropDownStyle=ComboBoxStyle.DropDown; name.MaxLength=80; name.Enabled=false;
        name.Items.AddRange(new object[]{"Vida e mana","Battle list","Cooldowns","Minimapa","Chat","Área personalizada"}); Controls.Add(name);
        squareOnly.Text="Manter quadrado"; squareOnly.SetBounds(345,49,170,30); Controls.Add(squareOnly);
        slotSnap.Text="Encaixar em slots do Tibia (34 · 70 · 106 px…)"; slotSnap.Checked=true; slotSnap.SetBounds(525,49,420,30); Controls.Add(slotSnap);
        Controls.Add(Theme.Label("Zoom",20,91,50,25,true));
        zoomBox.SetBounds(75,87,120,30); zoomBox.DropDownStyle=ComboBoxStyle.DropDownList;
        zoomBox.Items.AddRange(new object[]{"1× (ajustar)","2×","4×","8×"}); zoomBox.SelectedIndex=0; Controls.Add(zoomBox);
        Controls.Add(Theme.Label("Roda: zoom • Espaço (ou botão do meio/direito) + arrastar: mover a visão • Setas: mover (Shift: 1 px) • Alt + setas: mover o recorte",215,91,760,25,true));

        done=Theme.Button("Concluir",20,0,160,true); done.Anchor=AnchorStyles.Bottom|AnchorStyles.Left; done.Click+=delegate { Close(); }; Controls.Add(done); CancelButton=done;
        pixelLabel=Theme.Label("",200,0,300,24,true); pixelLabel.Anchor=AnchorStyles.Bottom|AnchorStyles.Left; Controls.Add(pixelLabel);
        var fields=new NumericUpDown[]{exactX,exactY,exactW,exactH};
        string[] labels={"Recorte X","Y","Largura","Altura"};
        for(int i=0;i<fields.Length;i++) {
            var label=Theme.Label(labels[i],20+i*190,ClientSize.Height-93,i==0?70:66,26,true); label.Anchor=AnchorStyles.Bottom|AnchorStyles.Left; Controls.Add(label);
            fields[i].SetBounds((i==0?96:86)+i*190,ClientSize.Height-96,110,30); fields[i].Anchor=label.Anchor; fields[i].Maximum=100000; fields[i].Enabled=false;
            fields[i].ValueChanged+=delegate { ApplyFields(); }; Controls.Add(fields[i]);
        }

        layersLabel=Theme.Label("Camadas (de cima para baixo)",0,0,PanelWidth,22,true); Controls.Add(layersLabel);
        layerList.EyeClicked+=delegate(int row) { ToggleLayer(row,true); };
        layerList.LockClicked+=delegate(int row) { ToggleLayer(row,false); };
        layerList.SelectedIndexChanged+=delegate { SelectFromList(); };
        Controls.Add(layerList);
        undoButton=PanelButton("Desfazer","Desfazer (Ctrl + Z)",delegate { Undo(); }); redoButton=PanelButton("Refazer","Refazer (Ctrl + Y)",delegate { Redo(); });
        upButton=PanelButton("Subir","Subir na ordem das camadas",delegate { MoveLayers(true); }); downButton=PanelButton("Descer","Descer na ordem das camadas",delegate { MoveLayers(false); });
        duplicateButton=PanelButton("Duplicar","Duplicar as áreas selecionadas (Ctrl + D)",delegate { Duplicate(); }); removeButton=PanelButton("Remover","Remover as áreas selecionadas (Delete)",delegate { RemoveSelected(); });
        string[] alignText={"Esq.","Centro","Dir.","Topo","Meio","Base"}, alignTip={"Alinhar à esquerda","Centralizar na horizontal","Alinhar à direita","Alinhar ao topo","Centralizar na vertical","Alinhar à base"};
        for(int i=0;i<alignButtons.Length;i++) { var kind=(Geometry.AlignKind)i; alignButtons[i]=PanelButton(alignText[i],alignTip[i],delegate { AlignSelected(kind); }); }
        distributeH=PanelButton("Dist. H","Distribuir na horizontal (3 ou mais áreas)",delegate { DistributeSelected(true); });
        distributeV=PanelButton("Dist. V","Distribuir na vertical (3 ou mais áreas)",delegate { DistributeSelected(false); });
        opacityLabel=Theme.Label("Opacidade (%)",0,0,110,24,true); Controls.Add(opacityLabel);
        opacityBox.Minimum=20; opacityBox.Maximum=100; opacityBox.Value=100; opacityBox.Enabled=false; opacityBox.ValueChanged+=delegate { ApplyOpacity(); }; Controls.Add(opacityBox);

        name.TextChanged+=delegate { Rename(); };
        name.Enter+=delegate { renameBefore=TakeSnapshot(); };
        name.Leave+=delegate { if(renameBefore!=null) { Commit(renameBefore); renameBefore=null; } };
        zoomBox.SelectedIndexChanged+=delegate { if(thumb!=null) SetZoom(ZoomLevels[zoomBox.SelectedIndex],new Point(preview.Left+preview.Width/2,preview.Top+preview.Height/2)); };
        LocationChanged+=delegate { PositionAdorner(); };
        Resize+=delegate { LayoutControls(); UpdateView(); };
        Deactivate+=delegate { spaceDown=false; };
        Shown+=delegate {
            try {
                thumb=new Thumbnail(Handle,source); sourceSize=thumb.SourceSize; viewport=new Rectangle(Point.Empty,sourceSize);
                foreach(var overlay in overlays) KeepInside(overlay);
                RebuildItems();
                if(selectedIndex>=0 && selectedIndex<items.Count) SetSelection(new[]{items[selectedIndex]},items[selectedIndex]); else SyncAll();
                LayoutControls(); UpdateView(); adorner.Show(this); ActiveControl=null;
            } catch(Exception ex) { MessageBox.Show(this,"Não foi possível abrir a prévia.\n"+ex.Message); DialogResult=DialogResult.Cancel; }
        };
        MouseDown+=delegate(object s,MouseEventArgs e) { BeginGesture(e); };
        MouseMove+=delegate(object s,MouseEventArgs e) { Continue(e.Location); UpdateCursor(e.Location); ShowPixel(e.Location); };
        MouseUp+=delegate(object s,MouseEventArgs e) { EndGesture(e); };
        MouseLeave+=delegate { pixelLabel.Text=""; };
        MouseWheel+=delegate(object s,MouseEventArgs e) {
            if(thumb==null || !preview.Contains(e.Location) || gesture!=Gesture.None || e.Delta==0) return;
            int index=Math.Max(0,Math.Min(ZoomLevels.Length-1,zoomBox.SelectedIndex+(e.Delta>0?1:-1)));
            SetZoom(ZoomLevels[index],e.Location);
            // O handler da combo não faz nada quando o zoom já é o pedido.
            zoomBox.SelectedIndex=index; UpdateCursor(e.Location);
        };
        MouseCaptureChanged+=delegate { if(!Capture) gesture=Gesture.None; };
    }

    Button PanelButton(string text,string tip,Action click) {
        var button=Theme.Button(text,false); button.Height=28; button.MinimumSize=new Size(0,28); button.Click+=delegate { click(); };
        tips.SetToolTip(button,tip); Controls.Add(button); return button;
    }
    // O painel de camadas fica à direita; os botões ocupam sete linhas fixas acima da faixa dos campos de recorte.
    void LayoutControls() {
        done.Top=ClientSize.Height-50; pixelLabel.Top=done.Top+10;
        int x=ClientSize.Width-PanelWidth-20, half=(PanelWidth-6)/2, third=(PanelWidth-12)/3, blockTop=ClientSize.Height-112-PanelRows*RowHeight;
        layersLabel.SetBounds(x,126,PanelWidth,22);
        layerList.SetBounds(x,150,PanelWidth,Math.Max(60,blockTop-150-8));
        Action<Control,int,int,int> place=delegate(Control c,int row,int column,int width) { c.SetBounds(x+column*(width+6),blockTop+row*RowHeight,width,28); };
        place(undoButton,0,0,half); place(redoButton,0,1,half);
        place(upButton,1,0,half); place(downButton,1,1,half);
        place(duplicateButton,2,0,half); place(removeButton,2,1,half);
        for(int i=0;i<3;i++) { place(alignButtons[i],3,i,third); place(alignButtons[i+3],4,i,third); }
        place(distributeH,5,0,half); place(distributeV,5,1,half);
        opacityLabel.SetBounds(x,blockTop+6*RowHeight+4,110,24); opacityBox.SetBounds(x+120,blockTop+6*RowHeight,PanelWidth-120,28);
    }
    Rectangle PreviewArea() { return new Rectangle(20,124,Math.Max(1,ClientSize.Width-40-PanelWidth-12),Math.Max(1,ClientSize.Height-124-112)); }
    double ViewScale { get { return preview.Width>0 && viewport.Width>0?(double)preview.Width/viewport.Width:1; } }
    Rectangle ToView(Rectangle r) {
        double s=ViewScale;
        return Rectangle.FromLTRB(preview.Left+(int)Math.Round((r.Left-viewport.Left)*s),preview.Top+(int)Math.Round((r.Top-viewport.Top)*s),preview.Left+(int)Math.Round((r.Right-viewport.Left)*s),preview.Top+(int)Math.Round((r.Bottom-viewport.Top)*s));
    }
    Point ToSource(Point p) {
        double s=ViewScale;
        return new Point(Math.Max(viewport.Left,Math.Min(viewport.Right,viewport.Left+(int)Math.Round((p.X-preview.X)/s))),Math.Max(viewport.Top,Math.Min(viewport.Bottom,viewport.Top+(int)Math.Round((p.Y-preview.Y)/s))));
    }
    Rectangle CropOf(Item item) { return item.Spec.Crop(sourceSize); }
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
    bool IsSelected(Item item) { return selection.Contains(item); }
    List<Item> Movable() { var movable=new List<Item>(); foreach(var item in selection) if(!item.Spec.Locked) movable.Add(item); return movable; }
    Item Find(string id) { foreach(var item in items) if(item.Spec.ObsId==id) return item; return null; }

    // ----- Itens, seleção e painel -----
    void RebuildItems() {
        foreach(var item in items) item.Thumb.Dispose();
        items.Clear();
        foreach(var overlay in overlays) items.Add(new Item { Overlay=overlay, Thumb=new Thumbnail(Handle,source) });
    }
    List<string> SelectionIds() { var ids=new List<string>(); foreach(var item in selection) ids.Add(item.Spec.ObsId); return ids; }
    void ReselectIds(List<string> ids,string primaryId) {
        selection.Clear();
        foreach(var item in items) if(ids.Contains(item.Spec.ObsId)) selection.Add(item);
        primary=primaryId==null?null:Find(primaryId);
        if(primary==null || !selection.Contains(primary)) primary=selection.Count>0?selection[selection.Count-1]:null;
        SyncAll(); Mark();
    }
    void SetSelection(IEnumerable<Item> chosen,Item newPrimary) {
        selection.Clear(); selection.AddRange(chosen);
        primary=newPrimary!=null && selection.Contains(newPrimary)?newPrimary:(selection.Count>0?selection[selection.Count-1]:null);
        SyncAll(); Mark();
    }
    void SyncAll() { SyncLayers(); SyncFields(); UpdateHistoryButtons(); }
    void SyncLayers() {
        binding=true;
        layerList.BeginUpdate();
        layerList.Items.Clear();
        for(int i=items.Count-1;i>=0;i--) { layerList.Items.Add(items[i].Spec); if(selection.Contains(items[i])) layerList.SetSelected(layerList.Items.Count-1,true); }
        layerList.EndUpdate();
        binding=false;
    }
    void SelectFromList() {
        if(binding) return;
        var chosen=new List<Item>(); Item last=null;
        foreach(int row in layerList.SelectedIndices) { var item=items[items.Count-1-row]; chosen.Add(item); last=item; }
        selection.Clear(); selection.AddRange(chosen); primary=last;
        SyncFields(); UpdateHistoryButtons(); Mark();
    }
    void ToggleLayer(int row,bool eye) {
        var item=items[items.Count-1-row];
        Mutate(delegate { if(eye) item.Spec.Hidden=!item.Spec.Hidden; else item.Spec.Locked=!item.Spec.Locked; });
        DrawItem(item); SyncAll(); Mark();
    }
    // Os campos mostram e editam o recorte de origem (pixels do jogo) quando há uma única área selecionada.
    void SyncFields() {
        binding=true;
        bool single=selection.Count==1, any=selection.Count>0;
        name.Enabled=exactX.Enabled=exactY.Enabled=exactW.Enabled=exactH.Enabled=single;
        name.Text=single?primary.Spec.Name:"";
        Rectangle crop=single?CropOf(primary):Rectangle.Empty;
        exactX.Value=crop.X; exactY.Value=crop.Y; exactW.Value=crop.Width; exactH.Value=crop.Height;
        opacityBox.Enabled=any; opacityBox.Value=any?Math.Max(20,Math.Min(100,primary.Spec.Opacity)):100;
        removeButton.Enabled=duplicateButton.Enabled=upButton.Enabled=downButton.Enabled=any;
        int unlocked=Movable().Count;
        foreach(var button in alignButtons) button.Enabled=unlocked>=2;
        distributeH.Enabled=distributeV.Enabled=unlocked>=3;
        binding=false;
    }
    void UpdateHistoryButtons() { undoButton.Enabled=history.CanUndo; redoButton.Enabled=history.CanRedo; }

    // ----- Desfazer: instantâneos do estado completo -----
    EditorHistory.Snapshot TakeSnapshot() {
        var snapshot=new EditorHistory.Snapshot();
        foreach(var overlay in overlays) { overlay.CaptureSpec(); snapshot.Areas.Add(overlay.Spec.Clone()); }
        return snapshot;
    }
    void Commit(EditorHistory.Snapshot before) {
        var after=TakeSnapshot();
        if(before.SameAs(after)) return;
        history.Push(before,after); UpdateHistoryButtons(); RaiseChanged();
    }
    void Mutate(Action action) { var before=TakeSnapshot(); action(); Commit(before); }
    void Undo() { if(history.CanUndo) Restore(history.Undo()); }
    void Redo() { if(history.CanRedo) Restore(history.Redo()); }
    // Leva as overlays ao estado guardado: apaga as que não existiam, recria as que foram apagadas (mesmo ObsId) e reaplica
    // recorte, posição, camada e ordem.
    void Restore(EditorHistory.Snapshot snapshot) {
        var ids=SelectionIds(); string primaryId=primary==null?null:primary.Spec.ObsId;
        var wanted=new HashSet<string>(); var recreated=new List<string>();
        foreach(var spec in snapshot.Areas) wanted.Add(spec.ObsId);
        for(int i=overlays.Count-1;i>=0;i--) if(!wanted.Contains(overlays[i].Spec.ObsId)) remove(overlays[i]);
        foreach(var spec in snapshot.Areas) {
            int at=IndexOfId(spec.ObsId);
            if(at<0) { create(spec.Clone()); at=IndexOfId(spec.ObsId); recreated.Add(spec.ObsId); }
            overlays[at].Restore(spec);
        }
        for(int i=0;i<snapshot.Areas.Count;i++) {
            int at=IndexOfId(snapshot.Areas[i].ObsId);
            if(at!=i) { var moved=overlays[at]; overlays.RemoveAt(at); overlays.Insert(i,moved); }
        }
        RebuildItems();
        // Refazer uma criação ou desfazer uma remoção devolve as áreas e as deixa selecionadas.
        if(recreated.Count>0) ReselectIds(recreated,recreated[recreated.Count-1]); else ReselectIds(ids,primaryId);
        UpdateView(); RaiseChanged();
    }
    int IndexOfId(string id) { for(int i=0;i<overlays.Count;i++) if(overlays[i].Spec.ObsId==id) return i; return -1; }

    // ----- Visão -----
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
        if(item.Spec.Hidden) { item.Thumb.Hide(); return; }
        Rectangle view=ToView(RectOf(item.Overlay)), crop=CropOf(item);
        int border=Math.Max(1,(int)Math.Round(Overlay.Border*ViewScale));
        Rectangle dest=Geometry.Fit(crop.Size,Rectangle.Inflate(view,-border,-border)), visible=Rectangle.Intersect(dest,preview);
        if(dest.IsEmpty || visible.Width<1 || visible.Height<1) { item.Thumb.Hide(); return; }
        double sx=(double)crop.Width/dest.Width, sy=(double)crop.Height/dest.Height;
        item.Thumb.Draw(Rectangle.FromLTRB(crop.Left+(int)Math.Round((visible.Left-dest.Left)*sx),crop.Top+(int)Math.Round((visible.Top-dest.Top)*sy),crop.Left+(int)Math.Round((visible.Right-dest.Left)*sx),crop.Top+(int)Math.Round((visible.Bottom-dest.Top)*sy)),visible);
    }
    void Mark() {
        var marks=new EditorAdorner.Mark[items.Count];
        for(int i=0;i<marks.Length;i++) marks[i]=new EditorAdorner.Mark { Rect=ToView(RectOf(items[i].Overlay)), Selected=IsSelected(items[i]), Hidden=items[i].Spec.Hidden, Locked=items[i].Spec.Locked };
        var sources=new Rectangle[selection.Count];
        for(int i=0;i<sources.Length;i++) sources[i]=ToView(CropOf(selection[i]));
        double s=ViewScale;
        var vertical=new int[verticalGuides.Count]; for(int i=0;i<vertical.Length;i++) vertical[i]=preview.Left+(int)Math.Round((verticalGuides[i]-viewport.Left)*s);
        var horizontal=new int[horizontalGuides.Count]; for(int i=0;i<horizontal.Length;i++) horizontal[i]=preview.Top+(int)Math.Round((horizontalGuides[i]-viewport.Top)*s);
        adorner.Update(preview,marks,sources,drawing.IsEmpty?Rectangle.Empty:ToView(drawing),marquee.IsEmpty?Rectangle.Empty:ToView(marquee),vertical,horizontal);
    }
    void PositionAdorner() { if(IsHandleCreated && !adorner.IsDisposed) adorner.Bounds=RectangleToScreen(ClientRectangle); }
    void ShowPixel(Point p) {
        if(thumb==null || !preview.Contains(p)) { pixelLabel.Text=""; return; }
        Point at=ToSource(p); pixelLabel.Text="x "+at.X+"   y "+at.Y;
    }
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

    // ----- Mouse -----
    // A última área da lista fica por cima na prévia, então é a primeira a receber o clique.
    Item ItemAt(Point p) {
        for(int i=items.Count-1;i>=0;i--) if(ToView(RectOf(items[i].Overlay)).Contains(p)) return items[i];
        return null;
    }
    void BeginGesture(MouseEventArgs e) {
        if(thumb==null || gesture!=Gesture.None || !preview.Contains(e.Location)) return;
        ActiveControl=null; // as teclas (setas, Delete, Espaço) passam a valer para a prévia, não para o campo que tinha o foco
        if(spaceDown || e.Button==MouseButtons.Middle || e.Button==MouseButtons.Right) {
            gesture=Gesture.Pan; panButton=e.Button; panStart=e.Location; panOrigin=viewport.Location; Capture=true; UpdateCursor(e.Location); return;
        }
        if(e.Button!=MouseButtons.Left) return;
        Keys modifiers=modifierKeys();
        bool ctrl=(modifiers&Keys.Control)!=0, shift=(modifiers&Keys.Shift)!=0;
        var hit=ItemAt(e.Location);
        if(hit!=null) {
            var chosen=new List<Item>(selection);
            if(ctrl) { if(!chosen.Remove(hit)) chosen.Add(hit); SetSelection(chosen,hit); return; }
            if(shift) { if(!chosen.Contains(hit)) chosen.Add(hit); SetSelection(chosen,hit); }
            else if(!IsSelected(hit)) SetSelection(new[]{hit},hit);
            else { primary=hit; SyncFields(); Mark(); }
            BeginMove(e.Location);
        } else if(shift) {
            gesture=Gesture.Marquee; marqueeAnchor=ToSource(e.Location); marquee=Rectangle.Empty; marqueeBase=SelectionIds();
        } else { SetSelection(new Item[0],null); gesture=Gesture.Draw; drawAnchor=ToSource(e.Location); drawing=Rectangle.Empty; }
        if(gesture!=Gesture.None) Capture=true;
        UpdateCursor(e.Location);
    }
    void BeginMove(Point p) {
        moveStarts=new Dictionary<Item,Rectangle>();
        foreach(var item in Movable()) moveStarts[item]=RectOf(item.Overlay);
        if(primary==null || !moveStarts.ContainsKey(primary)) return; // camada travada: só seleciona
        gestureBefore=TakeSnapshot();
        moveUnion=Rectangle.Empty;
        foreach(var rect in moveStarts.Values) moveUnion=moveUnion.IsEmpty?rect:Rectangle.Union(moveUnion,rect);
        Point at=ToSource(p); Rectangle start=moveStarts[primary];
        grabOffset=new Point(at.X-start.X,at.Y-start.Y);
        gesture=Gesture.Move;
    }
    void Continue(Point p) {
        if(gesture==Gesture.Pan) PanTo(p);
        else if(gesture==Gesture.Move) MoveGroup(p);
        else if(gesture==Gesture.Draw) {
            Point end=ToSource(p);
            drawing=slotSnap.Checked?Geometry.SlotBlock(drawAnchor,end,sourceSize,squareOnly.Checked)
                :squareOnly.Checked?Geometry.Square(drawAnchor,end,sourceSize)
                :Rectangle.FromLTRB(Math.Min(drawAnchor.X,end.X),Math.Min(drawAnchor.Y,end.Y),Math.Max(drawAnchor.X,end.X),Math.Max(drawAnchor.Y,end.Y));
            Mark();
        } else if(gesture==Gesture.Marquee) {
            Point end=ToSource(p);
            marquee=Rectangle.FromLTRB(Math.Min(marqueeAnchor.X,end.X),Math.Min(marqueeAnchor.Y,end.Y),Math.Max(marqueeAnchor.X,end.X),Math.Max(marqueeAnchor.Y,end.Y));
            var chosen=new List<Item>();
            foreach(var item in items) if(marqueeBase.Contains(item.Spec.ObsId) || (!marquee.IsEmpty && marquee.IntersectsWith(RectOf(item.Overlay)))) chosen.Add(item);
            selection.Clear(); selection.AddRange(chosen); primary=chosen.Count>0?chosen[chosen.Count-1]:null;
            SyncFields(); Mark();
        }
    }
    // Move o grupo todo pelo mesmo deslocamento: encaixa o canto do retângulo que envolve o grupo na grade, depois nas bordas
    // e centros das outras áreas (guias). Alt desliga os dois encaixes.
    void MoveGroup(Point p) {
        Point at=ToSource(p), target=new Point(at.X-grabOffset.X,at.Y-grabOffset.Y), origin=moveStarts[primary].Location;
        Point delta=new Point(target.X-origin.X,target.Y-origin.Y);
        Rectangle moved=moveUnion; moved.Offset(delta);
        verticalGuides.Clear(); horizontalGuides.Clear();
        if((modifierKeys()&Keys.Alt)==0) {
            Point snapped=Geometry.SnapToGrid(moved.Location,CellPixels);
            delta.Offset(snapped.X-moved.X,snapped.Y-moved.Y); moved.Location=snapped;
            var others=new List<Rectangle>();
            foreach(var item in items) if(!IsSelected(item) && !item.Spec.Hidden) others.Add(RectOf(item.Overlay));
            Point edge=Geometry.SnapToEdges(moved,others,Math.Max(2,(int)Math.Round(GuidePixels/ViewScale)),verticalGuides,horizontalGuides);
            delta.Offset(edge.X,edge.Y);
        }
        int dx=Math.Max(-moveUnion.Left,Math.Min(sourceSize.Width-moveUnion.Right,delta.X)), dy=Math.Max(-moveUnion.Top,Math.Min(sourceSize.Height-moveUnion.Bottom,delta.Y));
        foreach(var pair in moveStarts) Place(pair.Key,new Rectangle(pair.Value.X+dx,pair.Value.Y+dy,pair.Value.Width,pair.Value.Height));
        SyncFields(); Mark();
    }
    void EndGesture(MouseEventArgs e) {
        if(gesture==Gesture.None) return;
        if(gesture==Gesture.Pan) { if(e.Button!=panButton) return; PanTo(e.Location); }
        else if(e.Button!=MouseButtons.Left) return;
        else {
            Continue(e.Location);
            if(gesture==Gesture.Move) { Commit(gestureBefore); gestureBefore=null; }
            else if(gesture==Gesture.Draw && !drawing.IsEmpty) { var rect=drawing; drawing=Rectangle.Empty; Mutate(delegate { CreateArea(rect); }); }
        }
        gesture=Gesture.None; drawing=Rectangle.Empty; marquee=Rectangle.Empty; verticalGuides.Clear(); horizontalGuides.Clear(); Capture=false; SyncAll(); Mark(); UpdateCursor(e.Location);
    }
    void UpdateCursor(Point p) {
        bool inside=preview.Contains(p);
        Cursor=gesture==Gesture.Pan || (inside && spaceDown)?Cursors.Hand
            :gesture==Gesture.Move || (gesture==Gesture.None && inside && ItemAt(p)!=null)?Cursors.SizeAll
            :inside || gesture==Gesture.Draw || gesture==Gesture.Marquee?Cursors.Cross:Cursors.Default;
    }
    void Place(Item item,Rectangle rect) {
        SetRect(item.Overlay,Geometry.ClampInside(rect,sourceSize));
        DrawItem(item);
    }

    // ----- Operações (cada uma vira um passo de desfazer) -----
    // Nova área: o recorte é o retângulo desenhado, em tamanho real (1:1); a overlay nasce ao lado do recorte, escondida.
    void CreateArea(Rectangle crop) {
        if(overlays.Count>=MaxAreas) { MessageBox.Show(this,"O protótipo permite até 30 áreas por layout."); return; }
        var window=Native.ThumbnailBounds(source);
        var spec=new RegionSpec { Name=UniqueName("Área "+(overlays.Count+1)), X=(double)crop.X/sourceSize.Width, Y=(double)crop.Y/sourceSize.Height, W=(double)crop.Width/sourceSize.Width, H=(double)crop.Height/sourceSize.Height, Width=Math.Max(32,crop.Width), Height=Math.Max(20,crop.Height), Opacity=100 };
        var size=new Size(spec.Width+2*Overlay.Border,spec.Height+2*Overlay.Border);
        int x=crop.Right+8+size.Width<=sourceSize.Width?crop.Right+8:crop.Left-8-size.Width;
        var dest=Geometry.ClampInside(new Rectangle(x,crop.Top,size.Width,size.Height),sourceSize);
        spec.Left=window.Left+dest.X; spec.Top=window.Top+dest.Y;
        var overlay=create(spec);
        KeepInside(overlay); RebuildItems();
        var item=Find(overlay.Spec.ObsId);
        SetSelection(new[]{item},item); UpdateView();
    }
    string UniqueName(string wanted) {
        string candidate=wanted;
        for(int n=2;NameTaken(candidate);n++) candidate=wanted+" "+n;
        return candidate;
    }
    bool NameTaken(string candidate) { foreach(var overlay in overlays) if(overlay.Spec.Name==candidate) return true; return false; }
    void RemoveSelected() {
        var doomed=Movable();
        if(doomed.Count==0) return;
        var survivors=SelectionIds();
        foreach(var item in doomed) survivors.Remove(item.Spec.ObsId);
        Mutate(delegate {
            foreach(var item in doomed) remove(item.Overlay);
            RebuildItems(); ReselectIds(survivors,null); UpdateView();
        });
    }
    void Duplicate() {
        if(selection.Count==0) return;
        var copies=new List<string>();
        Mutate(delegate {
            foreach(var item in new List<Item>(selection)) {
                if(overlays.Count>=MaxAreas) break;
                var spec=item.Spec.Clone(); spec.ObsId=null; spec.ObsTitle=null; spec.Locked=false;
                spec.Name=UniqueName(item.Spec.Name+" cópia"); spec.Left+=CellPixels; spec.Top+=CellPixels;
                var overlay=create(spec); KeepInside(overlay); copies.Add(overlay.Spec.ObsId);
            }
            RebuildItems(); ReselectIds(copies,null); UpdateView();
        });
    }
    void SelectAll() { SetSelection(items,primary); }
    void AlignSelected(Geometry.AlignKind kind) {
        var chosen=Movable();
        if(chosen.Count<2) return;
        Mutate(delegate {
            var rects=new List<Rectangle>(); foreach(var item in chosen) rects.Add(RectOf(item.Overlay));
            var aligned=Geometry.Align(rects,kind);
            for(int i=0;i<chosen.Count;i++) Place(chosen[i],aligned[i]);
        });
        SyncFields(); Mark();
    }
    void DistributeSelected(bool horizontal) {
        var chosen=Movable();
        if(chosen.Count<3) return;
        Mutate(delegate {
            var rects=new List<Rectangle>(); foreach(var item in chosen) rects.Add(RectOf(item.Overlay));
            var spread=Geometry.Distribute(rects,horizontal);
            for(int i=0;i<chosen.Count;i++) Place(chosen[i],spread[i]);
        });
        SyncFields(); Mark();
    }
    // Sobe ou desce as áreas selecionadas um passo na ordem das camadas, sem atravessar outra selecionada.
    void MoveLayers(bool up) {
        if(selection.Count==0) return;
        var ids=SelectionIds(); string primaryId=primary==null?null:primary.Spec.ObsId;
        Mutate(delegate {
            var order=new List<Overlay>(overlays);
            Func<Overlay,bool> chosen=delegate(Overlay o) { return ids.Contains(o.Spec.ObsId); };
            if(up) { for(int i=order.Count-2;i>=0;i--) if(chosen(order[i]) && !chosen(order[i+1])) { var o=order[i]; order[i]=order[i+1]; order[i+1]=o; } }
            else { for(int i=1;i<order.Count;i++) if(chosen(order[i]) && !chosen(order[i-1])) { var o=order[i]; order[i]=order[i-1]; order[i-1]=o; } }
            for(int i=0;i<order.Count;i++) { int at=overlays.IndexOf(order[i]); if(at!=i) { overlays.RemoveAt(at); overlays.Insert(i,order[i]); } }
            RebuildItems(); ReselectIds(ids,primaryId); UpdateView();
        });
    }
    // Renomear atualiza na hora; o passo de desfazer fecha quando o campo perde o foco.
    void Rename() {
        if(binding || selection.Count!=1) return;
        string text=name.Text.Trim();
        if(text.Length==0) return;
        primary.Spec.Name=text; layerList.Invalidate(); RaiseChanged();
    }
    void ApplyOpacity() {
        if(binding || selection.Count==0) return;
        int value=(int)opacityBox.Value;
        Mutate(delegate { foreach(var item in selection) { item.Spec.Opacity=value; item.Overlay.ApplyStyle(); } });
    }
    void ApplyFields() {
        if(binding || selection.Count!=1) return;
        ApplyCrop(new Rectangle((int)exactX.Value,(int)exactY.Value,(int)exactW.Value,(int)exactH.Value));
    }
    void ApplyCrop(Rectangle crop) {
        int w=Math.Max(1,Math.Min(crop.Width,sourceSize.Width)), h=Math.Max(1,Math.Min(crop.Height,sourceSize.Height));
        crop=new Rectangle(Math.Max(0,Math.Min(crop.X,sourceSize.Width-w)),Math.Max(0,Math.Min(crop.Y,sourceSize.Height-h)),w,h);
        var spec=primary.Spec;
        Mutate(delegate {
            primary.Overlay.UpdateRegion(new RegionSpec { Name=spec.Name, X=(double)crop.X/sourceSize.Width, Y=(double)crop.Y/sourceSize.Height, W=(double)crop.Width/sourceSize.Width, H=(double)crop.Height/sourceSize.Height, Width=spec.Width, Height=spec.Height, Opacity=spec.Opacity });
            KeepInside(primary.Overlay); DrawItem(primary);
        });
        SyncFields(); Mark();
    }

    // ----- Teclado -----
    protected override bool ProcessCmdKey(ref Message msg,Keys keyData) {
        // Em campos de texto as teclas são do campo (Espaço digita, setas movem o cursor, Ctrl + Z desfaz o texto).
        if(ActiveControl is ComboBox || ActiveControl is NumericUpDown) return base.ProcessCmdKey(ref msg,keyData);
        Keys key=keyData&Keys.KeyCode;
        bool ctrl=(keyData&Keys.Control)!=0, shift=(keyData&Keys.Shift)!=0, alt=(keyData&Keys.Alt)!=0;
        if(ctrl) {
            switch(key) {
                case Keys.Z: if(shift) Redo(); else Undo(); return true;
                case Keys.Y: Redo(); return true;
                case Keys.A: SelectAll(); return true;
                case Keys.D: Duplicate(); return true;
                default: return base.ProcessCmdKey(ref msg,keyData);
            }
        }
        if(key==Keys.Space) { spaceDown=true; UpdateCursor(PointToClient(MousePosition)); return true; }
        if(key==Keys.Delete && selection.Count>0) { RemoveSelected(); return true; }
        int dx=0, dy=0;
        switch(key) {
            case Keys.Left: dx=-1; break;
            case Keys.Right: dx=1; break;
            case Keys.Up: dy=-1; break;
            case Keys.Down: dy=1; break;
            default: return base.ProcessCmdKey(ref msg,keyData);
        }
        if(selection.Count==0) return base.ProcessCmdKey(ref msg,keyData);
        if(alt) {
            if(selection.Count==1) { int step=shift?10:1; Rectangle crop=CropOf(primary); ApplyCrop(new Rectangle(crop.X+dx*step,crop.Y+dy*step,crop.Width,crop.Height)); }
        } else {
            int step=shift?1:CellPixels;
            Mutate(delegate { foreach(var item in Movable()) { Rectangle rect=RectOf(item.Overlay); Place(item,new Rectangle(rect.X+dx*step,rect.Y+dy*step,rect.Width,rect.Height)); } });
            SyncFields(); Mark();
        }
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
    protected override void Dispose(bool disposing) { if(disposing) { adorner.Dispose(); tips.Dispose(); } base.Dispose(disposing); }
}
