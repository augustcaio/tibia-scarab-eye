using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Reflection;
using System.Windows.Forms;

namespace TibiaScarabEye.UI;

// Janela sem a moldura do Windows. O corpo é um painel retangular cuja borda de cima é a faixa de espinhos do fan kit do
// Tibia; o emblema do escaravelho sobe acima dessa borda, no canto esquerdo, e o Region da janela é o painel unido à
// silhueta do emblema, então o vazio ao redor dele fica transparente. À direita ficam os botões da janela e, logo
// abaixo, o logo do Tibia. Mover, redimensionar, minimizar e fechar são desenhados aqui, pois não há barra de título nativa.
internal abstract class FramelessForm : Form {
    // Valores lógicos (96 dpi); a janela converte para pixels do dispositivo.
    const int EmblemHeight=112, EmblemLeft=14, PanelTop=44, StripHeight=22, ContentTop=PanelTop+104, Edge=8;
    const int CaptionButtonWidth=34, CaptionButtonHeight=26, TibiaLogoHeight=44, RightMargin=16;
    const int HitClient=1, HitCaption=2, HitLeft=10, HitRight=11, HitBottom=15, HitBottomLeft=16, HitBottomRight=17;
    const int WmNcHitTest=0x84, WmNcLButtonDblClk=0xA3, WmSysCommand=0x112, ScMaximize=0xF030, WsMinimizeBox=0x20000;
    const int OpaqueAlpha=150;
    static readonly Bitmap Emblem=Asset("emblem.png"), TibiaLogo=Asset("logo.png"), Thorns=Asset("thorns.png");
    static readonly Font TitleFont=new Font("Georgia",21f,FontStyle.Bold), SmallFont=new Font("Segoe UI",8.25f);
    readonly CaptionButton minimize=new CaptionButton(CaptionGlyph.Minimize), close=new CaptionButton(CaptionGlyph.Close);
    Bitmap scaledEmblem;
    Region emblemMask;

    protected string BandTitle="", BandSubtitle="", BandCaption="";

    protected FramelessForm() {
        FormBorderStyle=FormBorderStyle.None; DoubleBuffered=true; ResizeRedraw=true;
        minimize.Click+=delegate { WindowState=FormWindowState.Minimized; };
        close.Click+=delegate { Close(); };
        Controls.Add(minimize); Controls.Add(close);
    }

