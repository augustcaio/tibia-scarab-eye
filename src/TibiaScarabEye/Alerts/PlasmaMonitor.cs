using System;
using System.Collections.Generic;
using System.Drawing;

namespace TibiaScarabEye.Alerts;

// Por que o monitor está (ou não) enxergando os itens, para a janela explicar ao jogador.
internal enum PlasmaSource { NoTibia, NoObs, NoAreas, Reading }

// O que a janela mostra de um item.
internal struct PlasmaReading {
    public bool Enabled, Present;
    public PlasmaPhase Phase;
    public TimeSpan Remaining;
    // O tempo como o jogo o mostra embaixo do item ("1m28"), ou null enquanto não foi lido.
    public string GameText;
}

// Junta o que o OBS enxerga do jogo (o sprite do item nas áreas recortadas) com a contagem de cada item e toca os alertas.
// Cada chamada de Tick recebe as imagens das áreas do preset ativo. O sprite precisa aparecer em leituras seguidas para valer
// como equipado (e sumir em leituras seguidas para valer como tirado), assim um quadro ruim não dispara nem encerra nada.
// Sem imagem (OBS fechado ou jogo minimizado, por exemplo) o estado fica como estava: o item continua contando, e o aviso sai na hora certa.
internal sealed class PlasmaMonitor {
    public const int HitsToEquip=2, MissesToRemove=3;
    // Dois itens que avisam com até 10 s de diferença recebem o áudio dos dois em vez de dois avisos seguidos.
    public static readonly TimeSpan CombineWithin=TimeSpan.FromSeconds(10);
    sealed class Track {
        public PlasmaItem Item; public SpriteTemplate Sprite; public readonly PlasmaTimer Timer=new PlasmaTimer();
        public bool Enabled, Present; public int Hits, Misses, Unread;
        public string GameText;
    }
    readonly Func<TimeSpan> clock;
    readonly IAlertSound sound;
    readonly Dictionary<PlasmaItem,Track> tracks=new Dictionary<PlasmaItem,Track>();
    public PlasmaSource Source { get; private set; }
    // A última lista de imagens observada: a mesma lista de novo é o mesmo quadro e não conta como outra leitura.
    IReadOnlyList<PixelBuffer> lastFrame;

    readonly Action<string> log;

    public PlasmaMonitor(IAlertSound sound,Func<TimeSpan> clock,Func<PlasmaItem,SpriteTemplate> sprites,Action<string> log=null) {
        this.sound=sound; this.clock=clock; this.log=log??delegate(string message) { }; Source=PlasmaSource.NoTibia;
        foreach(var item in PlasmaInfo.All) tracks[item]=new Track { Item=item, Sprite=sprites(item) };
    }

    public bool AnyEnabled { get { foreach(var track in tracks.Values) if(track.Enabled) return true; return false; } }
    public bool IsEnabled(PlasmaItem item) { return tracks[item].Enabled; }
    // Desligar esquece a contagem e a leitura; ligar de novo começa do zero (o item, se estiver equipado, volta a ser notado).
    public void SetEnabled(PlasmaItem item,bool enabled) {
        var track=tracks[item];
        if(track.Enabled==enabled) return;
        track.Enabled=enabled;
        if(!enabled) Forget(track);
    }
    static void Forget(Track track) { track.Timer.Reset(); track.Present=false; track.Hits=track.Misses=track.Unread=0; track.GameText=null; }

    public PlasmaReading Read(PlasmaItem item) {
        var track=tracks[item];
        return new PlasmaReading { Enabled=track.Enabled, Present=track.Present, Phase=track.Timer.Phase, Remaining=track.Timer.Remaining(clock()), GameText=track.Present?track.GameText:null };
    }
    public void Restart(PlasmaItem item) { tracks[item].Timer.Restart(clock()); }

    // Botões de teste da janela: tocam o som mesmo com o interruptor desligado.
    public void Preview(PlasmaItem item,AlertKind kind) { sound.Play(item,kind); }
    public void PreviewCombinedWarning() { sound.PlayCombinedWarning(); }

    // O jogo fechou ou saiu do ar: as contagens pausam e a leitura recomeça quando ele voltar.
    public void Pause() {
        if(Source!=PlasmaSource.NoTibia) log("fonte: Tibia fora do ar, contagens pausadas");
        Source=PlasmaSource.NoTibia;
        TimeSpan now=clock();
        foreach(var track in tracks.Values) { track.Timer.Pause(now); track.Present=false; track.Hits=track.Misses=track.Unread=0; track.GameText=null; }
        lastFrame=null;
    }

