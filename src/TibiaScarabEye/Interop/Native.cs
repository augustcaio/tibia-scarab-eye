using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;

namespace TibiaScarabEye.Interop;

internal static class Native {
    [StructLayout(LayoutKind.Sequential)] internal struct Rect {
        public int Left, Top, Right, Bottom;
        public Rect(Rectangle r) { Left=r.Left; Top=r.Top; Right=r.Right; Bottom=r.Bottom; }
    }
    [StructLayout(LayoutKind.Sequential)] internal struct SizeI { public int Width, Height; }
    [StructLayout(LayoutKind.Sequential)] internal struct PointI { public int X,Y; }
    [StructLayout(LayoutKind.Sequential)] internal struct Props {
        public uint Flags;
        public Rect Destination, Source;
        public byte Opacity;
        [MarshalAs(UnmanagedType.Bool)] public bool Visible;
        [MarshalAs(UnmanagedType.Bool)] public bool ClientOnly;
    }
    internal delegate bool EnumProc(IntPtr h, IntPtr p);
    [DllImport("user32.dll")] internal static extern bool EnumWindows(EnumProc cb, IntPtr p);
    [DllImport("user32.dll")] internal static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] internal static extern bool IsWindow(IntPtr h);
    [DllImport("user32.dll")] internal static extern bool IsIconic(IntPtr h);
    [DllImport("user32.dll")] internal static extern bool GetWindowRect(IntPtr h, out Rect r);
    [DllImport("user32.dll")] internal static extern bool GetClientRect(IntPtr h, out Rect r);
    [DllImport("user32.dll")] internal static extern bool ClientToScreen(IntPtr h, ref PointI point);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll", EntryPoint="GetWindowLongW")] internal static extern int GetStyle(IntPtr h, int index);
    [DllImport("user32.dll", EntryPoint="SetWindowLongW")] internal static extern int SetStyle(IntPtr h, int index, int value);
    [DllImport("user32.dll")] internal static extern bool SetLayeredWindowAttributes(IntPtr h, uint key, byte alpha, uint flags);
    [DllImport("user32.dll")] internal static extern bool RegisterHotKey(IntPtr h, int id, uint modifiers, uint key);
    [DllImport("user32.dll")] internal static extern bool UnregisterHotKey(IntPtr h, int id);
    [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr h, IntPtr insertAfter, int x, int y, int width, int height, uint flags);
    // Põe a janela no topo da faixa das janelas topmost, sem mover, redimensionar nem ativar.
    internal static void BringToTop(IntPtr h) { SetWindowPos(h,new IntPtr(-1),0,0,0,0,0x13); }
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr h, int attribute, ref int value, int size);
    static int ColorRef(Color c) { return c.R|(c.G<<8)|(c.B<<16); }
    internal static void UseDarkTitleBar(IntPtr h, Color caption, Color text) {
        try {
            int on=1, back=ColorRef(caption), fore=ColorRef(text);
            DwmSetWindowAttribute(h,20,ref on,4);   // DWMWA_USE_IMMERSIVE_DARK_MODE
            DwmSetWindowAttribute(h,35,ref back,4); // DWMWA_CAPTION_COLOR (Windows 11)
            DwmSetWindowAttribute(h,36,ref fore,4); // DWMWA_TEXT_COLOR (Windows 11)
        } catch(EntryPointNotFoundException) { }
    }
    internal const int DWMWA_EXTENDED_FRAME_BOUNDS=9;
    internal static Rect ExtendedFrameBounds(IntPtr h) {
        Rect rect;
        try {
            if(DwmGetWindowAttribute(h,DWMWA_EXTENDED_FRAME_BOUNDS,out rect,Marshal.SizeOf(typeof(Rect)))==0)
                return rect;
        } catch {}
        if(GetWindowRect(h,out rect)) return rect;
        return new Rect();
    }
    // DWM thumbnail coordinates start at the window rectangle, which includes the invisible
    // resize border, while the thumbnail's size equals the extended frame size. Selector crops
    // (RegionSpec X/Y/W/H) live in that space, so their screen origin is GetWindowRect's corner.
    internal static Rect ThumbnailBounds(IntPtr h) {
        Rect ext=ExtendedFrameBounds(h), win;
        if(ext.Right<=ext.Left || ext.Bottom<=ext.Top || !GetWindowRect(h,out win)) return ext;
        return new Rect { Left=win.Left, Top=win.Top, Right=win.Left+(ext.Right-ext.Left), Bottom=win.Top+(ext.Bottom-ext.Top) };
    }
    [DllImport("dwmapi.dll")] internal static extern int DwmGetWindowAttribute(IntPtr window,int attribute,out Rect rect,int size);
    [DllImport("dwmapi.dll")] internal static extern int DwmRegisterThumbnail(IntPtr dest, IntPtr src, out IntPtr thumb);
    [DllImport("dwmapi.dll")] internal static extern int DwmUnregisterThumbnail(IntPtr thumb);
    [DllImport("dwmapi.dll")] internal static extern int DwmQueryThumbnailSourceSize(IntPtr thumb, out SizeI size);
    [DllImport("dwmapi.dll")] internal static extern int DwmUpdateThumbnailProperties(IntPtr thumb, ref Props p);
    internal static List<WindowItem> Windows() {
        var list=new List<WindowItem>();
        EnumWindows(delegate(IntPtr h, IntPtr p) {
            uint pid; GetWindowThreadProcessId(h,out pid);
            if (!IsWindowVisible(h) || pid==(uint)Process.GetCurrentProcess().Id) return true;
            var title=new StringBuilder(512); GetWindowText(h,title,title.Capacity);
            if (title.Length>0) list.Add(new WindowItem { Handle=h, Title=title.ToString() });
            return true;
        },IntPtr.Zero);
        list.Sort(delegate(WindowItem a,WindowItem b) { return string.Compare(a.Title,b.Title,StringComparison.CurrentCultureIgnoreCase); });
        return list;
    }
}
