using System;
using System.Drawing;
using System.Drawing.Imaging;
using TibiaScarabEye.Interop;

namespace TibiaScarabEye.Alerts;

// Copia o conteúdo da janela do jogo para um quadro de pixels (PrintWindow com PW_RENDERFULLCONTENT: pede ao DWM a imagem já
// composta, mesmo com outras janelas por cima). As miniaturas do DWM só mostram, não dão acesso aos pixels. Sem leitura de
// memória nem injeção: é a mesma imagem que o jogador vê. A origem do quadro é o canto de GetWindowRect, a mesma das áreas recortadas.
internal sealed class WindowCapture : IDisposable {
    const uint RenderFullContent=2;
    Bitmap canvas;

    // Null quando não foi possível ler (janela minimizada, sumiu ou o sistema devolveu uma imagem toda preta).
    public PixelBuffer Grab(IntPtr window) { return Grab(window,RenderFullContent); }
    internal PixelBuffer Grab(IntPtr window,uint flags) {
        Native.Rect rect;
        if(!Native.IsWindow(window) || Native.IsIconic(window) || !Native.GetWindowRect(window,out rect)) return null;
        int width=rect.Right-rect.Left, height=rect.Bottom-rect.Top;
        if(width<1 || height<1 || width>16384 || height>16384) return null;
        if(canvas==null || canvas.Width!=width || canvas.Height!=height) { if(canvas!=null) canvas.Dispose(); canvas=new Bitmap(width,height,PixelFormat.Format32bppArgb); }
        bool drawn;
        using(var g=Graphics.FromImage(canvas)) {
            g.Clear(Color.Black);
            IntPtr dc=g.GetHdc();
            try { drawn=Native.PrintWindow(window,dc,flags); } finally { g.ReleaseHdc(dc); }
        }
        if(!drawn) return null;
        var frame=PixelBuffer.FromBitmap(canvas);
        return frame.IsBlank()?null:frame;
    }
    public void Dispose() { if(canvas!=null) { canvas.Dispose(); canvas=null; } }
}
