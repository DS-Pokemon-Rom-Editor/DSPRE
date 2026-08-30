using System.Collections.Generic;
using System.Linq;
using DSPRE.ROMFiles;
using Xunit;

namespace DSPRE.Tests
{
    /// <summary>
    /// The gaps a message leaves for words the game fills in.
    ///
    /// The shape was read off the raw bytes of HeartGold text archive 542 message 1, which is
    /// FFFE 0103 0002 0000 0000 and which the text converter writes as "{STRVAR_1, 3, 0, 0}": family 1
    /// from the tag type's top byte, kind 3 from its bottom byte, then the two parameters. The first
    /// parameter is the word slot WORDSET_ExpandStr reads (wordset.c:1670) and the second is zero in all
    /// 9150 tags across the game's 829 text archives.
    /// </summary>
    public class FieldStringVarTests
    {
        [Fact]
        public void ARealMessageGivesUpItsGap()
        {
            var found = FieldStringVars.Find("Wait, {STRVAR_1, 3, 0, 0}!").ToList();

            var one = Assert.Single(found);
            Assert.Equal(1, one.family);
            Assert.Equal(3, one.kind);      // a person's name
            Assert.Equal(0, one.buffer);    // filled by the script's word slot 0
        }

        [Fact]
        public void SeveralGapsInOneLineAllComeBack()
        {
            var found = FieldStringVars.Find("{STRVAR_1, 3, 0, 0} caught a {STRVAR_1, 0, 1, 0}!").ToList();
            Assert.Equal(2, found.Count);
            Assert.Equal((1, 3, 0), (found[0].family, found[0].kind, found[0].buffer));
            Assert.Equal((1, 0, 1), (found[1].family, found[1].kind, found[1].buffer));
        }

        [Fact]
        public void APlainLineHasNoGaps()
        {
            Assert.False(FieldStringVars.Any("Wow, your Pokégear is impressive!"));
            Assert.Empty(FieldStringVars.Find("Wow, your Pokégear is impressive!"));
        }

        [Fact]
        public void AWholeArchiveOfSpacingStillParses()
        {
            // The converter writes the numbers with spaces after the commas; some hands write it without.
            Assert.Single(FieldStringVars.Find("{STRVAR_1,3,0,0}").ToList());
            Assert.Single(FieldStringVars.Find("{STRVAR_1,  3 , 0 , 0 }").ToList());
        }

        [Fact]
        public void FillingAGapPutsTheWordInTheLine()
        {
            string line = "Wait, {STRVAR_1, 3, 0, 0}!";
            string got = FieldStringVars.Expand(line, (f, k, b) => k == 3 ? "Ethan" : null);
            Assert.Equal("Wait, Ethan!", got);
        }

        [Fact]
        public void AGapNobodyFilledIsLeftAloneRatherThanVanishing()
        {
            string line = "Wait, {STRVAR_1, 3, 0, 0}!";
            Assert.Equal(line, FieldStringVars.Expand(line, (f, k, b) => null));
        }

        [Fact]
        public void APersonsNameStartsAsPlayer()
            => Assert.Equal("PLAYER", FieldStringVars.SuggestFor(3, 0, new List<int>()));

        [Fact]
        public void AGapOfAnUnknownKindNamesItselfAndItsScript()
        {
            string s = FieldStringVars.SuggestFor(200, 1, new List<int> { 42 });
            Assert.Contains("strvar 1", s);
            Assert.Contains("script 42", s);
        }

        [Fact]
        public void GatheringGroupsTheSameGapAcrossMessages()
        {
            var msgs = new[]
            {
                (1, "Wait, {STRVAR_1, 3, 0, 0}!"),
                (2, "Wait, {STRVAR_1, 3, 0, 0}!"),
                (6, "Hi, {STRVAR_1, 3, 0, 0}! Nice {STRVAR_1, 8, 1, 0}."),
            };
            var got = FieldStringVars.Gather(msgs, id => id == 6 ? new[] { 42 } : new int[0]);

            Assert.Equal(2, got.Count);

            var name = got[0];
            Assert.Equal(3, name.Kind);
            Assert.Equal(new[] { 1, 2, 6 }, name.Messages.ToArray());
            Assert.Equal(new[] { 42 }, name.Scripts.ToArray());
            Assert.Equal("PLAYER", name.Value);
            Assert.Equal("Person's name", name.KindName);

            var item = got[1];
            Assert.Equal(8, item.Kind);
            Assert.Equal(1, item.Buffer);
            Assert.Equal("ITEM", item.Value);
        }

        [Fact]
        public void TwoSlotsOfTheSameKindStayApart()
        {
            var got = FieldStringVars.Gather(new[] { (1, "{STRVAR_1, 3, 0, 0} and {STRVAR_1, 3, 1, 0}") });
            Assert.Equal(2, got.Count);
            Assert.Equal(0, got[0].Buffer);
            Assert.Equal(1, got[1].Buffer);
        }

        [Fact]
        public void NothingBlowsUpOnEmptyInput()
        {
            Assert.Empty(FieldStringVars.Gather(null));
            Assert.Empty(FieldStringVars.Find(null));
            Assert.False(FieldStringVars.Any(null));
            Assert.Null(FieldStringVars.Expand(null, (f, k, b) => "x"));
        }
    }
}
