using System.Collections.Generic;
using DSPRE.Avalonia.Data;
using Xunit;
using static DSPRE.RomInfo;

namespace DSPRE.Tests
{
    /// <summary>Every particle file outside the per-move ones is named for what loads it, in every game.</summary>
    public class ParticleLibraryCategoryTests
    {
        [Theory]
        [InlineData("a/1/1/6", 0, ParticleFileNames.Story, "Egg hatching: shell pieces and sparkles")]
        [InlineData("a/1/1/9", 0, ParticleFileNames.Story, "Evolution: light and sparkles around the Pokémon")]
        [InlineData("a/1/1/9", 1, ParticleFileNames.Unused, "Evolution placeholder (never loaded)")]
        [InlineData("a/0/5/9", 2, ParticleFileNames.Intros, "VS intro, link battle: sparks")]
        [InlineData("a/0/5/9", 3, ParticleFileNames.Unused, "Contest Dance leftover (never loaded)")]
        [InlineData("a/1/8/8", 5, ParticleFileNames.Frontier, "Battle Factory: green smoke and rising blocks")]
        [InlineData("a/2/0/6", 0, ParticleFileNames.Menus, "Form change: Giratina")]
        [InlineData("a/2/1/1", 0, ParticleFileNames.Menus, "Wobbuffet Pop (Wi-Fi Plaza): balloon bursts")]
        [InlineData("a/1/0/9", 151, ParticleFileNames.Intros, "VS intro, Elite Four and Champion: first burst")]
        [InlineData("a/1/0/9", 152, ParticleFileNames.Intros, "VS intro, Elite Four and Champion: second burst")]
        [InlineData("a/1/9/8", 19, ParticleFileNames.Unused, "Wi-Fi Plaza winner effect (never loaded)")]
        [InlineData("a/0/9/6", 1, ParticleFileNames.Unused, "Debug particles 2 (never loaded)")]
        [InlineData("a/2/4/2", 10, ParticleFileNames.Menus, "Aprijuice: giving juice to a Pokémon")]
        public void HeartGoldArchivesAreNamedByNumber(string path, int member, string category, string name)
            => Assert.Equal((category, name), ParticleFileNames.Loose(GameFamilies.HGSS, path, member));

        [Fact]
        public void TheNumberMapOnlyAppliesToHeartGold()
            => Assert.Equal(ParticleFileNames.Other, ParticleFileNames.Loose(GameFamilies.Plat, "a/1/1/9", 0).Category);

        [Theory]
        [InlineData("graphic/field_encounteffect.narc", 107, ParticleFileNames.Intros)]
        [InlineData("graphic/field_encounteffect.narc", 108, ParticleFileNames.Intros)]
        [InlineData("particledata/pl_frontier/frontier_particle.narc", 2, ParticleFileNames.Frontier)]
        [InlineData("particledata/pl_etc/pl_etc_particle.narc", 0, ParticleFileNames.Menus)]
        [InlineData("particledata/pl_pokelist/pokelist_particle.narc", 1, ParticleFileNames.Menus)]
        [InlineData("particledata/particledata.narc", 3, ParticleFileNames.Menus)]
        [InlineData("particledata/particledata.narc", 4, ParticleFileNames.Story)]
        [InlineData("demo/shinka/data/particle/shinka_demo_particle.narc", 0, ParticleFileNames.Story)]
        public void PlatinumArchivesAreNamedByPath(string path, int member, string category)
            => Assert.Equal(category, ParticleFileNames.Loose(GameFamilies.Plat, path, member).Category);

        [Theory]
        [InlineData(GameFamilies.Plat, 2, ParticleFileNames.BattleEffects, "Level up")]
        [InlineData(GameFamilies.Plat, 5, ParticleFileNames.BattleEffects, "Battle start: grass, part 1")]
        [InlineData(GameFamilies.Plat, 26, ParticleFileNames.BattleEffects, "Battle start: Great Marsh, part 2")]
        [InlineData(GameFamilies.HGSS, 26, ParticleFileNames.Unused, "Battle start: Great Marsh, part 2 (not used in this game)")]
        [InlineData(GameFamilies.HGSS, 31, ParticleFileNames.BattleEffects, "Shiny sparkles")]
        [InlineData(GameFamilies.Plat, 30, ParticleFileNames.Unused, "Turn damage (never loaded)")]
        public void LeadingMoveArchiveFilesAreBattleEffects(GameFamilies family, int file, string category, string name)
            => Assert.Equal((category, name), ParticleFileNames.MoveArchive(family, file));

        [Fact]
        public void MoveFilesPastTheLeadingOnesAreNamedByTheirMoves()
        {
            Assert.Null(ParticleFileNames.MoveArchive(GameFamilies.Plat, 31));
            Assert.Null(ParticleFileNames.MoveArchive(GameFamilies.HGSS, 32));
        }

        [Theory]
        [InlineData(GameFamilies.Plat, 17, "Park Ball opening")]
        [InlineData(GameFamilies.Plat, 19, "Master Ball caught stars")]
        [InlineData(GameFamilies.Plat, 35, "Park Ball caught stars")]
        [InlineData(GameFamilies.Plat, 36, "Pokémon returning to its ball")]
        [InlineData(GameFamilies.HGSS, 27, "Master Ball caught stars")]
        [InlineData(GameFamilies.HGSS, 51, "Park Ball caught stars")]
        [InlineData(GameFamilies.HGSS, 52, "Pokémon returning to its ball")]
        public void BallFilesBeyondTheOpeningsAreNamed(GameFamilies family, int file, string name)
        {
            var balls = new Dictionary<int, string> { [1] = "Master Ball" };
            Assert.Equal(name, ParticleFileNames.BallArchive(family, file, balls)?.Name);
        }
    }
}
