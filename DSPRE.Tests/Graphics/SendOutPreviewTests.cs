using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DSPRE.Avalonia;
using DSPRE.Avalonia.Data;
using DSPRE.Avalonia.ViewModels.Battle;
using Xunit;
using Xunit.Abstractions;
using static DSPRE.RomInfo;

namespace DSPRE.Tests
{
    /// <summary>
    /// The send-out preview's timing against gaps measured in real battles, and the ROM data it draws.
    /// </summary>
    [Collection("rom")]
    public class SendOutPreviewTests
    {
        private readonly ITestOutputHelper _out;
        public SendOutPreviewTests(ITestOutputHelper o) => _out = o;

        private const string Pineco = "PINECO";

        private static SendOutOptions Wild(TextSpeed speed = TextSpeed.Mid, PreviewSides sides = PreviewSides.Both, bool shiny = false) => new()
        {
            Kind = SendOutKind.Wild, Sides = sides, Speed = speed, Shiny = shiny, Text = Words,
        };

        private static SendOutOptions Trainer(PreviewSides sides = PreviewSides.Both, bool intro = true, int partyBalls = 3, bool rows = true) => new()
        {
            Kind = SendOutKind.Trainer, Sides = sides, TrainerIntro = intro, PartyBalls = partyBalls, ShowPartyBalls = rows,
            Speed = TextSpeed.Fast, Text = Words,
        };

        private static string Words(SendOutMessage m) => m switch
        {
            SendOutMessage.Challenged => "You are challenged by\nYoungster Joey!",
            SendOutMessage.WildAppeared => $"A wild {Pineco} appeared!",
            SendOutMessage.EnemySentOut => $"Youngster Joey sent\nout {Pineco}!",
            SendOutMessage.Go => $"Go! {Pineco}!",
            _ => "",
        };

        // Runs a sequence with the host's animation players replaced by fixed lengths: each side is busy for
        // animTicks after its animation starts, and a sparkle is busy for sparkleTicks after it starts.
        private sealed class Run
        {
            public readonly SendOutSequence S;
            private readonly int _anim, _sparkle;
            private int _enemyAnimEnd = int.MaxValue, _playerAnimEnd = int.MaxValue, _enemySparkleEnd = -1, _playerSparkleEnd = -1;
            public readonly List<(int Tick, string What)> Log = new();
            private SendOutMessage _lastMessage;

            public Run(SendOutOptions options, int animTicks = 40, int sparkleTicks = 50)
            {
                S = new SendOutSequence(options);
                _anim = animTicks;
                _sparkle = sparkleTicks;
            }

            public void Step()
            {
                int next = S.Tick + 1;
                S.EnemyBusy = next < _enemyAnimEnd;
                S.PlayerBusy = next < _playerAnimEnd;
                S.EnemySparkleBusy = next < _enemySparkleEnd;
                S.PlayerSparkleBusy = next < _playerSparkleEnd;
                string text = S.MessageText;
                S.Step();
                if (S.EnemyAnimStarts) { _enemyAnimEnd = S.Tick + _anim; Log.Add((S.Tick, "enemy anim")); }
                else if (_enemyAnimEnd == int.MaxValue) S.EnemyBusy = false;
                if (S.PlayerAnimStarts) { _playerAnimEnd = S.Tick + _anim; Log.Add((S.Tick, "player anim")); }
                if (S.EnemySparkleStarts) { _enemySparkleEnd = S.Tick + 1 + _sparkle; Log.Add((S.Tick, "enemy sparkle")); }
                if (S.PlayerSparkleStarts) { _playerSparkleEnd = S.Tick + 1 + _sparkle; Log.Add((S.Tick, "player sparkle")); }
                if (S.MessageText != text && text.Length == 0 || S.Message != _lastMessage && S.MessageText.Length > 0) Log.Add((S.Tick, "first letter " + S.Message));
                if (S.MessageText.Length > 0) _lastMessage = S.Message;
                if (S.MessageText != text && S.MessageText == Words(S.Message)) Log.Add((S.Tick, "last letter " + S.Message));
            }

            public int When(string what) => Log.First(e => e.What == what).Tick;