    // 'areas' são as imagens das áreas recortadas do preset ativo, como o OBS as enxerga; null quando o OBS não mandou imagens
    // recentes (aí o estado fica como estava e as contagens seguem).
    public void Tick(IReadOnlyList<PixelBuffer> areas) {
        TimeSpan now=clock();
        var source=areas==null?PlasmaSource.NoObs:areas.Count==0?PlasmaSource.NoAreas:PlasmaSource.Reading;
        if(source!=Source) log("fonte: "+Describe(source));
        Source=source;
        bool sameFrame=areas!=null && ReferenceEquals(areas,lastFrame);
        lastFrame=areas;
        foreach(var track in tracks.Values) {
            if(!track.Enabled) continue;
            // Em cada imagem nova: o sprite do item (as 22 linhas de cima) e o tempo que o jogo escreve embaixo dele.
            bool fresh=Source==PlasmaSource.Reading && !sameFrame, removed=false;
            var reading=default(TimerReading);
            PixelBuffer seenIn=null;
            if(fresh) {
                bool found=false;
                foreach(var area in areas) {
                    Point at;
                    if(!track.Sprite.Locate(area,out at)) continue;
                    found=true; seenIn=area;
                    var read=TimerReader.Read(area,at);
                    if(read.Valid && (!reading.Valid || (read.Exact && !reading.Exact))) reading=read;
                }
                removed=Observe(track,found);
            }
            var events=track.Timer.Update(track.Present,now);
            if(fresh && track.Present) events|=Game(track,reading,seenIn,now);
            if((events&PlasmaEvent.Equipped)!=0) { log(PlasmaInfo.Name(track.Item)+": equipado (restam "+Clock(track.Timer.Remaining(now))+")"); sound.Play(track.Item,AlertKind.Equipped); }
            if((events&PlasmaEvent.Warning)!=0) { log(PlasmaInfo.Name(track.Item)+": aviso (restam "+Clock(track.Timer.Remaining(now))+")"); Warn(track,now); }
            if((events&PlasmaEvent.Expired)!=0) log(PlasmaInfo.Name(track.Item)+": venceu");
            if(removed) { log(PlasmaInfo.Name(track.Item)+": retirado ("+track.Timer.Phase+", restam "+Clock(track.Timer.Remaining(now))+")"); sound.Play(track.Item,AlertKind.Removed); }
        }
    }
    // O tempo lido do jogo guia a contagem; o texto é anotado no registro quando muda e, se o sprite está lá mas o texto não é
    // legível por um tempo, o desenho da área vai para o registro (para ajustar a leitura depois).
    PlasmaEvent Game(Track track,TimerReading reading,PixelBuffer seenIn,TimeSpan now) {
        if(!reading.Valid) {
            if(seenIn!=null && ++track.Unread%300==30) log(PlasmaInfo.Name(track.Item)+": texto do tempo ilegível; desenho da área:\n"+TimerReader.Describe(seenIn));
            return PlasmaEvent.None;
        }
        track.Unread=0;
        if(track.GameText!=reading.Text) { track.GameText=reading.Text; log(PlasmaInfo.Name(track.Item)+": tempo do jogo "+reading.Text); }
        return track.Timer.ObserveGame(reading,now);
    }
    // Um único aviso quando o outro item está equipado e chega ao seu aviso em até CombineWithin: o áudio dos dois, e o outro não repete.
    void Warn(Track track,TimeSpan now) {
        foreach(var other in tracks.Values) {
            if(other==track || !other.Enabled || other.Timer.Phase!=PlasmaPhase.Equipped || other.Timer.Warned) continue;
            if(other.Timer.Remaining(now)>PlasmaTimer.WarnAt+CombineWithin) continue;
            other.Timer.MarkWarned(); log("aviso combinado com "+PlasmaInfo.Name(other.Item)); sound.PlayCombinedWarning();
            return;
        }
        sound.Play(track.Item,AlertKind.Warning);
    }
    static string Describe(PlasmaSource source) {
        switch(source) {
            case PlasmaSource.NoObs: return "sem imagem do OBS";
            case PlasmaSource.NoAreas: return "o OBS não mandou áreas";
            case PlasmaSource.NoTibia: return "sem Tibia";
            default: return "lendo as imagens do OBS";
        }
    }
    static string Clock(TimeSpan time) { return ((int)time.TotalMinutes)+":"+time.Seconds.ToString("00"); }
    // Devolve true quando o sprite acabou de sumir (o item foi tirado do slot).
    static bool Observe(Track track,bool found) {
        if(found) { track.Misses=0; if(++track.Hits>=HitsToEquip) track.Present=true; return false; }
        track.Hits=0;
        if(++track.Misses>=MissesToRemove && track.Present) { track.Present=false; return true; }
        return false;
    }
}
