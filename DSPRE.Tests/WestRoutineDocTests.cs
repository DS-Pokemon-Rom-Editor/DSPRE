using System;
using System.IO;
using System.Linq;
using System.Text;
using DSPRE.Avalonia.Data;
using Xunit;

namespace DSPRE.Tests
{
    /// <summary>
    /// Research/Moves/Animation/MoveAnimationRoutines.md is written from the same table the editor reads,
    /// so the two cannot drift.
    /// If this fails, the expected text is written next to the file and the message says where.
    /// </summary>
    public class WestRoutineDocTests
    {
        private static string RepoRoot()
        {
            var d = new DirectoryInfo(AppContext.BaseDirectory);
            while (d != null && !File.Exists(Path.Combine(d.FullName, "DS_Map.sln"))) d = d.Parent;
            return d?.FullName;
        }

        internal static string Build()
        {
            var sb = new StringBuilder();
            sb.Append("[Research](../../ResearchNotes.md) / [Move Research](../MoveResearch.md) / Move Animation Routines\n\n");
            sb.Append("# The move-effect support routines\n\n");
            sb.Append(
@"What each routine a move-effect script can call reads out of the words handed to it, taken from its own
C body in the HeartGold leak, not from inference. This file is written from `WestRoutines.cs`, which is
what the editor itself reads, so the two cannot drift apart.

A script calls one with `FUNC_CALL id, count, words`. The id is the routine's index in
`WeSysSP_FuncTable` (`west_sp.c:218` indexes it directly, no offset) and the words land in
`waza_eff_gp_wk`. `WEST_FUNC_CALL` copies `count` words in and then **zeros the rest** of the ten
(`we_sys.h:92`), so a routine handed fewer words than it reads still runs and sees zeros; it is never
skipped. The routine ids are identical in Platinum and HeartGold, checked by comparing every
`WEST_SP_DEF_CMD` line in both `west_sp_def.h` files.

A word shown as never read is one the scripts hand over that the routine never looks at. Those are left
blank on purpose rather than invented.

Where a word picks out Pokemon it is a target flag. Those names are relative to the move, not to the
sides of the field: M1 is the attacker and E1 the defender, M2 and E2 are their allies and only exist in
a double battle, STAGE is everybody and OTHER is everybody but the attacker (`we_tool.c:1431`).

");
            foreach (var r in WestRoutines.Known.OrderBy(x => x.Id))
            {
                sb.Append($"### {r.Id}. `{r.Name}`\n\n");
                sb.Append($"{r.Summary}  \n_{r.Source}_\n\n");
                if (r.Words.Length == 0) continue;
                sb.Append("| word | meaning |\n|---:|---|\n");
                for (int i = 0; i < r.Words.Length; i++)
                    sb.Append($"| {i} | {(string.IsNullOrEmpty(r.Words[i]) ? "_never read_" : r.Words[i])} |\n");
                sb.Append('\n');
            }
            return sb.ToString();
        }

        [Fact]
        public void TheDocumentSaysWhatTheTableSays()
        {
            string root = RepoRoot();
            Assert.True(root != null, "could not find the repository root, so nothing was checked");

            string path = Path.Combine(root, "Research", "Moves", "Animation", "MoveAnimationRoutines.md");
            string want = Build().Replace("\r\n", "\n");
            string have = File.Exists(path) ? File.ReadAllText(path).Replace("\r\n", "\n") : "";

            if (want != have)
            {
                string side = path + ".expected";
                File.WriteAllText(side, want);
                Assert.Fail($"MoveAnimationRoutines.md is out of step with WestRoutines.cs. The text it should have is in {side}.");
            }

            // And the document has to be worth reading, not an empty shell.
            Assert.True(WestRoutines.Known.Count >= 77, $"only {WestRoutines.Known.Count} routines are in the table");
            Assert.Contains("target flag", want);
        }
    }
}
