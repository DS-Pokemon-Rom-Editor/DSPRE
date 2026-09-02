using System;
using System.IO;
using System.Linq;
using MKDS_Course_Editor.NSBTA;
using Xunit;

namespace DSPRE.Tests
{
    /// <summary>
    /// Parses the real terrain-animation archive when a HeartGold project is available locally.
    /// </summary>
    public class GroundAnimationParseTests
    {
        private const string Archive =
            @"C:\Romhacking\ROMs\NDS\HGSS\HeartGold (USA)_DSPRE_contents\files\a\1\4\0";

        private static byte[][] ReadNarc(string path)
        {
            byte[] b = File.ReadAllBytes(path);
            int o = 16;
            int count = BitConverter.ToInt32(b, o + 8);
            int fat = o + 12;
            o += BitConverter.ToInt32(b, o + 4);
            o += BitConverter.ToInt32(b, o + 4);
            int img = o + 8;
            return Enumerable.Range(0, count).Select(i =>
            {
                int start = BitConverter.ToInt32(b, fat + i * 8);
                int end = BitConverter.ToInt32(b, fat + i * 8 + 4);
                return b.Skip(img + start).Take(end - start).ToArray();
            }).ToArray();
        }

        [Fact]
        public void TerrainAnimationsParseIntoMaterialTracks()
        {
            if (!File.Exists(Archive)) return;   // no local HeartGold project; nothing to check against

            byte[][] files = ReadNarc(Archive);
            Assert.NotEmpty(files);

            foreach (byte[] file in files)
            {
                Assert.Equal("BTA0", System.Text.Encoding.ASCII.GetString(file, 0, 4));

                var anim = NSBTA.Read(file);
                Assert.Equal("BTA0", anim.Header.ID);

                // Every animation targets at least one material, and each target has SRT tracks.
                Assert.True(anim.MAT.num_objs > 0, "animation targets no materials");
                Assert.Equal(anim.MAT.num_objs, anim.SRTData.Length);
                Assert.Equal(anim.MAT.num_objs, anim.MAT.names.Length);
                Assert.All(anim.MAT.names, n => Assert.False(string.IsNullOrWhiteSpace(n)));
            }
        }
    }
}
