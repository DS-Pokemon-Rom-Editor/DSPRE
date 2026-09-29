using System.IO;
using System.Linq;
using DSPRE.Avalonia.ViewModels.Pokemon;
using DSPRE.ROMFiles;
using Xunit;

namespace DSPRE.Tests.Pokemon
{
    /// <summary>Member numbers follow the pokeheartgold decomp's sPokeathlonPerformanceArcIdxs.</summary>
    [Collection("rom")]
    public class PokeathlonPerformanceTests
    {
        private const int Power = 0, Speed = 1, Jump = 2, Stamina = 3, Skill = 4;

        [Theory]
        [InlineData(1, 0, 0)]
        [InlineData(25, 0, 24)]
        [InlineData(172, 0, 171)]
        [InlineData(172, 1, 172)]
        [InlineData(201, 27, 228)]
        [InlineData(386, 1, 414)]
        [InlineData(412, 0, 442)]
        [InlineData(479, 5, 520)]
        [InlineData(493, 0, 536)]
        [InlineData(493, 9, 545)]
        [InlineData(493, 17, 553)]
        public void SpeciesFormsFindTheGamesMember(int species, int form, int member)
        {
            Assert.Equal(member, PokeathlonPerformance.MemberOf(species, form));
        }

        [Fact]
        public void EveryMemberBelongsToExactlyOneSpeciesForm()
        {
            var members = Enumerable.Range(1, PokeathlonPerformance.LastSpecies)
                .SelectMany(s => Enumerable.Range(0, PokeathlonPerformance.FormsOf(s)).Select(f => PokeathlonPerformance.MemberOf(s, f)))
                .ToList();
            Assert.Equal(Enumerable.Range(0, PokeathlonPerformance.RecordCount), members);
            Assert.Equal(-1, PokeathlonPerformance.MemberOf(25, 1));
            Assert.Equal(-1, PokeathlonPerformance.MemberOf(494, 0));
            foreach (int s in Enumerable.Range(1, PokeathlonPerformance.LastSpecies))
                Assert.Equal(PokeathlonPerformance.FormsOf(s), PokeathlonPerformance.FormNamesOf(s).Length);
        }

        [InlineData(496, 386, 1)]   // Deoxys Attack
        [InlineData(500, 413, 2)]   // Wormadam Trash
        [InlineData(501, 487, 1)]   // Giratina Origin
        [InlineData(502, 492, 1)]   // Shaymin Sky
        [InlineData(507, 479, 5)]   // Rotom Mow
        [InlineData(25, 25, 0)]
        [InlineData(494, 0, 0)]     // the egg has no record
        [InlineData(508, 0, 0)]
        [SkippableTheory]
        public void FormPersonalFilesOpenTheirOwnForm(int personalId, int species, int form)
        {
            OpenHeartGold();
            Assert.Equal((species, form), PersonalDataEditorViewModel.AthlonSpeciesOf(personalId));
        }

        [SkippableFact]
        public void TheRomsOwnTableGivesTheSameMembers()
        {
            OpenHeartGold();
            Assert.True(PokeathlonPerformance.UsesRomTable());
            Assert.Equal(171, PokeathlonPerformance.MemberOf(172, 0));
            Assert.Equal(228, PokeathlonPerformance.MemberOf(201, 27));
            Assert.Equal(553, PokeathlonPerformance.MemberOf(493, 17));
            var members = Enumerable.Range(1, PokeathlonPerformance.LastSpecies)
                .SelectMany(s => Enumerable.Range(0, PokeathlonPerformance.FormsOf(s)).Select(f => PokeathlonPerformance.MemberOf(s, f)));
            Assert.Equal(Enumerable.Range(0, PokeathlonPerformance.RecordCount), members);
        }

        private static void OpenHeartGold()
        {
            Skip.If(!Directory.Exists(TestRoms.HeartGold), "HeartGold test project not configured");
            new RomInfo("IPKE", TestRoms.HeartGold);
            Skip.If(PokeathlonPerformance.WhyNot() != null, PokeathlonPerformance.WhyNot() ?? "");
        }

        private static PokeathlonPerformance Of(int species, int form = 0) =>
            PokeathlonPerformance.Read(PokeathlonPerformance.MemberOf(species, form));

        [SkippableFact]
        public void StatsSitInTheOrderTheSpeciesShow()
        {
            OpenHeartGold();
            Assert.Equal(4, Of(291).Base(Speed));     // Ninjask
            Assert.Equal(4, Of(101).Base(Speed));     // Electrode
            Assert.Equal(4, Of(213).Base(Stamina));   // Shuckle
            Assert.Equal(0, Of(213).Base(Speed));
            Assert.Equal(4, Of(143).Base(Stamina));   // Snorlax
            Assert.Equal(4, Of(65).Base(Skill));      // Alakazam
            Assert.Equal(4, Of(68).Base(Power));      // Machamp
            Assert.Equal(4, Of(189).Base(Jump));      // Jumpluff
        }

        [SkippableFact]
        public void EveryRetailRecordIsAcceptedAsItIs()
        {
            OpenHeartGold();
            int checkedCount = 0;
            for (int m = 0; m < PokeathlonPerformance.RecordCount; m++)
            {
                var record = PokeathlonPerformance.Read(m);
                Assert.NotNull(record);
                Assert.Null(record.Problem());
                checkedCount++;
            }
            Assert.Equal(PokeathlonPerformance.RecordCount, checkedCount);
        }

        [SkippableFact]
        public void AWrittenRecordReadsBackAndKeepsItsEventBytes()
        {
            OpenHeartGold();
            int member = PokeathlonPerformance.MemberOf(25, 0);
            string dir = RomInfo.gameDirs[RomInfo.DirNames.pokeathlonPerformance].unpackedDir;
            string path = Path.Combine(dir, member.ToString("D4")), next = Path.Combine(dir, (member + 1).ToString("D4"));
            byte[] original = File.ReadAllBytes(path), neighbour = File.ReadAllBytes(next);
            try
            {
                var record = PokeathlonPerformance.Read(member);
                record.SetMin(Power, 1); record.SetBase(Power, 3); record.SetMax(Power, 4);
                record.Write();

                var after = PokeathlonPerformance.Read(member);
                Assert.Equal(1, after.Min(Power));
                Assert.Equal(3, after.Base(Power));
                Assert.Equal(4, after.Max(Power));
                byte[] written = File.ReadAllBytes(path);
                Assert.Equal(original.Skip(5).Take(4), written.Skip(5).Take(4));
                Assert.Equal(original[19], written[19]);
                Assert.Equal(original.Skip(1).Take(4), written.Skip(1).Take(4));
                Assert.Equal(neighbour, File.ReadAllBytes(next));

                record.SetMin(Power, 4);
                Assert.Throws<InvalidDataException>(() => record.Write());
                Assert.Equal(written, File.ReadAllBytes(path));
            }
            finally
            {
                File.WriteAllBytes(path, original);
            }
        }
    }
}
