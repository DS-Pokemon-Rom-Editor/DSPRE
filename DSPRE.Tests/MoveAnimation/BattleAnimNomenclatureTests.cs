using System.Collections.Generic;
using System.Linq;
using Xunit;
using DSPRE.Avalonia.Data;

namespace DSPRE.Tests
{
    /// <summary>
    /// Guards the single-word command nomenclature used by the move-animation / effect-script text editor:
    /// every opcode's command word and every argument's label/enum token must reverse-map unambiguously, so a
    /// "CommandName label=value" text line round-trips losslessly back to the bytecode.
    /// </summary>
    public class BattleAnimNomenclatureTests
    {
        private static IEnumerable<string> AllAnimationCommandNames(WazaSeqVersion v)
        {
            foreach (var o in BattleAnimCommands.Table(v)) yield return o.Name;
        }
        private static IEnumerable<string> AllBattleScriptCommandNames(WazaSeqVersion v)
        {
            foreach (var o in WazaSeqOpcodes.Table(v)) yield return o.Name;
        }

        [Theory]
        [InlineData(WazaSeqVersion.Plat)]
        [InlineData(WazaSeqVersion.HGSS)]
        public void EveryAnimationCommandName_MapsToOneCommand(WazaSeqVersion v) => AssertBijective(AllAnimationCommandNames(v).ToList(), false);

        [Theory]
        [InlineData(WazaSeqVersion.Plat)]
        [InlineData(WazaSeqVersion.HGSS)]
        public void EveryBattleScriptCommandName_MapsToOneCommand(WazaSeqVersion v) => AssertBijective(AllBattleScriptCommandNames(v).ToList(), true);

        private static void AssertBijective(List<string> names, bool script)
        {
            var twice = names.GroupBy(n => n).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
            Assert.True(twice.Count == 0, "names used by more than one command: " + string.Join(", ", twice));
            // The text view accepts a command's name or its editor word, so one must never be another command's other.
            var clashes = new List<string>();
            for (int i = 0; i < names.Count; i++)
                for (int j = 0; j < names.Count; j++)
                    if (i != j && string.Equals(names[i], BattleAnimSchema.CommandName(names[j], script), System.StringComparison.OrdinalIgnoreCase))
                        clashes.Add($"{names[i]} is the editor word for {names[j]}");
            Assert.True(clashes.Count == 0, string.Join("; ", clashes));

            var map = new Dictionary<string, int>(System.StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < names.Count; i++)
            {
                map[names[i]] = i;
                string cmd = BattleAnimSchema.CommandName(names[i], script);
                if (!string.IsNullOrEmpty(cmd) && !map.ContainsKey(cmd)) map[cmd] = i;
            }

            for (int i = 0; i < names.Count; i++)
            {
                string cmd = BattleAnimSchema.CommandName(names[i], script);
                Assert.False(string.IsNullOrWhiteSpace(cmd), $"opcode {names[i]} has no command name");
                Assert.True(map.ContainsKey(cmd), $"command name '{cmd}' did not reverse-map");
                int resolved = map[cmd];
                Assert.Equal(BattleAnimSchema.CommandName(names[i], script), BattleAnimSchema.CommandName(names[resolved], script));
            }
        }

        [Theory]
        [InlineData(WazaSeqVersion.Plat)]
        [InlineData(WazaSeqVersion.HGSS)]
        public void ArgTokens_AreUniquePerOpcode(WazaSeqVersion v)
        {
            foreach (var (name, script) in AllAnimationCommandNames(v).Select(n => (n, false)).Concat(AllBattleScriptCommandNames(v).Select(n => (n, true))))
            {
                var seen = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < 16; i++)
                {
                    string label = BattleAnimSchema.ParamName(name, i, script);
                    if (label.StartsWith("Param ")) break;   // ran off the end of the known fixed labels
                    string tok = BattleAnimSchema.ArgToken(name, i, script);
                    Assert.False(string.IsNullOrWhiteSpace(tok), $"{name} arg {i} has no token");
                    Assert.True(seen.Add(tok), $"{name} has a duplicate arg token '{tok}' at index {i}");
                }
            }
        }

        [Fact]
        public void OperatorSettings_EnumTokensAreUnambiguousPerField()
        {
            // For the operator-settings command, each enum field's tokens must be distinct AND not collide with
            // a plain integer literal, so "target=Attacker" round-trips and "position=3" still parses as a number.
            for (int field = 0; field <= 6; field++)
            {
                var opts = BattleAnimSchema.EnumFor("SetExtraParams", field);
                if (opts == null) continue;
                var tokens = opts.Select(o => BattleAnimSchema.Token(o.Label, true)).ToList();
                Assert.Equal(tokens.Count, tokens.Distinct(System.StringComparer.OrdinalIgnoreCase).Count());
                foreach (var t in tokens)
                    Assert.False(int.TryParse(t, out _), $"enum token '{t}' looks like a number");
            }
        }

        [Fact]
        public void EnumValue_RoundTripsThroughItsToken()
        {
            var opts = BattleAnimSchema.EnumFor("SetExtraParams", 2);
            Assert.NotNull(opts);
            var chosen = opts.First(o => o.Value == 2);
            string token = BattleAnimSchema.Token(chosen.Label, true);
            var back = opts.First(o => string.Equals(BattleAnimSchema.Token(o.Label, true), token, System.StringComparison.OrdinalIgnoreCase));
            Assert.Equal(2, back.Value);
        }

        [Theory]
        [InlineData("CreateEmitter")]
        [InlineData("LoadDebugParticleSystem")]
        [InlineData("SetExtraParams")]
        public void CommandName_IsSingleWord(string opName)
        {
            string cmd = BattleAnimSchema.CommandName(opName);
            Assert.DoesNotContain(" ", cmd);
            Assert.DoesNotContain(":", cmd);
        }
    }
}
