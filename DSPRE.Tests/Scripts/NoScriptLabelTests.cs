using DSPRE.ROMFiles;
using Xunit;

namespace DSPRE.Tests
{
    /// <summary>Script 0 runs an End-only file, so event labels name it as no script.</summary>
    public class NoScriptLabelTests
    {
        [Fact]
        public void TriggerWithScriptZeroSaysNoScript()
        {
            var t = new Trigger(0, 0) { variableWatched = 16384, expectedVarValue = 1 };
            Assert.Equal("No script when Var 16384 is 1", t.ToString());
            t.scriptNumber = 12;
            Assert.Equal("Run script 12 when Var 16384 is 1", t.ToString());
        }

        [Fact]
        public void SpawnableWithScriptZeroSaysNoScript()
        {
            var s = new Spawnable(0, 0) { type = Spawnable.TYPE_HIDDENITEM };
            Assert.Equal("Hidden Item, [No script]", s.ToString());
            s.scriptNumber = 8001;
            Assert.Equal("Hidden Item, [Scr 8001]", s.ToString());
        }

        [Fact]
        public void LevelScriptTriggerWithScriptZeroSaysNoScript()
        {
            Assert.Equal("Starts no script", new LevelScriptTrigger(LevelScriptTrigger.MAPCHANGE, 0).ToString());
            Assert.Equal("Starts Script 3", new LevelScriptTrigger(LevelScriptTrigger.MAPCHANGE, 3).ToString());
        }
    }
}
