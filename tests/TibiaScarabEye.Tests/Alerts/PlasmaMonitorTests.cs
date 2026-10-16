using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using TibiaScarabEye.Alerts;
using Xunit;

namespace TibiaScarabEye.Tests.Alerts;

public class PlasmaMonitorTests
{
    private sealed class FakeSound : IAlertSound
    {
        public readonly List<(PlasmaItem item, AlertKind kind)> Played = new();
        public int Combined;
        public void Play(PlasmaItem item, AlertKind kind) => Played.Add((item, kind));
        public void PlayCombinedWarning() => Combined++;
    }

    private sealed class Rig
    {
        public readonly FakeSound Sound = new FakeSound();
        public TimeSpan Now = TimeSpan.Zero;
        public readonly PlasmaMonitor Monitor;
        public readonly List<string> Log = new();
        public readonly PixelBuffer WithCollar = Frames.With(200, 150, (PlasmaItem.Collar, new Point(20, 20)));
        public readonly PixelBuffer WithRing = Frames.With(200, 150, (PlasmaItem.Ring, new Point(20, 20)));
        public readonly PixelBuffer Empty = Frames.Empty();

        public Rig(bool collar = true, bool ring = true)
        {
            Monitor = new PlasmaMonitor(Sound, () => Now, Frames.Template, Log.Add);
            Monitor.SetEnabled(PlasmaItem.Collar, collar);
            Monitor.SetEnabled(PlasmaItem.Ring, ring);
        }

        // Uma leitura a cada 500 ms, como o programa faz.
        public void Tick(PixelBuffer frame, int times = 1)
        {
            for (int i = 0; i < times; i++) { Monitor.Tick(new[] { frame }); Now += TimeSpan.FromMilliseconds(500); }
        }

        public void Advance(TimeSpan time) => Now += time;
    }

    [Fact]
    public void EquippedSoundPlaysAfterTwoConsecutiveReadingsAndOnlyOnce()
    {
        var rig = new Rig();

        rig.Tick(rig.WithRing);
        Assert.Empty(rig.Sound.Played);
        rig.Tick(rig.WithRing, 5);

        Assert.Equal(new[] { (PlasmaItem.Ring, AlertKind.Equipped) }, rig.Sound.Played);
        Assert.Equal(PlasmaPhase.Equipped, rig.Monitor.Read(PlasmaItem.Ring).Phase);
        Assert.Equal(PlasmaPhase.Idle, rig.Monitor.Read(PlasmaItem.Collar).Phase);
    }

    [Fact]
    public void TheSameFrameReadTwiceIsOneObservationOnly()
    {
        var rig = new Rig();
        var frame = new[] { rig.WithRing };

        for (int i = 0; i < 10; i++) rig.Monitor.Tick(frame); // o programa le o arquivo mais vezes do que o OBS grava
        Assert.Empty(rig.Sound.Played);

        rig.Monitor.Tick(new[] { rig.WithRing }); // um segundo quadro de verdade
        Assert.Equal(new[] { (PlasmaItem.Ring, AlertKind.Equipped) }, rig.Sound.Played);
    }

    [Fact]
    public void ASingleBadFrameDoesNotEquipOrUnequip()
    {
        var rig = new Rig();
        rig.Tick(rig.WithCollar);
        rig.Tick(rig.Empty);
        rig.Tick(rig.WithCollar);
        Assert.Empty(rig.Sound.Played); // o sprite nunca apareceu duas vezes seguidas

        rig.Tick(rig.WithCollar, 2);
        Assert.Single(rig.Sound.Played);

        rig.Tick(rig.Empty, 2); // sumiu por pouco tempo: continua equipado
        rig.Tick(rig.WithCollar);
        Assert.True(rig.Monitor.Read(PlasmaItem.Collar).Present);
        Assert.Single(rig.Sound.Played);
    }

    [Fact]
    public void TheWarningPlaysNinetySecondsBeforeTheEnd()
    {
        var rig = new Rig();
        rig.Tick(rig.WithCollar, 2);
        rig.Sound.Played.Clear();

        rig.Advance(new TimeSpan(0, 28, 0));
        rig.Tick(rig.WithCollar);
        Assert.Empty(rig.Sound.Played);

        rig.Advance(new TimeSpan(0, 0, 30));
        rig.Tick(rig.WithCollar);
        Assert.Equal(new[] { (PlasmaItem.Collar, AlertKind.Warning) }, rig.Sound.Played);

        rig.Tick(rig.WithCollar, 4);
        Assert.Single(rig.Sound.Played);
    }

