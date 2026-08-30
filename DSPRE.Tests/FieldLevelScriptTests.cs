using System.Collections.Generic;
using System.Linq;
using DSPRE.ROMFiles;
using Xunit;

namespace DSPRE.Tests
{
    /// <summary>
    /// What a map runs by itself. The engine looks these up with SpScriptSearch, and where each kind is
    /// asked for is what says when it runs: type 4 and type 3 during field setup (fieldmap.c:401 and
    /// :479), type 2 as the map changes (ev_mapchange.c:391), and type 1 on every step in the same check
    /// that does trainer line of sight (ev_check.c:505), but only once its variable holds the right value.
    /// </summary>
    public class FieldLevelScriptTests
    {
        private static LevelScriptFile FileWith(params LevelScriptTrigger[] triggers)
        {
            var f = new LevelScriptFile();
            foreach (var t in triggers) f.bufferSet.Add(t);
            return f;
        }

        [Fact]
        public void ArrivingRunsTheSetupScriptsBeforeTheMapChangeOne()
        {
            var file = FileWith(
                new MapScreenLoadTrigger(LevelScriptTrigger.MAPCHANGE, 100),
                new MapScreenLoadTrigger(LevelScriptTrigger.LOADGAME, 200),
                new MapScreenLoadTrigger(LevelScriptTrigger.SCREENRESET, 300));

            var order = FieldLevelScripts.OnArrival(file).Select(t => t.scriptTriggered).ToArray();

            // Field setup first (init, then objects), and the map change last.
            Assert.Equal(new[] { 200, 300, 100 }, order);
        }

        [Fact]
        public void AWatcherIsNotAnArrivalScript()
        {
            var file = FileWith(new VariableValueTrigger(50, 0x4001, 3));
            Assert.Empty(FieldLevelScripts.OnArrival(file));
            Assert.Single(FieldLevelScripts.Watchers(file));
        }

        [Fact]
        public void AWatcherOnlyFiresOnceItsVariableHoldsTheValue()
        {
            var file = FileWith(new VariableValueTrigger(50, 0x4001, 3));
            var values = new Dictionary<int, int>();
            int Value(int v) => values.TryGetValue(v, out int x) ? x : 0;

            Assert.Empty(FieldLevelScripts.ReadyToFire(file, Value));

            values[0x4001] = 2;
            Assert.Empty(FieldLevelScripts.ReadyToFire(file, Value));

            values[0x4001] = 3;
            Assert.Equal(50, FieldLevelScripts.ReadyToFire(file, Value).Single().scriptTriggered);
        }

        [Fact]
        public void AWatcherWaitingOnZeroIsReadyFromTheStart()
        {
            // A variable nobody has set reads zero, so a watcher waiting on zero is already satisfied.
            var file = FileWith(new VariableValueTrigger(7, 0x4002, 0));
            Assert.Equal(7, FieldLevelScripts.ReadyToFire(file, _ => 0).Single().scriptTriggered);
        }

        [Fact]
        public void WatchersComeBackInFileOrderSoTheFirstOneWins()
        {
            var file = FileWith(
                new VariableValueTrigger(11, 0x4001, 1),
                new VariableValueTrigger(22, 0x4002, 1),
                new VariableValueTrigger(33, 0x4003, 1));

            var ready = FieldLevelScripts.ReadyToFire(file, _ => 1);
            Assert.Equal(new[] { 11, 22, 33 }, ready.Select(t => t.scriptTriggered).ToArray());
        }

        [Fact]
        public void NothingBlowsUpOnAMapWithNoLevelScripts()
        {
            Assert.Empty(FieldLevelScripts.OnArrival(null));
            Assert.Empty(FieldLevelScripts.Watchers(null));
            Assert.Empty(FieldLevelScripts.ReadyToFire(null, _ => 0));
            Assert.Empty(FieldLevelScripts.OnArrival(FileWith()));
        }

        [Theory]
        [InlineData(LevelScriptTrigger.MAPCHANGE, "As you arrive on the map")]
        [InlineData(LevelScriptTrigger.SCREENRESET, "While the map sets up, once the music starts")]
        [InlineData(LevelScriptTrigger.LOADGAME, "While the map sets up, before anything else")]
        public void EachArrivalKindSaysWhenItRuns(int kind, string expected)
            => Assert.Equal(expected, FieldLevelScripts.WhenItRuns(new MapScreenLoadTrigger(kind, 1)));

        [Fact]
        public void AWatcherSaysWhatItIsWaitingFor()
        {
            var text = FieldLevelScripts.WhenItRuns(new VariableValueTrigger(1, 0x4010, 6));
            Assert.Contains("Every step", text);
            Assert.Contains("6", text);
        }
    }
}
