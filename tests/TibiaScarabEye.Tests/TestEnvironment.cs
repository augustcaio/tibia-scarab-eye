using System.Runtime.CompilerServices;
using System.Windows.Forms;

namespace TibiaScarabEye.Tests;

internal static class TestEnvironment
{
    // Equivale ao ApplicationConfiguration.Initialize() do app; precisa rodar antes de qualquer controle ser criado.
    [ModuleInitializer]
    internal static void Initialize()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.SetHighDpiMode(HighDpiMode.SystemAware);
    }
}
