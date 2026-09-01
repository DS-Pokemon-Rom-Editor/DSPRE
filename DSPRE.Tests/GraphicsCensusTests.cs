using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using DSPRE;
using DSPRE.Avalonia.Data;
using NarcAPI;
using Xunit;
using Xunit.Abstractions;
using static DSPRE.RomInfo;

namespace DSPRE.Tests
{
    /// <summary>
    /// What is actually inside every archive the games have.
    ///
    /// Written because the archive names are suggestive but not reliable. A name says what somebody meant
    /// the archive for, not what is in it, and several hold something other than what they sound like.
    /// So every archive is opened and every file in it identified by its first four bytes, which is what
    /// the format itself says it is.
    ///
    /// This is the list the graphics browser has to cover, and the check that stops an archive appearing
    /// in the games without anyone noticing.
    /// </summary>
    [Collection("rom")]
    public class GraphicsCensusTests
    {
        private readonly ITestOutputHelper _out;
        public GraphicsCensusTests(ITestOutputHelper o) { _out = o; }

        private const string Diamond =
            @"C:\Romhacking\ROMs\NDS\DP\Pokemon Diamond (v05) (U)(Legacy)\1015 - Pokemon Diamond (v05) (U)(Legacy)_DSPRE_contents";
        private const string Platinum =
            @"C:\Romhacking\ROMs\NDS\Plat\Pokemon - Platinum Version (USA) (Rev 1)\Pokemon - Platinum Version (USA) (Rev 1)_DSPRE_contents";
        private const string HeartGold =
            @"C:\Romhacking\ROMs\NDS\HGSS\HeartGold (USA)_DSPRE_contents";

        private static readonly (string code, string path, string name)[] Games =
        {
            ("ADAE", Diamond,  "Diamond"),
            ("CPUE", Platinum, "Platinum"),
            ("IPKE", HeartGold, "HeartGold"),
        };

        private static readonly string Doc =
            @"C:\Romhacking\Tooling\DSPRE\Research\Graphics\GraphicsCensus.md";

        private static readonly string RepoRoot = @"C:\Romhacking\Tooling\DSPRE";

        /// <summary>
        /// What a file says it is, from its first four bytes. Nitro formats write their tag backwards, so
        /// an NCLR palette starts "RLCN". Anything not listed is reported by its tag rather than guessed at.
        /// </summary>
        private static readonly Dictionary<string, string> Tags = new(StringComparer.Ordinal)
        {
            ["RLCN"] = "palette",              // NCLR
            ["RGCN"] = "tile graphic",         // NCGR
            ["RECN"] = "cell layout",          // NCER
            ["RNAN"] = "cell animation",       // NANR
            ["RCSN"] = "tile map",             // NSCR
            ["RNCN"] = "unpacked graphic",     // NCEC / rarely seen
            ["BMD0"] = "3D model",             // NSBMD
            ["BTX0"] = "3D texture bundle",    // NSBTX
            ["BCA0"] = "3D joint animation",   // NSBCA
            ["BTA0"] = "3D texture animation", // NSBTA
            ["BTP0"] = "3D texture swap",      // NSBTP
            ["BVA0"] = "3D visibility anim",   // NSBVA
            ["BMA0"] = "3D material anim",     // NSBMA
            ["NARC"] = "nested archive",
            ["SDAT"] = "sound archive",
            ["SSEQ"] = "music sequence",
            ["SSAR"] = "sequence archive",
            ["SBNK"] = "instrument bank",
            ["SWAR"] = "sound sample set",
            ["STRM"] = "streamed sound",
            ["RIFF"] = "wave sound",
        };

        private static string Identify(byte[] b)
        {
            if (b == null || b.Length == 0) return "empty";
            // Several archives store their contents squeezed down. Unsqueeze first, or every one of them
            // reads as "starts with 0x10" and nothing is learned.
            byte[] d = b;
            if (b.Length > 4 && (b[0] == 0x10 || b[0] == 0x11 || b[0] == 0x24 || b[0] == 0x28 || b[0] == 0x40))
            {
                try { var u = NitroBgCodec.Inflate(b); if (u != null && u.Length >= 4) d = u; } catch { }
            }
            if (d.Length < 4) return "too short to tell";
            string tag = Encoding.ASCII.GetString(d, 0, 4);
            if (Tags.TryGetValue(tag, out string what)) return what;
            bool printable = tag.All(c => c >= 32 && c < 127);
            return printable ? $"unknown ({tag})" : "raw data";
        }

