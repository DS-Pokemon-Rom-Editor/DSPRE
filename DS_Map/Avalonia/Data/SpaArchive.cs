using System;
using System.Collections.Generic;
using DSPRE.Avalonia.Gl;
using LibNDSFormats.NSBMD;

namespace DSPRE.Avalonia.Data
{
    /// <summary>A decoded SPA texture: RGBA8 pixels (top-to-bottom) for the particle billboard.</summary>
    public sealed class SpaTexture
    {
        public int Width, Height;
        public byte[] Rgba;   // width*height*4, or null if it couldn't be decoded
        // SPLTexParam flip flags (bits 14-15): the texture is a quadrant the hardware reflects across the particle
        // centre to build a symmetric sprite (e.g. a ring stored as one quarter). Mirror it at draw time.
        public bool MirrorX, MirrorY;
    }

    /// <summary>
    /// Parses an SPA particle archive (the NDS "simple particle library" format — see the leaked
    /// include/library/spl_resource.h). Layout: a 32-byte <c>SPLArcHdr</c> then <c>res_num</c> variable-length
    /// emitter records (an 88-byte <c>SPLResBase</c> plus flag-gated scale/colour/alpha/texture/child blocks and a
    /// field array), then a texture section. This decodes the header + each emitter's simulation-relevant base
    /// parameters and walks the records correctly; the texture section is located but not yet decoded.
    /// Foundation for the move-effect particle preview (the ~425 particle-based moves).
    /// </summary>
    public sealed class SpaEmitter
    {
        public int InitPosType;     // SPL_INIT_POS_TYPE_* (sphere/circle/…)
        public int CircleAxis;      // SPL_CIRCLE_AXIS_* (0=Z screen-plane, 1=Y, 2=X, 3=arbitrary)
        public int DrawType;        // billboard / polygon
        public double PosX, PosY, PosZ;   // emitter offset (world units; fx32 → /4096)
        public double GenNum;       // particles generated (fx32)
        public double Radius;       // emission radius (fx32)
        public byte ColorR, ColorG, ColorB;   // base colour (clr_n, expanded from RGB555)
        public double InitVelPos;   // initial speed along position vector (fx32)
        public double InitVelAxis;  // initial speed along axis (fx32)
        public double AxisX, AxisY; // base.axis (VecFx16): the emitter's own velocity axis (Flame Wheel spin/breath)
        public double BaseScale;    // base particle scale (fx32)
        public int EmitterLife;     // frames the emitter emits
        public int ParticleLife;    // frames each particle lives
        public int GenInterval;     // frames between emissions
        public int BaseAlpha;       // 0..31 base alpha
        public int AirResist;       // 0..255 (1.0 ≈ 0x80) velocity damping
        public int TexNo;           // texture index in the archive's texture section
        public bool UseScaleAnm, UseColorAnm, UseAlphaAnm, UseTexAnm, UseChild, SelfDestruct, FollowEmtr;
        // ptcl_random_loop_anm (base flag bit 20): for a LOOPING colour/texture anim, each particle starts at a random
        // phase, so the emitter shows the whole colour cycle at once and it "moves" (Aurora Beam's shifting rainbow).
        // Without it every particle animates in lockstep → the beam reads as one or two flat colours.
        public bool RandomLoopAnm;
        public bool ClrLoop, ClrRndm, TexLoop;   // anim loop / per-particle random-start flags (clr etc bits 0/1, tex etc bit 17)
        public double InitRot, RttMinRot, RttMaxRot, RotRate;   // billboard rotation (radians); RotRate = spin rad/frame
        public bool UseRttAnm, UseInitRttRndm;

