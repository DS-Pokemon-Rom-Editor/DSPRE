using System.Collections.Generic;
using System.Linq;
using DSPRE.ROMFiles;
using Xunit;
using static DSPRE.RomInfo;

namespace DSPRE.Tests
{
    /// <summary>Per game tile behaviour and collision names used by the permission painters.</summary>
    public class TilePermissionsTests
    {
        private static IEnumerable<byte> Range(int from, int to) => Enumerable.Range(from, to - from + 1).Select(v => (byte)v);

        // Values the pokeplatinum decomp names in include/constants/field/map_tile_behaviors.h (not UNUSED_ or UNKNOWN_).
        private static readonly byte[] PlatinumDecompNamed = new byte[]
        {
            0x00, 0x02, 0x03, 0x08, 0x0B, 0x0C, 0x10, 0x13, 0x15, 0x16, 0x17, 0x1D, 0x20, 0x21, 0x2C, 0x2D,
            0x49, 0x4A, 0x4B, 0x4C, 0x56, 0x57, 0x58, 0x59, 0x5E, 0x5F, 0x67, 0x69, 0x6A, 0x6B,
            0x80, 0x83, 0x85, 0x86, 0xE0, 0xE1, 0xE2, 0xE4, 0xE5, 0xEA, 0xEB, 0xEC,
        }.Concat(Range(0x30, 0x3B)).Concat(Range(0x40, 0x43)).Concat(Range(0x5A, 0x5D)).Concat(Range(0x62, 0x65))
         .Concat(Range(0x6C, 0x7D)).Concat(Range(0xA0, 0xA9)).Concat(Range(0xD7, 0xDB)).ToArray();

        // Values pokeheartgold names in include/constants/metatile_behavior.h or tests in src/metatile_behavior.c.
        private static readonly byte[] HeartGoldDecompUsed = new byte[]
        {
            0x00, 0x02, 0x03, 0x06, 0x08, 0x10, 0x11, 0x13, 0x15, 0x16, 0x17, 0x1D, 0x20, 0x21, 0x22, 0x23,
            0x2C, 0x2D, 0x2E, 0x49, 0x4A, 0x4B, 0x4C, 0x4D, 0x5E, 0x5F, 0x67, 0x69, 0x6A, 0x6B,
            0x80, 0x83, 0x85, 0x86, 0xA4, 0xA8, 0xA9, 0xE0, 0xE1, 0xE2, 0xE4, 0xE5, 0xEA, 0xEB, 0xEC, 0xFF,
        }.Concat(Range(0x30, 0x3E)).Concat(Range(0x40, 0x43)).Concat(Range(0x62, 0x65)).Concat(Range(0x6C, 0x73)).ToArray();

        // Behaviour values found in the land data of the US retail games.
        private static readonly byte[] PlatinumRetail =
        {
            0x00, 0x02, 0x03, 0x08, 0x0B, 0x0C, 0x10, 0x13, 0x15, 0x16, 0x17, 0x1D, 0x20, 0x21, 0x2C, 0x2D, 0x30, 0x31,
            0x38, 0x39, 0x3B, 0x3C, 0x3D, 0x3E, 0x3F, 0x40, 0x41, 0x42, 0x43, 0x49, 0x4A, 0x4B, 0x4C, 0x56, 0x57, 0x58, 0x59, 0x5A, 0x5B,
            0x5C, 0x5D, 0x5E, 0x5F, 0x60, 0x62, 0x63, 0x64, 0x65, 0x67, 0x69, 0x6A, 0x6B, 0x6C, 0x6D, 0x6E, 0x6F, 0x70,
            0x71, 0x72, 0x73, 0x75, 0x76, 0x79, 0x7A, 0x7B, 0x7C, 0x7D, 0x80, 0x83, 0x85, 0x86, 0x88, 0x8E, 0x8F, 0xA0,
            0xA1, 0xA2, 0xA3, 0xA4, 0xA5, 0xA6, 0xA7, 0xA8, 0xA9, 0xD7, 0xD8, 0xD9, 0xDA, 0xDB, 0xE0, 0xE1, 0xE2, 0xE4, 0xE5,
        };

