using System;
using System.IO;
using System.Linq;
using DSPRE;
using DSPRE.Avalonia.Data;
using Xunit;
using Xunit.Abstractions;
using static DSPRE.RomInfo;

namespace DSPRE.Tests
{
    /// <summary>
    /// The animation preview draws sprite sheets through <see cref="DsBgScreen.ReadCharacters"/>, which
    /// copies the pixels out as they are stored. Trainer sheets come out of that as noise, so this asks
    /// the shipped reader the same question: if <see cref="GraphicAssets.ReadIndexed"/> gets a picture
    /// where the animation path gets static, the animation path is missing whatever that one does.
    /// </summary>
    [Collection("rom")]
    public class TrainerSheetReadingTests
    {
        private readonly ITestOutputHelper _out;
        public TrainerSheetReadingTests(ITestOutputHelper output) => _out = output;

        /// <summary>A drawn picture repeats itself: shapes are runs of one colour. Static does not.</summary>
        private static bool LooksDrawn(byte[] pixels)
        {
            int same = 0;
            for (int i = 1; i < pixels.Length; i++)
                if (pixels[i] == pixels[i - 1]) same++;
            return same * 2 > pixels.Length;
        }

        private static int Repeats(byte[] pixels)
        {
            int same = 0;
            for (int i = 1; i < pixels.Length; i++)
                if (pixels[i] == pixels[i - 1]) same++;
            return same;
        }

        /// <summary>The same bytes as the animation path sees them: four bits a pixel, straight out.</summary>
        private static byte[] AsAnimationSeesIt(byte[] sheet)
        {
            var flat = new byte[sheet.Length * 2];
            for (int i = 0; i < sheet.Length; i++)
            {
                flat[i * 2] = (byte)(sheet[i] & 0x0F);
                flat[i * 2 + 1] = (byte)(sheet[i] >> 4);
            }
            return flat;
        }

        private static bool Open(string project, string id)
        {
            if (!Directory.Exists(project)) return false;
            try { new RomInfo(id, project); GraphicAssets.Forget(); } catch { return false; }
            return true;
        }

        [SkippableTheory]
        [InlineData("CPUE", "Platinum")]
        [InlineData("IPKE", "HeartGold")]
        public void TheShippedReaderAndTheAnimationPathAgreeOnWhetherASheetIsAPicture(string code, string game)
        {
            string path = code == "CPUE" ? TestRoms.Platinum : TestRoms.HeartGold;
            Skip.If(!Open(path, code), $"{game} is not unpacked here");

            var archive = GraphicAssets.All.FirstOrDefault(a => a.Dir == DirNames.trainerGraphics);
            Assert.NotNull(archive);

            var narc = new ScriptNarc(DirNames.trainerGraphics);
            Skip.If(!narc.Available, $"{game} has no trainer graphics archive");

            int looked = 0, shippedDraws = 0, animationDraws = 0;
            var disagreed = 0;

            // The drawing of a class sits at 5n, so these are real sheets rather than colours or layouts.
            for (int member = 0; member < Math.Min(narc.Count, 120); member += 5)
            {
                var ix = GraphicAssets.ReadIndexed(archive, member, out string whynot);
                byte[] raw = NitroBgCodec.Inflate(narc.Get(member));
                byte[] sheet = DsBgScreen.ReadCharacters(raw);
                if (ix?.Indices == null || sheet.Length == 0) continue;

                looked++;
                bool shipped = LooksDrawn(ix.Indices);
                bool animation = LooksDrawn(AsAnimationSeesIt(sheet));
                if (shipped) shippedDraws++;
                if (animation) animationDraws++;
                if (shipped != animation) disagreed++;

                if (looked <= 4)
                    _out.WriteLine($"{game} #{member}: shipped reader {(shipped ? "picture" : "noise")} "
                                 + $"({Repeats(ix.Indices)}/{ix.Indices.Length} repeat), "
                                 + $"animation path {(animation ? "picture" : "noise")} "
                                 + $"({Repeats(AsAnimationSeesIt(sheet))}/{sheet.Length * 2} repeat)"
                                 + (whynot == null ? "" : $", whynot: {whynot}"));
            }

            Skip.If(looked == 0, $"{game}: no trainer sheet could be read both ways");
            _out.WriteLine($"{game}: {looked} sheets, shipped reader drew {shippedDraws}, "
                         + $"animation path drew {animationDraws}, they disagreed on {disagreed}");

            // Recorded as a measurement, not a wish. If the shipped reader manages a picture where the
            // animation path does not, the animation path is missing a step and this says how often.
            Assert.True(looked > 5, $"{game}: only {looked} sheets were compared");
        }
    }
}
