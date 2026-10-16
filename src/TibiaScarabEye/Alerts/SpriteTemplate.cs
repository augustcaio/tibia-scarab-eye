using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace TibiaScarabEye.Alerts;

// Um sprite (32x32, com fundo transparente) a procurar dentro das áreas recortadas. Só os pixels opacos contam, comparados com o
// quadro cor a cor; o sprite vale como "à vista" quando a diferença média por canal fica dentro da tolerância. Tamanho 1:1.
internal sealed class SpriteTemplate {
    // Média de diferença por canal (0 a 255). Tolera a paleta reduzida do GIF da wiki sem confundir com os outros itens.
    public const int DefaultTolerance=24;
    // A área recortada pode errar a posição do slot por alguns pixels; a busca olha um pouco além dela.
    const int Margin=2, MinOpaquePixels=64;
    struct Px { public int X, Y; public byte B, G, R; }
    readonly Px[] opaque;
    public readonly int Width, Height;
    public readonly int Tolerance;

    SpriteTemplate(Px[] opaque,int width,int height,int tolerance) { this.opaque=opaque; Width=width; Height=height; Tolerance=tolerance; }
    public int OpaquePixels { get { return opaque.Length; } }

    public static SpriteTemplate FromBitmap(Bitmap bitmap,int tolerance=DefaultTolerance) {
        var frame=PixelBuffer.FromBitmap(bitmap);
        var list=new List<Px>();
        for(int y=0;y<frame.Height;y++) for(int x=0;x<frame.Width;x++) {
            int i=(y*frame.Width+x)*4;
            if(frame.Bgra[i+3]>=128) list.Add(new Px { X=x, Y=y, B=frame.Bgra[i], G=frame.Bgra[i+1], R=frame.Bgra[i+2] });
        }
        if(list.Count<MinOpaquePixels) throw new InvalidDataException("O sprite tem pixels opacos de menos para ser reconhecido.");
        return new SpriteTemplate(list.ToArray(),frame.Width,frame.Height,tolerance);
    }

    // Lê um sprite embutido no executável (Assets\Sprites\*.gif).
    public static SpriteTemplate FromResource(string name,int tolerance=DefaultTolerance) {
        using(var bitmap=LoadBitmap(name)) return FromBitmap(bitmap,tolerance);
    }
    // O GIF precisa do fluxo aberto enquanto o Bitmap existe; copia para um bitmap próprio, já em ARGB.
    public static Bitmap LoadBitmap(string name) {
        using(var stream=typeof(SpriteTemplate).Assembly.GetManifestResourceStream(name)) {
            if(stream==null) throw new FileNotFoundException("Sprite embutido não encontrado: "+name);
            using(var source=new Bitmap(stream)) {
                var copy=new Bitmap(source.Width,source.Height,PixelFormat.Format32bppArgb);
                using(var g=Graphics.FromImage(copy)) g.DrawImageUnscaled(source,0,0);
                return copy;
            }
        }
    }

    // O sprite está em alguma das áreas?
    public bool Find(PixelBuffer frame,IEnumerable<Rectangle> areas) {
        foreach(var area in areas) if(Find(frame,area)) return true;
        return false;
    }
    public bool Find(PixelBuffer frame,Rectangle area) {
        area.Inflate(Margin,Margin);
        area.Intersect(new Rectangle(0,0,frame.Width,frame.Height));
        int lastX=area.Right-Width, lastY=area.Bottom-Height, limit=Tolerance*3*opaque.Length;
        for(int y=area.Top;y<=lastY;y++) for(int x=area.Left;x<=lastX;x++) if(Distance(frame,x,y,limit)<=limit) return true;
        return false;
    }
    // Soma das diferenças absolutas nos três canais; para assim que passa do limite (a maioria das posições sai nos primeiros pixels).
    int Distance(PixelBuffer frame,int left,int top,int limit) {
        int sum=0; byte[] data=frame.Bgra;
        for(int k=0;k<opaque.Length;k++) {
            Px p=opaque[k];
            int at=((top+p.Y)*frame.Width+left+p.X)*4;
            sum+=Math.Abs(data[at]-p.B)+Math.Abs(data[at+1]-p.G)+Math.Abs(data[at+2]-p.R);
            if(sum>limit) return sum;
        }
        return sum;
    }
}