        // Animation curves over a particle's life (lifeRate 0..255), decoded from spl_anm.c — these are what make
        // particles fade, shrink/grow and recolour like the game.
        public double SclS, SclN, SclE; public int SclIn, SclOut;                    // scale anim (×base_scl)
        public byte ClrSR, ClrSG, ClrSB, ClrER, ClrEG, ClrEB; public int ClrIn, ClrPeak, ClrOut; public bool ClrInterp;
        public int AlpS, AlpN, AlpE, AlpIn, AlpOut;                                  // alpha anim (0..31)
        public double GravityX, GravityY;                                           // gravity field accel (px/frame²)
        public int SpinRadian;                                                      // spin field: rotate pos/frame (0x10000=360°)
        public int SpinAxis = 2;                                                    // spin axis_type: 0=X, 1=Y, 2=Z(screen plane)
        public bool UseMagnet; public double MagnetX, MagnetY, MagnetMag;            // magnet field: spring-pull toward a point
        public double Length;                                                       // cylinder length (px, along the axis)
        public double RandMagX, RandMagY; public int RandIntvl;                     // random field: velocity kick every intvl
        public bool UseConv; public double ConvX, ConvY, ConvRatio;                 // convergence field: lerp pos→point/frame
        public bool UseColl; public double CollY, CollBounce; public int CollEvent; // collision plane: kill(0)/bounce(1)
        // SPLResChld — parent particles spawn child particles (trails/sparks): half of all emitters use this.
        public int ChildLife, ChildGenNum, ChildGenStart, ChildGenIntvl, ChildTexNo;
        public double ChildVelRatio, ChildSclRatio, ChildSclEnd;
        public byte ChildR, ChildG, ChildB; public bool ChildUseClr;
        public bool RepeatS, RepeatT;   // etc.tex_repeat_num ≥ 1 → texcoord spans 2× (quadrant tiles into full sprite)
        public double Aspect = 1.0;     // base.aspect (fx16): billboard sclX = sclY × aspect (non-square sprites)
        public double DbbScale;         // etc.dbb_scale (fx16 ratio): directional-billboard stretch along velocity
        public double OffsetX, OffsetY; // base.offset_x/offset_y (fx16, half-size units): billboard quad centre offset
        // SPLResTexAnm: a particle cycles through tex_no[0..UseNum-1] over its life (spl_tex_ptn_anm: pick tex_no[i]
        // where i is the first index with lifeRate < Diff·(i+1)); UseRndm picks one at random at birth. This is what
        // makes e.g. Thunderbolt's sparks show full bolt sprites instead of the emitter's base quadrant texture.
        public int[] TexSeq; public int TexUseNum, TexDiff; public bool TexUseRndm;
    }

    public sealed class SpaArchive
    {
        public int Version;
        public int TextureCount;
        public int TextureOffset, TextureSize;
        public List<SpaEmitter> Emitters { get; } = new List<SpaEmitter>();
        public List<SpaTexture> Textures { get; } = new List<SpaTexture>();

        // struct sizes (bytes) from spl_resource.h / spl_field.h
        private const int HdrSize = 32, BaseSize = 88;
        private const int SclAnmSize = 12, ClrAnmSize = 12, AlpAnmSize = 8, TexAnmSize = 12, ChldSize = 20;

