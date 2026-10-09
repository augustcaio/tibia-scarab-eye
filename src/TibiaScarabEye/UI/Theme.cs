using System.Drawing;
using System.Windows.Forms;

namespace TibiaScarabEye.UI;

internal static class Theme {
    public static readonly Color Background=Color.FromArgb(53,53,51), Surface=Color.FromArgb(69,69,66), Ink=Color.FromArgb(230,230,220), Muted=Color.FromArgb(192,192,181), Gold=Color.FromArgb(215,207,173);
    static readonly Bitmap Stone=MakeStone();
    static Bitmap MakeStone() {
        var bitmap=new Bitmap(96,96); var random=new System.Random(71);
        for(int y=0;y<96;y++) for(int x=0;x<96;x++) { int shade=53+random.Next(-5,6)+((x+y)%13==0?3:0); bitmap.SetPixel(x,y,Color.FromArgb(shade,shade,shade-2)); }
        return bitmap;
    }
    public static void Apply(Form f) {
        f.Font=new Font("Tahoma",9); f.BackColor=Background; f.BackgroundImage=Stone; f.BackgroundImageLayout=ImageLayout.Tile; f.ForeColor=Ink; f.AutoScaleMode=AutoScaleMode.Dpi;
        f.ControlAdded+=delegate(object sender,ControlEventArgs e) { Style(e.Control); };
    }
    // ControlAdded só dispara para filhos diretos do form; controles dentro de painéis chamam Style explicitamente.
    public static void Style(Control control) {
        if(control is ComboBox || control is NumericUpDown || control is ListBox) {
            control.BackColor=Color.FromArgb(39,39,37); control.ForeColor=Ink;
            var combo=control as ComboBox;
            if(combo!=null) {
                combo.FlatStyle=FlatStyle.Flat; combo.DrawMode=DrawMode.OwnerDrawFixed;
                combo.DrawItem+=delegate(object drawSender,DrawItemEventArgs item) {
                    using(var brush=new SolidBrush((item.State&DrawItemState.Selected)!=0?Surface:Color.FromArgb(39,39,37))) item.Graphics.FillRectangle(brush,item.Bounds);
                    string text=item.Index>=0?combo.GetItemText(combo.Items[item.Index]):combo.Text;
                    TextRenderer.DrawText(item.Graphics,text,combo.Font,item.Bounds,Ink,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);
                    item.DrawFocusRectangle();
                };
            }
        }
        if(control is CheckBox) control.BackColor=Color.Transparent;
    }
    public static void Frame(Graphics g,Rectangle r) {
        if(r.Width<6 || r.Height<6) return;
        using(var light=new Pen(Color.FromArgb(133,133,126))) using(var dark=new Pen(Color.FromArgb(22,22,21))) using(var middle=new Pen(Color.FromArgb(79,79,74))) {
            g.DrawLine(light,r.Left,r.Top,r.Right-1,r.Top); g.DrawLine(light,r.Left,r.Top,r.Left,r.Bottom-1);
            g.DrawLine(dark,r.Left,r.Bottom-1,r.Right-1,r.Bottom-1); g.DrawLine(dark,r.Right-1,r.Top,r.Right-1,r.Bottom-1);
            r.Inflate(-1,-1); g.DrawRectangle(middle,r.Left,r.Top,r.Width-1,r.Height-1);
            r.Inflate(-1,-1); g.DrawRectangle(dark,r.Left,r.Top,r.Width-1,r.Height-1);
        }
    }
    public static Button Button(string text,int x,int y,int width,bool primary) {
        return new StoneButton { Text=text, Location=new Point(x,y), Size=new Size(width,38), FlatStyle=FlatStyle.Flat, BackColor=Surface, ForeColor=primary?Gold:Ink, Cursor=Cursors.Hand, UseVisualStyleBackColor=false };
    }
    // Variantes para layouts em TableLayoutPanel: o painel define posição e tamanho.
    public static Button Button(string text,bool primary) {
        return new StoneButton { Text=text, Size=new Size(120,30), MinimumSize=new Size(0,30), Margin=Padding.Empty, FlatStyle=FlatStyle.Flat, BackColor=Surface, ForeColor=primary?Gold:Ink, Cursor=Cursors.Hand, UseVisualStyleBackColor=false };
    }
    public static Label Caption(string text) {
        return new Label { Text=text, AutoSize=true, Anchor=AnchorStyles.Left, Margin=new Padding(0,0,8,0), BackColor=Color.Transparent, ForeColor=Ink };
    }
    public static Label Note(string text) {
        return new WrapLabel { Text=text, Dock=DockStyle.Fill, Margin=Padding.Empty, BackColor=Color.Transparent, ForeColor=Muted };
    }
    public static Label Label(string text,int x,int y,int w,int h,bool muted) {
        return new Label { Text=text, Location=new Point(x,y), Size=new Size(w,h), BackColor=Color.Transparent, ForeColor=muted?Muted:Ink };
    }
}
