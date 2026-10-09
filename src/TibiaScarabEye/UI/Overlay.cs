using System;
using System.Drawing;
using System.Windows.Forms;
using TibiaScarabEye.Interop;
using TibiaScarabEye.Layouts;

namespace TibiaScarabEye.UI;

internal sealed class Overlay : Form {
    public readonly RegionSpec Spec;
    readonly IntPtr source;
    Thumbnail thumb;
    bool locked;
    bool closing;
    bool obsMode;
    bool changingMode;
    public event Action Changed;
    public const int Bar=0;
    public const int Border=3;
    public Overlay(IntPtr src,RegionSpec spec) {
        source=src; Spec=spec; Theme.Apply(this);
        FormBorderStyle=FormBorderStyle.None; ShowInTaskbar=false; TopMost=true;
        StartPosition=FormStartPosition.Manual; MinimumSize=new Size(32+2*Border,Bar+20+2*Border);
        Bounds=Geometry.Restore(new Rectangle(spec.Left,spec.Top,spec.Width+2*Border,spec.Height+2*Border));
        if(string.IsNullOrEmpty(spec.ObsId)) spec.ObsId=Guid.NewGuid().ToString("N");
        if(string.IsNullOrEmpty(spec.ObsTitle)) spec.ObsTitle="Tibia Scarab Eye OBS • "+spec.Name+" • "+spec.ObsId;
        Text=spec.ObsTitle; BackColor=Color.Black;
    }
    protected override bool ShowWithoutActivation { get { return true; } }
    protected override CreateParams CreateParams { get { var p=base.CreateParams; p.ExStyle|=0x08000000; p.ExStyle=obsMode?p.ExStyle&~0x80:p.ExStyle|0x80; return p; } }
    protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); thumb=new Thumbnail(Handle,source); ApplyStyle(); }
    protected override void OnHandleDestroyed(EventArgs e) { if(thumb!=null) { thumb.Dispose(); thumb=null; } base.OnHandleDestroyed(e); }
    public void ApplyStyle() {
        if(!IsHandleCreated) return;
        int style=Native.GetStyle(Handle,-20)|0x80000;
        // OBS excludes WS_EX_TOOLWINDOW from its Window Capture list.
        style=obsMode?style&~0x80:style|0x80;
        style=locked?style|0x20:style&~0x20;
        Native.SetStyle(Handle,-20,style);
        Native.SetLayeredWindowAttributes(Handle,0,(byte)(Spec.Opacity*255/100),2);
    }
    public void SetObsMode(bool enabled) { obsMode=enabled; ApplyStyle(); Render(); }
    public void SetLocked(bool value) {
        if(locked==value) return;
        // Snapshot before MinimumSize can resize a short window on unlock.
        // Keep the content's screen rectangle unchanged while adding/removing chrome.
        Rectangle target=Bounds;
        target.Y+=value?Bar:-Bar;
        target.Height+=value?-Bar:Bar;
        changingMode=true;
        try {
            MinimumSize=Size.Empty;
            locked=value;
            Bounds=target;
            MinimumSize=new Size(32+2*Border,(locked?20:Bar+20)+2*Border);
            ApplyStyle();
        } finally { changingMode=false; }
        Render(); Invalidate();
    }
    public void CaptureSpec() { Spec.Left=Left; Spec.Top=Top-(locked?Bar:0); Spec.Width=ClientSize.Width-2*Border; Spec.Height=ClientSize.Height-(locked?0:Bar)-2*Border; }
    public void UpdateRegion(RegionSpec edited) {
        edited.Validate();
        Spec.Name=edited.Name; Spec.X=edited.X; Spec.Y=edited.Y; Spec.W=edited.W; Spec.H=edited.H;
        Rectangle crop=Spec.Crop(thumb.SourceSize);
        int width=ClientSize.Width-2*Border;
        int height=Math.Max(20,Math.Min(3000,(int)Math.Round((double)width*crop.Height/crop.Width)));
        Size=new Size(width+2*Border,height+(locked?0:Bar)+2*Border);
        CaptureSpec(); Render(); Invalidate(); if(Changed!=null) Changed();
    }
    // Cria a janela sem exibi-la: a overlay só aparece na tela no modo jogo, mas já precisa da miniatura para medir o recorte.
    public void Prepare() { if(!IsHandleCreated) CreateHandle(); Render(); }
    public void Zoom(double factor) {
        Size s=thumb.SourceSize; Rectangle crop=Spec.Crop(s);
        Size=new Size(Math.Max(32,Math.Min(4000,(int)(crop.Width*factor)))+2*Border,Math.Max(20,Math.Min(3000,(int)(crop.Height*factor)))+(locked?0:Bar)+2*Border);
        Bounds=Geometry.Restore(Bounds); CaptureSpec();
    }
    public void Render() {
        if(thumb==null || closing || changingMode || !Native.IsWindow(source)) return;
        Rectangle crop=Spec.Crop(thumb.SourceSize);
        int bar=locked?0:Bar;
        thumb.Draw(crop,Geometry.Fit(crop.Size,new Rectangle(Border,bar+Border,Math.Max(1,ClientSize.Width-2*Border),Math.Max(1,ClientSize.Height-bar-2*Border))));
    }
    protected override void OnResize(EventArgs e) { base.OnResize(e); Render(); }
    protected override void OnResizeEnd(EventArgs e) { base.OnResizeEnd(e); CaptureSpec(); if(Changed!=null) Changed(); }
    protected override void OnPaint(PaintEventArgs e) {
        base.OnPaint(e);
        Theme.Frame(e.Graphics,ClientRectangle);
    }
    protected override void WndProc(ref Message m) {
        if(m.Msg==0x84 && !locked) {
            Point p=PointToClient(new Point(unchecked((short)(m.LParam.ToInt64()&65535)),unchecked((short)((m.LParam.ToInt64()>>16)&65535))));
            int hit=1;
            if(p.Y>=Height-7) hit=p.X>=Width-7?17:p.X<7?16:15;
            else if(p.X>=Width-7) hit=11;
            else if(p.X<7) hit=10;
            else hit=2;
            m.Result=new IntPtr(hit); return;
        }
        base.WndProc(ref m);
    }
    protected override void OnFormClosing(FormClosingEventArgs e) { closing=true; base.OnFormClosing(e); }
}