        public static SpaArchive Parse(byte[] d)
        {
            var a = new SpaArchive();
            if (d == null || d.Length < HdrSize) return a;

            int U16(int o) => d[o] | (d[o + 1] << 8);
            int I32(int o) => d[o] | (d[o + 1] << 8) | (d[o + 2] << 16) | (d[o + 3] << 24);

            // SPLArcHdr
            int resNum = U16(8);
            a.TextureCount = U16(10);
            a.Version = I32(4);
            a.TextureSize = I32(20);
            a.TextureOffset = I32(24);

            int off = HdrSize;
            for (int i = 0; i < resNum; i++)
            {
                if (off + BaseSize > d.Length) break;
                uint flag = (uint)I32(off);
                var e = new SpaEmitter
                {
                    InitPosType = (int)(flag & 0xF),
                    DrawType = (int)((flag >> 4) & 0x3),
                    CircleAxis = (int)((flag >> 6) & 0x3),
                    UseScaleAnm = (flag & (1u << 8)) != 0,
                    UseColorAnm = (flag & (1u << 9)) != 0,
                    UseAlphaAnm = (flag & (1u << 10)) != 0,
                    UseTexAnm = (flag & (1u << 11)) != 0,
                    SelfDestruct = (flag & (1u << 14)) != 0,
                    FollowEmtr = (flag & (1u << 15)) != 0,   // particle tracks the (moving) emitter each frame, not just at birth
                    UseChild = (flag & (1u << 16)) != 0,
                    RandomLoopAnm = (flag & (1u << 20)) != 0,   // ptcl_random_loop_anm: random per-particle anim phase
                    // Spatial fields are in particle coordinates: 1 screen pixel = PT_LCD_DOT (172) units
                    // (we_def.h / we_tool.h), so divide by 172 to get pixels. gen_num/base_scl are plain fx32.
                    PosX = Px(I32(off + 4)),
                    PosY = Px(I32(off + 8)),
                    PosZ = Px(I32(off + 12)),
                    GenNum = Fx32(I32(off + 16)),
                    Radius = Px(I32(off + 20)),
                    InitVelPos = Px(I32(off + 36)),
                    InitVelAxis = Px(I32(off + 40)),
                    AxisX = (short)U16(off + 28) / 4096.0,   // base.axis VecFx16 (x@28, y@30) — unit dir, +Y up
                    AxisY = (short)U16(off + 30) / 4096.0,
                    Length = Px(I32(off + 24)),              // cylinder length (fx32) — spread along the emitter axis
                    BaseScale = Fx32(I32(off + 44)),
                    EmitterLife = U16(off + 60),
                    ParticleLife = U16(off + 62),
                    Aspect = (short)U16(off + 48) / 4096.0,   // base.aspect (fx16)
                    // Billboard rotation (SPLResBase): rtt_min@52 / rtt_max@54 (s16) + init_rtt@56 (u16); units = full
                    // turn / 65536. use_rtt_anm(bit12) spins by rtt_min/frame; use_init_rtt_rndm(bit13) randomises the
                    // start in [rtt_min,rtt_max]. This is what angles the Pin Missile / Sonic Boom / Horn Drill needles
                    // & waves (draw=0 billboards that otherwise render straight up).
                    InitRot = U16(off + 56) / 65536.0 * 2 * Math.PI,
                    RttMinRot = (short)U16(off + 52) / 65536.0 * 2 * Math.PI,
                    RttMaxRot = (short)U16(off + 54) / 65536.0 * 2 * Math.PI,
                    UseRttAnm = (flag & (1u << 12)) != 0,
                    UseInitRttRndm = (flag & (1u << 13)) != 0,
                };
                e.RotRate = e.UseRttAnm ? e.RttMinRot : 0.0;   // rtt_min = per-frame spin when the rotation anim is on
                if (e.Aspect <= 0) e.Aspect = 1.0;
                int clr = U16(off + 34);
                e.ColorR = Expand5((clr) & 0x1F);
                e.ColorG = Expand5((clr >> 5) & 0x1F);
                e.ColorB = Expand5((clr >> 10) & 0x1F);
                int etc0 = I32(off + 68);
                e.GenInterval = etc0 & 0xFF;
                e.BaseAlpha = (etc0 >> 8) & 0x1F;
                e.AirResist = (etc0 >> 16) & 0xFF;
                e.TexNo = (etc0 >> 24) & 0xFF;
                int etc1 = I32(off + 72);                 // loop_frame:8 | dbb_scale:16 | tex_repeat_num_s:2 | _t:2
                e.DbbScale = ((etc1 >> 8) & 0xFFFF) / 4096.0;
                e.RepeatS = ((etc1 >> 24) & 0x3) >= 1;
                e.RepeatT = ((etc1 >> 26) & 0x3) >= 1;
                // base.offset_x/offset_y (fx16 @ 80/82): the billboard QUAD centre offset in half-size units —
                // spl_draw_bb passes these to drawXYPlane, so the quad spans (offset±1). Anchors e.g. the Bite
                // jaws so the upper fang hangs DOWN from its top point and the lower fang rises UP (we_044).
                e.OffsetX = (short)U16(off + 80) / 4096.0;
                e.OffsetY = (short)U16(off + 82) / 4096.0;
                a.Emitters.Add(e);

                // parse / advance past this record's variable-length blocks (in flag order)
                int p = off + BaseSize;
                if (e.UseScaleAnm) { ParseScl(e, p, U16); p += SclAnmSize; }
                if (e.UseColorAnm) { ParseClr(e, p, U16); p += ClrAnmSize; }
                if (e.UseAlphaAnm) { ParseAlp(e, p, U16); p += AlpAnmSize; }
                if (e.UseTexAnm) { ParseTexAnm(e, p, d); p += TexAnmSize; }
                if (e.UseChild) { ParseChild(e, p, d); p += ChldSize; }
                // Fields in order (bits 24..29): gravity(8) random(8) magnet(16) spin(4) collision(8) convergence(16).
                int fp = p;
                if ((flag & (1u << 24)) != 0)
                {
                    if (fp + 4 <= d.Length) { e.GravityX = (short)U16(fp) / PtPerPixel; e.GravityY = (short)U16(fp + 2) / PtPerPixel; }
                    fp += 8;
                }
                if ((flag & (1u << 25)) != 0)             // SPLRandom: VecFx16 mag(6) + u16 intvl(2)
                {
                    if (fp + 8 <= d.Length)
                    {
                        e.RandMagX = (short)U16(fp) / PtPerPixel; e.RandMagY = (short)U16(fp + 2) / PtPerPixel;
                        e.RandIntvl = Math.Max(1, U16(fp + 6));
                    }
                    fp += 8;
                }
                if ((flag & (1u << 26)) != 0)             // SPLMagnet: VecFx32 pos(12) + fx16 mag(2) + u16(2)
                {
                    if (fp + 14 <= d.Length)
                    {
                        e.MagnetX = Px(I32(fp)); e.MagnetY = Px(I32(fp + 4));   // target point (particle px space)
                        e.MagnetMag = (short)U16(fp + 12) / 4096.0;             // spring/damper coefficient (fx16)
                        e.UseMagnet = e.MagnetMag != 0;
                    }
                    fp += 16;
                }
                if ((flag & (1u << 27)) != 0)
                {
                    if (fp + 4 <= d.Length) { e.SpinRadian = (short)U16(fp); e.SpinAxis = U16(fp + 2) & 0x3; }   // SPLSpin: radian + axis_type(0=X,1=Y,2=Z)
                    fp += 4;
                }
                if ((flag & (1u << 28)) != 0)             // SPLSimpleCollisionField: fx32 y(4) + fx16 coeff_bounce(2) + etc(2)
                {
                    if (fp + 8 <= d.Length)
                    {
                        e.CollY = Px(I32(fp)); e.CollBounce = (short)U16(fp + 4) / 4096.0;
                        e.CollEvent = U16(fp + 6) & 0x3; e.UseColl = true;
                    }
                    fp += 8;
                }
                if ((flag & (1u << 29)) != 0)             // SPLConvergence: VecFx32 pos(12) + fx16 ratio(2) + u16(2)
                {
                    if (fp + 14 <= d.Length)
                    {
                        e.ConvX = Px(I32(fp)); e.ConvY = Px(I32(fp + 4));
                        e.ConvRatio = (short)U16(fp + 12) / 4096.0;
                        e.UseConv = e.ConvRatio != 0;
                    }
                    fp += 16;
                }
                off = fp;
            }
            a.DecodeTextures(d);
            return a;
        }

