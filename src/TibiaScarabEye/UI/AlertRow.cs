using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Forms;
using TibiaScarabEye.Alerts;

namespace TibiaScarabEye.UI;

// Uma linha de alerta: o sprite do item num encaixe, o nome com o estado da contagem e, à direita, o interruptor ON/OFF.
// Mesma placa escura da lâmpada de status. Com o alerta desligado o sprite aparece esmaecido.
internal sealed class AlertRow : Control {
    const int RowHeight=46, SlotSize=38, SpriteSize=32;
    readonly AlertSwitch toggle=new AlertSwitch();
    readonly TestButton[] testButtons;
    readonly Bitmap sprite;
    string title="", detail="";
    public event EventHandler CheckedChanged;
    // O jogador pediu para ouvir um dos alertas deste item.
    public event Action<AlertKind> TestRequested;

    public AlertRow(string title,Bitmap sprite) {
        SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.SupportsTransparentBackColor,true);
        this.title=title; this.sprite=sprite;
        Dock=DockStyle.Fill; Margin=Padding.Empty; BackColor=Color.Transparent; TabStop=false;
        Height=LogicalToDeviceUnits(RowHeight); MinimumSize=new Size(0,Height);
        toggle.AccessibleName="Alerta de "+title;
        toggle.CheckedChanged+=delegate { Invalidate(); if(CheckedChanged!=null) CheckedChanged(this,EventArgs.Empty); };
        Controls.Add(toggle);
        // Um ícone por alerta, ao lado do interruptor: equipado, aviso (1:30) e retirado.
        testButtons=new[]{ NewTest(AlertKind.Equipped,"Ouvir o alerta de equipado"), NewTest(AlertKind.Warning,"Ouvir o aviso de 1:30 restantes"), NewTest(AlertKind.Removed,"Ouvir o alerta de retirado") };
    }
    TestButton NewTest(AlertKind kind,string tip) {
        var button=new TestButton(kind,tip+" ("+title+")");
        button.Click+=delegate { if(TestRequested!=null) TestRequested(kind); };
        Controls.Add(button);
        return button;
    }
    public AlertSwitch Switch { get { return toggle; } }
    public IReadOnlyList<TestButton> TestButtons { get { return testButtons; } }
    public bool Checked { get { return toggle.Checked; } set { toggle.Checked=value; } }
    public string Title { get { return title; } }
    public string Detail {
        get { return detail; }
        set { if(detail==value) return; detail=value; Invalidate(); }
    }
    public override Size GetPreferredSize(Size proposedSize) { return new Size(LogicalToDeviceUnits(260),LogicalToDeviceUnits(RowHeight)); }

    protected override void OnLayout(LayoutEventArgs e) {
        base.OnLayout(e);
        if(testButtons==null) return; // o tamanho inicial é definido no construtor, antes de os botões existirem
        int margin=LogicalToDeviceUnits(10), width=toggle.Width, height=toggle.Height;
        toggle.SetBounds(Width-margin-width,(Height-height)/2,width,height);
        // Os ícones de teste ficam à esquerda do interruptor, na ordem equipado, aviso, retirado.
        int gap=LogicalToDeviceUnits(4), x=toggle.Left-LogicalToDeviceUnits(12);
        for(int i=testButtons.Length-1;i>=0;i--) {
            var button=testButtons[i];
            x-=button.Width;
            button.SetBounds(x,(Height-button.Height)/2,button.Width,button.Height);
            x-=gap;
        }
    }
    // Onde o texto termina: antes do primeiro ícone de teste.
    int TextRight { get { return testButtons[0].Left-LogicalToDeviceUnits(10); } }
    protected override void OnPaint(PaintEventArgs e) {
        var g=e.Graphics;
        var plaque=new Rectangle(0,0,Width-1,Height-1);
        using(var back=new SolidBrush(Color.FromArgb(39,39,37))) g.FillRectangle(back,plaque);
        using(var border=new Pen(Color.FromArgb(79,79,74))) g.DrawRectangle(border,plaque);

        int slot=LogicalToDeviceUnits(SlotSize), pad=(Height-slot)/2, left=pad+LogicalToDeviceUnits(4);
        var slotRect=new Rectangle(left,pad,slot,slot);
        using(var well=new SolidBrush(Color.FromArgb(22,22,21))) g.FillRectangle(well,slotRect);
        using(var shade=new Pen(Color.FromArgb(10,10,9))) using(var glint=new Pen(Color.FromArgb(79,79,74))) {
            g.DrawLine(shade,slotRect.Left,slotRect.Top,slotRect.Right-1,slotRect.Top); g.DrawLine(shade,slotRect.Left,slotRect.Top,slotRect.Left,slotRect.Bottom-1);
            g.DrawLine(glint,slotRect.Left,slotRect.Bottom-1,slotRect.Right-1,slotRect.Bottom-1); g.DrawLine(glint,slotRect.Right-1,slotRect.Top,slotRect.Right-1,slotRect.Bottom-1);
        }
        if(sprite!=null) {
            int size=LogicalToDeviceUnits(SpriteSize);
            var target=new Rectangle(slotRect.Left+(slotRect.Width-size)/2,slotRect.Top+(slotRect.Height-size)/2,size,size);
            g.InterpolationMode=InterpolationMode.NearestNeighbor; g.PixelOffsetMode=PixelOffsetMode.Half;
            using(var attributes=new ImageAttributes()) {
                if(!toggle.Checked) attributes.SetColorMatrix(new ColorMatrix { Matrix33=0.4f });
                g.DrawImage(sprite,target,0,0,sprite.Width,sprite.Height,GraphicsUnit.Pixel,attributes);
            }
        }

        int textLeft=slotRect.Right+LogicalToDeviceUnits(12), textWidth=TextRight-textLeft;
        if(textWidth<=0) return;
        using(var bold=new Font(Font,FontStyle.Bold)) {
            int lineHeight=bold.Height, gap=LogicalToDeviceUnits(2), top=(Height-(lineHeight+gap+Font.Height))/2;
            TextRenderer.DrawText(g,title,bold,new Rectangle(textLeft,top,textWidth,lineHeight),toggle.Checked?Theme.Ink:Theme.Muted,TextFormatFlags.Left|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPadding);
            TextRenderer.DrawText(g,detail,Font,new Rectangle(textLeft,top+lineHeight+gap,textWidth,Font.Height),Theme.Muted,TextFormatFlags.Left|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPadding);
        }
    }
    protected override void Dispose(bool disposing) { if(disposing) sprite?.Dispose(); base.Dispose(disposing); }
}
