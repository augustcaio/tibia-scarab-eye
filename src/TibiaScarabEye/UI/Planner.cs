using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using TibiaScarabEye.Interop;
using TibiaScarabEye.Layouts;

namespace TibiaScarabEye.UI;

// Planejador: mostra a janela do jogo ao vivo, com a grade por cima, e deixa o usuário posicionar as overlays
// antes de elas aparecerem na tela. Trabalha em coordenadas da janela de origem e só grava nas overlays ao aplicar.
internal sealed class Planner : Form {
    sealed class Item {
        public Overlay Overlay;
        public Rectangle Original, Rect;
        public Thumbnail Thumb;
    }
    // Grade densa e fixa: 8 px de célula deixam posicionar com precisão sem um controle a mais na janela principal.
    internal const int CellPixels=8;
    readonly IntPtr source;
    readonly IList<Overlay> overlays;
    readonly List<Item> items=new List<Item>();
    readonly PlanAdorner adorner=new PlanAdorner();
    readonly Button apply, cancel;
    readonly Label shortcuts;
    Thumbnail thumb;
    Size sourceSize;
    Rectangle preview;
    Item selected, dragging;
    Point grabOffset;
    public bool Moved { get; private set; }
    public Planner(IntPtr src,IList<Overlay> areas,int selectedIndex) {
        source=src; overlays=areas;
        Theme.Apply(this); Text="Posicionar áreas • Tibia Scarab Eye";
        Size=new Size(1060,740); MinimumSize=new Size(760,480); StartPosition=FormStartPosition.CenterParent;
        Controls.Add(Theme.Label("Arraste as áreas para posicioná-las no jogo. Elas só aparecem na tela no modo jogo.",20,16,1000,26,false));
        Controls.Add(Theme.Label("Grade de "+CellPixels+" px sobre a janela do jogo. Segure Alt para mover sem prender na grade.",20,44,1000,24,true));
        apply=Theme.Button("Aplicar posições",20,0,190,true); apply.Anchor=AnchorStyles.Bottom|AnchorStyles.Left; apply.Click+=delegate { Apply(); }; Controls.Add(apply);
        cancel=Theme.Button("Cancelar",224,0,120,false); cancel.Anchor=apply.Anchor; cancel.Click+=delegate { DialogResult=DialogResult.Cancel; }; Controls.Add(cancel); CancelButton=cancel;
        shortcuts=Theme.Label("Setas: mover uma célula • Shift + setas: mover 1 px",360,0,600,26,true); shortcuts.Anchor=apply.Anchor; Controls.Add(shortcuts);
        LocationChanged+=delegate { PositionAdorner(); };
        Resize+=delegate { LayoutBottom(); UpdateView(); };
        Shown+=delegate {
            try {
                thumb=new Thumbnail(Handle,source); sourceSize=thumb.SourceSize; LoadItems(selectedIndex);
                LayoutBottom(); UpdateView(); adorner.Show(this);
            } catch(Exception ex) { MessageBox.Show(this,"Não foi possível abrir a prévia.\n"+ex.Message); DialogResult=DialogResult.Cancel; }
        };
        MouseDown+=delegate(object s,MouseEventArgs e) { if(e.Button==MouseButtons.Left) BeginDrag(e.Location); };
        MouseMove+=delegate(object s,MouseEventArgs e) { if(dragging!=null) Drag(e.Location); else Cursor=ItemAt(e.Location)!=null?Cursors.SizeAll:Cursors.Default; };
        MouseUp+=delegate(object s,MouseEventArgs e) { if(dragging!=null) { Drag(e.Location); dragging=null; Capture=false; } };
        MouseCaptureChanged+=delegate { if(!Capture) dragging=null; };
    }
    void LayoutBottom() { apply.Top=ClientSize.Height-50; cancel.Top=apply.Top; shortcuts.Top=apply.Top+8; }
    Rectangle PreviewArea() { return new Rectangle(20,78,Math.Max(1,ClientSize.Width-40),Math.Max(1,ClientSize.Height-78-64)); }
    double ViewScale { get { return preview.Width>0 && sourceSize.Width>0?(double)preview.Width/sourceSize.Width:1; } }
    Rectangle ToView(Rectangle r) {
        double s=ViewScale;
        return Rectangle.FromLTRB(preview.Left+(int)Math.Round(r.Left*s),preview.Top+(int)Math.Round(r.Top*s),preview.Left+(int)Math.Round(r.Right*s),preview.Top+(int)Math.Round(r.Bottom*s));
    }
    Point ToWindow(Point p) { double s=ViewScale; return new Point((int)Math.Round((p.X-preview.Left)/s),(int)Math.Round((p.Y-preview.Top)/s)); }

