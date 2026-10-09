using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Windows.Forms;
using TibiaScarabEye.Interop;
using TibiaScarabEye.Layouts;
using TibiaScarabEye.UI;

namespace TibiaScarabEye.Obs;

// Only geometry is published. OBS uses its existing game_capture source for
// the pixels; no GDI readback, extra game hook, or game-memory access occurs.
internal sealed class ObsBridge : IDisposable {
    public static readonly string StatePath=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"TibiaScarabEye","obs-layout.json");
    readonly IntPtr source;
    readonly IList<Overlay> overlays;
    readonly Timer timer=new Timer();
    readonly ObsPacket packet=new ObsPacket();
    string previous="";
    long lastWrite;
    int writeFailures;
    bool disposed;
    public event Action<string> Failed;
    public ObsBridge(IntPtr window,IList<Overlay> regions) {
        source=window; overlays=regions; timer.Interval=100;
        timer.Tick+=delegate {
            try { Publish(true); writeFailures=0; }
            catch(IOException ex) {
                // A reader may briefly hold the old file without delete sharing.
                // Retry the atomic replacement instead of stopping on one collision.
                if(++writeFailures>=10) { timer.Stop(); if(Failed!=null) Failed("Sincronização pausada: "+ex.Message); }
            }
            catch(Exception ex) { timer.Stop(); if(Failed!=null) Failed("Sincronização pausada: "+ex.Message); }
        };
    }
    public void Start() { Publish(true); timer.Start(); }
    static long Now() { return (long)(DateTime.UtcNow-new DateTime(1970,1,1,0,0,0,DateTimeKind.Utc)).TotalSeconds; }
    internal static Rectangle ClientBounds(IntPtr window) {
        Native.Rect rect; var origin=new Native.PointI();
        if(!Native.GetClientRect(window,out rect) || !Native.ClientToScreen(window,ref origin)) return Rectangle.Empty;
        return new Rectangle(origin.X,origin.Y,rect.Right-rect.Left,rect.Bottom-rect.Top);
    }
    ObsState ReadState(bool enabled) {
        var state=new ObsState();
        if(!enabled || !Native.IsWindow(source) || Native.IsIconic(source)) return state;
        Native.Rect window=Native.ThumbnailBounds(source);
        if(window.Right<=window.Left || window.Bottom<=window.Top) return state;
        Rectangle client=ClientBounds(source);
        if(client.Width<1 || client.Height<1) return state;
        state.Enabled=true; state.Width=client.Width; state.Height=client.Height;
        var windowSize=new Size(window.Right-window.Left,window.Bottom-window.Top);
        foreach(var overlay in overlays) {
            if(overlay.IsDisposed) continue;
            RegionSpec spec=overlay.Spec;
            Rectangle crop=spec.Crop(windowSize);
            var absoluteCrop=new Rectangle(window.Left+crop.X,window.Top+crop.Y,crop.Width,crop.Height);
            Rectangle clipped=Rectangle.Intersect(absoluteCrop,client);
            if(clipped.Width<1 || clipped.Height<1) continue;
            Rectangle fit=Geometry.Fit(crop.Size,new Rectangle(Overlay.Border,Overlay.Border,Math.Max(1,overlay.ClientSize.Width-2*Overlay.Border),Math.Max(1,overlay.ClientSize.Height-2*Overlay.Border)));
            double sx=(double)fit.Width/crop.Width,sy=(double)fit.Height/crop.Height;
            state.Regions.Add(new ObsRegion {
                Id=spec.ObsId,Left=overlay.Left-client.Left,Top=overlay.Top-client.Top,
                Width=overlay.ClientSize.Width,Height=overlay.ClientSize.Height,
                Opacity=spec.Opacity,Visible=overlay.Visible,
                CropX=(double)(clipped.Left-client.Left)/client.Width,CropY=(double)(clipped.Top-client.Top)/client.Height,
                CropW=(double)clipped.Width/client.Width,CropH=(double)clipped.Height/client.Height,
                ContentX=fit.X+(clipped.Left-absoluteCrop.Left)*sx,ContentY=fit.Y+(clipped.Top-absoluteCrop.Top)*sy,
                ContentW=clipped.Width*sx,ContentH=clipped.Height*sy
            });
        }
        return state;
    }
    static string Json(object value) {
        using(var stream=new MemoryStream()) {
            new DataContractJsonSerializer(value.GetType()).WriteObject(stream,value);
            return Encoding.UTF8.GetString(stream.ToArray());
        }
    }
    void Publish(bool enabled) {
        ObsState state=ReadState(enabled); string content=Json(state); long now=Now();
        if(content==previous && now==lastWrite) return;
        if(content!=previous) packet.Revision++;
        packet.Layout=state; packet.Heartbeat=now;
        string directory=Path.GetDirectoryName(StatePath); Directory.CreateDirectory(directory);
        string temp=StatePath+"."+Guid.NewGuid().ToString("N")+".tmp";
        try {
            File.WriteAllText(temp,Json(packet),new UTF8Encoding(false));
            if(File.Exists(StatePath)) File.Replace(temp,StatePath,null); else File.Move(temp,StatePath);
            previous=content; lastWrite=now;
        } finally { if(File.Exists(temp)) File.Delete(temp); }
    }
    public void Dispose() {
        if(disposed) return; disposed=true; timer.Stop(); timer.Dispose();
        try { Publish(false); } catch(IOException) { } catch(UnauthorizedAccessException) { }
    }
}
