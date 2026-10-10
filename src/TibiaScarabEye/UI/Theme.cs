using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace TibiaScarabEye.UI;

// Identidade visual: laca verde-azulada escura (o interior dos painéis do fan kit do Tibia), ouro dos arabescos como
// único acento de ação e o vermelho da gema como alerta. Segoe UI nos controles; Georgia nos títulos, que ecoa a
// serifa do jogo.
internal static class Theme {
    public static readonly Color
        Background=Color.FromArgb(14,20,22), Surface=Color.FromArgb(22,31,34), Raised=Color.FromArgb(32,45,49), Line=Color.FromArgb(46,62,67),
        Teal=Color.FromArgb(14,62,63), Ink=Color.FromArgb(233,228,210), Muted=Color.FromArgb(150,164,164),
        Gold=Color.FromArgb(235,192,98), GoldMid=Color.FromArgb(198,122,37), GoldDark=Color.FromArgb(145,63,18), Danger=Color.FromArgb(208,58,44);
    public static readonly Font Body=new Font("Segoe UI",9f);
    public static void Apply(Form f) {
        f.Font=Body; f.BackColor=Background; f.ForeColor=Ink; f.AutoScaleMode=AutoScaleMode.Dpi;
        f.ControlAdded+=delegate(object sender,ControlEventArgs e) { Style(e.Control); };
    }
    // ControlAdded só dispara para filhos diretos do form; controles dentro de painéis chamam Style explicitamente.
    public static void Style(Control control) {
        if(control is NumericUpDown || control is ListBox) { control.BackColor=Surface; control.ForeColor=Ink; }
        if(control is CheckBox) control.BackColor=Color.Transparent;
    }
    public static GraphicsPath Rounded(Rectangle r,int radius) {
        var path=new GraphicsPath(); int d=radius*2;
        if(radius<=0 || r.Width<d || r.Height<d) { path.AddRectangle(r); return path; }
        path.AddArc(r.Left,r.Top,d,d,180,90); path.AddArc(r.Right-d,r.Top,d,d,270,90);
        path.AddArc(r.Right-d,r.Bottom-d,d,d,0,90); path.AddArc(r.Left,r.Bottom-d,d,d,90,90);
        path.CloseFigure(); return path;
    }
    // Moldura fina das overlays sobre o jogo (3 px): contorno escuro, fio de ouro queimado e contorno escuro.
    public static void OverlayFrame(Graphics g,Rectangle r) {
        if(r.Width<6 || r.Height<6) return;
        using(var dark=new Pen(Color.FromArgb(4,7,8))) using(var gold=new Pen(Color.FromArgb(150,98,42))) {
            g.DrawRectangle(dark,r.Left,r.Top,r.Width-1,r.Height-1);
            g.DrawRectangle(gold,r.Left+1,r.Top+1,r.Width-3,r.Height-3);
            g.DrawRectangle(dark,r.Left+2,r.Top+2,r.Width-5,r.Height-5);
        }
    }
    public static Button Button(string text,int x,int y,int width,bool primary) {
        return new ThemedButton { Text=text, Location=new Point(x,y), Size=new Size(width,32), Primary=primary };
    }
    // Variantes para layouts em TableLayoutPanel: o painel define posição e tamanho.
    public static Button Button(string text,bool primary) {
        return new ThemedButton { Text=text, Size=new Size(120,32), MinimumSize=new Size(0,32), Margin=Padding.Empty, Primary=primary };
    }
    public static Label Caption(string text) {
        return new Label { Text=text, AutoSize=true, Anchor=AnchorStyles.Left, Margin=new Padding(0,0,8,0), BackColor=Color.Transparent, ForeColor=Muted };
    }
    public static Label Note(string text) {
        return new WrapLabel { Text=text, Dock=DockStyle.Fill, Margin=Padding.Empty, BackColor=Color.Transparent, ForeColor=Muted };
    }
    public static Label Label(string text,int x,int y,int w,int h,bool muted) {
        return new Label { Text=text, Location=new Point(x,y), Size=new Size(w,h), BackColor=Color.Transparent, ForeColor=muted?Muted:Ink };
    }
}
