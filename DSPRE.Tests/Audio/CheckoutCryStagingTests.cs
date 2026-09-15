using System;
using System.IO;
using System.Linq;
using DSPRE.Avalonia.Data;
using Xunit;

namespace DSPRE.Tests
{
    /// <summary>On hg-engine an imported cry is checked and held, and only written when the editor saves.</summary>
    public class CheckoutCryStagingTests
    {
        private static string MakeRoot(out string wavPath, out byte[] wav)
        {
            string root = Path.Combine(Path.GetTempPath(), "dspre_cry_stage_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            var pcm = Enumerable.Range(0, 800).Select(i => (short)((i % 40 - 20) * 800)).ToArray();
            wav = CryFiles.WriteWav(pcm, 22050);
            wavPath = Path.Combine(root, "in.wav");
            File.WriteAllBytes(wavPath, wav);
            return root;
        }

        [Fact]
        public void APreparedCryWritesNothingUntilItIsWritten()
        {
            string root = MakeRoot(out string wavPath, out byte[] wav);
            try
            {
                var cry = SoundArchive.PrepareCheckoutCry(25, wavPath, out string problem);
                Assert.True(cry != null, problem);
                Assert.Equal("sound/cries/025.wav", cry.RelPath);

                string target = Path.Combine(root, "sound", "cries", "025.wav");
                Assert.False(File.Exists(target));

                Assert.True(SoundArchive.WriteCheckoutCry(cry, root, out problem), problem);
                Assert.Equal(wav, File.ReadAllBytes(target));
            }
            finally { Directory.Delete(root, true); }
        }

        [Fact]
        public void ASlotWithoutACryOfItsOwnIsRefused()
        {
            string root = MakeRoot(out string wavPath, out _);
            try
            {
                var cry = SoundArchive.PrepareCheckoutCry(500, wavPath, out string problem);
                Assert.Null(cry);
                Assert.False(string.IsNullOrEmpty(problem));
                Assert.False(Directory.Exists(Path.Combine(root, "sound")));
            }
            finally { Directory.Delete(root, true); }
        }
    }
}
