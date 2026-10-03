using System.Collections.Generic;
using DSPRE.Avalonia;
using DSPRE.Avalonia.ViewModels.Text;
using Xunit;

namespace DSPRE.Tests
{
    /// <summary>Matching a jump-table slot to its document symbol for "Go to script" navigation.</summary>
    public class ScriptEntryNavigationTests
    {
        [Fact]
        public void FindScriptEntryLine_MatchesJumpTableSlotExactly()
        {
            // A slot that prefix-matches another comes first, so a loose matcher picks the wrong one.
            var symbols = new List<RotomLspSymbol>
            {
                new RotomLspSymbol("script_21", 12, "#21", 20, 1),
                new RotomLspSymbol("script_2", 12, "#2", 10, 1),
                new RotomLspSymbol("script_2", 12, "#2", 30, 1),
            };

            Assert.Equal(10, ScriptEditorViewModel.FindScriptEntryLine(symbols, 2));
            Assert.Equal(20, ScriptEditorViewModel.FindScriptEntryLine(symbols, 21));

            // Symbols without a slot (labels, actions, groups) are never picked.
            Assert.Null(ScriptEditorViewModel.FindScriptEntryLine(
                new List<RotomLspSymbol> { new RotomLspSymbol("Helper", 12, null, 1, 1) }, 1));
            Assert.Null(ScriptEditorViewModel.FindScriptEntryLine(symbols, 3));
        }
    }
}