    [Fact]
    public void WithoutAFrameTheCountdownKeepsRunningSoTheWarningIsNotLost()
    {
        var rig = new Rig();
        rig.Tick(rig.WithRing, 2); // equipado, depois o jogo e minimizado
        rig.Sound.Played.Clear();

        rig.Advance(new TimeSpan(0, 28, 30));
        rig.Monitor.Tick(null);

        Assert.Equal(PlasmaSource.NoObs, rig.Monitor.Source);
        Assert.Equal(new[] { (PlasmaItem.Ring, AlertKind.Warning) }, rig.Sound.Played);
    }

    [Fact]
    public void RemovingTheItemNeedsSeveralReadingsAndThenPausesTheCount()
    {
        var rig = new Rig();
        rig.Tick(rig.WithRing, 2);
        rig.Advance(TimeSpan.FromMinutes(10));

        rig.Tick(rig.Empty, PlasmaMonitor.MissesToRemove - 1);
        Assert.Equal(PlasmaPhase.Equipped, rig.Monitor.Read(PlasmaItem.Ring).Phase);
        rig.Tick(rig.Empty);

        var reading = rig.Monitor.Read(PlasmaItem.Ring);
        Assert.Equal(PlasmaPhase.Paused, reading.Phase);
        Assert.InRange(reading.Remaining.TotalMinutes, 19, 20);
    }

    [Fact]
    public void TheRemovedSoundPlaysWhenTheSpriteLeavesTheSlot()
    {
        var rig = new Rig();
        rig.Tick(rig.WithCollar, 2);
        rig.Sound.Played.Clear();

        rig.Tick(rig.Empty, PlasmaMonitor.MissesToRemove - 1);
        Assert.Empty(rig.Sound.Played);
        rig.Tick(rig.Empty, 3);

        Assert.Equal(new[] { (PlasmaItem.Collar, AlertKind.Removed) }, rig.Sound.Played); // uma vez so
    }

    [Fact]
    public void NoRemovedSoundWhenTheGameGoesAwayOrTheItemWasNeverEquipped()
    {
        var rig = new Rig();
        rig.Tick(rig.Empty, 6);
        Assert.Empty(rig.Sound.Played);

        rig.Tick(rig.WithRing, 2);
        rig.Sound.Played.Clear();
        rig.Monitor.Pause();

        Assert.Empty(rig.Sound.Played);
    }

    [Fact]
    public void BothItemsWarningTogetherPlayTheCombinedSoundOnce()
    {
        var rig = new Rig();
        var both = Frames.With(200, 150, (PlasmaItem.Collar, new Point(10, 10)), (PlasmaItem.Ring, new Point(100, 80)));
        rig.Tick(both, 2);
        rig.Sound.Played.Clear();

        rig.Advance(new TimeSpan(0, 28, 30));
        rig.Tick(both, 3);

        Assert.Equal(1, rig.Sound.Combined);
        Assert.DoesNotContain(rig.Sound.Played, p => p.kind == AlertKind.Warning);
    }

    [Fact]
    public void ItemsWarningSomeSecondsApartStillShareTheCombinedSound()
    {
        var rig = new Rig();
        rig.Tick(rig.WithCollar, 2);
        rig.Advance(TimeSpan.FromSeconds(6)); // o anel entra 6 s depois do colar
        var both = Frames.With(200, 150, (PlasmaItem.Collar, new Point(10, 10)), (PlasmaItem.Ring, new Point(100, 80)));
        rig.Tick(both, 2);
        rig.Sound.Played.Clear();

        rig.Advance(new TimeSpan(0, 28, 20));
        rig.Tick(both, 40);

        Assert.Equal(1, rig.Sound.Combined);
        Assert.DoesNotContain(rig.Sound.Played, p => p.kind == AlertKind.Warning);
    }