            public int Until(Func<SendOutSequence, bool> condition)
            {
                for (int guard = 0; guard < 5000 && !S.Done; guard++) { Step(); if (condition(S)) return S.Tick; }
                throw new Xunit.Sdk.XunitException("never happened");
            }

            public void ToEnd() { for (int guard = 0; guard < 5000 && !S.Done; guard++) Step(); Assert.True(S.Done); }
        }

        [Fact]
        public void TheThrownBallFollowsTheGamesArc()
        {
            // One step in, the top of the arc, and the end where the dip has not quite closed.
            Assert.Equal((40, 94), SendOutSequence.ArcPoint(39, 100, 64, 144, 48, 20, 1));
            Assert.Equal((51, 73), SendOutSequence.ArcPoint(39, 100, 64, 144, 48, 20, 10));
            Assert.Equal((64, 142), SendOutSequence.ArcPoint(39, 100, 64, 144, 48, 20, 20));
            // A mid-battle throw from the left edge: its second, tenth and last positions.
            Assert.Equal((12, 94), SendOutSequence.ArcPoint(10, 100, 64, 144, 48, 20, 1));
            Assert.Equal((34, 71), SendOutSequence.ArcPoint(10, 100, 64, 144, 48, 20, 9));
            Assert.Equal((63, 142), SendOutSequence.ArcPoint(10, 100, 64, 144, 48, 20, 20));
        }

        [Fact]
        public void AWildPokemonLandsShadedAndItsBarFollows()
        {
            var run = new Run(Wild());
            run.Until(s => s.Tick == 59);
            Assert.True(run.S.Enemy.OffsetX < 0, "still sliding in");
            Assert.Equal(8 / 16.0, run.S.Enemy.Tint);
            run.Step();
            Assert.Equal(60, run.When("enemy anim"));
            Assert.Equal(0, run.S.Enemy.OffsetX);
            run.Step();
            Assert.Equal(7 / 16.0, run.S.Enemy.Tint);
            Assert.True(run.S.EnemyGaugeVisible && run.S.EnemyGaugeOffsetX == -160);
            run.Step();
            Assert.Equal(-136, run.S.EnemyGaugeOffsetX);   // measured: the bar starts moving two ticks after landing
        }

        [Fact]
        public void TheTextBoxComesUpFromBlackBeforeTheLanding()
        {
            var run = new Run(Wild());
            run.Until(s => s.Tick == 49);
            Assert.Equal(0, run.S.TextBox);
            run.Step();
            Assert.Equal(2 / 16.0, run.S.TextBox);
            run.Until(s => s.Tick == 56);
            Assert.True(run.S.TextBox < 1);
            run.Step();
            Assert.Equal(1.0, run.S.TextBox);
        }

        [Theory]
        [InlineData(40)]
        [InlineData(49)]
        public void AWildMessageStartsFourTicksAfterTheAnimationEnds(int animTicks)
        {
            // HeartGold wild Pineco: last animation change on frame 808, first letter on 816.
            var run = new Run(Wild(TextSpeed.Fast), animTicks);
            run.ToEnd();
            int animEnd = run.When("enemy anim") + animTicks;
            Assert.Equal(animEnd + 4, run.When("first letter WildAppeared"));
        }

        [Theory]
        [InlineData(TextSpeed.Fast, 69)]
        [InlineData(TextSpeed.Mid, 72)]
        public void TheWildMessageHoldsBeforeGo(TextSpeed speed, int ticks)
        {
            // Platinum at mid speed measured 144 frames between the last letter and "Go!".
            var run = new Run(Wild(speed));
            run.ToEnd();
            Assert.Equal(ticks, run.When("first letter Go") - run.When("last letter WildAppeared"));
        }

        [Fact]
        public void YourTrainerMovesFourTicksAfterGoFinishes()
        {
            // HeartGold at fast speed: "Go!" finished on frame 884 and the trainer moved on 892.
            var run = new Run(Wild(TextSpeed.Fast));
            run.Until(s => s.Message == SendOutMessage.Go && s.MessageText == Words(SendOutMessage.Go));
            int lastLetter = run.S.Tick;
            int moved = run.Until(s => s.PlayerTrainer.X < 64 && s.PlayerTrainer.AnimTicks >= 0);
            Assert.Equal(4, moved - lastLetter);
            Assert.Equal(59, run.S.PlayerTrainer.X);
        }

