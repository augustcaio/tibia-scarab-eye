using System;
using System.Drawing;
using System.Windows.Forms;
using TibiaScarabEye.Interop;
using TibiaScarabEye.Layouts;

namespace TibiaScarabEye.UI;

// Click-through screen guide. Drawing clips itself to the saved game/map region.
internal sealed class GridOverlay : Form {
    readonly IntPtr source;
    readonly RegionSpec map;
    readonly int cell;
    Rectangle mapScreen;
    const int KeyColor=0x12FE45;
    internal GridOverlay(IntPtr source,RegionSpec map,int cell) {
        this.source=source; this.map=map; this.cell=cell;
        FormBorderStyle=FormBorderStyle.None; ShowInTaskbar=false; TopMost=true;
        StartPosition=FormStartPosition.Manual; AutoScaleMode=AutoScaleMode.None;
        BackColor=Color.FromArgb(KeyColor); TransparencyKey=BackColor;
        Bounds=Screen.PrimaryScreen.Bounds;
    }
    protected override bool ShowWithoutActivation { get { return true; } }
    protected override CreateParams CreateParams { get { var p=base.CreateParams; p.ExStyle|=0x80000|0x20|0x80|0x08000000; return p; } }
    internal void RefreshGrid() {
        if(!Native.IsWindow(source) || Native.IsIconic(source)) { Hide(); return; }
        var window=Native.ThumbnailBounds(source);
        if(window.Right<=window.Left || window.Bottom<=window.Top) { Hide(); return; }
        Rectangle bounds=Rectangle.FromLTRB(window.Left,window.Top,window.Right,window.Bottom);
        int x=bounds.Left+(int)Math.Round(map.X*bounds.Width);
        int y=bounds.Top+(int)Math.Round(map.Y*bounds.Height);
        int right=bounds.Left+(int)Math.Round((map.X+map.W)*bounds.Width);
        int bottom=bounds.Top+(int)Math.Round((map.Y+map.H)*bounds.Height);
        mapScreen=Rectangle.Intersect(Rectangle.FromLTRB(x,y,right,bottom),bounds);
        if(!Visible) Show();
        Invalidate();
    }
    internal Rectangle GridBounds { get { return mapScreen; } }
    protected override void OnPaint(PaintEventArgs e) {
        base.OnPaint(e);
        if(mapScreen.IsEmpty || cell<4) return;
        Rectangle local=RectangleToClient(mapScreen);
        e.Graphics.SetClip(local);
        using(var pen=new Pen(Color.FromArgb(105,212,213,199),1)) {
            for(int x=local.Left;x<=local.Right;x+=cell) e.Graphics.DrawLine(pen,x,local.Top,x,local.Bottom);
            for(int y=local.Top;y<=local.Bottom;y+=cell) e.Graphics.DrawLine(pen,local.Left,y,local.Right,y);
            e.Graphics.DrawRectangle(pen,local);
        }
    }
}
