using System;
using System.IO;
using DSPRE.Avalonia.Data;
using Xunit;

namespace DSPRE.Tests
{
    /// <summary>
    /// The SDAT sound archive = a 48-byte header giving the offset/size of an optional SYMB (name) block, a
    /// mandatory INFO block and a mandatory FAT block. INFO holds one offset table per category (sequences,
    /// banks, wave archives, ...), each entry pointing (relative to the INFO block's own start) at a small fixed
    /// record; FAT holds one {byte offset, size} pair per sub-file, absolute from the .sdat file's own start.
    /// These pin that layout with a minimal hand-built archive (one sequence, no banks/wave archives/names).
    /// </summary>
    public class SdatArchiveTests
    {
        private static void W16(byte[] b, int o, int v) { b[o] = (byte)v; b[o + 1] = (byte)(v >> 8); }
        private static void W32(byte[] b, int o, int v) { for (int i = 0; i < 4; i++) b[o + i] = (byte)(v >> (8 * i)); }
        private static void WSig(byte[] b, int o, string sig) { for (int i = 0; i < 4; i++) b[o + i] = (byte)sig[i]; }

        [Fact]
        public void Parse_OneSequence_ResolvesFileBytes()
        {
            var b = new byte[140];
            WSig(b, 0, "SDAT");
            W32(b, 24, 48);    // infoOffset
            W32(b, 28, 60);    // infoSize
            W32(b, 32, 108);   // fatOffset
            W32(b, 36, 32);    // fatSize

            // INFO block @48: 8-byte block header (unchecked) + 8 category offsets (relative to infoOffset).
            W32(b, 48 + 8 + 0, 40);   // seqOffset -> offset table at 48+40=88

            // seq offset table @88: count=1, offset[0]=48 (relative to infoOffset) -> record at 48+48=96
            W32(b, 88, 1);
            W32(b, 92, 48);

            // NNSSndArcSeqInfo @96 (12 bytes): fileId, bankNo, volume, channelPrio, playerPrio, playerNo, reserved
            W32(b, 96, 0);       // fileId = 0
            W16(b, 100, 0xFFFF); // bankNo (not asserted)
            b[102] = 100;        // volume

            // FAT block @108: 8-byte block header (unchecked) + count=1 + one {offset,size,mem,reserved} entry.
            W32(b, 108 + 8, 1);
            W32(b, 108 + 12, 136);   // files[0].offset (absolute from file start)
            W32(b, 108 + 16, 4);     // files[0].size
            WSig(b, 136, "TEST");    // the sub-file's raw bytes

            var a = SdatArchive.Parse(b);
            Assert.Single(a.Sequences);
            Assert.Equal(0, a.Sequences[0].FileId);
            Assert.Equal(100, a.Sequences[0].Volume);

            var fileBytes = a.GetFileBytes(0);
            Assert.Equal(4, fileBytes.Length);
            Assert.Equal("TEST", System.Text.Encoding.ASCII.GetString(fileBytes));
        }
    }

    /// <summary>SWAR wave archives: a file header, a DATA block with a 32-byte reserved area (the in-memory
    /// struct's link pointers, zero on disk) before the real wave count, then an offset table whose entries are
    /// absolute from the SWAR sub-file's own byte 0. Pins PCM8/PCM16 decode to 16-bit PCM.</summary>
    public class SwavSampleTests
    {
        private static void W16(byte[] b, int o, int v) { b[o] = (byte)v; b[o + 1] = (byte)(v >> 8); }
        private static void W32(byte[] b, int o, int v) { for (int i = 0; i < 4; i++) b[o + i] = (byte)(v >> (8 * i)); }
        private static void WSig(byte[] b, int o, string sig) { for (int i = 0; i < 4; i++) b[o + i] = (byte)sig[i]; }

