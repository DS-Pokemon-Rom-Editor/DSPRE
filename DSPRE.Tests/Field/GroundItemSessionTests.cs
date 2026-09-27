using System.Collections.Generic;
using System.IO;
using System.Linq;
using DSPRE.ROMFiles;
using DSPRE.Tests.Pokemon;
using Xunit;

namespace DSPRE.Tests.Field
{
    /// <summary>The ground item list keeps its edits in memory until Save, then renumbers the item events.</summary>
    [Collection("rom")]
    public class GroundItemSessionTests
    {
        public static readonly TheoryData<string> Games = new() { "HeartGold", "Platinum" };

        // Everything a save can write: the item script (binary and plaintext), every event file, and Platinum's overlay 9.
        private static Dictionary<string, byte[]> Snapshot()
        {
            var paths = new List<string>();
            var (bin, txt) = DSPRE.ROMFiles.ScriptFile.GetFilePaths(RomInfo.itemScriptFileNumber);
            paths.Add(bin); paths.Add(txt);
            paths.AddRange(Directory.GetFiles(RomInfo.gameDirs[RomInfo.DirNames.eventFiles].unpackedDir));
            if (RomInfo.gameFamily == RomInfo.GameFamilies.Plat) paths.Add(OverlayUtils.GetPath(9));
            return paths.ToDictionary(p => p, p => File.Exists(p) ? File.ReadAllBytes(p) : null);
        }

        private static void Restore(Dictionary<string, byte[]> snap)
        {
            foreach (var (path, bytes) in snap)
            {
                if (bytes != null) File.WriteAllBytes(path, bytes);
                else if (File.Exists(path)) File.Delete(path);
            }
        }

        private static List<int> ItemEventScripts() => Enumerable.Range(0, Filesystem.GetEventFileCount())
            .SelectMany(i => new EventFile(i).overworlds)
            .Where(o => o.scriptNumber >= GroundItemScriptsLogic.ItemScrMin && o.scriptNumber <= GroundItemScriptsLogic.ItemScrMax)
            .Select(o => (int)o.scriptNumber).ToList();

        [SkippableTheory]
        [MemberData(nameof(Games))]
        public void RemovingAnUnusedEntryWaitsForSaveThenShiftsLaterEvents(string game)
        {
            GameTablesTests.Open(game);
            var session = new GroundItemScriptsLogic.Session();
            var entries = session.Entries();
            var unused = entries.FirstOrDefault(e => !e.InUse && entries.Any(l => l.InUse && l.ScriptIndex > e.ScriptIndex));
            Skip.If(unused == null, "No unused entry sits before a used one in this ROM.");

            var snap = Snapshot();
            try
            {
                var before = ItemEventScripts();
                int removedNumber = GroundItemScriptsLogic.ItemScrMin + unused.ScriptIndex;
                int later = before.Count(n => n > removedNumber);
                Assert.True(later > 0);

                Assert.Null(session.Remove(unused.ScriptIndex));
                Assert.True(session.HasChanges);
                Assert.Equal(before, ItemEventScripts());   // nothing written yet

                session.Save();
                Assert.False(session.HasChanges);
                var after = ItemEventScripts();
                Assert.Equal(before.Select(n => n > removedNumber ? n - 1 : n).OrderBy(n => n), after.OrderBy(n => n));
                Assert.Equal(entries.Count - 1, new GroundItemScriptsLogic.Session().Entries().Count);
            }
            finally { Restore(snap); }
        }

        [SkippableTheory]
        [MemberData(nameof(Games))]
        public void AnEntryAnEventUsesCantBeRemoved(string game)
        {
            GameTablesTests.Open(game);
            var session = new GroundItemScriptsLogic.Session();
            var used = session.Entries().FirstOrDefault(e => e.InUse);
            Skip.If(used == null, "No entry is used in this ROM.");
            Assert.NotNull(session.Remove(used.ScriptIndex));
            Assert.False(session.HasChanges);
        }
    }
}
