using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DSPRE;
using DSPRE.Avalonia.Data;
using Xunit;
using Xunit.Abstractions;
using static DSPRE.RomInfo;

namespace DSPRE.Tests
{
    /// <summary>No bare number is left on screen where the games have a name for it.</summary>
    [Collection("rom")]
    public class WestNamedValueTests
    {
        private readonly ITestOutputHelper _out;
        public WestNamedValueTests(ITestOutputHelper o) { _out = o; }

        private static readonly string HeartGold = TestRoms.HeartGold;
        private static readonly string Platinum = TestRoms.Platinum;

        private static string ScriptDir(string project, string gameCode)
        {
            if (!Directory.Exists(project)) return null;
            try { new RomInfo(gameCode, project); } catch { return null; }
            var narc = new ScriptNarc(DirNames.wazaEffectScripts);
            return narc.Available ? gameDirs[DirNames.wazaEffectScripts].unpackedDir : null;
        }

        private static List<WazaSeqCommand> Load(byte[] bytes, WazaSeqVersion v)
        {
            var cmds = WestScript.Parse(bytes, v);
            int pos = 0;
            foreach (var c in cmds) { c.WordPos = pos; pos += 1 + c.Args.Length; }
            return cmds;
        }

        /// <summary>What should appear instead of the number, or null when nothing can name it.</summary>
        private static string NameFor(string opName, int[] args, int index, WazaSeqVersion version)
        {
            if (opName is "WEST_FUNC_CALL" or "WEST_OLDACT_FUNC_CALL")
            {
                if (index == 0) return WestScriptDisplay.RoutineName(args[0]);
                if (index == 1) return null;                 // the word count, a plain number
                string meaning = WestRoutines.WordMeaning(args[0], index - 2);
                if (meaning != null && meaning.Contains("target flag"))
                    return WestTargetFlags.Describe(args[index], brief: true);
                return null;
            }

            var options = WestParamSchema.EnumFor(opName, index);
            if (options != null)
                foreach (var o in options)
                    if (o.Value == args[index]) return o.Label;

            return null;
        }

        [Fact]
        public void EveryHeartGoldValueWithANameShowsItsName() => Sweep(HeartGold, "IPKE", WazaSeqVersion.HGSS);

        [Fact]
        public void EveryPlatinumValueWithANameShowsItsName() => Sweep(Platinum, "CPUE", WazaSeqVersion.Plat);

        private void Sweep(string project, string gameCode, WazaSeqVersion version)
        {
            string dir = ScriptDir(project, gameCode);
            Assert.True(dir != null, gameCode + ": the move-effect archive could not be unpacked, so nothing was checked");

            int scripts = 0, parameters = 0, nameable = 0, named = 0, omitted = 0;
            var bare = new List<string>();
            var perSource = new SortedDictionary<string, int>();

            foreach (var f in RomFiles.Settled(dir))
            {
                var bytes = File.ReadAllBytes(f);
                if (bytes.Length == 0) continue;
                var cmds = Load(bytes, version);
                if (cmds.Count == 0) continue;
                scripts++;

                foreach (var mode in new[] { WestViewMode.Guided, WestViewMode.Script })
                {
                    var lines = WestScriptDisplay.Build(cmds, version, mode);
                    // Only the lines that stand for exactly one command: a folded shorthand shows its own
                    // settings instead, and is checked by the fold tests.
                    foreach (var line in lines)
                    {
                        if (line.IsHeading || line.Index < 0 || line.Covers != 1) continue;
                        var c = cmds[line.Index];
                        string opName = WestOpcodes.Name(version, c.OpId);
                        if (opName == null) continue;

                        for (int i = 0; i < c.Args.Length; i++)
                        {
                            parameters++;
                            string want = NameFor(opName, c.Args, i, version);
                            if (want == null) continue;
                            nameable++;
                            string source = opName is "WEST_FUNC_CALL" or "WEST_OLDACT_FUNC_CALL"
                                ? (i == 0 ? "routine name" : "target flag") : "named setting";
                            perSource[source] = perSource.TryGetValue(source, out int n) ? n + 1 : 1;

                            // A named setting can be shown by its name, or left out entirely when it is
                            // switched off.
                            string label = WestParamSchema.ParamName(opName, i);
                            if (label != null && WestParamSchema.EnumFor(opName, i) != null)
                            {
                                if (line.Text.Contains($"{label}={want}")) { named++; continue; }
                                if (!line.Text.Contains($"{label}=")) { omitted++; continue; }
                                bare.Add($"{Path.GetFileName(f)} {mode} {opName}[{i}]={c.Args[i]}: "
                                         + $"wanted \"{label}={want}\" in \"{line.Text.Trim()}\"");
                                continue;
                            }

                            if (line.Text.Contains(want)) named++;
                            else bare.Add($"{Path.GetFileName(f)} {mode} {opName}[{i}]={c.Args[i]}: "
                                          + $"wanted \"{want}\" in \"{line.Text.Trim()}\"");
                        }
                    }
                }
            }

            _out.WriteLine($"{gameCode}: {scripts} scripts, {parameters} parameters looked at across the two readable views");
            _out.WriteLine($"  {nameable} of them have a name; {named} show it, {omitted} are left out because "
                           + $"they are switched off, {bare.Count} show a bare number");
            foreach (var kv in perSource) _out.WriteLine($"  from the {kv.Key}: {kv.Value}");

            Assert.True(scripts >= 500, $"only {scripts} scripts were read");
            Assert.True(parameters > 20000, $"only {parameters} parameters were examined, so this checked very little");
            Assert.True(nameable > 3000, $"only {nameable} parameters had a name to show, so this checked very little");
            Assert.True(bare.Count == 0,
                $"{bare.Count} of {nameable} values show a number where a name exists:\n"
                + string.Join("\n", bare.Take(10)));
        }
    }
}
