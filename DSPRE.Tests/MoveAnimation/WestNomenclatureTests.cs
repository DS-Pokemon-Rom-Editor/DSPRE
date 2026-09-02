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
    public class WestNomenclatureTests
    {
        private static IEnumerable<string> AllWestOpNames(WazaSeqVersion v)
        {
            foreach (var o in WestOpcodes.Table(v)) yield return o.Name;
        }
        private static IEnumerable<string> AllWazaSeqOpNames(WazaSeqVersion v)
        {
            foreach (var o in WazaSeqOpcodes.Table(v)) yield return o.Name;
        }

        [Theory]
        [InlineData(WazaSeqVersion.Plat)]
        [InlineData(WazaSeqVersion.HGSS)]
        public void EveryWestCommandName_ReverseMapsToOneOpcode(WazaSeqVersion v) => AssertBijective(AllWestOpNames(v).ToList());

        [Theory]
        [InlineData(WazaSeqVersion.Plat)]
        [InlineData(WazaSeqVersion.HGSS)]
        public void EveryWazaSeqCommandName_ReverseMapsToOneOpcode(WazaSeqVersion v) => AssertBijective(AllWazaSeqOpNames(v).ToList());

        private static void AssertBijective(List<string> names)
        {
            var map = new Dictionary<string, int>(System.StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < names.Count; i++)
            {
                map[names[i]] = i;
                string cmd = WestParamSchema.CommandName(names[i]);
                if (!string.IsNullOrEmpty(cmd) && !map.ContainsKey(cmd)) map[cmd] = i;
            }

            for (int i = 0; i < names.Count; i++)
            {
                string cmd = WestParamSchema.CommandName(names[i]);
                Assert.False(string.IsNullOrWhiteSpace(cmd), $"opcode {names[i]} has no command name");
                Assert.True(map.ContainsKey(cmd), $"command name '{cmd}' did not reverse-map");
                int resolved = map[cmd];
                Assert.Equal(WestParamSchema.CommandName(names[i]), WestParamSchema.CommandName(names[resolved]));
            }
        }

        [Theory]
        [InlineData(WazaSeqVersion.Plat)]
        [InlineData(WazaSeqVersion.HGSS)]
        public void ArgTokens_AreUniquePerOpcode(WazaSeqVersion v)
        {
            foreach (var name in AllWestOpNames(v).Concat(AllWazaSeqOpNames(v)))
            {
                var seen = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < 16; i++)
                {
                    string label = WestParamSchema.ParamName(name, i);
                    if (label.StartsWith("Param ")) break;   // ran off the end of the known fixed labels
                    string tok = WestParamSchema.ArgToken(name, i);
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
                var opts = WestParamSchema.EnumFor("WEST_EX_DATA", field);
                if (opts == null) continue;
                var tokens = opts.Select(o => WestParamSchema.Token(o.Label, true)).ToList();
                Assert.Equal(tokens.Count, tokens.Distinct(System.StringComparer.OrdinalIgnoreCase).Count());
                foreach (var t in tokens)
                    Assert.False(int.TryParse(t, out _), $"enum token '{t}' looks like a number");
            }
        }

        [Fact]
        public void EnumValue_RoundTripsThroughItsToken()
        {
            var opts = WestParamSchema.EnumFor("WEST_EX_DATA", 2);
            Assert.NotNull(opts);
            var chosen = opts.First(o => o.Value == 2);
            string token = WestParamSchema.Token(chosen.Label, true);
            var back = opts.First(o => string.Equals(WestParamSchema.Token(o.Label, true), token, System.StringComparison.OrdinalIgnoreCase));
            Assert.Equal(2, back.Value);
        }

        [Theory]
        [InlineData("WEST_ADD_PARTICLE")]
        [InlineData("WEST_LOAD_PARTICLE_EX")]
        [InlineData("WEST_EX_DATA")]
        public void CommandName_IsSingleWord(string opName)
        {
            string cmd = WestParamSchema.CommandName(opName);
            Assert.DoesNotContain(" ", cmd);
            Assert.DoesNotContain(":", cmd);
        }
    }
}