        [Fact]
        public void AShinyWaitsForItsSparkleBeforeTheMessage()
        {
            var run = new Run(Wild(TextSpeed.Fast, shiny: true), animTicks: 40, sparkleTicks: 50);
            run.ToEnd();
            int animEnd = run.When("enemy anim") + 40;
            int sparkle = run.When("enemy sparkle");
            Assert.Equal(animEnd + 1, sparkle);
            Assert.Equal(sparkle + 1 + 50 + 3, run.When("first letter WildAppeared"));
        }

        [Fact]
        public void ATrainerBattleChallengesThenThrows()
        {
            var run = new Run(Trainer());
            run.ToEnd();
            Assert.Equal(50, run.When("first letter Challenged"));
            // The enemy trainer's first step off comes two ticks after "sent out" starts printing.
            var again = new Run(Trainer());
            int sentOut = again.Until(s => s.Message == SendOutMessage.EnemySentOut);
            int stepped = again.Until(s => s.EnemyTrainer.X > 192);
            Assert.Equal(2, stepped - sentOut);
            // And the messages come in order.
            var order = run.Log.Where(e => e.What.StartsWith("first letter")).Select(e => e.What).ToArray();
            Assert.Equal(new[] { "first letter Challenged", "first letter EnemySentOut", "first letter Go" }, order);
        }

        [Fact]
        public void TheTrainerLandsOneTickAfterAWildPokemonWould()
        {
            var run = new Run(Trainer());
            int landed = run.Until(s => s.EnemyTrainer.X == 192);
            Assert.Equal(61, landed);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(3)]
        [InlineData(6)]
        public void YourPartyRowKnocksOncePerSlot(int partyBalls)
        {
            var run = new Run(Trainer(partyBalls: partyBalls));
            var sounds = new List<string>();
            for (int guard = 0; guard < 5000 && !run.S.Done; guard++) { run.Step(); sounds.AddRange(run.S.Sounds); }
            Assert.Equal(partyBalls, sounds.Count(x => x == "SEQ_SE_DP_TB_KON"));
            Assert.Equal(6 - partyBalls, sounds.Count(x => x == "SEQ_SE_DP_TB_KARA"));
            Assert.Equal(2, sounds.Count(x => x == "SEQ_SE_DP_TB_START"));
            Assert.False(run.S.EnemyRow.Visible || run.S.PlayerRow.Visible);
        }

        [Fact]
        public void TheRowsRestWhereTheGameDrawsThem()
        {
            var run = new Run(Trainer());
            run.Until(s => s.Message == SendOutMessage.Challenged && s.PlayerRow.Balls[5].X == 242 && !s.PlayerRow.Balls[5].Animating);
            Assert.Equal(new double[] { 162, 178, 194, 210, 226, 242 }, run.S.PlayerRow.Balls.Select(b => b.X));
            Assert.Equal(new double[] { 94, 78, 62, 46, 30, 14 }, run.S.EnemyRow.Balls.Select(b => b.X));
            Assert.Equal((224.0, 120.0), (run.S.PlayerRow.BarX, run.S.PlayerRow.BarY));
            Assert.Equal((32.0, 56.0), (run.S.EnemyRow.BarX, run.S.EnemyRow.BarY));
        }

        [Fact]
        public void AMidBattleSendOutHasNoTrainersAndThrowsFromTheSide()
        {
            var run = new Run(Trainer(intro: false));
            bool trainerSeen = false, platformMoved = false;
            (int X, int Y) firstPlayerBall = (-1, -1);
            for (int guard = 0; guard < 5000 && !run.S.Done; guard++)
            {
                run.Step();
                trainerSeen |= run.S.EnemyTrainer.Visible || run.S.PlayerTrainer.Visible;
                platformMoved |= run.S.EnemyPlatformOffsetX != 0 || run.S.PlayerPlatformOffsetX != 0;
                if (firstPlayerBall.X < 0 && run.S.PlayerBall.Visible) firstPlayerBall = (run.S.PlayerBall.X, run.S.PlayerBall.Y);
            }
            Assert.False(trainerSeen);
            Assert.False(platformMoved);
            Assert.Equal((10, 100), firstPlayerBall);
            Assert.Equal(1.0, run.S.TextBox);
        }

