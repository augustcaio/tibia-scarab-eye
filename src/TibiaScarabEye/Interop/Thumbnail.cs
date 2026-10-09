using System;
using System.Drawing;
using System.Runtime.InteropServices;

namespace TibiaScarabEye.Interop;

internal sealed class Thumbnail : IDisposable {
    IntPtr handle;
    public Thumbnail(IntPtr destination, IntPtr source) {
        Marshal.ThrowExceptionForHR(Native.DwmRegisterThumbnail(destination,source,out handle));
    }
    public Size SourceSize {
        get { Native.SizeI s; Marshal.ThrowExceptionForHR(Native.DwmQueryThumbnailSourceSize(handle,out s)); return new Size(s.Width,s.Height); }
    }
    public void Draw(Rectangle source, Rectangle destination) {
        if (destination.Width<1 || destination.Height<1) return;
        var p=new Native.Props { Flags=31, Source=new Native.Rect(source), Destination=new Native.Rect(destination), Opacity=255, Visible=true, ClientOnly=false };
        Marshal.ThrowExceptionForHR(Native.DwmUpdateThumbnailProperties(handle,ref p));
    }
    public void Dispose() { if (handle!=IntPtr.Zero) { Native.DwmUnregisterThumbnail(handle); handle=IntPtr.Zero; } }
}
