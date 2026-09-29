using DSPRE.ROMFiles;
using Xunit;

namespace DSPRE.Tests
{
    /// <summary>Where a script id really lives. </summary>
    public class SharedScriptResolutionTests
    {
        [Theory]
        [InlineData(1)]
        [InlineData(500)]
        [InlineData(1999)]
        public void LowIdsBelongToTheMapsOwnFile(int scriptNumber)
        {
            var r = CommonScriptId.Resolve(RomInfo.GameFamilies.HGSS, scriptNumber);
            Assert.Equal(CommonScriptId.Kind.NotCommon, r.Kind);
        }

        [Fact]
        public void CommonScriptsStartTheirOwnNumbering()
        {
            // Common scripts start at 2000, so script 2000 is the first one in the common file.
            var first = CommonScriptId.Resolve(RomInfo.GameFamilies.HGSS, 2000);
            Assert.Equal(CommonScriptId.Kind.Resolved, first.Kind);
            Assert.Equal(0, first.LocalScriptId);
            Assert.Equal(1, first.ManualUserId);

            var later = CommonScriptId.Resolve(RomInfo.GameFamilies.HGSS, 2042);
            Assert.Equal(42, later.LocalScriptId);
            Assert.Equal(first.ScriptArchiveId, later.ScriptArchiveId);
        }

        [Fact]
        public void ATrainerScriptIsReadFromTheTrainerFile()
        {
            // A trainer's number is a script id, and that script is in the shared trainer file rather
            // than the map's own, numbered from 3000.
            var r = CommonScriptId.Resolve(RomInfo.GameFamilies.HGSS, 3042);
            Assert.Equal(CommonScriptId.Kind.Resolved, r.Kind);
            Assert.Equal(42, r.LocalScriptId);

            // And the trainer it stands for is a separate question from where its script lives.
            Assert.Equal(43, TrainerScripts.TrainerIdFor(3042));
        }

        [Fact]
        public void HiddenItemsAndGroundItemsUseDifferentFiles()
        {
            var hidden = CommonScriptId.Resolve(RomInfo.GameFamilies.HGSS, 8000);
            var ground = CommonScriptId.Resolve(RomInfo.GameFamilies.HGSS, 7000);
            Assert.Equal(CommonScriptId.Kind.Resolved, hidden.Kind);
            Assert.Equal(CommonScriptId.Kind.Resolved, ground.Kind);
            Assert.NotEqual(hidden.ScriptArchiveId, ground.ScriptArchiveId);
            Assert.Equal(0, hidden.LocalScriptId);
            Assert.Equal(0, ground.LocalScriptId);
        }

        [Theory]
        [InlineData(2000)]   // common
        [InlineData(2500)]   // BG attribute
        [InlineData(2800)]   // berry trees
        [InlineData(3000)]   // trainer
        [InlineData(5000)]   // double battle trainer
        [InlineData(7000)]   // ground item
        [InlineData(8000)]   // hidden item
        [InlineData(10000)]  // HM
        public void EveryRangeStartFromTheEngineHeaderResolves(int scriptNumber)
        {
            // Each shared script offset must land on a real file.
            var r = CommonScriptId.Resolve(RomInfo.GameFamilies.HGSS, scriptNumber);
            Assert.Equal(CommonScriptId.Kind.Resolved, r.Kind);
            Assert.Equal(0, r.LocalScriptId);
        }

        [Fact]
        public void PlatinumHasItsOwnTable()
        {
            var hgss = CommonScriptId.Resolve(RomInfo.GameFamilies.HGSS, 2000);
            var plat = CommonScriptId.Resolve(RomInfo.GameFamilies.Plat, 2000);
            Assert.Equal(CommonScriptId.Kind.Resolved, plat.Kind);
            Assert.NotEqual(hgss.ScriptArchiveId, plat.ScriptArchiveId);
        }

        [Theory]
        [InlineData(2000, 205, 199)]
        [InlineData(2500, 1, 13)]
        [InlineData(2800, 378, 350)]
        [InlineData(3000, 1040, 199)]
        [InlineData(5000, 1040, 199)]
        [InlineData(7000, 370, 325)]
        [InlineData(8000, 374, 333)]
        [InlineData(8800, 462, 485)]
        [InlineData(8900, 389, 380)]
        [InlineData(8950, 463, 486)]
        [InlineData(8970, 390, 7)]
        [InlineData(9000, 207, 207)]
        [InlineData(9100, 0, 9)]
        [InlineData(9200, 388, 379)]
        [InlineData(9300, 372, 329)]
        [InlineData(9400, 391, 381)]
        [InlineData(9500, 464, 492)]
        [InlineData(9600, 377, 199)]
        [InlineData(9700, 387, 378)]
        [InlineData(9800, 206, 203)]
        [InlineData(9900, 365, 199)]
        [InlineData(9950, 376, 335)]
        [InlineData(10000, 375, 334)]
        [InlineData(10100, 1041, 563)]
        [InlineData(10150, 1042, 562)]
        [InlineData(10200, 373, 332)]
        [InlineData(10300, 977, 496)]
        public void DiamondPearlRangesMatchTheScriptManager(int start, int scriptArchive, int textArchive)
        {
            // The 27 branches of pokediamond LoadScriptsAndMessagesByMapId.
            var r = CommonScriptId.Resolve(RomInfo.GameFamilies.DP, start);
            Assert.Equal(CommonScriptId.Kind.Resolved, r.Kind);
            Assert.Equal(scriptArchive, r.ScriptArchiveId);
            Assert.Equal(textArchive, r.TextArchiveId);
            Assert.Equal(0, r.LocalScriptId);
        }

        [Fact]
        public void TheBerryRangeIsApricornsOnlyInHeartGold()
        {
            // 2800 is berry soil in DP and Pt but apricorn trees in HGSS, each from its own file.
            var dp = CommonScriptId.Resolve(RomInfo.GameFamilies.DP, 2800);
            var pt = CommonScriptId.Resolve(RomInfo.GameFamilies.Plat, 2800);
            var hg = CommonScriptId.Resolve(RomInfo.GameFamilies.HGSS, 2800);
            Assert.Equal(378, dp.ScriptArchiveId);
            Assert.Equal(413, pt.ScriptArchiveId);
            Assert.Equal(150, hg.ScriptArchiveId);
        }

        [Fact]
        public void ARangeWithConflictingRecordsIsRefusedRatherThanGuessed()
        {
            // DSPRE's own table disagrees with itself between 9300 and 9700; better to say so than to
            // read the wrong file and show a script that is not the one being run.
            var r = CommonScriptId.Resolve(RomInfo.GameFamilies.HGSS, 9400);
            Assert.Equal(CommonScriptId.Kind.Discrepancy, r.Kind);
            Assert.NotEmpty(r.CandidateArchives);
        }
    }
}