        [Theory]
        [InlineData(0, false, 0)]
        [InlineData(1, false, 0)]
        [InlineData(5, false, 4)]
        [InlineData(0, true, 7)]
        [InlineData(5, true, 4)]
        public void TheCryWaitsItsDelayAfterTheAnimationStarts(int delay, bool heartGold, int ticksAfterStart)
        {
            var options = Wild(sides: PreviewSides.Theirs);
            options.EnemyCryDelay = delay;
            options.CryZeroIsEight = heartGold;
            var s = new SendOutSequence(options);
            int starts = -1, cry = -1;
            for (int guard = 0; guard < 5000 && !s.Done && cry < 0; guard++)
            {
                s.Step();
                if (s.EnemyAnimStarts) starts = s.Tick;
                if (s.EnemyCry) cry = s.Tick;
            }
            Assert.Equal(ticksAfterStart, cry - starts);
        }

        [SkippableTheory]
        [InlineData("CPUE", "Platinum")]
        [InlineData("IPKE", "HeartGold")]
        public void EveryBallHasItsGraphicAndBurst(string code, string game)
        {
            Load(code, game, DirNames.battleObj, DirNames.ballParticles);
            var balls = SendOutGraphics.Balls();
            Assert.Equal(gameFamily == GameFamilies.HGSS ? 24 : 16, balls.Count);
            _out.WriteLine(string.Join(", ", balls.Select(b => $"{b.Ball}={b.Name}")));

            var bursts = new ScriptNarc(DirNames.ballParticles);
            foreach (var (ball, name) in balls)
            {
                var f = SendOutGraphics.BallFiles(ball);
                var cells = new WeCellAnimRenderer();
                Assert.True(cells.Load(DirNames.battleObj, f.Ncgr, DirNames.battleObj, f.Nclr, DirNames.battleObj, f.Ncer, DirNames.battleObj, f.Nanr),
                            $"{name} ({ball}) graphic did not load from {f}");
                var seqs = cells.BuildSequences();
                Assert.True(seqs.Length >= 2, $"{name} has {seqs.Length} sequences, needs a spin and an open");
                Assert.True(Opaque(cells.RenderCellRgba(seqs[1].Frames[0].Cell)) > 50, $"{name} draws nothing");

                var arc = SpaArchive.Parse(bursts.Get(SendOutGraphics.BurstEntry(ball)));
                Assert.True(arc != null && arc.Emitters.Count > 0, $"{name} has no burst");
            }
        }

        [SkippableTheory]
        [InlineData("CPUE", "Platinum")]
        [InlineData("IPKE", "HeartGold")]
        public void ThePartyRowHasItsBallsAndBars(string code, string game)
        {
            Load(code, game, DirNames.battleObj);
            var g = new SendOutGraphics();
            var seqs = g.PartyRowSequences();
            Assert.Equal(9, seqs.Length);
            var cells = new WeCellAnimRenderer();
            Assert.True(cells.Load(DirNames.battleObj, 340, DirNames.battleObj, 110, DirNames.battleObj, 341, DirNames.battleObj, 342));
            Assert.Equal(8, seqs[SendOutGraphics.RowPlayerHealthy].Frames.Length);
            Assert.True(Opaque(cells.RenderCellRgba(seqs[SendOutGraphics.RowPlayerHealthy].Frames[0].Cell)) > 40, "a healthy ball draws");
            Assert.True(Opaque(cells.RenderCellRgba(seqs[SendOutGraphics.RowEmpty].Frames[0].Cell)) > 10, "an empty slot draws");
            // The bars are 192-pixel cells; HeartGold leaves the last few columns clear.
            var bar = cells.RenderCellRgba(seqs[SendOutGraphics.RowPlayerBar].Frames[0].Cell);
            int left = 256, right = -1;
            for (int y = 0; y < 256; y++)
                for (int x = 0; x < 256; x++)
                    if (bar.Rgba[(y * 256 + x) * 4 + 3] != 0) { left = Math.Min(left, x); right = Math.Max(right, x); }
            Assert.InRange(right - left + 1, 180, 192);
        }

