using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Reflection;
using System.Windows.Forms;

namespace TibiaScarabEye.UI;

// Janela sem a moldura do Windows. O corpo é um painel retangular e o logo do Tibia sobe acima da borda superior:
// o Region da janela é o painel unido à silhueta do logo, então o vazio ao redor do logo fica transparente.
// Mover, redimensionar, minimizar e fechar são desenhados aqui, pois não há barra de título nativa.
internal abstract class FramelessForm : Form {
    // Valores lógicos (96 dpi); a janela converte para pixels do dispositivo.
    const int LogoHeight=128, PanelTop=116, ContentTop=PanelTop+48, Edge=8, CaptionButtonWidth=30, CaptionButtonHeight=22;
    const int HitClient=1, HitCaption=2, HitLeft=10, HitRight=11, HitBottom=15, HitBottomLeft=16, HitBottomRight=17;
    const int WmNcHitTest=0x84, WmNcLButtonDblClk=0xA3, WmSysCommand=0x112, ScMaximize=0xF030, WsMinimizeBox=0x20000;
    const int OpaqueAlpha=128;
    static readonly Bitmap Logo=LoadLogo();
    readonly CaptionButton minimize=new CaptionButton(CaptionGlyph.Minimize), close=new CaptionButton(CaptionGlyph.Close);
    Bitmap scaledLogo;
    Region logoMask;

    protected string BandTitle="", BandCaption="";

    protected FramelessForm() {
        FormBorderStyle=FormBorderStyle.None; DoubleBuffered=true; ResizeRedraw=true;
        minimize.Click+=delegate { WindowState=FormWindowState.Minimized; };
        close.Click+=delegate { Close(); };
        Controls.Add(minimize); Controls.Add(close);
    }

    static Bitmap LoadLogo() {
        using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("logo.png")) return new Bitmap(stream);
    }
    int Px(int logical) { return LogicalToDeviceUnits(logical); }
    Rectangle PanelBounds { get { int top=Px(PanelTop); return new Rectangle(0,top,ClientSize.Width,ClientSize.Height-top); } }
    Rectangle LogoBounds {
        get {
            int height=Px(LogoHeight), width=(int)Math.Round(height*(double)Logo.Width/Logo.Height);
            return new Rectangle((ClientSize.Width-width)/2,0,width,height);
        }
    }

    protected override CreateParams CreateParams {
        get { var p=base.CreateParams; p.Style|=WsMinimizeBox; return p; } // sem isso o clique na barra de tarefas não minimiza uma janela sem borda
    }

    protected override void OnLayout(LayoutEventArgs e) {
        var padding=new Padding(Px(Edge),Px(ContentTop),Px(Edge),Px(Edge));
        if(Padding!=padding) Padding=padding;
        base.OnLayout(e);
    }
    protected override void OnSizeChanged(EventArgs e) {
        base.OnSizeChanged(e);
        if(ClientSize.Width<=0 || ClientSize.Height<=0) return;
        int y=Px(PanelTop)+Px(8), gap=Px(4);
        close.SetBounds(ClientSize.Width-Px(Edge)-Px(8)-Px(CaptionButtonWidth),y,Px(CaptionButtonWidth),Px(CaptionButtonHeight));
        minimize.SetBounds(close.Left-gap-Px(CaptionButtonWidth),y,Px(CaptionButtonWidth),Px(CaptionButtonHeight));
        UpdateShape();
    }

    // Região da janela = painel + silhueta do logo (pixels com alpha suficiente). A máscara é refeita só quando o tamanho do logo muda.
    void UpdateShape() {
        var logo=LogoBounds;
        if(scaledLogo==null || scaledLogo.Size!=logo.Size) BuildLogo(logo.Size);
        var shape=new Region(PanelBounds);
        using(var mask=logoMask.Clone()) { mask.Translate(logo.X,0); shape.Union(mask); }
        var previous=Region; Region=shape; if(previous!=null) previous.Dispose();
    }
    void BuildLogo(Size size) {
        if(scaledLogo!=null) scaledLogo.Dispose();
        if(logoMask!=null) logoMask.Dispose();
        scaledLogo=new Bitmap(size.Width,size.Height,System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using(var g=Graphics.FromImage(scaledLogo)) { g.InterpolationMode=InterpolationMode.HighQualityBicubic; g.PixelOffsetMode=PixelOffsetMode.HighQuality; g.DrawImage(Logo,0,0,size.Width,size.Height); }
        using(var path=new GraphicsPath()) {
            for(int y=0;y<size.Height;y++) {
                int start=-1;
                for(int x=0;x<=size.Width;x++) {
                    bool solid=x<size.Width && scaledLogo.GetPixel(x,y).A>=OpaqueAlpha;
                    if(solid && start<0) start=x;
                    else if(!solid && start>=0) { path.AddRectangle(new Rectangle(start,y,x-start,1)); start=-1; }
                }
            }
            logoMask=new Region(path);
        }
    }

    protected override void OnPaint(PaintEventArgs e) {
        base.OnPaint(e);
        Theme.GoldFrame(e.Graphics,PanelBounds,Px(2));
        if(scaledLogo!=null) e.Graphics.DrawImageUnscaled(scaledLogo,LogoBounds.Location);
        var logo=LogoBounds; int left=Px(Edge)+Px(10), top=Px(PanelTop)+Px(8), width=logo.Left-left-Px(8);
        if(width<=0) return;
        using(var title=new Font("Tahoma",11,FontStyle.Bold)) using(var caption=new Font("Tahoma",8)) {
            TextRenderer.DrawText(e.Graphics,BandTitle,title,new Rectangle(left,top,width,title.Height+Px(2)),Theme.Gold,TextFormatFlags.Left|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPadding);
            TextRenderer.DrawText(e.Graphics,BandCaption,caption,new Rectangle(left,top+title.Height+Px(2),width,caption.Height+Px(2)),Theme.Muted,TextFormatFlags.Left|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPadding);
        }
    }

    protected override void WndProc(ref Message m) {
        if(m.Msg==WmNcHitTest) {
            long lParam=m.LParam.ToInt64();
            m.Result=(IntPtr)HitTest(PointToClient(new Point((short)(lParam&0xFFFF),(short)((lParam>>16)&0xFFFF))));
            return;
        }
        // Sem barra de título nativa não há o que maximizar: ignora o duplo clique no topo e o comando de sistema.
        if(m.Msg==WmNcLButtonDblClk || (m.Msg==WmSysCommand && (m.WParam.ToInt64()&0xFFF0)==ScMaximize)) return;
        base.WndProc(ref m);
    }
    // Bordas do painel redimensionam; o topo (faixa do logo) arrasta a janela.
    int HitTest(Point p) {
        int edge=Px(Edge);
        if(p.Y>=Px(PanelTop)) {
            bool left=p.X<edge, right=p.X>=ClientSize.Width-edge, bottom=p.Y>=ClientSize.Height-edge;
            if(bottom) return left?HitBottomLeft:right?HitBottomRight:HitBottom;
            if(left) return HitLeft;
            if(right) return HitRight;
        }
        return p.Y<Padding.Top?HitCaption:HitClient;
    }

    protected override void Dispose(bool disposing) {
        if(disposing) { if(scaledLogo!=null) scaledLogo.Dispose(); if(logoMask!=null) logoMask.Dispose(); }
        base.Dispose(disposing);
    }
}
