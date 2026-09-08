using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DSPRE.HgEngine;
using Xunit;
using Xunit.Abstractions;

namespace DSPRE.Tests
{
    /// <summary>
    /// The parts of arm9 and the overlays hg-engine's build patches, read from its own lists. Getting an
    /// address wrong here either blocks a write that was fine or lets one through that the build undoes.
    /// </summary>
    public class HgEngineClaimedRangesTests
    {
        private readonly ITestOutputHelper _out;
        public HgEngineClaimedRangesTests(ITestOutputHelper o) => _out = o;

        // Overlay 12 in HeartGold loads here; the real lists are written against addresses like this.
        private const long Overlay12Ram = 0x022378C0;
        private static long Ram(int overlay) => overlay == 12 ? Overlay12Ram : 0x02200000;

        private static string Make(string listName, params string[] lines)
        {
            string root = Path.Combine(Path.GetTempPath(), "dspre_claims_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            File.WriteAllLines(Path.Combine(root, listName), lines);
            return root;
        }

        [Fact]
        public void AMainRamAddressIsRelativeToWhereTheBinaryLoads()
        {
            // arm9 loads at 0x02000000, so this is 0x78384 into the file.
            string root = Make("bytereplacement", "arm9 02078384 60 B4 C0 46");
            try
            {
                var claim = Assert.Single(HgEngineClaimedRanges.ReadAll(root, Ram)[-1]);
                Assert.Equal(0x78384, claim.Offset);
                Assert.Equal(4, claim.Length);
            }
            finally { Directory.Delete(root, true); }
        }

        [Fact]
        public void AnOverlayAddressIsRelativeToThatOverlaysLoadAddress()
        {
            string root = Make("bytereplacement", "0012 022378D0 01 02");
            try
            {
                var claim = Assert.Single(HgEngineClaimedRanges.ReadAll(root, Ram)[12]);
                Assert.Equal(0x10, claim.Offset);
                Assert.Equal(2, claim.Length);
            }
            finally { Directory.Delete(root, true); }
        }

        [Fact]
        public void AnAddressWrittenAgainstTheFileBaseIsAlreadyAnOffset()
        {
            // 0x08... is not a load address, it is the file offset written the old way.
            string root = Make("bytereplacement", "0012 08012852 08");
            try
            {
                var claim = Assert.Single(HgEngineClaimedRanges.ReadAll(root, Ram)[12]);
                Assert.Equal(0x12852, claim.Offset);
            }
            finally { Directory.Delete(root, true); }
        }

        [Theory]
        // A branch at a word boundary is four bytes plus its target; off one, it needs a spare halfword.
        [InlineData("arm9 Sym 02000010 1", 8)]
        [InlineData("arm9 Sym 02000012 1", 10)]
        // No register column means the whole function is replaced, which needs 0x18 plus the target.
        [InlineData("arm9 Sym 02000010", 0x1C)]
        [InlineData("arm9 Sym 02000010 255", 0x1C)]
        public void AHookClaimsAsMuchAsItWrites(string line, int expected)
        {
            string root = Make("hooks", line);
            try
            {
                Assert.Equal(expected, Assert.Single(HgEngineClaimedRanges.ReadAll(root, Ram)[-1]).Length);
            }
            finally { Directory.Delete(root, true); }
        }

        [Fact]
        public void ARepointIsAlwaysAPointer()
        {
            string root = Make("repoints", "arm9 sPocketCounts 08077C14");
            try
            {
                var claim = Assert.Single(HgEngineClaimedRanges.ReadAll(root, Ram)[-1]);
                Assert.Equal(0x77C14, claim.Offset);
                Assert.Equal(4, claim.Length);
            }
            finally { Directory.Delete(root, true); }
        }

        [Fact]
        public void CommentsAndBlankLinesAreNotClaims()
        {
            string root = Make("repoints", "# a comment", "", "   ", "arm9 Sym 08000010");
            try
            {
                Assert.Single(HgEngineClaimedRanges.ReadAll(root, Ram)[-1]);
            }
            finally { Directory.Delete(root, true); }
        }

        [Fact]
        public void AClaimOnlyCatchesAWriteThatTouchesIt()
        {
            var claim = new HgEngineClaim { OverlayNumber = -1, Offset = 0x100, Length = 4 };

            Assert.True(claim.Overlaps(0x100, 1));
            Assert.True(claim.Overlaps(0x103, 1));
            Assert.True(claim.Overlaps(0x0FE, 4));   // a write that runs into it
            Assert.False(claim.Overlaps(0x104, 4));  // starts just after
            Assert.False(claim.Overlaps(0x0FC, 4));  // ends just before
        }

        [Theory]
        [InlineData("arm9.bin", true, -1)]
        [InlineData("overlay_0026.bin", true, 26)]
        [InlineData("ov026.bin", true, 26)]
        [InlineData("overarm9.bin", false, 0)]
        [InlineData("a027", false, 0)]
        public void OnlyArm9AndOverlaysAreGuarded(string fileName, bool identified, int expected)
        {
            Assert.Equal(identified, HgEngineWriteGuard.TryIdentify(fileName, out int overlay));
            if (identified) Assert.Equal(expected, overlay);
        }

        [SkippableFact]
        public void TheRealListsLandInsideTheBinariesTheyName()
        {
            string root = Environment.GetEnvironmentVariable("DSPRE_TEST_HGENGINE");
            Skip.If(string.IsNullOrWhiteSpace(root) || !File.Exists(Path.Combine(root, "hooks")),
                "Set DSPRE_TEST_HGENGINE to a checkout with its hook lists.");

            var map = HgEngineClaimedRanges.ReadAll(root, Ram);
            Assert.NotEmpty(map);

            foreach (var (overlay, claims) in map.OrderBy(kv => kv.Key))
            {
                _out.WriteLine($"{(overlay < 0 ? "arm9" : "overlay " + overlay)}: {claims.Count} claims, " +
                    $"0x{claims.Min(c => c.Offset):X}..0x{claims.Max(c => c.Offset + c.Length):X}");

                // An address that resolved against the wrong base shows up immediately as a negative or
                // absurd offset, which is the mistake worth catching here.
                Assert.All(claims, c => Assert.True(c.Offset >= 0, $"{c} resolved before the start of the file"));
                Assert.All(claims, c => Assert.True(c.Offset < 0x400000, $"{c} resolved past any plausible size"));
                Assert.All(claims, c => Assert.True(c.Length > 0, c.ToString()));
            }
        }
    }
}
