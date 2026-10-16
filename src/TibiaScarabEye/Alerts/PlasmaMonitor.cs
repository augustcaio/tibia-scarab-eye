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
        public bool Enabled, Present; public int Hits, Misses;
    }
    readonly Func<TimeSpan> clock;
    readonly IAlertSound sound;
    readonly Dictionary<PlasmaItem,Track> tracks=new Dictionary<PlasmaItem,Track>();
    public PlasmaSource Source { get; private set; }
    // A última lista de imagens observada: a mesma lista de novo é o mesmo quadro e não conta como outra leitura.
    IReadOnlyList<PixelBuffer> lastFrame;

    public PlasmaMonitor(IAlertSound sound,Func<TimeSpan> clock,Func<PlasmaItem,SpriteTemplate> sprites) {
        this.sound=sound; this.clock=clock; Source=PlasmaSource.NoTibia;
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
    static void Forget(Track track) { track.Timer.Reset(); track.Present=false; track.Hits=track.Misses=0; }

    public PlasmaReading Read(PlasmaItem item) {
        var track=tracks[item];
        return new PlasmaReading { Enabled=track.Enabled, Present=track.Present, Phase=track.Timer.Phase, Remaining=track.Timer.Remaining(clock()) };
    }
    public void Restart(PlasmaItem item) { tracks[item].Timer.Restart(clock()); }

    // Botões de teste da janela: tocam o som mesmo com o interruptor desligado.
    public void Preview(PlasmaItem item,AlertKind kind) { sound.Play(item,kind); }
    public void PreviewCombinedWarning() { sound.PlayCombinedWarning(); }

    // O jogo fechou ou saiu do ar: as contagens pausam e a leitura recomeça quando ele voltar.
    public void Pause() {
        Source=PlasmaSource.NoTibia;
        TimeSpan now=clock();
        foreach(var track in tracks.Values) { track.Timer.Pause(now); track.Present=false; track.Hits=track.Misses=0; }
        lastFrame=null;
    }

    // 'areas' são as imagens das áreas recortadas do preset ativo, como o OBS as enxerga; null quando o OBS não mandou imagens
    // recentes (aí o estado fica como estava e as contagens seguem).
    public void Tick(IReadOnlyList<PixelBuffer> areas) {
        TimeSpan now=clock();
        Source=areas==null?PlasmaSource.NoObs:areas.Count==0?PlasmaSource.NoAreas:PlasmaSource.Reading;
        bool sameFrame=areas!=null && ReferenceEquals(areas,lastFrame);
        lastFrame=areas;
        foreach(var track in tracks.Values) {
            if(!track.Enabled) continue;
            bool removed=Source==PlasmaSource.Reading && !sameFrame && Observe(track,track.Sprite.Find(areas));
            var events=track.Timer.Update(track.Present,now);
            if((events&PlasmaEvent.Equipped)!=0) sound.Play(track.Item,AlertKind.Equipped);
            if((events&PlasmaEvent.Warning)!=0) Warn(track,now);
            if(removed) sound.Play(track.Item,AlertKind.Removed);
        }
    }
    // Um único aviso quando o outro item está equipado e chega ao seu aviso em até CombineWithin: o áudio dos dois, e o outro não repete.
    void Warn(Track track,TimeSpan now) {
        foreach(var other in tracks.Values) {
            if(other==track || !other.Enabled || other.Timer.Phase!=PlasmaPhase.Equipped || other.Timer.Warned) continue;
            if(other.Timer.Remaining(now)>PlasmaTimer.WarnAt+CombineWithin) continue;
            other.Timer.MarkWarned(); sound.PlayCombinedWarning();
            return;
        }
        sound.Play(track.Item,AlertKind.Warning);
    }
    // Devolve true quando o sprite acabou de sumir (o item foi tirado do slot).
    static bool Observe(Track track,bool found) {
        if(found) { track.Misses=0; if(++track.Hits>=HitsToEquip) track.Present=true; return false; }
        track.Hits=0;
        if(++track.Misses>=MissesToRemove && track.Present) { track.Present=false; return true; }
        return false;
    }
}
