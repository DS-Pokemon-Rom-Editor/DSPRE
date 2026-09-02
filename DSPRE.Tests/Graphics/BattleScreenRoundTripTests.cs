using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DSPRE;
using DSPRE.Avalonia.Data;
using Xunit;
using Xunit.Abstractions;

namespace DSPRE.Tests
{
    /// <summary>
    /// Taking a piece of the battle screen out as a PNG and putting it straight back must change
    /// nothing. Everything here happens on a copy of the project, never the real one.
    /// </summary>
    [Collection("rom")]
    public class BattleScreenRoundTripTests : IDisposable
    {
        private readonly ITestOutputHelper _out;
        public BattleScreenRoundTripTests(ITestOutputHelper o) => _out = o;

        private const string Source =
            @"C:\Romhacking\ROMs\NDS\HGSS\HeartGold (USA)_DSPRE_contents";

        private string _work;

        private bool OpenACopy()
        {
            if (!Directory.Exists(Source)) return false;
            _work = Path.Combine(Path.GetTempPath(), "dspre_battle_" + Guid.NewGuid().ToString("N")[..8]);
            Directory.CreateDirectory(_work);
            foreach (var d in Directory.GetDirectories(Source, "*", SearchOption.AllDirectories))
                Directory.CreateDirectory(d.Replace(Source, _work));
            foreach (var f in Directory.GetFiles(Source, "*", SearchOption.AllDirectories))
                File.Copy(f, f.Replace(Source, _work), true);
            new RomInfo("IPKE", _work);
            GraphicAssets.Forget();
            DSUtils.TryUnpackNarcs(new List<RomInfo.DirNames> {
                RomInfo.DirNames.battleObj, RomInfo.DirNames.battleBg, RomInfo.DirNames.windowFrames });
            return true;
        }

        public void Dispose()
        {
            if (_work != null && Directory.Exists(_work))
                try { Directory.Delete(_work, true); } catch { }
        }

        [Fact]
        public void EveryPieceTheEditorOffersToEditGoesOutAndBackUnchanged()
        {
            if (!OpenACopy()) { _out.WriteLine("HeartGold is not unpacked here"); return; }

            var pieces = new BattleScreenRenderer().Build(new BattleScreenRenderer.Options { TerrainId = 2 });
            Assert.NotEmpty(pieces);

            string png = Path.Combine(_work, "piece.png");
            int tried = 0;
            foreach (var piece in pieces)
            {
                // Only what the editor actually offers to edit. It says on screen why the rest are
                // left out, and this walks the same list rather than a wider one.
                if (piece.Rgba == null || piece.Drawing < 0 || piece.CannotEditBecause != null) continue;
                var archive = GraphicAssets.All.FirstOrDefault(a => a.Dir == piece.Archive);
                if (archive == null) continue;
                int at = piece.Layout >= 0 ? piece.Layout : piece.Drawing;

                string dir = RomInfo.gameDirs[piece.Archive].unpackedDir;
                if (!Directory.Exists(dir)) continue;
                var files = RomFiles.Settled(dir);
                if (at >= files.Length) continue;

                byte[] before = File.ReadAllBytes(files[at]);

                string trouble = GraphicAssets.ExportPng(archive, at, png);
                Assert.True(trouble == null, $"{piece.Name}: could not be saved out: {trouble}");
                Assert.True(new FileInfo(png).Length > 0, piece.Name + ": saved an empty file");

                trouble = GraphicAssets.ImportPng(archive, at, png, out _);
                Assert.True(trouble == null, $"{piece.Name}: could not be put back: {trouble}");

                byte[] after = File.ReadAllBytes(files[at]);
                Assert.True(before.SequenceEqual(after),
                    $"{piece.Name}: entry {at} of {piece.Archive} changed when nothing was edited "
                    + $"({before.Length} bytes before, {after.Length} after)");
                tried++;
                _out.WriteLine($"{piece.Name}: entry {at} came back byte for byte");
            }

            Assert.True(tried > 0, "no piece was editable, so the round trip proved nothing");
            _out.WriteLine($"{tried} pieces went out and back unchanged");
        }
    }
}
