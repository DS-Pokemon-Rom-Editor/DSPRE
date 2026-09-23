using System.Collections.Generic;
using System.IO;
using System.Linq;
using DSPRE.HgEngine;
using Xunit;

namespace DSPRE.Tests
{
    /// <summary>hg-engine reads its tables out of a/0/2/8 by fixed member index, so a member left behind
    /// by an earlier build moves every table away from the index the game reads.</summary>
    public class HgEngineCodeAddonsTests
    {
        private const int Species = 1535;

        /// <summary>The 13 tables in hg-engine's own order, at the sizes a real build gives them: the
        /// first two are two bytes a species and the icon palettes one, which is what identifies them.</summary>
        private static List<byte[]> Tables()
        {
            var tables = new List<byte[]>
            {
                new byte[Species * 2],  // hidden abilities
                new byte[Species * 2],  // base experience
                IconPalettes(),
                new byte[2152], new byte[68800], new byte[918], new byte[798], new byte[68684],
                new byte[12488], new byte[0], new byte[610], new byte[1848], new byte[640],
            };
            // A byte over the last bank keeps these from looking like the icon table.
            foreach (var t in tables.Where(t => t.Length > 0 && t.Length != Species)) t[0] = 9;
            return tables;
        }

        private static byte[] IconPalettes()
        {
            var table = new byte[Species];
            for (int i = 0; i < table.Length; i++) table[i] = (byte)(i % 3);
            return table;
        }

        private static List<byte[]> Vanilla() => new List<byte[]>
        {
            Junk(552), Junk(32832), Junk(2084), Junk(552), Junk(3152), Junk(1154), Junk(1086),
        };

        private static byte[] Junk(int length)
        {
            var bytes = new byte[length];
            for (int i = 0; i < bytes.Length; i++) bytes[i] = (byte)(200 + (i % 50));
            return bytes;
        }

        [Fact]
        public void AHealthyArchiveReadsAsHealthy()
        {
            var members = Vanilla().Concat(Tables()).ToList();

            var layout = HgEngineCodeAddons.DescribeMembers(members, Species);

            Assert.True(layout.IsHealthy);
            Assert.Equal(HgEngineCodeAddons.HiddenAbilities, layout.TableBlockStart);
            Assert.Equal(HgEngineCodeAddons.IconPalettes, layout.IconPaletteMemberIndex);
            Assert.Empty(layout.StaleMembers);
            Assert.Equal(20, members.Count);
        }

        /// <summary>The shape a commission ROM arrived in: leftovers before the tables, and the whole
        /// table set packed again twice after them.</summary>
        [Fact]
        public void LeftoverMembersShiftTheTablesAndAreListedForDropping()
        {
            var members = Vanilla();
            members.AddRange(new[] { new byte[0], new byte[0], Junk(312) });
            members.AddRange(Tables());
            members.AddRange(new[] { new byte[0], new byte[0], new byte[0] });
            members.AddRange(Tables());

            var layout = HgEngineCodeAddons.DescribeMembers(members, Species);

            Assert.False(layout.IsHealthy);
            Assert.Equal(10, layout.TableBlockStart);
            Assert.Equal(12, layout.IconPaletteMemberIndex);
            Assert.Equal(new[] { 7, 8, 9 }.Concat(Enumerable.Range(23, 16)), layout.StaleMembers);
            Assert.Contains("3 members later", layout.Summary);

            // Dropping them leaves exactly the members the game reads, in the right order.
            var repaired = members.Where((_, i) => !layout.StaleMembers.Contains(i)).ToList();
            var after = HgEngineCodeAddons.DescribeMembers(repaired, Species);
            Assert.True(after.IsHealthy);
            Assert.Equal(HgEngineCodeAddons.IconPalettes, after.IconPaletteMemberIndex);
        }

        [Fact]
        public void TheMemberTheGameReadsIsNamedEvenWhenItHoldsSomethingElse()
        {
            var members = Vanilla();
            members.AddRange(new[] { new byte[0], new byte[0], Junk(312) });
            members.AddRange(Tables());

            var layout = HgEngineCodeAddons.DescribeMembers(members, Species);

            var readAsIconPalettes = layout.Members[HgEngineCodeAddons.IconPalettes];
            Assert.Equal("Icon palettes", readAsIconPalettes.ReadAs);
            Assert.Null(readAsIconPalettes.Holds);
            Assert.True(readAsIconPalettes.IsStale);
            Assert.Equal("Icon palettes", layout.Members[12].Holds);
        }