        [Fact]
        public void ParseArchive_Pcm16_DecodesSamples()
        {
            var b = new byte[84];
            WSig(b, 0, "SWAR");
            WSig(b, 16, "DATA");
            W32(b, 56, 1);    // wave count (after the 32-byte reserved area @24..55)
            W32(b, 60, 64);   // offset[0], absolute from file start

            b[64] = 1;                  // waveType = PCM16
            b[65] = 0;                  // loop = false
            W16(b, 66, 16554);          // sampleRate
            W16(b, 70, 0);               // loopOffsetWords
            W32(b, 72, 2);               // nonLoopLenWords -> 2*2 = 4 PCM16 samples
            W16(b, 76, 100); W16(b, 78, unchecked((ushort)(-100)));
            W16(b, 80, 200); W16(b, 82, unchecked((ushort)(-200)));

            var waves = SwavSample.ParseArchive(b);
            Assert.Single(waves);
            Assert.Equal(16554, waves[0].SampleRate);
            Assert.False(waves[0].Loop);
            Assert.Equal(new short[] { 100, -100, 200, -200 }, waves[0].Pcm);
        }

        [Fact]
        public void ParseArchive_Pcm8_ScalesToFullRange()
        {
            var b = new byte[80];
            WSig(b, 0, "SWAR");
            WSig(b, 16, "DATA");
            W32(b, 56, 1);
            W32(b, 60, 64);

            b[64] = 0;                  // waveType = PCM8
            b[65] = 0;                  // loop = false
            W16(b, 66, 8000);           // sampleRate
            W16(b, 70, 0);              // loopOffsetWords
            W32(b, 72, 1);              // nonLoopLenWords -> 1*4 = 4 PCM8 samples
            b[76] = unchecked((byte)(sbyte)10);
            b[77] = unchecked((byte)(sbyte)-10);
            b[78] = 127;
            b[79] = 128;   // (sbyte)128 == -128

            var waves = SwavSample.ParseArchive(b);
            Assert.Single(waves);
            Assert.Equal(new short[] { 2560, -2560, 32512, -32768 }, waves[0].Pcm);
        }
    }

    /// <summary>SBNK instrument banks: same reserved-area convention as SWAR, then a type-tagged offset table
    /// (record type in the low byte, a 24-bit offset, absolute from the SBNK sub-file's own byte 0, in the
    /// rest). Pins the single-region (type 1) and key-split (type 0x11, multi-region) record shapes.</summary>
    public class SbnkBankTests
    {
        private static void W16(byte[] b, int o, int v) { b[o] = (byte)v; b[o + 1] = (byte)(v >> 8); }
        private static void W32(byte[] b, int o, int v) { for (int i = 0; i < 4; i++) b[o + i] = (byte)(v >> (8 * i)); }
        private static void WSig(byte[] b, int o, string sig) { for (int i = 0; i < 4; i++) b[o + i] = (byte)sig[i]; }
        private static void WEntry(byte[] b, int o, int recordType, int relOff) => W32(b, o, recordType | (relOff << 8));

        [Fact]
        public void ParseBank_SingleRegion_CoversWholeKeyRange()
        {
            var b = new byte[74];
            WSig(b, 0, "SBNK");
            WSig(b, 16, "DATA");
            W32(b, 56, 1);            // instrument count
            WEntry(b, 60, 1, 64);     // slot 0: type=1 (single-region PCM) @ offset 64

            W16(b, 64, 5);    // sampleIndex
            W16(b, 66, 2);    // waveArcSlot
            b[68] = 60;       // unityKey / base note

            var insts = SbnkBank.ParseBank(b);
            Assert.Single(insts);
            var region = insts[0].Resolve(72);
            Assert.NotNull(region);
            Assert.Equal(0, region.LowKey);
            Assert.Equal(127, region.HighKey);
            Assert.Equal(5, region.WaveIndex);
            Assert.Equal(2, region.WaveArcSlot);
            Assert.Equal(60, region.BaseNote);
        }

        [Fact]
        public void ParseBank_KeySplit_ResolvesRegionByNote()
        {
            // Two regions: notes 0..59 -> region A, notes 60..127 -> region B.
            var b = new byte[64 + 8 + 24];
            WSig(b, 0, "SBNK");
            WSig(b, 16, "DATA");
            W32(b, 56, 1);
            WEntry(b, 60, 0x11, 64);   // slot 0: type 0x11 (key-split) @ offset 64

            b[64] = 59;    // keyRanges[0] (region A's high key)
            b[65] = 127;   // keyRanges[1] (region B's high key)
            b[66] = 0;     // terminator

            int rgnA = 64 + 8, rgnB = rgnA + 12;
            W16(b, rgnA + 2, 10); W16(b, rgnA + 4, 0); b[rgnA + 6] = 48;
            W16(b, rgnB + 2, 20); W16(b, rgnB + 4, 1); b[rgnB + 6] = 72;

            var insts = SbnkBank.ParseBank(b);
            Assert.Single(insts);
            Assert.Equal(2, insts[0].Regions.Count);

            var low = insts[0].Resolve(30);
            Assert.Equal(10, low.WaveIndex); Assert.Equal(0, low.WaveArcSlot); Assert.Equal(48, low.BaseNote);
            var high = insts[0].Resolve(90);
            Assert.Equal(20, high.WaveIndex); Assert.Equal(1, high.WaveArcSlot); Assert.Equal(72, high.BaseNote);
        }
    }

