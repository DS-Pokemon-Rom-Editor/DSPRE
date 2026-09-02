using System;
using System.Collections.Generic;
using System.Linq;
using DSPRE.ROMFiles;
using Xunit;

namespace DSPRE.Tests
{
    /// <summary>
    /// Telling a plain number in a script apart from a variable, and the message commands that were being
    /// read as though their variables were values.
    /// </summary>
    public class ScriptValueTests
    {
        private static ScriptCommand Cmd(string display, params long[] values)
            => new ScriptCommand(display, values.Select(v => BitConverter.GetBytes((ushort)v)).ToList());

        private static ScriptWalker Walker(IEnumerable<ScriptCommand> script, Func<int, string> messages = null)
            => new ScriptWalker(
                new List<ScriptCommandContainer>
                {
                    new ScriptCommandContainer(1, ScriptFile.ContainerTypes.Script, -1, script.ToList()),
                },
                new List<ScriptCommandContainer>(), messages);

        // ── the ranges ──────────────────────────────────────────────────────────────────
        [Theory]
        [InlineData(0, false)]
        [InlineData(5, false)]
        [InlineData(0x3FFF, false)]      // still just a number
        [InlineData(0x4000, true)]       // SVWK_START: the first saved variable
        [InlineData(0x7FFF, true)]
        [InlineData(0x8000, true)]       // SCWK_START: the script's own slots
        [InlineData(0x800C, true)]
        public void OnlyNumbersFromTheVariableRangeUpAreVariables(int value, bool isVar)
            => Assert.Equal(isVar, FieldScriptValues.IsVariable(value));

        [Theory]
        [InlineData(0x8000, "PARAM0")]
        [InlineData(0x8004, "TEMP0")]
        [InlineData(0x8008, "REG0")]
        [InlineData(0x800C, "ANSWER")]
        [InlineData(0x800D, "TARGET_OBJID")]
        public void TheScriptsOwnSlotsAreKnownByName(int value, string name)
            => Assert.Equal(name, FieldScriptValues.NameOf(value));

        [Fact]
        public void APlainNumberReadsAsItselfAndAVariableReadsAsWhatItIs()
        {
            Assert.Equal("2", FieldScriptValues.Describe(2));
            Assert.Equal("ANSWER", FieldScriptValues.Describe(0x800C));
            Assert.Equal("variable 0x4001", FieldScriptValues.Describe(0x4001));
            Assert.Equal("script slot 0x8020", FieldScriptValues.Describe(0x8020));
        }

        // ── the commands that were wrong ────────────────────────────────────────────────
        [Fact]
        public void TheSharedArchiveCommandSaysWhichArchiveAndWhereItPutsIt()
        {
            // GetCommonMessageArchive 2, 0x800c: archive 2 of four, stored in ANSWER. It shows nothing.
            var w = Walker(new[] { Cmd("GetCommonMessageArchive 2 32780", 2, 0x800C), Cmd("End") });
            w.Start(1);

            var step = w.Steps.First(s => s.CommandName == "GetCommonMessageArchive");
            Assert.Equal(ScriptStepKind.Command, step.Kind);       // nothing is shown by it
            Assert.Contains("cameraman", step.Text);
            Assert.Contains("ANSWER", step.Text);
            Assert.DoesNotContain("32780", step.Text);             // not a raw number any more
        }

        [Fact]
        public void TheFourSharedArchivesAreTheOnesTheTableHolds()
        {
            Assert.Equal(4, FieldSharedMessageArchives.Count);
            Assert.NotNull(FieldSharedMessageArchives.NameOf(0));
            Assert.NotNull(FieldSharedMessageArchives.NameOf(3));
            Assert.Null(FieldSharedMessageArchives.NameOf(4));
        }

        [Fact]
        public void AMessageSlotHoldingAVariableSaysSoRatherThanInventingALine()
        {
            // Looking message 0x800c up in the text archive would be nonsense; it is a variable.
            var w = Walker(new[] { Cmd("MessageFlex 32780", 0x800C), Cmd("End") },
                           id => $"line {id}");
            w.Start(1);

            var step = w.Steps.First(s => s.Kind == ScriptStepKind.Message);
            Assert.Contains("ANSWER", step.Text);
            Assert.DoesNotContain("line 32780", step.Text);
        }

        [Fact]
        public void AnOrdinaryMessageStillReadsItsRealText()
        {
            var w = Walker(new[] { Cmd("Message 5", 5), Cmd("End") }, id => id == 5 ? "Hello there!" : null);
            w.Start(1);
            Assert.Contains("Hello there!", w.Steps.First(s => s.Kind == ScriptStepKind.Message).Text);
        }

        [Fact]
        public void AGenderMessageShowsOneLineAndNamesTheOther()
        {
            var w = Walker(new[] { Cmd("GenderMessage 3 4", 3, 4), Cmd("End") },
                           id => id == 3 ? "Hello, sir." : "Hello, madam.");
            w.Start(1);

            var step = w.Steps.First(s => s.Kind == ScriptStepKind.Message);
            Assert.Contains("Hello, sir.", step.Text);   // the male line is the one shown
            Assert.Contains("4", step.Text);             // and the female one is named
        }

        [Fact]
        public void AMessageFromAnArchiveNamesBothHalves()
        {
            var w = Walker(new[] { Cmd("MessageFromArchive 32780 6", 0x800C, 6), Cmd("End") });
            w.Start(1);

            var step = w.Steps.First(s => s.Kind == ScriptStepKind.Message);
            Assert.Contains("ANSWER", step.Text);
            Assert.Contains("6", step.Text);
        }

        [Fact]
        public void ASignIsPutUpShownWrittenIntoAndTakenDown()
        {
            var w = Walker(new[]
            {
                Cmd("SetTextBoard 1 0", 1, 0),
                Cmd("ShowBoard 0", 0),
                Cmd("BoardMessage 7 0", 7, 0),
                Cmd("CloseBoard 0", 0),
                Cmd("End"),
            }, id => id == 7 ? "NEW BARK TOWN" : null);
            w.Start(1);

            Assert.Contains(w.Steps, s => s.CommandName == "SetTextBoard" && s.Text.Contains("sign"));
            Assert.Contains(w.Steps, s => s.CommandName == "ShowBoard" && s.Text.Contains("Shows the sign"));
            Assert.Contains(w.Steps, s => s.CommandName == "CloseBoard" && s.Text.Contains("down"));

            // Only the writing puts words on screen, and it is a message so the box holds for it.
            var msg = w.Steps.First(s => s.CommandName == "BoardMessage");
            Assert.Equal(ScriptStepKind.Message, msg.Kind);
            Assert.Contains("NEW BARK TOWN", msg.Text);
        }

        [Fact]
        public void ComparingAgainstAVariableNamesItRatherThanPrintingItsNumber()
        {
            var w = Walker(new[] { Cmd("CompareVarValue 32780 1", 0x800C, 1), Cmd("End") });
            w.Start(1);
            Assert.NotNull(w.Pending);
            Assert.Contains("ANSWER", w.Pending.Prompt);
        }
    }
}