        /// <summary>Which source files mention an archive, so "which editor covers this" stays true by
        /// itself instead of being a list somebody has to remember to update.</summary>
        private static Dictionary<string, List<string>> BuildEditorMap()
        {
            var map = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            foreach (var dir in new[] { "DSPRE.Avalonia", "DS_Map" })
            {
                string root = Path.Combine(RepoRoot, dir);
                if (!Directory.Exists(root)) continue;
                foreach (var f in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
                {
                    string text;
                    try { text = File.ReadAllText(f); } catch { continue; }
                    string leaf = Path.GetFileNameWithoutExtension(f);
                    // only the screens a person opens, not the plumbing
                    bool isEditor = leaf.EndsWith("ViewModel", StringComparison.Ordinal)
                                 || leaf.EndsWith("Editor", StringComparison.Ordinal);
                    if (!isEditor) continue;
                    foreach (var name in Enum.GetNames(typeof(DirNames)))
                    {
                        if (!text.Contains("DirNames." + name, StringComparison.Ordinal)) continue;
                        if (!map.TryGetValue(name, out var l)) map[name] = l = new List<string>();
                        string shown = leaf.Replace("ViewModel", "");
                        if (!l.Contains(shown)) l.Add(shown);
                    }
                }
            }
            return map;
        }

        [Fact]
        public void WriteWhatEveryArchiveHolds()
        {
            var editors = BuildEditorMap();
            _out.WriteLine($"{editors.Count} archives are referred to by an editor somewhere in the repo");

            // archive -> per game: (entries, the kinds inside and how many of each)
            var rows = new SortedDictionary<string, Dictionary<string, (int entries, SortedDictionary<string, int> kinds)>>(
                StringComparer.Ordinal);
            var gamesSeen = new List<string>();

            foreach (var (code, path, name) in Games)
            {
                if (!Directory.Exists(path)) { _out.WriteLine($"{name}: not unpacked here, skipped"); continue; }
                try { new RomInfo(code, path); }
                catch (Exception ex) { _out.WriteLine($"{name}: would not load ({ex.Message}), skipped"); continue; }
                gamesSeen.Add(name);

                foreach (var kvp in gameDirs)
                {
                    string archive = kvp.Key.ToString();

                    // Read the PACKED archive, not the unpacked folder. DSPRE only unpacks an archive when
                    // an editor that needs it is opened, so going by the folders reports whatever this
                    // particular project happens to have been used for. Nineteen archives were missed that
                    // way. The packed file is always there.
                    int count;
                    Func<int, byte[]> read;
                    string packed = kvp.Value.packedDir;
                    string unpacked = kvp.Value.unpackedDir;

                    if (!string.IsNullOrEmpty(packed) && File.Exists(packed))
                    {
                        NarcAPI.Narc narc;
                        try { narc = NarcAPI.Narc.Open(packed); }
                        catch { continue; }
                        count = narc.ElementCount;
                        read = i => { try { return narc.GetElementBytes(i); } catch { return null; } };
                    }
                    else if (!string.IsNullOrEmpty(unpacked) && Directory.Exists(unpacked))
                    {
                        string[] files;
                        try { files = RomFiles.Settled(unpacked); } catch { continue; }
                        count = files.Length;
                        read = i => { try { return File.ReadAllBytes(files[i]); } catch { return null; } };
                    }
                    else continue;

                    if (count == 0) continue;

                    var kinds = new SortedDictionary<string, int>(StringComparer.Ordinal);
                    // Reading every file of every archive of three games is far more than is needed to say
                    // what an archive holds. Up to 40 spread through it catches the mixed ones.
                    int step = Math.Max(1, count / 40);
                    int looked = 0;
                    for (int i = 0; i < count; i += step)
                    {
                        byte[] b = read(i);
                        if (b == null) continue;
                        string what = Identify(b);
                        kinds[what] = kinds.TryGetValue(what, out int n) ? n + 1 : 1;
                        looked++;
                    }
                    if (looked == 0) continue;
                    int filesLength = count;

                    if (!rows.TryGetValue(archive, out var per))
                        rows[archive] = per = new Dictionary<string, (int, SortedDictionary<string, int>)>(StringComparer.Ordinal);
                    per[name] = (filesLength, kinds);
                }
            }

            Assert.True(gamesSeen.Count > 0, "no game could be read, so this proves nothing");
            Assert.True(rows.Count > 20, $"only {rows.Count} archives were read, which is too few to be the whole set");

            string Kinds(SortedDictionary<string, int> k) =>
                string.Join(", ", k.OrderByDescending(x => x.Value).Select(x => x.Key));

            var sb = new StringBuilder();
            sb.Append("[Research](../ResearchNotes.md) / Graphics Census\n\n");
            sb.Append("# What is inside every archive\n\n");
            sb.Append("Generated by `GraphicsCensusTests`. Do not edit by hand.\n\n");
            sb.Append("Every file is identified by its own first four bytes, not by the archive's name, because a\n");
            sb.Append("name says what somebody meant an archive for and not what ended up in it. Files stored\n");
            sb.Append("squeezed down are unsqueezed first. Up to forty files spread through each archive are read,\n");
            sb.Append("which is enough to notice an archive holding more than one kind of thing.\n\n");
            sb.Append($"Games read: {string.Join(", ", gamesSeen)}. Archives found: {rows.Count}.\n\n");

            sb.Append("| archive | what is in it | entries | editor |\n|---|---|---|---|\n");
            foreach (var kv in rows)
            {
                var per = kv.Value;
                var allKinds = new SortedDictionary<string, int>(StringComparer.Ordinal);
                foreach (var g in per.Values)
                    foreach (var k in g.kinds)
                        allKinds[k.Key] = allKinds.TryGetValue(k.Key, out int n) ? n + k.Value : k.Value;
                string counts = string.Join(" / ", gamesSeen.Where(per.ContainsKey).Select(g => per[g].entries.ToString()));
                string ed = editors.TryGetValue(kv.Key, out var l) ? string.Join(", ", l) : "**none**";
                sb.Append($"| {kv.Key} | {Kinds(allKinds)} | {counts} | {ed} |\n");
            }

            sb.Append("\nEntry counts are listed in the order the games are named above, for the games that have\n");
            sb.Append("that archive.\n");

            // What the checked-in table said before this run, so a new archive appearing in the games, or
            // an archive losing its editor, fails here instead of going unnoticed. Only the archive names
            // and their editors are compared, not the entry counts: those move with the ROM revision
            // somebody happens to have, and failing on that would be noise.
            var wasListed = new HashSet<string>(StringComparer.Ordinal);
            var hadEditor = new HashSet<string>(StringComparer.Ordinal);
            if (File.Exists(Doc))
            {
                foreach (var line in File.ReadAllLines(Doc))
                {
                    if (!line.StartsWith("| ", StringComparison.Ordinal)) continue;
                    var cells = line.Split('|');
                    if (cells.Length < 5) continue;
                    string a = cells[1].Trim();
                    if (a.Length == 0 || a == "archive" || a.StartsWith("-", StringComparison.Ordinal)) continue;
                    wasListed.Add(a);
                    if (!cells[4].Contains("none", StringComparison.Ordinal)) hadEditor.Add(a);
                }
            }

            Directory.CreateDirectory(Path.GetDirectoryName(Doc));
            File.WriteAllText(Doc, sb.ToString());

            if (wasListed.Count > 0)
            {
                var appeared = rows.Keys.Where(k => !wasListed.Contains(k)).ToList();
                var lostEditor = rows.Keys.Where(k => hadEditor.Contains(k) && !editors.ContainsKey(k)).ToList();
                Assert.True(appeared.Count == 0,
                    $"the games have {appeared.Count} archives the census document does not list, so the "
                    + $"graphics browser has not been told about them: {string.Join(", ", appeared)}. "
                    + "The document has been rewritten with them in.");
                Assert.True(lostEditor.Count == 0,
                    $"{lostEditor.Count} archives had an editor and no longer do: {string.Join(", ", lostEditor)}");
            }

            int uncovered = rows.Keys.Count(k => !editors.ContainsKey(k));
            _out.WriteLine($"{rows.Count} archives across {gamesSeen.Count} games; {uncovered} have no editor");
            foreach (var kv in rows.Where(r => !editors.ContainsKey(r.Key)).Take(40))
            {
                var allKinds = new SortedDictionary<string, int>(StringComparer.Ordinal);
                foreach (var g in kv.Value.Values)
                    foreach (var k in g.kinds) allKinds[k.Key] = 1;
                _out.WriteLine($"  no editor: {kv.Key} ({Kinds(allKinds)})");
            }
            _out.WriteLine("written to " + Doc);
        }
    }
}
