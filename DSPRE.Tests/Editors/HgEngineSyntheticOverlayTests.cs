using System;
using System.IO;
using System.Linq;
using DSPRE.HgEngine;
using Xunit;
using Xunit.Abstractions;

namespace DSPRE.Tests
{
    /// <summary>
    /// The synthetic overlay archive both tools keep tables in. hg-engine repacks it from its own build
    /// directory every compile, so which member is whose decides whether an expansion can survive.
    /// </summary>
    public class HgEngineSyntheticOverlayTests
    {
        private readonly ITestOutputHelper _out;
        public HgEngineSyntheticOverlayTests(ITestOutputHelper o) => _out = o;

        [Theory]
        [InlineData("8_0", 0)]
        [InlineData("9_07", 7)]
        [InlineData("9_18", 18)]
        [InlineData("0000", -1)]
        [InlineData("", -1)]
        public void AMemberFileNameNamesItsArchiveIndex(string fileName, int expected)
            => Assert.Equal(expected, HgEngineSyntheticOverlay.MemberIndexOf(fileName));

        [SkippableFact]
        public void TheCheckoutsOwnRulesSayWhichMembersItGenerates()
        {
            string root = Environment.GetEnvironmentVariable("DSPRE_TEST_HGENGINE");
            Skip.If(string.IsNullOrWhiteSpace(root) ||
                    !File.Exists(Path.Combine(root, "data", "codetables.mk")),
                "Set DSPRE_TEST_HGENGINE to a checkout with data/codetables.mk.");

            var generated = HgEngineSyntheticOverlay.GeneratedMembersAt(root);
            _out.WriteLine("generates: " + string.Join(", ", generated));

            Assert.NotEmpty(generated);

            // Every one has to name an index, or nothing can be compared against DSPRE's member.
            Assert.All(generated, n => Assert.True(HgEngineSyntheticOverlay.MemberIndexOf(n) >= 0, n));

            // hg-engine adds its tables above the archive's original members rather than replacing them,
            // so DSPRE's HGSS member (0) is not one of them.
            Assert.DoesNotContain(0, generated.Select(HgEngineSyntheticOverlay.MemberIndexOf));
        }

        [SkippableFact]
        public void AMemberDspreExpandsIntoIsPresentButNotGenerated()
        {
            string root = Environment.GetEnvironmentVariable("DSPRE_TEST_HGENGINE");
            Skip.If(string.IsNullOrWhiteSpace(root) ||
                    !Directory.Exists(Path.Combine(root, "build", "a028")),
                "Set DSPRE_TEST_HGENGINE to a built checkout.");

            var members = HgEngineSyntheticOverlay.MembersIn(Path.Combine(root, "build", "a028"));
            _out.WriteLine("members: " + string.Join(", ", members));
            Assert.NotEmpty(members);

            // DSPRE expands member 0 on HGSS, and the checkout carries it round untouched.
            Assert.Contains(0, members.Select(HgEngineSyntheticOverlay.MemberIndexOf));
        }
    }
}