        private static readonly byte[] DiamondRetail =
        {
            0x00, 0x02, 0x03, 0x08, 0x0B, 0x0C, 0x10, 0x13, 0x15, 0x16, 0x17, 0x20, 0x21, 0x30, 0x31, 0x38, 0x39, 0x3B, 0x3E, 0x3F,
            0x40, 0x41, 0x42, 0x43, 0x49, 0x4A, 0x4B, 0x4C, 0x56, 0x57, 0x58, 0x59, 0x5E, 0x5F, 0x60, 0x62, 0x63, 0x64,
            0x65, 0x67, 0x69, 0x6A, 0x6B, 0x6C, 0x6D, 0x6E, 0x6F, 0x70, 0x71, 0x72, 0x73, 0x75, 0x76, 0x79, 0x7A, 0x7B,
            0x7C, 0x7D, 0x80, 0x83, 0x85, 0x86, 0x88, 0x8E, 0x8F, 0xA0, 0xA1, 0xA2, 0xA3, 0xA4, 0xA5, 0xA6, 0xA7, 0xA8,
            0xD7, 0xD8, 0xD9, 0xDA, 0xDB, 0xE0, 0xE1, 0xE2, 0xE4, 0xE5,
        };

        private static readonly byte[] HeartGoldRetail =
        {
            0x00, 0x02, 0x03, 0x06, 0x08, 0x0B, 0x10, 0x11, 0x13, 0x15, 0x16, 0x17, 0x20, 0x21, 0x22, 0x24, 0x2C, 0x2D,
            0x2E, 0x30, 0x31, 0x32, 0x33, 0x38, 0x39, 0x3B, 0x3C, 0x3D, 0x3E, 0x40, 0x41, 0x42, 0x43, 0x4A, 0x4B, 0x4C,
            0x4D, 0x5E, 0x5F, 0x62, 0x63, 0x65, 0x67, 0x69, 0x6A, 0x6B, 0x6C, 0x6D, 0x6E, 0x6F, 0x70, 0x71, 0x72, 0x73,
            0x80, 0x83, 0x85, 0x86, 0xA4, 0xA8, 0xE0, 0xE1, 0xE2, 0xE4, 0xE5,
        };

        // High bytes found in the HeartGold land data: the blocked bit plus a footstep sound.
        private static readonly byte[] HeartGoldRetailCollisions =
        {
            0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x09, 0x0A, 0x0B, 0x0D, 0x80, 0x81, 0x82, 0x84, 0x85, 0x86, 0x8A,
        };

        private static bool Known(byte value, GameFamilies family) => TilePermissions.FindBehaviour(value, family) != null;

        [Theory]
        [InlineData(GameFamilies.DP)]
        [InlineData(GameFamilies.Plat)]
        [InlineData(GameFamilies.HGSS)]
        public void EveryListIsNamedOrderedAndUnique(GameFamilies family)
        {
            var list = TilePermissions.BehavioursFor(family);
            Assert.True(list.Count > 50);
            Assert.Equal(list.Select(b => b.Value).OrderBy(v => v), list.Select(b => b.Value));
            Assert.Equal(list.Count, list.Select(b => b.Value).Distinct().Count());
            Assert.Equal(list.Count, list.Select(b => b.Key).Distinct().Count());
            Assert.All(list, b => Assert.False(string.IsNullOrWhiteSpace(b.Name)));
            Assert.All(list, b => Assert.DoesNotContain("HGSS", b.Name));
            Assert.All(list, b => Assert.DoesNotContain("DPPt", b.Name));
            Assert.All(list, b => Assert.Equal($"[{b.Value:X2}] {b.Name}", b.Label));
        }

        [Fact]
        public void PlatinumNamesEveryValueTheDecompNames()
        {
            var missing = PlatinumDecompNamed.Where(v => !Known(v, GameFamilies.Plat)).ToList();
            Assert.True(missing.Count == 0, "Unnamed: " + string.Join(", ", missing.Select(v => v.ToString("X2"))));
        }

        [Fact]
        public void HeartGoldNamesEveryValueTheDecompHandles()
        {
            var missing = HeartGoldDecompUsed.Where(v => !Known(v, GameFamilies.HGSS)).ToList();
            Assert.True(missing.Count == 0, "Unnamed: " + string.Join(", ", missing.Select(v => v.ToString("X2"))));
        }

