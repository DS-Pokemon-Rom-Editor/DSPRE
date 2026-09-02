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
            // ID_COMMON_SCR_OFFSET is 2000, so script 2000 is the first one in the common file.
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
            // These are the offsets scr_offset.h defines; each one must land on a real file.
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
