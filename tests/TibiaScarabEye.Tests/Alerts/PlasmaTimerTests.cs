using System;
using TibiaScarabEye.Alerts;
using Xunit;

namespace TibiaScarabEye.Tests.Alerts;

public class PlasmaTimerTests
{
    private static TimeSpan Min(double minutes) => TimeSpan.FromMinutes(minutes);

    [Fact]
    public void Equipping_StartsTheThirtyMinuteCountdownAndSignalsOnce()
    {
        var timer = new PlasmaTimer();

        Assert.Equal(PlasmaEvent.None, timer.Update(false, Min(0)));
        Assert.Equal(PlasmaEvent.Equipped, timer.Update(true, Min(1)));
        Assert.Equal(PlasmaEvent.None, timer.Update(true, Min(2)));

        Assert.Equal(PlasmaPhase.Equipped, timer.Phase);
        Assert.Equal(Min(29), timer.Remaining(Min(2)));
    }

    [Fact]
    public void TheWarningComesAtNinetySecondsLeftAndOnlyOnce()
    {
        var timer = new PlasmaTimer();
        timer.Update(true, TimeSpan.Zero);

        Assert.Equal(PlasmaEvent.None, timer.Update(true, new TimeSpan(0, 28, 29)));
        Assert.Equal(PlasmaEvent.Warning, timer.Update(true, new TimeSpan(0, 28, 30)));
        Assert.Equal(PlasmaEvent.None, timer.Update(true, new TimeSpan(0, 28, 45)));
        Assert.Equal(TimeSpan.FromSeconds(75), timer.Remaining(new TimeSpan(0, 28, 45)));
    }

    [Fact]
    public void ExpiringSignalsAndIgnoresTheSpriteUntilItDisappears()
    {
        var timer = new PlasmaTimer();
        timer.Update(true, TimeSpan.Zero);
        timer.Update(true, new TimeSpan(0, 29, 0));

        Assert.Equal(PlasmaEvent.Expired, timer.Update(true, Min(30)));
        Assert.Equal(PlasmaPhase.Idle, timer.Phase);
        Assert.Equal(PlasmaEvent.None, timer.Update(true, Min(30.5))); // o sprite ainda na tela por um instante: nao e um item novo
        Assert.Equal(PlasmaEvent.None, timer.Update(false, Min(31)));
        Assert.Equal(PlasmaEvent.Equipped, timer.Update(true, Min(40)));
        Assert.Equal(Min(30), timer.Remaining(Min(40)));
    }

    [Fact]
    public void TakingTheItemOffEarlyPausesAndPutOnAgainResumes()
    {
        var timer = new PlasmaTimer();
        timer.Update(true, TimeSpan.Zero);
        timer.Update(false, Min(10));

        Assert.Equal(PlasmaPhase.Paused, timer.Phase);
        Assert.Equal(Min(20), timer.Remaining(Min(50))); // fora do slot nada corre

        Assert.Equal(PlasmaEvent.Equipped, timer.Update(true, Min(50)));
        Assert.Equal(Min(15), timer.Remaining(Min(55)));
    }

    [Fact]
    public void SwappingAfterTheWarningSpendsTheItemSoTheNextOneStartsFresh()
    {
        var timer = new PlasmaTimer();
        timer.Update(true, TimeSpan.Zero);
        Assert.Equal(PlasmaEvent.Warning, timer.Update(true, new TimeSpan(0, 28, 30)));

        timer.Update(false, new TimeSpan(0, 28, 40)); // a troca que o aviso pediu

        Assert.Equal(PlasmaPhase.Idle, timer.Phase);
        Assert.Equal(PlasmaEvent.Equipped, timer.Update(true, Min(35)));
        Assert.Equal(Min(30), timer.Remaining(Min(35)));
        Assert.Equal(PlasmaEvent.Warning, timer.Update(true, Min(35) + new TimeSpan(0, 28, 30))); // o novo item tambem avisa
    }

    [Fact]
    public void Restart_MakesTheEquippedItemANewOneWithoutAnotherEquippedSignal()
    {
        var timer = new PlasmaTimer();
        timer.Update(true, TimeSpan.Zero);
        timer.Update(true, Min(20));

        timer.Restart(Min(20));

        Assert.Equal(PlasmaPhase.Equipped, timer.Phase);
        Assert.Equal(Min(30), timer.Remaining(Min(20)));
        Assert.Equal(PlasmaEvent.None, timer.Update(true, Min(21)));
    }

    [Fact]
    public void Restart_OfAPausedItemForgetsIt()
    {
        var timer = new PlasmaTimer();
        timer.Update(true, TimeSpan.Zero);
        timer.Update(false, Min(10));

        timer.Restart(Min(11));

        Assert.Equal(PlasmaPhase.Idle, timer.Phase);
        Assert.Equal(PlasmaEvent.Equipped, timer.Update(true, Min(12)));
        Assert.Equal(Min(30), timer.Remaining(Min(12)));
    }

    private static TimerReading Game(string text) => TimerReader.Parse(text);

    [Fact]
    public void TheGamesOwnTimerGivesTheWarningWhateverOurClockSays()
    {
        var timer = new PlasmaTimer();
        timer.Update(true, TimeSpan.Zero);
        Assert.Equal(Min(30), timer.Remaining(TimeSpan.Zero));

        // Nosso relogio diz 20 minutos, mas o jogo ja mostra 1m31: nada ainda; 1m30: aviso, uma vez so.
        Assert.Equal(PlasmaEvent.None, timer.ObserveGame(Game("1m31"), Min(10)));
        Assert.Equal(PlasmaEvent.Warning, timer.ObserveGame(Game("1m30"), Min(10) + TimeSpan.FromSeconds(1)));
        Assert.Equal(PlasmaEvent.None, timer.ObserveGame(Game("1m29"), Min(10) + TimeSpan.FromSeconds(2)));
        Assert.Equal(TimeSpan.FromSeconds(89), timer.Remaining(Min(10) + TimeSpan.FromSeconds(2)));
        Assert.Equal(PlasmaEvent.None, timer.Update(true, Min(10) + TimeSpan.FromSeconds(3))); // nosso relogio nao repete o aviso
    }

    [Fact]
    public void AMinuteLevelReadingStartsTheCountAtTheRightPlaceOnlyOnce()
    {
        var timer = new PlasmaTimer();
        timer.Update(true, TimeSpan.Zero); // programa aberto com o item ja na reta final: 30:00 e palpite

        timer.ObserveGame(Game("11m"), Min(1));
        Assert.InRange(timer.Remaining(Min(1)).TotalSeconds, 11 * 60, 11 * 60 + 59);

        timer.ObserveGame(Game("30m"), Min(2)); // uma leitura de minutos nao puxa a contagem para cima depois
        Assert.InRange(timer.Remaining(Min(2)).TotalSeconds, 10 * 60, 11 * 60 + 59);
    }

    [Fact]
    public void TheGameTimerIsIgnoredWhileTheItemIsNotRunning()
    {
        var timer = new PlasmaTimer();

        Assert.Equal(PlasmaEvent.None, timer.ObserveGame(Game("1m10"), Min(1)));
        Assert.Equal(PlasmaPhase.Idle, timer.Phase);
    }

    [Fact]
    public void Pause_StopsTheCountdownLikeTakingTheItemOff()
    {
        var timer = new PlasmaTimer();
        timer.Update(true, TimeSpan.Zero);

        timer.Pause(Min(5));

        Assert.Equal(PlasmaPhase.Paused, timer.Phase);
        Assert.Equal(Min(25), timer.Remaining(Min(99)));
    }
}
