using System;
using System.Drawing;
using System.Windows.Forms;

namespace TibiaScarabEye.UI;

// Pergunta curta: um nome e, quando faz sentido, se vale para todos os personagens.
internal sealed class PromptForm : Form {
    readonly TextBox input=new TextBox();
    readonly CheckBox everyone=new CheckBox();
    public string Value { get { return input.Text.Trim(); } }
    public bool ForEveryone { get { return everyone.Checked; } }

    public PromptForm(string title,string label,string initial,bool offerEveryone,bool everyoneChecked) {
        Theme.Apply(this); Text=title;
        FormBorderStyle=FormBorderStyle.FixedDialog; MaximizeBox=false; MinimizeBox=false; ShowInTaskbar=false; StartPosition=FormStartPosition.CenterParent;
        ClientSize=new Size(420,offerEveryone?150:116);
        Controls.Add(Theme.Label(label,20,16,380,22,false));
        input.SetBounds(20,42,380,26); input.MaxLength=80; input.Text=initial; input.BorderStyle=BorderStyle.FixedSingle; Controls.Add(input);
        if(offerEveryone) { everyone.Text="Disponível para todos os personagens"; everyone.Checked=everyoneChecked; everyone.SetBounds(20,76,380,24); Controls.Add(everyone); }
        var ok=Theme.Button("Salvar",20,ClientSize.Height-48,120,true); ok.Click+=delegate { if(Value.Length>0) DialogResult=DialogResult.OK; };
        var cancel=Theme.Button("Cancelar",154,ClientSize.Height-48,120,false); cancel.Click+=delegate { DialogResult=DialogResult.Cancel; };
        Controls.Add(ok); Controls.Add(cancel); AcceptButton=ok; CancelButton=cancel;
        Shown+=delegate { input.SelectAll(); input.Focus(); };
    }

    // Devolve false se o usuário cancelou.
    public static bool Ask(IWin32Window owner,string title,string label,ref string value,bool offerEveryone,ref bool everyone) {
        using(var form=new PromptForm(title,label,value,offerEveryone,everyone)) {
            if(form.ShowDialog(owner)!=DialogResult.OK) return false;
            value=form.Value; everyone=offerEveryone && form.ForEveryone;
            return true;
        }
    }
}