        [Fact]
        public void RepairingDropsTheLeftoversAndRenumbersWhatIsKept()
        {
            var members = Vanilla();
            members.AddRange(new[] { new byte[0], new byte[0], Junk(312) });
            members.AddRange(Tables());
            members.AddRange(new[] { new byte[0], new byte[0], new byte[0] });
            members.AddRange(Tables());

            string dir = Path.Combine(Path.GetTempPath(), "dspre-a028-" + Path.GetRandomFileName());
            Directory.CreateDirectory(dir);
            try
            {
                for (int i = 0; i < members.Count; i++)
                    File.WriteAllBytes(Path.Combine(dir, i.ToString("D4")), members[i]);

                var before = HgEngineCodeAddons.DescribeMembers(members, Species);
                Assert.True(HgEngineCodeAddonRepair.TryRepair(dir, before.StaleMembers, out string error), error);

                var names = Directory.GetFiles(dir).Select(Path.GetFileName).OrderBy(n => n).ToList();
                Assert.Equal(HgEngineCodeAddons.HealthyMemberCount, names.Count);
                Assert.Equal(Enumerable.Range(0, names.Count).Select(i => i.ToString("D4")), names);

                var after = HgEngineCodeAddons.DescribeMembers(
                    names.Select(n => File.ReadAllBytes(Path.Combine(dir, n))).ToList(), Species);
                Assert.True(after.IsHealthy);
                Assert.Equal(Species, File.ReadAllBytes(
                    Path.Combine(dir, HgEngineCodeAddons.IconPalettes.ToString("D4"))).Length);
            }
            finally { Directory.Delete(dir, recursive: true); }
        }

        [Fact]
        public void RepairingAnArchiveWithNothingStaleIsRefused()
        {
            string dir = Path.Combine(Path.GetTempPath(), "dspre-a028-" + Path.GetRandomFileName());
            Directory.CreateDirectory(dir);
            try
            {
                File.WriteAllBytes(Path.Combine(dir, "0000"), new byte[4]);
                Assert.False(HgEngineCodeAddonRepair.TryRepair(dir, new int[0], out string error));
                Assert.Equal("There is nothing to drop.", error);
                Assert.Single(Directory.GetFiles(dir));
            }
            finally { Directory.Delete(dir, recursive: true); }
        }

        /// <summary>A linked hg-engine checkout answers the species count from its own build, so the
        /// reading has to stand up without one.</summary>
        [Fact]
        public void TheTablesAreFoundWithoutKnowingTheSpeciesCount()
        {
            var members = Vanilla();
            members.AddRange(new[] { new byte[0], new byte[0], Junk(312) });
            members.AddRange(Tables());

            var layout = HgEngineCodeAddons.DescribeMembers(members, speciesCount: 0);

            Assert.Equal(12, layout.IconPaletteMemberIndex);
            Assert.Equal(10, layout.TableBlockStart);
            Assert.False(layout.IsHealthy);
            Assert.Equal(new[] { 7, 8, 9 }, layout.StaleMembers);
        }

        [Fact]
        public void AHealthyArchiveReadsAsHealthyWithoutTheSpeciesCount()
        {
            var layout = HgEngineCodeAddons.DescribeMembers(Vanilla().Concat(Tables()).ToList(), speciesCount: 0);

            Assert.True(layout.IsHealthy);
            Assert.Equal(HgEngineCodeAddons.IconPalettes, layout.IconPaletteMemberIndex);
        }

        [Fact]
        public void AnArchiveWithoutTheTablesSaysSoRatherThanGuessing()
        {
            var layout = HgEngineCodeAddons.DescribeMembers(Vanilla(), Species);

            Assert.False(layout.IsHealthy);
            Assert.Equal(-1, layout.TableBlockStart);
            Assert.Empty(layout.StaleMembers);
            Assert.Contains("could not be identified", layout.Summary);
        }
    }
}