        [SkippableTheory]
        [InlineData("CPUE", "Platinum", 65, 3)]    // Aaron holds a pose from his third animation during the slide
        [InlineData("IPKE", "HeartGold", 66, 3)]   // so does Falkner
        public void AnimatedTrainersHaveTheirLandingAnimations(string code, string game, int trainerClass, int sequences)
        {
            Load(code, game, DirNames.trainerGraphics);
            var g = new SendOutGraphics();
            Assert.Equal(sequences, g.EnemyTrainerSequenceCount(trainerClass));
            Assert.True(g.EnemyTrainerSequenceTicks(trainerClass, 1) > 0);
        }

        // Pose times in ticks; only the long follow-through differs between the games.
        [SkippableTheory]
        [InlineData("CPUE", "Platinum", 28)]
        [InlineData("IPKE", "HeartGold", 26)]
        public void YourTrainerThrowsWithTheRomsOwnPoseTimes(string code, string game, int followThrough)
        {
            Load(code, game, DirNames.trainerBackGraphics);
            var r = new TrainerClassSpriteRenderer();
            r.Load(0, DirNames.trainerBackGraphics);
            var ticks = Enumerable.Range(0, r.FrameCount).Select(i => r.GetFrameDuration(i) / 2).ToArray();
            _out.WriteLine(string.Join(",", ticks));
            Assert.Equal(new[] { 4, 8, 3, 1, 2, 1, 1, followThrough, 1 }, ticks.Take(9));
        }

        [SkippableFact]
        public void ASendOutStartsTheFramesAndMovementWhenThePokemonLands()
        {
            // HGSS Aggron: frame 0 for 18+1 ticks then frame 1, while script 3 pulses the scale. It lands on tick 60.
            Load("IPKE", "HeartGold", DirNames.pokemonSpriteOffsets, DirNames.pokeAnim, DirNames.pokeAnimDefs);
            var vm = new BattleDisplayEditorViewModel();
            vm.LoadMon(306);
            vm.SendOutKindIndex = (int)SendOutKind.Wild;
            vm.SideIndex = (int)PreviewSides.Theirs;
            vm.ToggleSendOutPlayback();
            Assert.True(vm.IsPlaying);

            for (int i = 1; i <= 60; i++) vm.GameTick();
            Assert.Equal(1.0, vm.AnimScaleX);
            Assert.Equal(0, vm.FrontFrameShown);

            for (int i = 0; i < 18; i++) vm.GameTick();
            Assert.Equal(0, vm.FrontFrameShown);
            vm.GameTick();
            Assert.Equal(1, vm.FrontFrameShown);
            Assert.True(vm.AnimScaleX < 1, $"scale {vm.AnimScaleX} 19 ticks after landing, the pulse should be running");

            // The message prints once the animation is over, a letter at a time.
            string seen = "";
            for (int i = 0; i < 1000 && vm.IsPlaying; i++)
            {
                vm.GameTick();
                if (vm.IsPlaying && vm.MessageBoxText.Length > seen.Length) seen = vm.MessageBoxText;
            }
            Assert.False(vm.IsPlaying);
            Assert.StartsWith("A wild ", seen);
            Assert.True(vm.EnemyShown && vm.PlayerShown);
            Assert.Equal("WHAT WILL AGGRON DO?", vm.MessageBoxText.ToUpperInvariant());
        }

        private static int Opaque(WeCellAnimRenderer.CellPixels pixels)
        {
            int n = 0;
            if (pixels.Rgba != null) for (int i = 3; i < pixels.Rgba.Length; i += 4) if (pixels.Rgba[i] != 0) n++;
            return n;
        }

        private static void Load(string code, string game, params DirNames[] dirs)
        {
            string project = code == "IPKE" ? TestRoms.HeartGold : TestRoms.Platinum;
            Skip.If(!Directory.Exists(project), $"{game} not unpacked here");
            SettingsManager.Load();
            new RomInfo(code, project);
            DSUtils.TryUnpackNarcs(dirs.ToList());
        }
    }
}
