using System;
using System.IO;

namespace TibiaScarabEye.Alerts;

// Registro dos alertas (alerts.log, ao lado do alerts.json): equipado, retirado, aviso, vencido, mudanças de fonte e queda do
// Tibia, cada um com a hora. Serve para entender depois por que um alerta não saiu. O arquivo é recomeçado quando passa de
// 256 KB e escrever nele nunca derruba o programa.
internal sealed class AlertLog {
    const long MaxBytes=256*1024;
    readonly string path;
    readonly Func<DateTime> now;
    public AlertLog(string path) : this(path,null) { }
    public AlertLog(string path,Func<DateTime> now) {
        this.path=path; this.now=now??delegate { return DateTime.Now; };
        try { if(File.Exists(path) && new FileInfo(path).Length>MaxBytes) File.Delete(path); } catch(Exception ex) when(ex is IOException || ex is UnauthorizedAccessException) { }
    }
    public void Write(string message) {
        try {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.AppendAllText(path,now().ToString("yyyy-MM-dd HH:mm:ss.fff")+"  "+message+Environment.NewLine);
        } catch(Exception ex) when(ex is IOException || ex is UnauthorizedAccessException) { }
    }
}