        // Walks the texture section (at tex_offset): each SPLTexHdr (32 B) + texel + palette, decoded via the
        // shared NDS texture decoder. Overlapped textures reuse a sibling's pixels.
        private void DecodeTextures(byte[] d)
        {
            int RI32(int o) => d[o] | (d[o + 1] << 8) | (d[o + 2] << 16) | (d[o + 3] << 24);
            int RU16(int o) => d[o] | (d[o + 1] << 8);

            int pos = TextureOffset;
            for (int i = 0; i < TextureCount; i++)
            {
                if (pos < 0 || pos + 32 > d.Length) break;
                int param = RI32(pos + 4);
                int texSize = RI32(pos + 8);
                int pltOfst = RI32(pos + 12), pltSize = RI32(pos + 16);
                int pltIdxOfst = RI32(pos + 20), pltIdxSize = RI32(pos + 24);
                int totalSize = RI32(pos + 28);

                int fmt = param & 0xF;
                int w = 8 << ((param >> 4) & 0xF);
                int h = 8 << ((param >> 8) & 0xF);
                bool color0Transparent = ((param >> 16) & 1) != 0;
                bool overlapped = ((param >> 17) & 1) != 0;
                int sharedNo = (param >> 18) & 0xFF;
                bool flipS = ((param >> 14) & 1) != 0;   // SPLTexParam.flp bit0 → mirror across S
                bool flipT = ((param >> 15) & 1) != 0;   // .flp bit1 → mirror across T (quadrant → full sprite)

                SpaTexture tex;
                if (overlapped && sharedNo >= 0 && sharedNo < Textures.Count)
                {
                    var shared = Textures[sharedNo];
                    tex = new SpaTexture { Width = shared.Width, Height = shared.Height, Rgba = shared.Rgba };
                }
                else
                {
                    byte[] texdata = Slice(d, pos + 32, texSize);
                    RGBA[] pal = (pltSize > 0) ? ReadPalette(d, pos + pltOfst, pltSize, RU16) : null;
                    byte[] spdata = (fmt == 5 && pltIdxSize > 0) ? Slice(d, pos + pltIdxOfst, pltIdxSize) : null;
                    tex = DecodeOne(fmt, w, h, texdata, pal, spdata, color0Transparent);
                }
                tex.MirrorX = flipS; tex.MirrorY = flipT;
                Textures.Add(tex);
                pos += totalSize > 0 ? totalSize : (32 + texSize);
            }
        }

