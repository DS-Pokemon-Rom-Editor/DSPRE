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
    /// Putting a 3D file back into an archive.
    ///
    /// A mesh from a 3D program still cannot become one of these: a model here is a list of drawing
    /// commands for the DS's own hardware plus the bytecode that moves its bones, and nothing writes
    /// either, in DSPRE or in the other tools that open these files. What can be done is putting a
    /// finished NSBMD, NSBTX or animation file in, which is what the tools that build these produce, and
    /// that is what these check.
    /// </summary>
    [Collection("rom")]
    public class ModelImportTests
    {
        private readonly ITestOutputHelper _out;
        public ModelImportTests(ITestOutputHelper o) { _out = o; }

        private const string HeartGold = @"C:\Romhacking\ROMs\NDS\HGSS\HeartGold (USA)_DSPRE_contents";

        private static bool Ready()
        {
            if (!Directory.Exists(HeartGold)) return false;
            try { new RomInfo("IPKE", HeartGold); } catch { return false; }
            return true;
        }

        private static ModelAssets.Archive FirstArchiveWithModels(out int count)
        {
            foreach (var a in ModelAssets.All)
            {
                int n = ModelAssets.Count(a);
                if (n < 2) continue;
                var narc = new ScriptNarc(a.Dir);
                var b = narc.Get(0);
                if (b != null && ModelAssets.Identify(b) == ModelAssets.Kind.Model) { count = n; return a; }
            }
            count = 0;
            return null;
        }

        [Fact]
        public void AFilePutBackInComesOutTheSameBytes()
        {
            if (!Ready()) { _out.WriteLine("HeartGold not unpacked here"); return; }

            var archive = FirstArchiveWithModels(out int count);
            Assert.NotNull(archive);
            Assert.True(count >= 2, "need two entries so the test can also check the other one is left alone");

            var narc = new ScriptNarc(archive.Dir);
            byte[] mine = narc.Get(0)?.ToArray();
            byte[] neighbour = narc.Get(1)?.ToArray();
            Assert.NotNull(mine);
            Assert.NotNull(neighbour);

            // Take entry 1 out and put it in over entry 0, so what goes in is provably not what was there.
            Assert.False(mine.SequenceEqual(neighbour), "the two entries are identical, so this proves nothing");

            string file = Path.Combine(Path.GetTempPath(), "dspre_model_test.nsbmd");
            try
            {
                Assert.Null(ModelAssets.SaveRaw(archive, 1, file));
                Assert.True(File.ReadAllBytes(file).SequenceEqual(neighbour));

                Assert.Null(ModelAssets.ImportRaw(archive, 0, file));

                var after = new ScriptNarc(archive.Dir).Get(0);
                Assert.NotNull(after);
                Assert.True(neighbour.SequenceEqual(after),
                    $"what came back is {after.Length} bytes, not the {neighbour.Length} that went in");
                _out.WriteLine($"{archive.Title}: put {neighbour.Length} bytes in over {mine.Length}, "
                             + "byte for byte");
            }
            finally
            {
                new ScriptNarc(archive.Dir).Put(0, mine);
                try { File.Delete(file); } catch { }
            }

            // And the entry is back to exactly what it was.
            var restored = new ScriptNarc(archive.Dir).Get(0);
            Assert.True(mine.SequenceEqual(restored));
        }

        [Fact]
        public void TheWrongKindOfFileIsRefusedWithAReason()
        {
            if (!Ready()) { _out.WriteLine("HeartGold not unpacked here"); return; }

            var models = FirstArchiveWithModels(out int count);
            Assert.NotNull(models);

            // Something that is not 3D data at all.
            string junk = Path.Combine(Path.GetTempPath(), "dspre_not_a_model.bin");
            File.WriteAllBytes(junk, new byte[] { 0x50, 0x4E, 0x47, 0x0D, 1, 2, 3, 4 });

            // A texture bundle, which is 3D data but the wrong kind for a model's slot.
            byte[] textures = null;
            foreach (var a in ModelAssets.All)
            {
                var narc = new ScriptNarc(a.Dir);
                if (!narc.Available) continue;
                for (int i = 0; i < Math.Min(narc.Count, 40); i++)
                {
                    var b = narc.Get(i);
                    if (b != null && ModelAssets.Identify(b) == ModelAssets.Kind.TextureBundle)
                    { textures = b; break; }
                }
                if (textures != null) break;
            }
            string wrongKind = Path.Combine(Path.GetTempPath(), "dspre_wrong_kind.nsbtx");
            if (textures != null) File.WriteAllBytes(wrongKind, textures);

            byte[] before = new ScriptNarc(models.Dir).Get(0)?.ToArray();
            Assert.NotNull(before);
            try
            {
                string why = ModelAssets.ImportRaw(models, 0, junk);
                Assert.False(string.IsNullOrWhiteSpace(why));
                _out.WriteLine("not 3D data: " + why);

                if (textures != null)
                {
                    string why2 = ModelAssets.ImportRaw(models, 0, wrongKind);
                    Assert.False(string.IsNullOrWhiteSpace(why2));
                    _out.WriteLine("wrong kind: " + why2);
                }
                else _out.WriteLine("no texture bundle found to try the wrong-kind case with");

                // Nothing may have been written by either refusal.
                var now = new ScriptNarc(models.Dir).Get(0);
                Assert.True(before.SequenceEqual(now), "a refused file was written anyway");
            }
            finally
            {
                try { File.Delete(junk); } catch { }
                try { File.Delete(wrongKind); } catch { }
            }
        }

        /// <summary>The refusal check proves able to fail: a file of the right kind must be accepted, or
        /// the test above would pass simply because everything is refused.</summary>
        [Fact]
        public void TheRefusalCheckStillLetsTheRightKindThrough()
        {
            if (!Ready()) { _out.WriteLine("HeartGold not unpacked here"); return; }
            var archive = FirstArchiveWithModels(out int count);
            Assert.NotNull(archive);

            var narc = new ScriptNarc(archive.Dir);
            byte[] mine = narc.Get(0)?.ToArray();
            Assert.NotNull(mine);

            string file = Path.Combine(Path.GetTempPath(), "dspre_same_kind.nsbmd");
            File.WriteAllBytes(file, mine);
            try
            {
                Assert.Null(ModelAssets.ImportRaw(archive, 0, file));
                _out.WriteLine($"a {ModelAssets.Identify(mine)} was accepted into a {ModelAssets.Identify(mine)} slot");
            }
            finally
            {
                new ScriptNarc(archive.Dir).Put(0, mine);
                try { File.Delete(file); } catch { }
            }
        }

        /// <summary>Every 3D entry in every archive either says a file can go in it or says why not.
        /// Never a button that is off with nothing said.</summary>
        [Fact]
        public void EveryEntryEitherTakesAFileOrSaysWhyNot()
        {
            if (!Ready()) { _out.WriteLine("HeartGold not unpacked here"); return; }

            int looked = 0, takes = 0;
            var silent = new List<string>();
            foreach (var a in ModelAssets.All)
            {
                int n = ModelAssets.Count(a);
                if (n == 0) continue;
                // Every entry of the small archives, and a spread through the large ones.
                int step = n > 60 ? n / 60 : 1;
                for (int i = 0; i < n; i += step)
                {
                    looked++;
                    string why = ModelAssets.CannotImportBecause(a, i);
                    if (why == null) { takes++; continue; }
                    if (string.IsNullOrWhiteSpace(why)) silent.Add($"{a.Title}[{i}]");
                }
            }

            _out.WriteLine($"{looked} entries looked at across {ModelAssets.All.Length} archives, "
                         + $"{takes} take a file, {looked - takes} say why not");
            Assert.True(looked > 200, $"only {looked} entries were looked at, the sweep proved little");
            Assert.True(takes > 0, "nothing at all takes a file, so the sweep proved nothing");
            Assert.Empty(silent);
        }
    }
}