    [Fact]
    public void ItemsWarningFarApartKeepTheirOwnSounds()
    {
        var rig = new Rig();
        rig.Tick(rig.WithCollar, 2);
        rig.Advance(TimeSpan.FromMinutes(5)); // o anel entra 5 min depois
        var both = Frames.With(200, 150, (PlasmaItem.Collar, new Point(10, 10)), (PlasmaItem.Ring, new Point(100, 80)));
        rig.Tick(both, 2);
        rig.Sound.Played.Clear();

        rig.Advance(new TimeSpan(0, 23, 30));
        rig.Tick(both, 2);
        Assert.Equal(new[] { (PlasmaItem.Collar, AlertKind.Warning) }, rig.Sound.Played);

        rig.Advance(TimeSpan.FromMinutes(5));
        rig.Tick(both, 2);
        Assert.Equal(0, rig.Sound.Combined);
        Assert.Contains((PlasmaItem.Ring, AlertKind.Warning), rig.Sound.Played);
    }

    [Fact]
    public void PreviewPlaysEvenWithTheSwitchOff()
    {
        var rig = new Rig(collar: false);

        rig.Monitor.Preview(PlasmaItem.Collar, AlertKind.Removed);
        rig.Monitor.PreviewCombinedWarning();

        Assert.Equal(new[] { (PlasmaItem.Collar, AlertKind.Removed) }, rig.Sound.Played);
        Assert.Equal(1, rig.Sound.Combined);
    }

    private static PixelBuffer Timed(PlasmaItem item, string text) => Frames.WithTimers(48, 40, (item, new Point(2, 1), text));

    [Fact]
    public void TheWarningComesWhenTheGameShowsOneMinuteThirtyEvenIfOurClockDisagrees()
    {
        var rig = new Rig();
        rig.Tick(Timed(PlasmaItem.Collar, "30m"), 2);
        rig.Sound.Played.Clear();

        rig.Advance(TimeSpan.FromMinutes(5)); // pelo nosso relogio ainda faltam 25 minutos
        rig.Tick(Timed(PlasmaItem.Collar, "1m31"), 2);
        Assert.Empty(rig.Sound.Played);

        rig.Tick(Timed(PlasmaItem.Collar, "1m30"));
        Assert.Equal(new[] { (PlasmaItem.Collar, AlertKind.Warning) }, rig.Sound.Played);
        Assert.Equal("1m30", rig.Monitor.Read(PlasmaItem.Collar).GameText);

        rig.Tick(Timed(PlasmaItem.Collar, "1m28"), 3);
        Assert.Single(rig.Sound.Played);
    }

    [Fact]
    public void TheTextOfTheOtherItemInTheSameAreaDoesNotTriggerTheWrongWarning()
    {
        var rig = new Rig();
        var both = Frames.WithTimers(90, 40, (PlasmaItem.Collar, new Point(2, 1), "20m"), (PlasmaItem.Ring, new Point(46, 1), "1m30"));
        rig.Tick(both, 5);

        // o anel ja aparece em 1m30 e avisa; o colar, com 20m, nao
        Assert.Equal(new[] { (PlasmaItem.Collar, AlertKind.Equipped), (PlasmaItem.Ring, AlertKind.Equipped), (PlasmaItem.Ring, AlertKind.Warning) }, rig.Sound.Played);
        Assert.Equal("20m", rig.Monitor.Read(PlasmaItem.Collar).GameText);
        Assert.Equal("1m30", rig.Monitor.Read(PlasmaItem.Ring).GameText);
    }

    [Fact]
    public void AnUnreadableTimerIsLoggedWithTheAreaDrawingOnceInAWhile()
    {
        var rig = new Rig();
        var unreadable = Timed(PlasmaItem.Collar, "1m3"); // falta o ultimo digito: nao se adivinha

        rig.Tick(unreadable, 40);

        Assert.Single(rig.Log, l => l.Contains("texto do tempo ilegível"));
        Assert.True(rig.Monitor.Read(PlasmaItem.Collar).Present);
        Assert.Null(rig.Monitor.Read(PlasmaItem.Collar).GameText);
    }

