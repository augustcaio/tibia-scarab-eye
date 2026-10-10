using System;
using System.Drawing;
using System.Windows.Forms;

namespace TibiaScarabEye.UI;

// Janela sem a moldura do Windows. O corpo é um retângulo com a borda dourada; o cabeçalho tem o título e o texto pequeno à
// esquerda e os botões de minimizar e fechar à direita. Mover, redimensionar, minimizar e fechar são desenhados aqui, pois
// não há barra de título nativa.
internal abstract class FramelessForm : Form {
    // Valores lógicos (96 dpi); a janela converte para pixels do dispositivo.
    // Edge é a espessura da borda dourada e também a faixa que redimensiona; Margin é o respiro entre a borda e o conteúdo.
    const int Edge=8, Margin=24, HeaderTop=Edge+14, ContentTop=HeaderTop+62, CaptionButtonWidth=30, CaptionButtonHeight=22;
    const int HitClient=1, HitCaption=2, HitLeft=10, HitRight=11, HitBottom=15, HitBottomLeft=16, HitBottomRight=17;
    const int WmNcHitTest=0x84, WmNcLButtonDblClk=0xA3, WmSysCommand=0x112, ScMaximize=0xF030, WsMinimizeBox=0x20000;
    readonly CaptionButton minimize=new CaptionButton(CaptionGlyph.Minimize), close=new CaptionButton(CaptionGlyph.Close);

    protected string BandTitle="", BandCaption="";

    protected FramelessForm() {
        FormBorderStyle=FormBorderStyle.None; DoubleBuffered=true; ResizeRedraw=true;
        minimize.Click+=delegate { WindowState=FormWindowState.Minimized; };
        close.Click+=delegate { Close(); };
        Controls.Add(minimize); Controls.Add(close);
    }

    int Px(int logical) { return LogicalToDeviceUnits(logical); }

    protected override CreateParams CreateParams {
        get { var p=base.CreateParams; p.Style|=WsMinimizeBox; return p; } // sem isso o clique na barra de tarefas não minimiza uma janela sem borda
    }

    // O conteúdo começa depois do cabeçalho; o respiro lateral e o de baixo (Margin) vêm do Padding da janela principal.
    protected override void OnLayout(LayoutEventArgs e) {
        var padding=new Padding(Px(Edge)+Px(Margin),Px(ContentTop),Px(Edge)+Px(Margin),Px(Edge)+Px(Margin-4));
        if(Padding!=padding) Padding=padding;
        base.OnLayout(e);
    }
    protected override void OnSizeChanged(EventArgs e) {
        base.OnSizeChanged(e);
        if(ClientSize.Width<=0 || ClientSize.Height<=0) return;
        int y=Px(HeaderTop), gap=Px(4);
        close.SetBounds(ClientSize.Width-Px(Edge)-Px(Margin)-Px(CaptionButtonWidth),y,Px(CaptionButtonWidth),Px(CaptionButtonHeight));
        minimize.SetBounds(close.Left-gap-Px(CaptionButtonWidth),y,Px(CaptionButtonWidth),Px(CaptionButtonHeight));
    }

    protected override void OnPaint(PaintEventArgs e) {
        base.OnPaint(e);
        Theme.GoldFrame(e.Graphics,ClientRectangle,Px(2));
        int left=Px(Edge)+Px(Margin), top=Px(HeaderTop), width=minimize.Left-left-Px(12);
        if(width<=0) return;
        using(var title=new Font("Tahoma",12,FontStyle.Bold)) using(var caption=new Font("Tahoma",8)) {
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
