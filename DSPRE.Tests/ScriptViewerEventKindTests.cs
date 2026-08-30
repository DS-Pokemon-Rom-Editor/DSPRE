using System;
using System.Collections.Generic;
using System.Linq;
using DSPRE.ROMFiles;
using Xunit;

namespace DSPRE.Tests
{
    /// <summary>
    /// Every kind of event the player can set off hands the viewer a script id, and the viewer treats
    /// them all the same. A trainer is not a special case: its number is a script id too.
    /// </summary>
    public class ScriptViewerEventKindTests
    {
        private static ScriptCommand Cmd(string display, params long[] values)
            => new ScriptCommand(display, values.Select(v => BitConverter.GetBytes((ushort)v)).ToList());

        /// <summary>A file with one script per id, each saying which id it was.</summary>
        private static ScriptWalker WalkerFor(int scriptNumber) =>
            new ScriptWalker(
                new List<ScriptCommandContainer>
                {
                    new ScriptCommandContainer((uint)scriptNumber, ScriptFile.ContainerTypes.Script, -1,
                        new List<ScriptCommand> { Cmd($"Message {scriptNumber}", scriptNumber), Cmd("End") }),
                },
                new List<ScriptCommandContainer>(),
                id => $"this is script {id}");

        [Theory]
        [InlineData(42)]        // an ordinary person
        [InlineData(3042)]      // a trainer: still just a script
        [InlineData(5000)]      // a double battle trainer
        [InlineData(11)]        // a sign
        [InlineData(20)]        // a trigger
        public void EveryKindOfEventRunsThroughTheSameViewer(int scriptNumber)
        {
            var w = WalkerFor(scriptNumber);
            w.Start(scriptNumber);

            Assert.True(w.Finished);
            Assert.Contains(w.Steps, s => s.Kind == ScriptStepKind.Message
                                       && s.Text.Contains($"this is script {scriptNumber}"));
        }

        [Fact]
        public void ATrainerScriptIsNotTreatedDifferentlyByTheViewer()
        {
            // The only thing that marks a trainer out is that its id falls in a range which names a
            // trainer; the script itself runs exactly like anyone else's.
            var ordinary = WalkerFor(42);
            var trainer = WalkerFor(3042);
            ordinary.Start(42);
            trainer.Start(3042);

            Assert.Equal(ordinary.Steps.Count, trainer.Steps.Count);
            Assert.Equal(ordinary.Steps.Select(s => s.Kind), trainer.Steps.Select(s => s.Kind));
        }

        [Fact]
        public void TheTrainerNumberIsOnlyEverALookup()
        {
            // Reading the number straight off as a trainer would give 3042; the lookup gives 43.
            Assert.Equal(43, TrainerScripts.TrainerIdFor(3042));
            Assert.NotEqual(3042, TrainerScripts.TrainerIdFor(3042));
        }

        [Fact]
        public void ScriptsOutsideTheTrainerRangesAreJustScripts()
        {
            foreach (int id in new[] { 0, 1, 42, 2999, 7000, 9999 })
                Assert.Null(TrainerScripts.TrainerIdFor(id));
        }

        [Fact]
        public void TheTrainerRangesMeetWithoutAGap()
        {
            // Single battles run to 4999 and doubles start at 5000, so nothing falls between them.
            Assert.True(TrainerScripts.IsTrainerScript(TrainerScripts.SingleLast));
            Assert.True(TrainerScripts.IsTrainerScript(TrainerScripts.DoubleFirst));
            Assert.Equal(TrainerScripts.SingleLast + 1, TrainerScripts.DoubleFirst);
            Assert.False(TrainerScripts.IsTrainerScript(TrainerScripts.SingleFirst - 1));
            Assert.False(TrainerScripts.IsTrainerScript(TrainerScripts.DoubleLast + 1));
        }
    }
}
