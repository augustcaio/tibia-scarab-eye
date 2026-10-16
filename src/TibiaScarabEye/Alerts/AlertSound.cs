using System;
using System.Collections.Generic;
using System.IO;
using System.Media;

namespace TibiaScarabEye.Alerts;

// Equipped: o item foi equipado. Warning: faltam 1:30. Removed: o item saiu do slot (trocado ou vencido).
public enum AlertKind { Equipped, Warning, Removed }

internal interface IAlertSound {
    void Play(PlasmaItem item,AlertKind kind);
    // Os dois itens chegam ao aviso quase juntos: um único áudio no lugar de dois.
    void PlayCombinedWarning();
}

// Toca os sons (WAV PCM): collar-on, collar-warning, collar-off, ring-on, ring-warning, ring-off e plasmas-warning. Procura
// primeiro em %LOCALAPPDATA%\TibiaScarabEye\sounds (para o jogador trocar um som) e depois na pasta sounds ao lado do
// executável. Sem o arquivo, toca um som do Windows no lugar, para o alerta nunca ficar mudo.
internal sealed class FileAlertSound : IAlertSound {
    public static readonly string UserFolder=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"TibiaScarabEye","sounds");
    public static readonly string AppFolder=Path.Combine(AppContext.BaseDirectory,"sounds");
    public const string CombinedFile="plasmas-warning.wav";
    readonly string[] folders;
    // Um player por arquivo, já carregado na memória: tocar não espera ler o disco. Se o arquivo mudar, recarrega.
    readonly Dictionary<string,KeyValuePair<DateTime,SoundPlayer>> players=new Dictionary<string,KeyValuePair<DateTime,SoundPlayer>>();
    SoundPlayer current;

    public FileAlertSound() : this(UserFolder,AppFolder) { }
    public FileAlertSound(params string[] folders) { this.folders=folders; }

    public static string FileName(PlasmaItem item,AlertKind kind) {
        return PlasmaInfo.SoundPrefix(item)+"-"+(kind==AlertKind.Equipped?"on":kind==AlertKind.Warning?"warning":"off")+".wav";
    }
    // O primeiro arquivo que existe, ou null.
    public string Find(string fileName) {
        foreach(var folder in folders) { string path=Path.Combine(folder,fileName); if(File.Exists(path)) return path; }
        return null;
    }
    public string Find(PlasmaItem item,AlertKind kind) { return Find(FileName(item,kind)); }

    public void Play(PlasmaItem item,AlertKind kind) {
        Play(Find(item,kind),kind==AlertKind.Equipped?SystemSounds.Asterisk:kind==AlertKind.Warning?SystemSounds.Exclamation:SystemSounds.Beep);
    }
    public void PlayCombinedWarning() { Play(Find(CombinedFile),SystemSounds.Exclamation); }

    // Carrega todos os sons já na abertura do programa, para o primeiro alerta também sair sem atraso.
    public void Preload() {
        foreach(var item in PlasmaInfo.All) foreach(AlertKind kind in Enum.GetValues(typeof(AlertKind))) Prepare(Find(item,kind));
        Prepare(Find(CombinedFile));
    }
    SoundPlayer Prepare(string path) {
        if(path==null) return null;
        try {
            DateTime stamp=File.GetLastWriteTimeUtc(path);
            KeyValuePair<DateTime,SoundPlayer> entry;
            if(players.TryGetValue(path,out entry)) {
                if(entry.Key==stamp) return entry.Value;
                entry.Value.Dispose();
            }
            var player=new SoundPlayer(path);
            player.Load();
            players[path]=new KeyValuePair<DateTime,SoundPlayer>(stamp,player);
            return player;
        } catch(Exception ex) when(ex is IOException || ex is InvalidOperationException || ex is UnauthorizedAccessException || ex is TimeoutException) { players.Remove(path); return null; }
    }

    void Play(string path,SystemSound fallback) {
        var player=Prepare(path);
        if(player!=null) {
            try {
                // Parar o anterior (se ainda tocava) é melhor que sobrepor dois alertas.
                if(current!=null && current!=player) current.Stop();
                current=player;
                player.Play();
                return;
            } catch(Exception ex) when(ex is IOException || ex is InvalidOperationException || ex is UnauthorizedAccessException || ex is TimeoutException) { }
        }
        fallback.Play();
    }
}
