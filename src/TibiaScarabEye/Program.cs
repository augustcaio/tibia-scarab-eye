using System;
using System.Windows.Forms;

namespace TibiaScarabEye;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new Form { Text = "Tibia Scarab Eye" });
    }
}