    static Bitmap Asset(string name) {
        using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream(name)) return new Bitmap(stream);
    }
    int Px(int logical) { return LogicalToDeviceUnits(logical); }
    Rectangle PanelBounds { get { int top=Px(PanelTop); return new Rectangle(0,top,ClientSize.Width,ClientSize.Height-top); } }
    Rectangle EmblemBounds {
        get {
            int height=Px(EmblemHeight), width=(int)Math.Round(height*(double)Emblem.Width/Emblem.Height);
            return new Rectangle(Px(EmblemLeft),0,width,height);
        }
    }
    int ButtonsTop { get { return Px(PanelTop)+Px(StripHeight)+Px(4); } }
    Rectangle TibiaLogoBounds {
        get {
            int height=Px(TibiaLogoHeight), width=(int)Math.Round(height*(double)TibiaLogo.Width/TibiaLogo.Height);
            return new Rectangle(ClientSize.Width-Px(Edge)-Px(RightMargin)-width,ButtonsTop+Px(CaptionButtonHeight)+Px(8),width,height);
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
        int y=ButtonsTop, gap=Px(2);
        close.SetBounds(ClientSize.Width-Px(Edge)-Px(RightMargin)-Px(CaptionButtonWidth)+Px(4),y,Px(CaptionButtonWidth),Px(CaptionButtonHeight));
        minimize.SetBounds(close.Left-gap-Px(CaptionButtonWidth),y,Px(CaptionButtonWidth),Px(CaptionButtonHeight));
        UpdateShape();
    }

    // Região da janela = painel + silhueta do emblema (pixels com alpha suficiente). A máscara é refeita só quando o tamanho muda.
    void UpdateShape() {
        var emblem=EmblemBounds;
        if(scaledEmblem==null || scaledEmblem.Size!=emblem.Size) BuildEmblem(emblem.Size);
        var shape=new Region(PanelBounds);
        using(var mask=emblemMask.Clone()) { mask.Translate(emblem.X,0); shape.Union(mask); }
        var previous=Region; Region=shape; if(previous!=null) previous.Dispose();
    }
    void BuildEmblem(Size size) {
        if(scaledEmblem!=null) scaledEmblem.Dispose();
        if(emblemMask!=null) emblemMask.Dispose();
        scaledEmblem=new Bitmap(size.Width,size.Height,System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using(var g=Graphics.FromImage(scaledEmblem)) { g.InterpolationMode=InterpolationMode.HighQualityBicubic; g.PixelOffsetMode=PixelOffsetMode.HighQuality; g.DrawImage(Emblem,0,0,size.Width,size.Height); }
        using(var path=new GraphicsPath()) {
            for(int y=0;y<size.Height;y++) {
                int start=-1;
                for(int x=0;x<=size.Width;x++) {
                    bool solid=x<size.Width && scaledEmblem.GetPixel(x,y).A>=OpaqueAlpha;
                    if(solid && start<0) start=x;
                    else if(!solid && start>=0) { path.AddRectangle(new Rectangle(start,y,x-start,1)); start=-1; }
                }
            }
            emblemMask=new Region(path);
        }
    }

    protected override void OnPaint(PaintEventArgs e) {
        base.OnPaint(e);
        var g=e.Graphics; var panel=PanelBounds;
        using(var wash=new LinearGradientBrush(panel,Color.FromArgb(22,32,35),Theme.Background,90f)) g.FillRectangle(wash,panel);
        DrawThorns(g,panel);
        // laterais e base: um fio escuro por fora e um fio de ouro queimado por dentro
        using(var outer=new Pen(Color.FromArgb(4,7,8))) using(var inner=new Pen(Color.FromArgb(92,62,30))) {
            g.DrawLine(outer,0,panel.Top,0,panel.Bottom-1); g.DrawLine(outer,panel.Right-1,panel.Top,panel.Right-1,panel.Bottom-1); g.DrawLine(outer,0,panel.Bottom-1,panel.Right-1,panel.Bottom-1);
            g.DrawLine(inner,1,panel.Top,1,panel.Bottom-2); g.DrawLine(inner,panel.Right-2,panel.Top,panel.Right-2,panel.Bottom-2); g.DrawLine(inner,1,panel.Bottom-2,panel.Right-2,panel.Bottom-2);
        }
        // divisor entre a identidade (acima) e os controles do programa (abaixo)
        int dividerY=Px(ContentTop)-Px(8), dividerLeft=Px(Edge)+Px(RightMargin);
        using(var line=new LinearGradientBrush(new Rectangle(dividerLeft,dividerY,Math.Max(1,Width-dividerLeft*2),2),Color.FromArgb(0,Theme.GoldMid),Color.FromArgb(0,Theme.GoldMid),0f)) {
            var blend=new ColorBlend(3) { Colors=new[]{Color.FromArgb(0,Theme.GoldMid),Color.FromArgb(150,Theme.GoldMid),Color.FromArgb(0,Theme.GoldMid)}, Positions=new[]{0f,0.5f,1f} };
            line.InterpolationColors=blend; g.FillRectangle(line,dividerLeft,dividerY,Math.Max(1,Width-dividerLeft*2),1);
        }
        var emblem=EmblemBounds;
        if(scaledEmblem!=null) g.DrawImageUnscaled(scaledEmblem,emblem.Location);
        var logo=TibiaLogoBounds;
        g.InterpolationMode=InterpolationMode.HighQualityBicubic; g.PixelOffsetMode=PixelOffsetMode.HighQuality;
        g.DrawImage(TibiaLogo,logo);
        int left=emblem.Right+Px(14), top=Px(PanelTop)+Px(StripHeight)+Px(6), width=logo.Left-left-Px(12);
        if(width<=0) return;
        TextRenderer.DrawText(g,BandTitle,TitleFont,new Rectangle(left,top,width,TitleFont.Height+Px(2)),Theme.Gold,TextFormatFlags.Left|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPadding);
        TextRenderer.DrawText(g,BandSubtitle,Theme.Body,new Rectangle(left,top+TitleFont.Height+Px(2),width,Theme.Body.Height+Px(2)),Theme.Ink,TextFormatFlags.Left|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPadding);
        TextRenderer.DrawText(g,BandCaption,SmallFont,new Rectangle(left,top+TitleFont.Height+Theme.Body.Height+Px(6),width,SmallFont.Height+Px(2)),Theme.Muted,TextFormatFlags.Left|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPadding);
    }
    // A faixa de espinhos do fan kit forma a borda de cima; cópias espelhadas lado a lado emendam sem costura.
    void DrawThorns(Graphics g,Rectangle panel) {
        int scale=Math.Max(1,(int)Math.Round(DeviceDpi/96.0)), tileWidth=Thorns.Width*scale, tileHeight=Thorns.Height*scale;
        var previous=g.InterpolationMode; var previousOffset=g.PixelOffsetMode;
        g.InterpolationMode=InterpolationMode.NearestNeighbor; g.PixelOffsetMode=PixelOffsetMode.Half;
        for(int i=0;i*tileWidth<panel.Width;i++) {
            int x=i*tileWidth, y=panel.Top;
            if(i%2==0) g.DrawImage(Thorns,new Rectangle(x,y,tileWidth,tileHeight),0,0,Thorns.Width,Thorns.Height,GraphicsUnit.Pixel);
            else g.DrawImage(Thorns,new[]{ new Point(x+tileWidth,y),new Point(x,y),new Point(x+tileWidth,y+tileHeight) },new Rectangle(0,0,Thorns.Width,Thorns.Height),GraphicsUnit.Pixel);
        }
        g.InterpolationMode=previous; g.PixelOffsetMode=previousOffset;
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
    // Bordas do painel redimensionam; o topo (faixa do emblema) arrasta a janela.
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
        if(disposing) { if(scaledEmblem!=null) scaledEmblem.Dispose(); if(emblemMask!=null) emblemMask.Dispose(); }
        base.Dispose(disposing);
    }
}
