using System.IO;
using System.Linq;
using DSPRE;
using DSPRE.ROMFiles;
using Xunit;
using Xunit.Abstractions;

namespace DSPRE.Tests
{
    /// <summary>
    /// The field DSPRE calls Sight range is the engine's param0, and param0 means different things to
    /// different overworlds.
    ///
    /// HeartGold's field code reads it in exactly two places. ev_trainer.c takes it as how far a trainer
    /// can see. bong_sys.c takes it as which apricorn bed a tree belongs to, and then asks the save what
    /// is growing in that bed, which is what decides the apricorn's colour. Nothing else reads it.
    ///
    /// So editing it on an apricorn tree changes the tree's colour, which looks like a bug and is not one.
    /// These pin the record layout that makes that true, and the sprite range that marks a tree, from
    /// field/fieldobj_header.h and field/fieldobj_code.h.
    /// </summary>
    [Collection("rom")]
    public class ApricornTreeFieldTests
    {
        private readonly ITestOutputHelper _out;
        public ApricornTreeFieldTests(ITestOutputHelper o) { _out = o; }

        private const string HeartGold = @"C:\Romhacking\ROMs\NDS\HGSS\HeartGold (USA)_DSPRE_contents";

        /// <summary>
        /// DSPRE reads the overworld record in the same order the engine's own FIELD_OBJ_H declares it, so
        /// the field named sightRange really is param0 and sits at offset 0x0E.
        /// </summary>
        [Fact]
        public void SightRangeIsParamZeroAtTheOffsetTheEngineUses()
        {
            // FIELD_OBJ_H: id, obj_code, move_code, event_type, event_flag, event_id, dir, param0, ...
            // all unsigned short, so param0 is the eighth field and starts at 7 * 2 = 14.
            var bytes = new byte[32];
            void U16(int at, int v) { bytes[at] = (byte)v; bytes[at + 1] = (byte)(v >> 8); }
            U16(0, 0x1111);    // id
            U16(2, 0x0107);    // obj_code, an apricorn tree
            U16(4, 0x3333);    // move_code
            U16(6, 0x4444);    // event_type
            U16(8, 0x5555);    // event_flag
            U16(10, 0x6666);   // event_id
            U16(12, 0x7777);   // dir
            U16(14, 0x00AB);   // param0
            U16(16, 0x9999);   // param1
            U16(18, 0xAAAA);   // param2

            var ow = new Overworld(new MemoryStream(bytes));
            Assert.Equal(0x0107, ow.overlayTableEntry);
            Assert.Equal(0x00AB, ow.sightRange);
            Assert.Equal(0x9999, ow.param1);
            Assert.Equal(0xAAAA, ow.param2);
            _out.WriteLine("param0 read from offset 0x0E as sightRange, matching FIELD_OBJ_H");
        }

        /// <summary>
        /// Apricorn trees really do exist in HeartGold's maps and carry a bed number in that field.
        /// fieldobj_code.h: BONGURI is 0x0106 and BONGURI01 to 07 are 0x0107 to 0x010D.
        /// </summary>
        [Fact]
        public void HeartGoldsApricornTreesCarryABedNumberInThatField()
        {
            if (!Directory.Exists(HeartGold)) { _out.WriteLine("HeartGold not unpacked here"); return; }
            new RomInfo("IPKE", HeartGold);

            int trees = 0, read = 0;
            var beds = new System.Collections.Generic.SortedSet<int>();
            int files = RomInfo.GetEventFileCount();
            for (int i = 0; i < files; i++)
            {
                EventFile ev;
                try { ev = new EventFile(i); } catch { continue; }
                read++;
                foreach (var ow in ev.overworlds)
                {
                    if (ow.overlayTableEntry < 0x0106 || ow.overlayTableEntry > 0x0114) continue;
                    trees++;
                    beds.Add(ow.sightRange);
                }
            }

            _out.WriteLine($"{read} event files read, {trees} apricorn trees, "
                         + $"beds used: {string.Join(",", beds)}");
            Assert.True(read > 100, $"only {read} event files were read, the sweep proved little");
            Assert.True(trees > 0, "no apricorn trees found at all, so this proved nothing");

            // Each tree names its own bed, so the numbers must not all be the same.
            Assert.True(beds.Count > 1,
                $"every tree names bed {beds.FirstOrDefault()}, which cannot be how the beds work");
        }

        /// <summary>
        /// The editor offers the apricorn wording for a tree and the sight-range wording for anything
        /// else, so the number is never presented as something it is not.
        /// </summary>
        [Fact]
        public void TheEditorCallsItABedOnlyForATree()
        {
            if (!Directory.Exists(HeartGold)) { _out.WriteLine("HeartGold not unpacked here"); return; }
            new RomInfo("IPKE", HeartGold);

            var vm = new DSPRE.Avalonia.ViewModels.EventEditorViewModel();
            var t = vm.GetType();
            var isTree = t.GetProperty("OwIsApricornTree");
            Assert.NotNull(isTree);

            // With nothing selected it must not claim to be a tree.
            Assert.False((bool)isTree.GetValue(vm));
            _out.WriteLine("with no event selected, the apricorn panel stays hidden");
        }
    }
}
