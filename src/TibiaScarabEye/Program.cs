using System;
using System.Windows.Forms;
using TibiaScarabEye.UI;

namespace TibiaScarabEye;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.ThreadException += delegate(object s, System.Threading.ThreadExceptionEventArgs e) { MessageBox.Show("A operação não pôde ser concluída.\n" + e.Exception.Message, "Tibia Scarab Eye"); };
        Application.Run(new MainForm());
    }
}
