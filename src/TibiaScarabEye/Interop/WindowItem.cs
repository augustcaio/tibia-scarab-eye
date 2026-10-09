using System;

namespace TibiaScarabEye.Interop;

internal sealed class WindowItem {
    public IntPtr Handle; public string Title;
    public override string ToString() { return Title; }
}
