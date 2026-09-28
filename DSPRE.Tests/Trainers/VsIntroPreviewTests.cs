using System;
using System.Collections.Generic;
using System.Linq;
using DSPRE.Avalonia.Data;
using DSPRE.ROMFiles;
using Xunit;
using Xunit.Abstractions;
using static DSPRE.ROMFiles.VsIntroTables;

namespace DSPRE.Tests
{
    /// <summary>The intro preview draws each record's own art from the retail archives.</summary>
    [Collection("rom")]
    public class VsIntroPreviewTests
    {
        private readonly ITestOutputHelper _out;
        public VsIntroPreviewTests(ITestOutputHelper o) => _out = o;

        private static int Differing(byte[] a, byte[] b) => Enumerable.Range(0, a.Length / 4).Count(i => a[4 * i] != b[4 * i] || a[4 * i + 1] != b[4 * i + 1] || a[4 * i + 2] != b[4 * i + 2]);

        [SkippableTheory]
        [InlineData("HeartGold")]
        [InlineData("Platinum")]
        public void EveryMugshotDrawsItsFaceAndBanner(string game)
        {
            DSPRE.Tests.Pokemon.GameTablesTests.Open(game);
            var t = VsIntroTables.Load();
            var art = new ScriptNarc(RomInfo.DirNames.encounterEffectGraphics);
            Assert.True(art.Available);
            var kinds = Enumerable.Range(0, art.Count).Select(i => GraphicAssets.Identify(art.Get(i))).ToArray();
            int[] Ordered(int first, int n) => new[] { GraphicAssets.Kind.Palette, GraphicAssets.Kind.TileGraphic, GraphicAssets.Kind.CellLayout, GraphicAssets.Kind.CellAnimation }
                .Select(k => Enumerable.Range(first, n).FirstOrDefault(i => kinds[i] == k, -1)).ToArray();

            var s = RomInfo.VsIntroCodeSites;
            var preview = new VsIntroPreview(art.Get, null);
            int checkedCount = 0;
            foreach (var r in t.Records)
            {
                var frame = Ordered(s.LeagueFrame, 3);
                VsIntroPreview.Scene Scene(bool face, bool banner) => new VsIntroPreview.Scene
                {
                    Kind = r.Kind == RecordKind.League ? VsIntroPreview.Layout.League : r.Kind == RecordKind.Executive ? VsIntroPreview.Layout.Executive : VsIntroPreview.Layout.Gym,
                    Face = face ? r.FaceMembers() : null, Banner = banner ? r.BannerMembers() : null, Vs = Ordered(s.VsMark, 4),
                    EndX = r.Has(RecordField.EndX) ? r.Get(RecordField.EndX) : 214,
                    Frame = new[] { frame[1], frame[2] }, FramePalette = r.Has(RecordField.FramePalette) ? r.Get(RecordField.FramePalette) : -1,
                    ClashFrames = r.Has(RecordField.ClashFrames) ? r.Get(RecordField.ClashFrames) : 0,
                };

                byte[] full = preview.Draw(Scene(true, true), -1);
                int face = Differing(full, preview.Draw(Scene(false, true), -1));
                _out.WriteLine($"{r.Kind} {r.Index}: face {face} px");
                Assert.True(face > 500, $"{r.Kind} {r.Index}: the face drew {face} pixels");
                if (r.BannerMembers() != null)
                {
                    int banner = Differing(full, preview.Draw(Scene(true, false), -1));
                    Assert.True(banner > 2000, $"{r.Kind} {r.Index}: the banner drew {banner} pixels");
                }
                var scene = Scene(true, true);
                for (int f = 0; f < VsIntroPreview.Length(scene); f++) Assert.Equal(full.Length, preview.Draw(scene, f).Length);
                checkedCount++;
            }
            Assert.True(checkedCount >= 13);
        }
    }
}