        [Theory]
        [InlineData(GameFamilies.DP)]
        [InlineData(GameFamilies.Plat)]
        [InlineData(GameFamilies.HGSS)]
        public void EveryValueInRetailMapsIsNamed(GameFamilies family)
        {
            byte[] retail = family == GameFamilies.DP ? DiamondRetail : family == GameFamilies.Plat ? PlatinumRetail : HeartGoldRetail;
            Assert.Empty(retail.Where(v => !Known(v, family)));
        }

        [Theory]
        [InlineData(GameFamilies.DP)]
        [InlineData(GameFamilies.Plat)]
        public void RegisteredValuesNothingReadsAreStillOffered(GameFamilies family)
        {
            foreach (byte v in new byte[] { 0x3C, 0x3D, 0x3E, 0x3F, 0x54, 0x55, 0xD0, 0xD1 })
                Assert.True(Known(v, family), v.ToString("X2"));
            Assert.True(Known(0xD1, GameFamilies.HGSS));
            Assert.False(Known(0xD0, GameFamilies.HGSS));
        }

        [Fact]
        public void DiamondAndPearlLackThePlatinumAdditions()
        {
            foreach (byte v in new byte[] { 0x1D, 0x2C, 0x2D, 0x5A, 0x5B, 0x5C, 0x5D, 0xA9 })
            {
                Assert.False(Known(v, GameFamilies.DP), v.ToString("X2"));
                Assert.True(Known(v, GameFamilies.Plat), v.ToString("X2"));
            }
            foreach (var b in TilePermissions.BehavioursFor(GameFamilies.DP))
                Assert.Equal(b.Key, TilePermissions.FindBehaviour(b.Value, GameFamilies.Plat)?.Key);
        }

        [Fact]
        public void HeartGoldOnlyValuesStayOutOfSinnoh()
        {
            foreach (byte v in new byte[] { 0x06, 0x11, 0x22, 0x23, 0x24, 0x2E, 0x4D })
            {
                Assert.True(Known(v, GameFamilies.HGSS), v.ToString("X2"));
                Assert.False(Known(v, GameFamilies.Plat), v.ToString("X2"));
                Assert.False(Known(v, GameFamilies.DP), v.ToString("X2"));
            }
            // Sinnoh registers 3C-3E too, but only Johto's are ladders.
            foreach (byte v in new byte[] { 0x3C, 0x3D, 0x3E })
                Assert.NotEqual(TilePermissions.FindBehaviour(v, GameFamilies.HGSS).Key, TilePermissions.FindBehaviour(v, GameFamilies.Plat).Key);
        }

        [Fact]
        public void SinnohOnlyValuesStayOutOfJohto()
        {
            var sinnohOnly = new byte[] { 0x0C, 0x44, 0x48, 0x50, 0x56, 0x59, 0x5A, 0x5D, 0x74, 0x75, 0x76, 0x7D, 0x90, 0xA0, 0xA1, 0xA3, 0xA5, 0xA7, 0xD7, 0xDB };
            foreach (byte v in sinnohOnly)
            {
                Assert.True(Known(v, GameFamilies.Plat), v.ToString("X2"));
                Assert.False(Known(v, GameFamilies.HGSS), v.ToString("X2"));
            }
        }

        [Fact]
        public void SharedValuesThatChangedMeaningAreNamedPerGame()
        {
            Assert.Equal("mirror_floor", TilePermissions.FindBehaviour(0x2C, GameFamilies.Plat).Key);
            Assert.Equal("magma", TilePermissions.FindBehaviour(0x2C, GameFamilies.HGSS).Key);
            Assert.Equal("no_explorer_kit", TilePermissions.FindBehaviour(0x2D, GameFamilies.Plat).Key);
            Assert.Equal("mirror_floor", TilePermissions.FindBehaviour(0x2D, GameFamilies.HGSS).Key);
        }