        private static SpaTexture DecodeOne(int fmt, int w, int h, byte[] texdata, RGBA[] pal, byte[] spdata, bool color0Transparent)
        {
            try
            {
                var mat = new NSBMDMaterial
                {
                    format = fmt, width = w, height = h,
                    texdata = texdata, paldata = pal, spdata = spdata,
                    color0 = color0Transparent ? 1 : 0,
                    repeatS = 0, repeatT = 0, flipS = 0, flipT = 0,
                };
                var dec = NsbmdTextureDecoder.Decode(mat);
                if (dec != null) return new SpaTexture { Width = dec.Width, Height = dec.Height, Rgba = dec.Rgba };
            }
            catch { }
            return new SpaTexture { Width = w, Height = h, Rgba = null };
        }

        private static byte[] Slice(byte[] d, int off, int len)
        {
            if (off < 0 || len <= 0 || off + len > d.Length) return Array.Empty<byte>();
            var b = new byte[len];
            Array.Copy(d, off, b, 0, len);
            return b;
        }

        private static RGBA[] ReadPalette(byte[] d, int off, int size, Func<int, int> ru16)
        {
            int n = size / 2;
            var pal = new RGBA[Math.Max(n, 256)];   // pad so format index lookups never overrun
            for (int i = 0; i < n; i++)
            {
                if (off + i * 2 + 1 >= d.Length) break;
                int c = ru16(off + i * 2);
                pal[i] = new RGBA
                {
                    R = (byte)((c & 0x1F) << 3),
                    G = (byte)(((c >> 5) & 0x1F) << 3),
                    B = (byte)(((c >> 10) & 0x1F) << 3),
                    A = 255,
                };
            }
            return pal;
        }

        // Sum of the present field structs (flags use_fld_grvt..use_fld_cngc at bits 24..29).
        private static int FieldBytes(uint flag)
        {
            int n = 0;
            if ((flag & (1u << 24)) != 0) n += 8;    // SPLGravity
            if ((flag & (1u << 25)) != 0) n += 8;    // SPLRandom
            if ((flag & (1u << 26)) != 0) n += 16;   // SPLMagnet
            if ((flag & (1u << 27)) != 0) n += 4;    // SPLSpin
            if ((flag & (1u << 28)) != 0) n += 8;    // SPLSimpleCollisionField
            if ((flag & (1u << 29)) != 0) n += 16;   // SPLConvergence
            return n;
        }

        // SPLResSclAnm: scl_s/n/e (fx16) + in_out (u16: in:8,out:8).
        private static void ParseScl(SpaEmitter e, int p, Func<int, int> U16)
        {
            e.SclS = (short)U16(p) / 4096.0;
            e.SclN = (short)U16(p + 2) / 4096.0;
            e.SclE = (short)U16(p + 4) / 4096.0;
            int io = U16(p + 6); e.SclIn = io & 0xFF; e.SclOut = (io >> 8) & 0xFF;
        }

