using System;
using System.Drawing;
using System.Drawing.Imaging;

namespace TibiaScarabEye.Alerts;

// Quadro de pixels BGRA lidos da janela do jogo (4 bytes por pixel, sem folga entre linhas).
internal sealed class PixelBuffer {
    public readonly int Width, Height;
    public readonly byte[] Bgra;
    public PixelBuffer(int width,int height,byte[] bgra) {
        if(width<1 || height<1 || bgra==null || bgra.Length<width*height*4) throw new ArgumentException("Quadro de pixels inválido.");
        Width=width; Height=height; Bgra=bgra;
    }
    public static PixelBuffer FromBitmap(Bitmap bitmap) {
        var data=bitmap.LockBits(new Rectangle(0,0,bitmap.Width,bitmap.Height),ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);
        try {
            var bytes=new byte[bitmap.Width*bitmap.Height*4];
            for(int y=0;y<bitmap.Height;y++) System.Runtime.InteropServices.Marshal.Copy(data.Scan0+y*data.Stride,bytes,y*bitmap.Width*4,bitmap.Width*4);
            return new PixelBuffer(bitmap.Width,bitmap.Height,bytes);
        } finally { bitmap.UnlockBits(data); }
    }
    // Janelas que o sistema não consegue copiar (proteção ou superfície de jogo) saem pretas: sem uma amostra de cor, não há leitura.
    public bool IsBlank() {
        for(int i=0;i<Bgra.Length-3;i+=4*61) if(Bgra[i]!=0 || Bgra[i+1]!=0 || Bgra[i+2]!=0) return false;
        return true;
    }
}
