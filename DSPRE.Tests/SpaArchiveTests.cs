using System;
using DSPRE.Avalonia.Data;
using Xunit;

namespace DSPRE.Tests
{
    /// <summary>
    /// The SPA particle archive = a 32-byte header then variable-length emitter records (88-byte base + flag-gated
    /// anim/child blocks + fields). These pin the header fields, the SPLResBase offsets, and the record-walk that
    /// must skip optional blocks to reach the next emitter, matching the NDS particle-library resource format.
    /// </summary>
    public class SpaArchiveTests
    {
        private static void W16(byte[] b, int o, int v) { b[o] = (byte)v; b[o + 1] = (byte)(v >> 8); }
        private static void W32(byte[] b, int o, int v) { for (int i = 0; i < 4; i++) b[o + i] = (byte)(v >> (8 * i)); }

        private static void WriteHeader(byte[] b, int resNum, int texNum, int texSize, int texOffset)
        {
            W32(b, 0, 0x41505320); // 'id'
            W32(b, 4, 0x0200);     // ver
            W16(b, 8, resNum);
            W16(b, 10, texNum);
            W32(b, 20, texSize);
            W32(b, 24, texOffset);
        }

        // Fills an 88-byte SPLResBase at offset `o`.
        private static void WriteBase(byte[] b, int o, uint flag)
        {
            W32(b, o + 0, (int)flag);
            W32(b, o + 4, 0x2000);   // pos.x = 2.0
            W32(b, o + 8, 0x1000);   // pos.y = 1.0
            W32(b, o + 12, 0);       // pos.z
            W32(b, o + 16, 0x8000);  // gen_num = 8.0
            W32(b, o + 20, 0x4000);  // radius = 4.0
            W16(b, o + 34, 0x001F);  // clr_n = red (R=31)
            W32(b, o + 36, 0x1000);  // init_vel_mag_pos = 1.0
            W32(b, o + 44, 0x2000);  // base_scl = 2.0
            W16(b, o + 60, 60);      // emtr_life
            W16(b, o + 62, 30);      // ptcl_life
            W32(b, o + 68, 2 | (31 << 8) | (0x80 << 16) | (3 << 24)); // gen_intvl,base_alp,air_resist,tex_no
        }

        [Fact]
        public void Parse_SingleEmitter_BaseParams()
        {
            var b = new byte[32 + 88];
            WriteHeader(b, resNum: 1, texNum: 2, texSize: 0x100, texOffset: 0x200);
            WriteBase(b, 32, flag: 1);   // init_pos_type = 1, no anim/fields

            var a = SpaArchive.Parse(b);
            Assert.Equal(2, a.TextureCount);
            Assert.Equal(0x200, a.TextureOffset);
            Assert.Equal(0x100, a.TextureSize);
            Assert.Single(a.Emitters);

            var e = a.Emitters[0];
            Assert.Equal(1, e.InitPosType);
            Assert.Equal(0, e.DrawType);
            // Spatial fields are particle-coords → pixels (÷172 = PT_LCD_DOT); gen_num / base_scl stay fx32 (÷4096).
            Assert.Equal(0x2000 / 172.0, e.PosX, 3);
            Assert.Equal(0x1000 / 172.0, e.PosY, 3);
            Assert.Equal(8.0, e.GenNum);
            Assert.Equal(0x4000 / 172.0, e.Radius, 3);
            Assert.Equal(0x1000 / 172.0, e.InitVelPos, 3);
            Assert.Equal(2.0, e.BaseScale);
            Assert.Equal(255, e.ColorR);
            Assert.Equal(0, e.ColorG);
            Assert.Equal(0, e.ColorB);
            Assert.Equal(60, e.EmitterLife);
            Assert.Equal(30, e.ParticleLife);
            Assert.Equal(2, e.GenInterval);
            Assert.Equal(31, e.BaseAlpha);
            Assert.Equal(0x80, e.AirResist);
            Assert.Equal(3, e.TexNo);
        }

        [Fact]
        public void Parse_WalksOptionalBlocks_ToReachNextEmitter()
        {
            // Emitter 0 has use_scl_anm (bit 8) + use_fld_grvt (bit 24) → 12 + 8 trailing bytes before emitter 1.
            uint flag0 = 1u | (1u << 8) | (1u << 24);
            int e1 = 32 + 88 + 12 + 8;
            var b = new byte[e1 + 88];
            WriteHeader(b, resNum: 2, texNum: 0, texSize: 0, texOffset: 0);
            WriteBase(b, 32, flag0);
            WriteBase(b, e1, flag: 5);   // init_pos_type = 5 (distinct), no extras

            var a = SpaArchive.Parse(b);
            Assert.Equal(2, a.Emitters.Count);
            Assert.True(a.Emitters[0].UseScaleAnm);
            Assert.Equal(1, a.Emitters[0].InitPosType);
            Assert.Equal(5, a.Emitters[1].InitPosType);   // proves the walk skipped scl-anm + gravity field
            Assert.Equal(30, a.Emitters[1].ParticleLife);
        }
    }
}