        // SPLResClrAnm: clr_s/clr_e (RGB555) + in_peak_out (u32) + etc (interpolation bit 2). Peak colour = clr_n.
        private static void ParseClr(SpaEmitter e, int p, Func<int, int> U16)
        {
            int cs = U16(p), ce = U16(p + 2);
            e.ClrSR = Expand5(cs & 0x1F); e.ClrSG = Expand5((cs >> 5) & 0x1F); e.ClrSB = Expand5((cs >> 10) & 0x1F);
            e.ClrER = Expand5(ce & 0x1F); e.ClrEG = Expand5((ce >> 5) & 0x1F); e.ClrEB = Expand5((ce >> 10) & 0x1F);
            int ipo = U16(p + 4) | (U16(p + 6) << 16);
            e.ClrIn = ipo & 0xFF; e.ClrPeak = (ipo >> 8) & 0xFF; e.ClrOut = (ipo >> 16) & 0xFF;
            int cetc = U16(p + 8);
            e.ClrRndm = (cetc & 1) != 0; e.ClrLoop = ((cetc >> 1) & 1) != 0; e.ClrInterp = ((cetc >> 2) & 1) != 0;
        }

        // SPLResChld (20 B): flag(2) init_vel_mag_rndm(2) scl_e@4(fx16) life@6(u16) ratio@8(vel:8,scl:8) clr@10
        // etc1@12 (gen_num:8, gen_start:8, gen_intvl:8, tex_no:8) etc2@16.
        private static void ParseChild(SpaEmitter e, int p, byte[] d)
        {
            if (p + 16 > d.Length) return;
            int U16l(int o) => d[o] | (d[o + 1] << 8);
            e.ChildSclEnd = (short)U16l(p + 4) / 4096.0;
            e.ChildLife = Math.Max(1, U16l(p + 6));
            int ratio = U16l(p + 8);
            e.ChildVelRatio = (ratio & 0xFF) / 256.0; e.ChildSclRatio = ((ratio >> 8) & 0xFF) / 256.0;
            int cc = U16l(p + 10);
            e.ChildR = Expand5(cc & 0x1F); e.ChildG = Expand5((cc >> 5) & 0x1F); e.ChildB = Expand5((cc >> 10) & 0x1F);
            int etc1 = U16l(p + 12) | (U16l(p + 14) << 16);
            e.ChildGenNum = etc1 & 0xFF;
            // gen_start is a FRACTION of the parent's life (spl_emitter.c: age ≥ life·gen_start/256), NOT an absolute
            // frame. Treating the raw byte as a frame count silently skipped ALL children whenever gen_start > life.
            e.ChildGenStart = e.ParticleLife * ((etc1 >> 8) & 0xFF) / 256;
            e.ChildGenIntvl = Math.Max(1, (etc1 >> 16) & 0xFF); e.ChildTexNo = (etc1 >> 24) & 0xFF;
            e.ChildUseClr = (U16l(p) & (1 << 6)) != 0;   // SPLResChldFlag.use_chld_clr (bit 6)
        }

        // SPLResTexAnm: u8 tex_no[8] + etc (u32: use_num:8, diff:8, use_rndm:1, loop:1).
        private static void ParseTexAnm(SpaEmitter e, int p, byte[] d)
        {
            if (p + 12 > d.Length) return;
            var seq = new int[8];
            for (int i = 0; i < 8; i++) seq[i] = d[p + i];
            int etc = d[p + 8] | (d[p + 9] << 8) | (d[p + 10] << 16) | (d[p + 11] << 24);
            e.TexSeq = seq;
            e.TexUseNum = Math.Max(1, etc & 0xFF);
            e.TexDiff = (etc >> 8) & 0xFF;
            e.TexUseRndm = ((etc >> 16) & 1) != 0;
            e.TexLoop = ((etc >> 17) & 1) != 0;
        }

        // SPLResAlpAnm: alp (u16: s:5,n:5,e:5) + in_out (u16: in:8,out:8 at +4).
        private static void ParseAlp(SpaEmitter e, int p, Func<int, int> U16)
        {
            int alp = U16(p);
            e.AlpS = alp & 0x1F; e.AlpN = (alp >> 5) & 0x1F; e.AlpE = (alp >> 10) & 0x1F;
            int io = U16(p + 4); e.AlpIn = io & 0xFF; e.AlpOut = (io >> 8) & 0xFF;
        }

        private const double PtPerPixel = 172.0;                         // PT_LCD_DOT: particle units per screen pixel
        private static double Fx32(int v) => v / 4096.0;                 // fx32: 1.0 == 0x1000 (counts / scale)
        private static double Px(int v) => v / PtPerPixel;               // particle coords → screen pixels
        private static byte Expand5(int c5) => (byte)((c5 * 255) / 31);  // 5-bit → 8-bit channel
    }
}