    /// <summary>End-to-end coverage against the real HeartGold ROM's sound archive: renders every real SEQ_SE_*
    /// sequence to PCM and asserts none of them throw, mirroring this project's established whole-archive smoke
    /// test pattern used for the particle engine. Skips (rather than fails) if that ROM project isn't present on
    /// this machine, since it's a large external test asset, not something checked into the repo.</summary>
    public class SseqWholeArchiveSmokeTests
    {
        private static readonly string RomSdatPath = TestRoms.HeartGold + @"\files\data\sound\gs_sound_data.sdat";

        [SkippableFact]
        public void RenderEverySoundEffect_NoExceptions()
        {
            Skip.If(!File.Exists(RomSdatPath), "the extracted game project these tests read is not on this machine");

            var sdat = SdatArchive.Parse(File.ReadAllBytes(RomSdatPath));
            int total = 0, exceptions = 0;
            foreach (var kv in sdat.SeqNames)
            {
                if (!kv.Value.StartsWith("SEQ_SE_", StringComparison.Ordinal)) continue;
                total++;
                try { SseqPlayer.Render(sdat, kv.Key, maxSeconds: 5.0); }
                catch { exceptions++; }
            }

            Assert.True(total > 500, $"expected hundreds of SEQ_SE_* entries, found {total}, is this the right ROM?");
            Assert.Equal(0, exceptions);
        }

        // Platinum's own file name, which the game asks for by name:
        // "data/sound/pl_sound_data.sdat". Same container/SSEQ/SBNK/SWAV format as
        // HGSS (shared Nitro SDK), this guards that the parser/renderer generalize, not just fit one ROM's data.
        private static readonly string PlatRomSdatPath = TestRoms.Platinum + @"\files\data\sound\pl_sound_data.sdat";

        [SkippableFact]
        public void RenderEverySoundEffect_Platinum_NoExceptions()
        {
            Skip.If(!File.Exists(PlatRomSdatPath), "the extracted game project these tests read is not on this machine");

            var sdat = SdatArchive.Parse(File.ReadAllBytes(PlatRomSdatPath));
            int total = 0, exceptions = 0;
            foreach (var kv in sdat.SeqNames)
            {
                if (!kv.Value.StartsWith("SEQ_SE_", StringComparison.Ordinal)) continue;
                total++;
                try { SseqPlayer.Render(sdat, kv.Key, maxSeconds: 5.0); }
                catch { exceptions++; }
            }

            Assert.True(total > 500, $"expected hundreds of SEQ_SE_* entries, found {total}, is this the right ROM?");
            Assert.Equal(0, exceptions);
        }

        // DP's file name is genuinely different from Platinum's, not just "whatever Plat uses":
        // Diamond and Pearl ask for "data/sound/sound_data.sdat", with no prefix, and Platinum
        // replaced it with its own.
        private static readonly string DpRomSdatPath = TestRoms.Diamond + @"\files\data\sound\sound_data.sdat";

        [SkippableFact]
        public void RenderEverySoundEffect_Diamond_NoExceptions()
        {
            Skip.If(!File.Exists(DpRomSdatPath), "the extracted game project these tests read is not on this machine");

            var sdat = SdatArchive.Parse(File.ReadAllBytes(DpRomSdatPath));
            int total = 0, exceptions = 0;
            foreach (var kv in sdat.SeqNames)
            {
                if (!kv.Value.StartsWith("SEQ_SE_", StringComparison.Ordinal)) continue;
                total++;
                try { SseqPlayer.Render(sdat, kv.Key, maxSeconds: 5.0); }
                catch { exceptions++; }
            }

            Assert.True(total > 500, $"expected hundreds of SEQ_SE_* entries, found {total}, is this the right ROM?");
            Assert.Equal(0, exceptions);
        }
    }
}
