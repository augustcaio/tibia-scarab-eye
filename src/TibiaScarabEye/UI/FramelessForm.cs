using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Reflection;
using System.Windows.Forms;

namespace TibiaScarabEye.UI;

// Janela sem a moldura do Windows. A borda de cima é a faixa de espinhos do fan kit do Tibia. Abaixo dela, o cabeçalho tem o
// emblema do escaravelho à esquerda e, à direita, os botões da janela com o logo do Tibia logo abaixo; um divisor separa o
// cabeçalho dos controles do programa. Mover, redimensionar, minimizar e fechar são desenhados aqui, pois não há barra de
// título nativa.
internal abstract class FramelessForm : Form {
    // Valores lógicos (96 dpi); a janela converte para pixels do dispositivo.
    const int StripHeight=22, HeaderTop=StripHeight+10, EmblemHeight=76, ContentTop=HeaderTop+EmblemHeight+24, Edge=8;
    const int CaptionButtonWidth=34, CaptionButtonHeight=26, TibiaLogoHeight=40, SideMargin=16;
    const int HitClient=1, HitCaption=2, HitLeft=10, HitRight=11, HitBottom=15, HitBottomLeft=16, HitBottomRight=17;
    const int WmNcHitTest=0x84, WmNcLButtonDblClk=0xA3, WmSysCommand=0x112, ScMaximize=0xF030, WsMinimizeBox=0x20000;
    static readonly Bitmap Emblem=Asset("emblem.png"), TibiaLogo=Asset("logo.png"), Thorns=Asset("thorns.png");
    static readonly Font SmallFont=new Font("Segoe UI",8.25f);
    readonly CaptionButton minimize=new CaptionButton(CaptionGlyph.Minimize), close=new CaptionButton(CaptionGlyph.Close);

    // Texto pequeno ao lado do emblema; o Tibia Scarab Eye usa para a data do executável, que denuncia um executável velho.
    protected string BandCaption="";

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
    Rectangle EmblemBounds {
        get {
            int height=Px(EmblemHeight), width=(int)Math.Round(height*(double)Emblem.Width/Emblem.Height);
            return new Rectangle(Px(Edge)+Px(SideMargin),Px(HeaderTop),width,height);
        }
    }
    // Os botões ficam no topo do cabeçalho e o logo do Tibia na base dele, alinhada com a base do emblema, com um vão entre os dois.
    Rectangle TibiaLogoBounds {
        get {
            int height=Px(TibiaLogoHeight), width=(int)Math.Round(height*(double)TibiaLogo.Width/TibiaLogo.Height);
            return new Rectangle(ClientSize.Width-Px(Edge)-Px(SideMargin)-width,Px(HeaderTop)+Px(EmblemHeight)-height,width,height);
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
        int y=Px(HeaderTop), gap=Px(2);
        close.SetBounds(ClientSize.Width-Px(Edge)-Px(SideMargin)-Px(CaptionButtonWidth)+Px(4),y,Px(CaptionButtonWidth),Px(CaptionButtonHeight));
        minimize.SetBounds(close.Left-gap-Px(CaptionButtonWidth),y,Px(CaptionButtonWidth),Px(CaptionButtonHeight));
    }

    protected override void OnPaint(PaintEventArgs e) {
        base.OnPaint(e);
        var g=e.Graphics; var panel=ClientRectangle;
        using(var wash=new LinearGradientBrush(panel,Color.FromArgb(22,32,35),Theme.Background,90f)) g.FillRectangle(wash,panel);
        DrawThorns(g,panel);
        // laterais e base: um fio escuro por fora e um fio de ouro queimado por dentro
        using(var outer=new Pen(Color.FromArgb(4,7,8))) using(var inner=new Pen(Color.FromArgb(92,62,30))) {
            g.DrawLine(outer,0,0,0,panel.Bottom-1); g.DrawLine(outer,panel.Right-1,0,panel.Right-1,panel.Bottom-1); g.DrawLine(outer,0,panel.Bottom-1,panel.Right-1,panel.Bottom-1);
            g.DrawLine(inner,1,0,1,panel.Bottom-2); g.DrawLine(inner,panel.Right-2,0,panel.Right-2,panel.Bottom-2); g.DrawLine(inner,1,panel.Bottom-2,panel.Right-2,panel.Bottom-2);
        }
        // divisor entre a identidade (acima) e os controles do programa (abaixo)
        int dividerY=Px(ContentTop)-Px(14), dividerLeft=Px(Edge)+Px(SideMargin), dividerWidth=Math.Max(1,Width-dividerLeft*2);
        using(var line=new LinearGradientBrush(new Rectangle(dividerLeft,dividerY,dividerWidth,2),Color.Transparent,Color.Transparent,0f)) {
            line.InterpolationColors=new ColorBlend(3) { Colors=new[]{Color.FromArgb(0,Theme.GoldMid),Color.FromArgb(150,Theme.GoldMid),Color.FromArgb(0,Theme.GoldMid)}, Positions=new[]{0f,0.5f,1f} };
            g.FillRectangle(line,dividerLeft,dividerY,dividerWidth,1);
        }
        g.InterpolationMode=InterpolationMode.HighQualityBicubic; g.PixelOffsetMode=PixelOffsetMode.HighQuality;
        var emblem=EmblemBounds;
        g.DrawImage(Emblem,emblem);
        g.DrawImage(TibiaLogo,TibiaLogoBounds);
        int left=emblem.Right+Px(14), width=TibiaLogoBounds.Left-left-Px(12);
        if(width>0) TextRenderer.DrawText(g,BandCaption,SmallFont,new Rectangle(left,emblem.Bottom-SmallFont.Height-Px(2),width,SmallFont.Height+Px(2)),Theme.Muted,TextFormatFlags.Left|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPadding);
    }
    // A faixa de espinhos do fan kit forma a borda de cima; cópias espelhadas lado a lado emendam sem costura.
    void DrawThorns(Graphics g,Rectangle panel) {
        int scale=Math.Max(1,(int)Math.Round(DeviceDpi/96.0)), tileWidth=Thorns.Width*scale, tileHeight=Thorns.Height*scale;
        var previous=g.InterpolationMode; var previousOffset=g.PixelOffsetMode;
        g.InterpolationMode=InterpolationMode.NearestNeighbor; g.PixelOffsetMode=PixelOffsetMode.Half;
        for(int i=0;i*tileWidth<panel.Width;i++) {
            int x=i*tileWidth;
            if(i%2==0) g.DrawImage(Thorns,new Rectangle(x,0,tileWidth,tileHeight),0,0,Thorns.Width,Thorns.Height,GraphicsUnit.Pixel);
            else g.DrawImage(Thorns,new[]{ new Point(x+tileWidth,0),new Point(x,0),new Point(x+tileWidth,tileHeight) },new Rectangle(0,0,Thorns.Width,Thorns.Height),GraphicsUnit.Pixel);
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
    // Bordas laterais e de baixo redimensionam; o cabeçalho arrasta a janela.
    int HitTest(Point p) {
        int edge=Px(Edge);
        bool left=p.X<edge, right=p.X>=ClientSize.Width-edge, bottom=p.Y>=ClientSize.Height-edge;
        if(bottom) return left?HitBottomLeft:right?HitBottomRight:HitBottom;
        if(left) return HitLeft;
        if(right) return HitRight;
        return p.Y<Padding.Top?HitCaption:HitClient;
    }
}