    [Fact]
    public void TheLogTellsEquippedWarningRemovedAndWhereTheImagesCameFrom()
    {
        var rig = new Rig();
        rig.Monitor.Tick(null);
        rig.Tick(rig.WithCollar, 2);
        rig.Advance(new TimeSpan(0, 28, 30));
        rig.Tick(rig.WithCollar);
        rig.Tick(rig.Empty, PlasmaMonitor.MissesToRemove);

        Assert.Contains(rig.Log, l => l == "fonte: sem imagem do OBS");
        Assert.Contains(rig.Log, l => l == "fonte: lendo as imagens do OBS");
        int equipped = rig.Log.FindIndex(l => l.StartsWith("Collar of Red Plasma: equipado (restam 30:00"));
        int warned = rig.Log.FindIndex(l => l.StartsWith("Collar of Red Plasma: aviso (restam 1:"));
        int removed = rig.Log.FindIndex(l => l.StartsWith("Collar of Red Plasma: retirado"));
        Assert.True(equipped >= 0 && warned > equipped && removed > warned, string.Join(" | ", rig.Log));
    }

    [Fact]
    public void ADisabledItemMakesNoSoundAndIsNotTracked()
    {
        var rig = new Rig(collar: false);

        rig.Tick(rig.WithCollar, 4);

        Assert.Empty(rig.Sound.Played);
        Assert.False(rig.Monitor.Read(PlasmaItem.Collar).Enabled);
        Assert.Equal(PlasmaPhase.Idle, rig.Monitor.Read(PlasmaItem.Collar).Phase);
    }

    [Fact]
    public void SwitchingAnItemOffForgetsItsCountAndTheOtherKeepsGoing()
    {
        var rig = new Rig();
        var both = Frames.With(200, 150, (PlasmaItem.Collar, new Point(10, 10)), (PlasmaItem.Ring, new Point(100, 80)));
        rig.Tick(both, 2);
        Assert.Equal(2, rig.Sound.Played.Count);

        rig.Monitor.SetEnabled(PlasmaItem.Collar, false);

        Assert.Equal(PlasmaPhase.Idle, rig.Monitor.Read(PlasmaItem.Collar).Phase);
        Assert.Equal(PlasmaPhase.Equipped, rig.Monitor.Read(PlasmaItem.Ring).Phase);
        rig.Monitor.SetEnabled(PlasmaItem.Collar, true);
        rig.Tick(both, 2);
        Assert.Equal(3, rig.Sound.Played.Count); // ligar de novo com o colar equipado: volta a ser notado
        Assert.Equal(PlasmaItem.Collar, rig.Sound.Played.Last().item);
    }

    [Fact]
    public void ReportsWhyItCannotSee()
    {
        var rig = new Rig();

        rig.Monitor.Tick(new PixelBuffer[0]);
        Assert.Equal(PlasmaSource.NoAreas, rig.Monitor.Source);

        rig.Monitor.Tick(null);
        Assert.Equal(PlasmaSource.NoObs, rig.Monitor.Source);

        rig.Monitor.Tick(new[] { rig.Empty });
        Assert.Equal(PlasmaSource.Reading, rig.Monitor.Source);

        rig.Monitor.Pause();
        Assert.Equal(PlasmaSource.NoTibia, rig.Monitor.Source);
    }

    [Fact]
    public void Pause_FreezesTheCountWhenTheGameGoesAway()
    {
        var rig = new Rig();
        rig.Tick(rig.WithRing, 2);
        rig.Advance(TimeSpan.FromMinutes(5));

        rig.Monitor.Pause();
        rig.Advance(TimeSpan.FromMinutes(60));

        var reading = rig.Monitor.Read(PlasmaItem.Ring);
        Assert.Equal(PlasmaPhase.Paused, reading.Phase);
        Assert.InRange(reading.Remaining.TotalMinutes, 24, 25);
        Assert.False(reading.Present);
    }

    [Fact]
    public void Restart_ResetsTheCountToThirtyMinutes()
    {
        var rig = new Rig();
        rig.Tick(rig.WithRing, 2);
        rig.Advance(TimeSpan.FromMinutes(12));

        rig.Monitor.Restart(PlasmaItem.Ring);

        Assert.Equal(TimeSpan.FromMinutes(30), rig.Monitor.Read(PlasmaItem.Ring).Remaining);
    }
}
