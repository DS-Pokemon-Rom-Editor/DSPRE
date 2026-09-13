using System.Collections.Generic;
using System.IO;
using DSPRE;
using DSPRE.Avalonia;
using DSPRE.Avalonia.Data;
using DSPRE.ROMFiles;
using Xunit;
using static DSPRE.RomInfo;

namespace DSPRE.Tests
{
    /// <summary>A particle quad spans 2^tileCount texture widths; the texture's repeat and flip bits decide what lies past the first.</summary>
    [Collection("rom")]
    public class SpaTextureTilingTests
    {
        [Fact]
        public void WithoutRepeatACoordinatePastTheEdgeKeepsTheEdgeTexel()
        {
            Assert.Equal(0.5, SpaParticlePreview.TexelCoord(0.5, repeat: false, flip: false), 6);
            Assert.True(SpaParticlePreview.TexelCoord(1.5, repeat: false, flip: false) > 0.9999);
            Assert.True(SpaParticlePreview.TexelCoord(1.5, repeat: false, flip: true) > 0.9999);
            Assert.Equal(0.0, SpaParticlePreview.TexelCoord(-0.2, repeat: false, flip: false), 6);
        }

        [Fact]
        public void RepeatWrapsAndRepeatWithFlipMirrors()
        {
            Assert.Equal(0.25, SpaParticlePreview.TexelCoord(1.25, repeat: true, flip: false), 6);
            Assert.Equal(0.75, SpaParticlePreview.TexelCoord(1.25, repeat: true, flip: true), 6);
            Assert.Equal(0.25, SpaParticlePreview.TexelCoord(0.25, repeat: true, flip: true), 6);
            Assert.True(SpaParticlePreview.TexelCoord(1.999, repeat: true, flip: true) < 0.01);
        }

        private static string Project(string name) => name switch
        {
            "Diamond" => TestRoms.Diamond, "Platinum" => TestRoms.Platinum, _ => TestRoms.HeartGold,
        };

        [SkippableTheory]
        [InlineData("ADAE", "Diamond")]
        [InlineData("CPUE", "Platinum")]
        [InlineData("IPKE", "HeartGold")]
        public void FloraSealCTilesAQuadrantItsTextureDoesNotRepeat(string code, string name)
        {
            // Each petal fills one corner of its quad at half size.
            Skip.IfNot(Directory.Exists(Project(name)), $"{name} is not unpacked here");
            new RomInfo(code, Project(name));
            DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.ballParticles });
            var flora = BallSeals.Read()[39];
            Assert.StartsWith("Flora Seal C", flora.Name, System.StringComparison.OrdinalIgnoreCase);

            var arc = SpaArchive.Parse(new ScriptNarc(DirNames.ballParticles).Get(flora.Particle));
            var em = arc.Emitters[0];
            Assert.Equal(2, em.TileS);
            Assert.Equal(2, em.TileT);
            var tex = arc.Textures[em.TexNo];
            Assert.False(tex.RepeatS);
            Assert.False(tex.RepeatT);
        }
    }
}
