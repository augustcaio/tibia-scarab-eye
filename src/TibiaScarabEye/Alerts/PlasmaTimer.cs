using System;

namespace TibiaScarabEye.Alerts;

[Flags] internal enum PlasmaEvent { None=0, Equipped=1, Warning=2, Expired=4 }

internal enum PlasmaPhase { Idle, Equipped, Paused }

// A contagem de um item: 30 minutos de duração, que só correm com ele equipado. O relógio vem de fora (tempo monotônico),
// então a lógica não depende da tela nem do relógio do sistema.
//   - Equipado: toca "equipado" e a contagem corre; aos 1:30 restantes dá o aviso uma única vez.
//   - Tirado com mais de 1:30 sobrando: a contagem pausa e volta de onde parou quando o sprite reaparece.
//   - Tirado com 1:30 ou menos (a troca que o aviso pede) ou vencido: o item está gasto; o próximo sprite é um item novo (30:00).
// O sprite não diferencia dois itens iguais; para o caso de o palpite estar errado há Restart (zerar a contagem).
internal sealed class PlasmaTimer {
    public static readonly TimeSpan Duration=TimeSpan.FromMinutes(30), WarnAt=TimeSpan.FromSeconds(90);
    TimeSpan left=Duration, anchor;
    bool running, warned, used, blocked;

    // O aviso dos 1:30 já saiu (ou foi dado junto com o do outro item, veja MarkWarned).
    public bool Warned { get { return warned; } }
    public void MarkWarned() { warned=true; }
    public PlasmaPhase Phase { get { return running?PlasmaPhase.Equipped:used?PlasmaPhase.Paused:PlasmaPhase.Idle; } }
    public TimeSpan Remaining(TimeSpan now) {
        if(!running) return left;
        TimeSpan value=left-(now-anchor);
        return value<TimeSpan.Zero?TimeSpan.Zero:value;
    }

    // 'present' diz se o sprite está à vista neste instante. Devolve o que aconteceu agora.
    public PlasmaEvent Update(bool present,TimeSpan now) {
        var result=PlasmaEvent.None;
        if(running) {
            left-=now-anchor; anchor=now;
            if(left<=TimeSpan.Zero) { Reset(); blocked=true; return PlasmaEvent.Expired; }
            if(!warned && left<=WarnAt) { warned=true; result|=PlasmaEvent.Warning; }
            if(!present) { running=false; if(left<=WarnAt) Reset(); }
        } else if(present) {
            // Logo depois de vencer o sprite ainda pode estar na tela por um instante: só vale um novo depois de sumir.
            if(blocked) return PlasmaEvent.None;
            running=true; anchor=now; used=true; result|=PlasmaEvent.Equipped;
        } else blocked=false;
        return result;
    }

    // Volta ao item novo e fechado: 30:00, sem aviso dado.
    public void Reset() { left=Duration; running=false; warned=false; used=false; }

    // O jogador diz que esse é um item novo: recomeça os 30:00 (se já estiver equipado, sem tocar o som de equipado).
    public void Restart(TimeSpan now) {
        bool wasRunning=running;
        Reset(); blocked=false;
        if(wasRunning) { running=true; used=true; anchor=now; }
    }

    // O jogo saiu do ar: pausa a contagem como se o item tivesse sido tirado.
    public void Pause(TimeSpan now) { if(running) Update(false,now); }
}