        [Fact]
        public void UnknownValuesShowTheirValue()
        {
            Assert.Equal("Unknown (0x05)", TilePermissions.BehaviourName(0x05, GameFamilies.Plat));
            Assert.Equal("Unknown (0x05)", TilePermissions.BehaviourLabel(0x05, GameFamilies.Plat));
            Assert.Equal("[3C] No effect (3C)", TilePermissions.BehaviourLabel(0x3C, GameFamilies.Plat));
            Assert.Equal("[3C] Ladder up (walk up)", TilePermissions.BehaviourLabel(0x3C, GameFamilies.HGSS));
            Assert.Equal("Unknown (0x05)", TilePermissions.BehaviourName(0x05, GameFamilies.HGSS));
        }

        [Fact]
        public void OnlyOpenWaterCountsAsSurf()
        {
            Assert.Equal(new byte[] { 0x10, 0x13, 0x15, 0x50, 0x51, 0x52, 0x53 },
                TilePermissions.BehavioursFor(GameFamilies.Plat).Where(b => b.Surf).Select(b => b.Value));
            Assert.Equal(new byte[] { 0x10, 0x11, 0x13, 0x15 },
                TilePermissions.BehavioursFor(GameFamilies.HGSS).Where(b => b.Surf).Select(b => b.Value));
            Assert.False(TilePermissions.IsSurfWater(0x17, GameFamilies.Plat));
            Assert.False(TilePermissions.IsSurfWater(0x73, GameFamilies.HGSS));
        }

        [Fact]
        public void AMeaningHasOneColourInEveryGame()
        {
            Assert.Equal(TilePermissions.Colour(0x2C, false, GameFamilies.Plat), TilePermissions.Colour(0x2D, false, GameFamilies.HGSS));
            Assert.Equal(TilePermissions.Colour(0x02, false, GameFamilies.DP), TilePermissions.Colour(0x02, false, GameFamilies.HGSS));
            Assert.NotEqual(TilePermissions.Colour(0x2C, false, GameFamilies.Plat), TilePermissions.Colour(0x2C, false, GameFamilies.HGSS));
        }

        [Theory]
        [InlineData(GameFamilies.DP)]
        [InlineData(GameFamilies.Plat)]
        [InlineData(GameFamilies.HGSS)]
        public void MeaningsInOneGameNeverShareAColour(GameFamilies family)
        {
            var colours = TilePermissions.BehavioursFor(family).Select(b => TilePermissions.Colour(b.Value, false, family)).ToList();
            Assert.Equal(colours.Count, colours.Distinct().Count());
        }

        [Theory]
        [InlineData(GameFamilies.DP)]
        [InlineData(GameFamilies.Plat)]
        public void SinnohCollisionIsWalkableOrBlocked(GameFamilies family)
        {
            Assert.Equal(new byte[] { 0x00, 0x80 }, TilePermissions.CollisionsFor(family).Select(c => c.Value));
            Assert.Equal("Walkable", TilePermissions.CollisionName(0x00, family));
            Assert.Equal("Blocked", TilePermissions.CollisionName(0x80, family));
            Assert.Equal("Unknown (0x04)", TilePermissions.CollisionName(0x04, family));
            Assert.Equal("Unknown (0x81)", TilePermissions.CollisionLabel(0x81, family));
        }

        [Fact]
        public void HeartGoldCollisionCarriesTheFootstepSound()
        {
            var offered = TilePermissions.CollisionsFor(GameFamilies.HGSS).Select(c => c.Value).ToList();
            Assert.Equal(Range(0x00, 0x0F).Concat(Range(0x80, 0x8F)), offered);
            Assert.All(HeartGoldRetailCollisions, v => Assert.Contains(v, offered));
            Assert.Equal("Walkable, footsteps: wooden planks", TilePermissions.CollisionName(0x0D, GameFamilies.HGSS));
            Assert.Equal("Blocked, footsteps: hard floor", TilePermissions.CollisionName(0x86, GameFamilies.HGSS));
            Assert.Equal("Walkable, footsteps: spare 1 (unused)", TilePermissions.CollisionName(0x0E, GameFamilies.HGSS));
            Assert.Equal("Unknown (0x10)", TilePermissions.CollisionName(0x10, GameFamilies.HGSS));
            var missing = HeartGoldRetailCollisions.Where(v => TilePermissions.CollisionName(v, GameFamilies.HGSS).StartsWith("Unknown")).ToList();
            Assert.True(missing.Count == 0, "Unnamed: " + string.Join(", ", missing.Select(v => v.ToString("X2"))));
        }
    }
}