    void LoadItems(int selectedIndex) {
        var window=Native.ThumbnailBounds(source);
        for(int i=0;i<overlays.Count;i++) {
            var overlay=overlays[i];
            var original=new Rectangle(overlay.Left-window.Left,overlay.Top-window.Top,overlay.Width,overlay.Height);
            items.Add(new Item { Overlay=overlay, Original=original, Rect=Geometry.ClampInside(original,sourceSize), Thumb=new Thumbnail(Handle,source) });
            if(i==selectedIndex) selected=items[i];
        }
        if(selected==null && items.Count>0) selected=items[0];
    }
    void UpdateView() {
        if(thumb==null) return;
        preview=Geometry.Fit(sourceSize,PreviewArea());
        thumb.Draw(new Rectangle(Point.Empty,sourceSize),preview);
        foreach(var item in items) DrawItem(item);
        Mark(); PositionAdorner();
    }
    // A miniatura da área segue o mesmo encaixe que a overlay usa na tela, dentro da borda dela.
    void DrawItem(Item item) {
        Rectangle view=ToView(item.Rect);
        int border=Math.Max(1,(int)Math.Round(Overlay.Border*ViewScale));
        Rectangle crop=item.Overlay.Spec.Crop(sourceSize);
        item.Thumb.Draw(crop,Geometry.Fit(crop.Size,Rectangle.Inflate(view,-border,-border)));
    }
    void Mark() {
        var marks=new PlanAdorner.Mark[items.Count];
        for(int i=0;i<marks.Length;i++) marks[i]=new PlanAdorner.Mark { Rect=ToView(items[i].Rect), Selected=items[i]==selected };
        adorner.Update(preview,CellPixels*ViewScale,marks);
    }
    void PositionAdorner() { if(IsHandleCreated && !adorner.IsDisposed) adorner.Bounds=RectangleToScreen(ClientRectangle); }

    // A última da lista fica por cima na miniatura, então é a primeira a receber o clique.
    Item ItemAt(Point p) {
        for(int i=items.Count-1;i>=0;i--) if(ToView(items[i].Rect).Contains(p)) return items[i];
        return null;
    }
    void BeginDrag(Point p) {
        var hit=ItemAt(p);
        if(hit==null) return;
        selected=dragging=hit; Point at=ToWindow(p);
        grabOffset=new Point(at.X-hit.Rect.X,at.Y-hit.Rect.Y);
        Capture=true; Mark();
    }
    void Drag(Point p) {
        Point at=ToWindow(p), target=new Point(at.X-grabOffset.X,at.Y-grabOffset.Y);
        if((ModifierKeys&Keys.Alt)==0) target=Geometry.SnapToGrid(target,CellPixels);
        Place(dragging,target);
    }
    void Place(Item item,Point location) {
        item.Rect=Geometry.ClampInside(new Rectangle(location,item.Rect.Size),sourceSize);
        DrawItem(item); Mark();
    }
    protected override bool ProcessCmdKey(ref Message msg,Keys keyData) {
        int step=(keyData&Keys.Shift)!=0?1:CellPixels, dx=0, dy=0;
        switch(keyData&Keys.KeyCode) {
            case Keys.Left: dx=-step; break;
            case Keys.Right: dx=step; break;
            case Keys.Up: dy=-step; break;
            case Keys.Down: dy=step; break;
            default: return base.ProcessCmdKey(ref msg,keyData);
        }
        if(selected==null) return base.ProcessCmdKey(ref msg,keyData);
        Place(selected,new Point(selected.Rect.X+dx,selected.Rect.Y+dy));
        return true;
    }

    // Grava as posições nas overlays em coordenadas de tela, relativas à posição atual da janela do jogo.
    void Apply() {
        var window=Native.ThumbnailBounds(source);
        foreach(var item in items) {
            if(item.Rect==item.Original) continue;
            item.Overlay.Bounds=new Rectangle(window.Left+item.Rect.X,window.Top+item.Rect.Y,item.Rect.Width,item.Rect.Height);
            item.Overlay.CaptureSpec(); Moved=true;
        }
        DialogResult=DialogResult.OK;
    }
    protected override void OnFormClosed(FormClosedEventArgs e) {
        adorner.Close();
        foreach(var item in items) item.Thumb.Dispose();
        if(thumb!=null) thumb.Dispose();
        base.OnFormClosed(e);
    }
    protected override void Dispose(bool disposing) { if(disposing) adorner.Dispose(); base.Dispose(disposing); }
}
