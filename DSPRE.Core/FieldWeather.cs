using System;
using System.Collections.Generic;

namespace DSPRE
{
    /// <summary>What a field weather draws, per game, as the weather task of each game sets it up.</summary>
    public static class FieldWeather
    {
        public enum Kind
        {
            None, Cloudy, Rain, HeavyRain, Thunderstorm, Lightning, Snow, HeavySnow, Blizzard, Fog, Ash, Sandstorm,
            Storm, DiamondDust, Spirits, Mystic, Mist, DeepFog, DarkFlash, FogOnly, Rainbow, SunThroughLeaves,
            CaveDark, HgFlash,
        }

        /// <summary>
        /// Fog over the 3D as G3X_SetFog takes it: a slope shift, a depth offset, a colour and its alpha, and the
        /// 32-entry density table, out of 127. A null table is the one every weather fades in to, entry i = 4i.
        /// </summary>
        public sealed record Fog(int Slope, int Offset, ushort Colour, int Alpha = 31, byte[] Table = null)
        {
            public byte Density(int i) => Table != null ? (byte)(Table[i] & 0x7F) : (byte)Math.Min(127, 4 * i);
        }

        public sealed class Spec
        {
            public Kind Kind;
            public int Id;

            /// <summary>Particle graphics set in weather_sys, or -1.</summary>
            public int Particles = -1;

            /// <summary>Background graphics set in weather_sys, or -1.</summary>
            public int Background = -1;

            /// <summary>Background blend over the 3D, out of 16 as the hardware takes it; null draws it as it is.</summary>
            public (int Eva, int Evb)? Blend;

            /// <summary>The background sits behind the 3D and shows only where nothing is drawn.</summary>
            public bool BackgroundBehind;

            public Fog FogSettings;

            /// <summary>The lightning that runs alongside a thunderstorm.</summary>
            public Spec Companion;

            /// <summary>How much larger than the model the HGSS Flash light is drawn.</summary>
            public int SpotScale = 1;
        }

        /// <summary>HGSS field effect model dun_spot, the light around the player in the dark.</summary>
        public const int HgFlashSpotModel = 106;

        // weather_sys particle sets: NCER, NANR, NCGR, NCLR members. The same files in all three games;
        // only the index tables that list them sit at different members.
        public static readonly (int Ncer, int Nanr, int Ncgr, int Nclr)[] ParticleSets =
        {
            (5, 4, 6, 7),       // rain
            (19, 18, 20, 21),   // snow
            (33, 32, 34, 35),   // sandstorm
            (27, 26, 28, 21),   // light snow
            (23, 22, 24, 25),   // diamond dust
            (12, 11, 13, 7),    // heavy rain
            (40, 39, 41, 42),   // ash
            (1, 0, 2, 3),       // spirits
            (15, 14, 16, 17),   // unused
            (27, 26, 28, 21),   // blizzard, same as light snow
        };

        // weather_sys background sets: NCLR, NCGR, NSCR members. Platinum adds the last two.
        public static readonly (int Nclr, int Ncgr, int Nscr)[] BackgroundSets =
        {
            (21, 36, 38), (37, 36, 38), (9, 8, 10), (42, 44, 43), (30, 29, 31), (46, 45, 47),
            (51, 45, 47), (52, 53, 54), (49, 48, 50), (55, 56, 57), (58, 59, 60),
        };

        // Platinum's cave darkness density table.
        private static readonly byte[] CaveFog =
        {
            0x38, 0x30, 0x28, 0x20, 0x18, 0x10, 0x08, 0x00, 0x08, 0x08, 0x08, 0x10, 0x18, 0x20, 0x28, 0x30,
            0x38, 0x40, 0x44, 0x48, 0x4C, 0x50, 0x54, 0x58, 0x5C, 0x60, 0x64, 0x68, 0x6C, 0x70, 0x74, 0x78,
        };

        // HGSS darkness fills the table with 0xFF, so every fogged pixel is fully fogged.
        private static readonly byte[] FullFog = Fill(32, 0xFF);

        private static byte[] Fill(int n, byte v) { byte[] a = new byte[n]; Array.Fill(a, v); return a; }

        private static ushort Rgb(int r, int g, int b) => (ushort)(r | (g << 5) | (b << 10));

        /// <summary>
        /// The weather a header value gives in <paramref name="family"/>. Diamond and Pearl's calendar values
        /// and Platinum's yearly ones depend on the date, so they come back as clear.
        /// </summary>
        public static Spec For(RomInfo.GameFamilies family, int value) =>
            family == RomInfo.GameFamilies.HGSS ? Hgss(value) : DpPt(family, value);

        private static Spec DpPt(RomInfo.GameFamilies family, int id)
        {
            int count = family == RomInfo.GameFamilies.Plat ? 31 : 23;
            if (id < 0 || id >= count) return new Spec { Kind = Kind.None, Id = id };

            ushort grey = Rgb(26, 26, 26);
            Spec s = id switch
            {
                1 => new Spec { Kind = Kind.Cloudy, Background = 5, Blend = (4, 12) },
                2 => new Spec { Kind = Kind.Rain, Particles = 0, FogSettings = new Fog(3, 0x726F, grey) },
                3 => new Spec { Kind = Kind.HeavyRain, Particles = 5, FogSettings = new Fog(3, 0x6F6F, grey) },
                4 => new Spec { Kind = Kind.Thunderstorm, Particles = 5, FogSettings = new Fog(3, 0x6F6F, grey),
                                Companion = new Spec { Kind = Kind.Lightning, Id = 17, Background = 4 } },
                5 => new Spec { Kind = Kind.Snow, Particles = 3, FogSettings = new Fog(3, 0x726F, grey) },
                6 => new Spec { Kind = Kind.HeavySnow, Particles = 1, FogSettings = new Fog(3, 0x6D6F, Rgb(24, 24, 24)) },
                7 => new Spec { Kind = Kind.Blizzard, Particles = 9, Background = 0, FogSettings = new Fog(3, 0x6B6F, Rgb(24, 24, 24)) },
                8 => new Spec { Kind = Kind.Fog, Background = 6, Blend = (16, 0), BackgroundBehind = true,
                                FogSettings = new Fog(5, 0x7A0F, 0x7FFF) },
                9 => new Spec { Kind = Kind.Ash, Particles = 6, Background = 3, FogSettings = new Fog(3, 0x6F2F, Rgb(20, 20, 14)) },
                10 => new Spec { Kind = Kind.Sandstorm, Particles = 2, Background = 1, FogSettings = new Fog(3, 0x6EEF, Rgb(26, 20, 5)) },
                11 => new Spec { Kind = Kind.DiamondDust, Particles = 4, FogSettings = new Fog(3, 0x716F, grey) },
                12 => new Spec { Kind = Kind.Spirits, Particles = 7 },
                13 => new Spec { Kind = Kind.Mystic, Background = 8, Blend = (16, 0), BackgroundBehind = true },
                14 => new Spec { Kind = Kind.Mist, Background = 6, Blend = (9, 7), FogSettings = new Fog(6, 0x7555, 0x7FFF) },
                15 => new Spec { Kind = Kind.DeepFog, Background = 6, Blend = (9, 7), FogSettings = new Fog(7, 0x764F, 0) },
                16 => new Spec { Kind = Kind.DarkFlash, Background = 7 },
                17 => new Spec { Kind = Kind.Lightning, Background = 4, Blend = (0, 31) },
                18 => new Spec { Kind = Kind.FogOnly, FogSettings = new Fog(3, 0x716F, grey) },
                19 => new Spec { Kind = Kind.FogOnly, FogSettings = new Fog(3, 0x658F, grey) },
                20 => new Spec { Kind = Kind.Rainbow, Background = 2, Blend = (10, 16) },
                21 => new Spec { Kind = Kind.HeavySnow, Particles = 1, FogSettings = new Fog(3, 0x6F6F, Rgb(24, 24, 24)) },
                22 => new Spec { Kind = Kind.Storm, Particles = 2, FogSettings = new Fog(3, 0x6EEF, Rgb(26, 20, 5)) },
                23 => new Spec { Kind = Kind.SunThroughLeaves, Background = 9, Blend = (7, 9), FogSettings = new Fog(3, 0x692F, 0x7FFF) },
                24 => new Spec { Kind = Kind.CaveDark, Background = 10, Blend = (4, 12), FogSettings = new Fog(5, 0x6FAF, 0, Table: CaveFog) },
                25 => new Spec { Kind = Kind.CaveDark, Background = 10, Blend = (6, 10), FogSettings = new Fog(5, 0x6FAF, 0, Table: CaveFog) },
                26 => new Spec { Kind = Kind.FogOnly, FogSettings = new Fog(3, 0x65EF, Rgb(2, 2, 6)) },
                27 => new Spec { Kind = Kind.FogOnly, FogSettings = new Fog(2, 0x672F, Rgb(13, 25, 30)) },
                28 => new Spec { Kind = Kind.FogOnly, FogSettings = new Fog(2, 0x672F, Rgb(20, 0, 0)) },
                29 => new Spec { Kind = Kind.FogOnly, FogSettings = new Fog(2, 0x672F, Rgb(0, 0, 20)) },
                30 => new Spec { Kind = Kind.FogOnly, FogSettings = new Fog(1, 0x4B6F, Rgb(1, 1, 1)) },
                _ => new Spec { Kind = Kind.None },
            };
            s.Id = id;
            return s;
        }

        // HeartGold and SoulSilver map their ids onto six field weathers; sandstorm shows only in battle and
        // there is no lightning.
        private static Spec Hgss(int id)
        {
            ushort grey = Rgb(26, 26, 26);
            Spec s = id switch
            {
                1 or 2 or 3 => new Spec { Kind = Kind.Rain, Particles = 0, FogSettings = new Fog(3, 0x726F, grey) },
                4 or 5 or 6 => new Spec { Kind = Kind.HeavySnow, Particles = 1, FogSettings = new Fog(3, 0x726F, Rgb(24, 24, 24)) },
                8 => new Spec { Kind = Kind.DiamondDust, Particles = 4, FogSettings = new Fog(3, 0x716F, grey) },
                9 or 10 => new Spec { Kind = Kind.Mist, Background = 6, Blend = (9, 7), FogSettings = new Fog(6, 0x7555, 0x7FFF) },
                // GX_FOGSLOPE_0x0020, offset 0, black at fog alpha 0, every entry full: the map vanishes outside the
                // light the Flash spot model makes.
                11 => new Spec { Kind = Kind.HgFlash, FogSettings = new Fog(10, 0, 0, Alpha: 0, Table: FullFog) },
                // Once Flash is used the same light is drawn four times as large.
                12 => new Spec { Kind = Kind.HgFlash, FogSettings = new Fog(10, 0, 0, Alpha: 0, Table: FullFog), SpotScale = 4 },
                13 => new Spec { Kind = Kind.FogOnly, FogSettings = new Fog(1, 0x4B6F, Rgb(1, 1, 1)) },
                _ => new Spec { Kind = Kind.None },
            };
            s.Id = id;
            return s;
        }
    }

    /// <summary>
    /// A field weather run the way the field weather task runs it after a map loads with it: started without
    /// a fade, its particles already spread over the screen, then one <see cref="Step"/> per field tick (the
    /// field's main loop waits two vertical blanks a turn, so thirty a second). Positions are top-screen pixels
    /// and name a particle's cell centre. Follows Platinum's code, with Diamond's and HGSS's own rain,
    /// heavy rain and snow timing where they differ.
    /// </summary>
    public sealed class FieldWeatherSim
    {
        public sealed class Particle
        {
            public int X, Y, Frame;
            internal readonly int[] W = new int[10];
            internal bool Gone;
        }

        public FieldWeather.Spec Spec { get; }

        /// <summary>The live particles, oldest first, which is the order the game draws them in.</summary>
        public List<Particle> Particles { get; } = new();

        /// <summary>Background scroll in pixels.</summary>
        public int BgX { get; private set; }
        public int BgY { get; private set; }

        /// <summary>The background's blend, out of 16; null draws the background as it is.</summary>
        public (int Eva, int Evb)? Blend { get; private set; }

        /// <summary>The thunderstorm's lightning blend eva, out of 16, or 0 between flashes.</summary>
        public int LightningEva { get; private set; }

        private readonly RomInfo.GameFamilies _family;
        private readonly Random _rng;
        private readonly int _pool;

        // The spawner: how many, how often, and its countdown.
        private int _count, _interval, _timer;

        // The weather's own work: phase counters, the doubling flag, scroll.
        private int _w1, _w2, _w4;
        private bool _double;
        private readonly int[] _state = new int[8];

        public FieldWeatherSim(RomInfo.GameFamilies family, FieldWeather.Spec spec, int seed = 1)
        {
            _family = family;
            Spec = spec;
            _rng = new Random(seed);
            _pool = family == RomInfo.GameFamilies.Plat ? 48 : 64;
            Blend = spec.Blend;
            StartInstantly();
        }

        private bool Dp => _family == RomInfo.GameFamilies.DP;
        private bool TwoHalfSteps => _family != RomInfo.GameFamilies.Plat;

        private uint Next() => (uint)_rng.NextInt64(0, 1L << 32);
        private int R(int n) => (int)(Next() % (uint)n);

        // ── start ───────────────────────────────────────────────────────────────

        // The no-fade start a map load uses: the steady spawner, particles spread through their lives, and the
        // first tick's move.
        private void StartInstantly()
        {
            switch (Spec.Kind)
            {
                case FieldWeather.Kind.Rain:
                    if (TwoHalfSteps) Steady(20, 1); else Steady(4, 0);
                    Prewarm(20, 10, 1); break;
                case FieldWeather.Kind.HeavyRain:
                case FieldWeather.Kind.Thunderstorm:
                    if (Dp) Steady(24, 1); else Steady(10, 0);
                    Prewarm(20, 5, 1); break;
                case FieldWeather.Kind.Snow:
                    Steady(1, Dp ? 8 : 14); Prewarm(20, 2, 24); break;
                case FieldWeather.Kind.HeavySnow:
                    Steady(6, 3); Prewarm(20, 2, 3); break;
                case FieldWeather.Kind.Blizzard:
                    Steady(10, 1); Prewarm(20, 2, 2); break;
                case FieldWeather.Kind.Sandstorm:
                case FieldWeather.Kind.Storm:
                    Steady(8, 1); Prewarm(24, 2, 2); break;
                case FieldWeather.Kind.Ash:
                    Steady(1, 6); Prewarm(20, 2, 16); break;
                case FieldWeather.Kind.DiamondDust:
                    Steady(20, 2); Prewarm(20, 10, 1); break;
                case FieldWeather.Kind.Spirits:
                    Steady(15, 15); Prewarm(16, 2, 1); break;
                case FieldWeather.Kind.Rainbow:
                    BgY = 32;
                    _state[1] = 10; _state[2] = 10 + R(20); _state[3] = 5 + R(3); _state[4] = 1;
                    Blend = (10, 16);
                    break;
                case FieldWeather.Kind.CaveDark:
                    _state[0] = Spec.Id == 25 ? 1024 : 1280;
                    break;
                case FieldWeather.Kind.Lightning:
                    Blend = (0, 31);
                    break;
            }
            MoveAll();
        }

        private void Steady(int count, int interval) { _count = count; _interval = interval; _timer = 0; }

        // Spawns n, then moves the first of them step times, step growing by add every every-th particle.
        private void Prewarm(int n, int every, int add)
        {
            int first = Particles.Count;
            Spawn(n);
            int steps = 0;
            for (int i = 0; i < n && first + i < Particles.Count; i++)
            {
                Particle p = Particles[first + i];
                for (int k = 0; k < steps && !p.Gone; k++) Move(p);
                if (i >= every && i % every == 0) steps += add;
            }
            Particles.RemoveAll(p => p.Gone);
        }

        // ── each tick ───────────────────────────────────────────────────────────

        public void Step()
        {
            switch (Spec.Kind)
            {
                case FieldWeather.Kind.HeavyRain:
                case FieldWeather.Kind.Thunderstorm:
                    _w2 = (_w2 + 1) % 300;
                    SteadySpawn(1);
                    break;
                case FieldWeather.Kind.Blizzard:
                    SteadySpawn(new[] { -4, -6, -8, -10 }[_w1 / 512] <= -8 ? 2 : 1);
                    break;
                case FieldWeather.Kind.Sandstorm:
                    SteadySpawn(new[] { -3, -5, -5, -3, -5, -6, -10, -6 }[_w1 / 40] <= -6 ? 2 : 1);
                    break;
                default:
                    SteadySpawn(1);
                    break;
            }

            MoveAll();
            Layers();
            if (Spec.Companion?.Kind == FieldWeather.Kind.Lightning || Spec.Kind == FieldWeather.Kind.Lightning) Lightning();
        }

        private void SteadySpawn(int times)
        {
            if (_count <= 0) return;
            if (_timer-- <= 0)
            {
                Spawn(_count * times);
                _timer = _interval;
            }
        }

        private void MoveAll()
        {
            foreach (Particle p in Particles.ToArray()) if (!p.Gone) Move(p);
            Particles.RemoveAll(p => p.Gone);
        }

        private static void Wrap(Particle p)
        {
            if (p.X > 255 + 64) p.X %= 255 + 64; else if (p.X < -64) p.X += 255 + 64;
            if (p.Y > 192 + 64) p.Y %= 192 + 64; else if (p.Y < -64) p.Y += 192 + 64;
        }

        private Particle Add()
        {
            if (Particles.Count >= _pool) return null;
            Particle p = new Particle();
            Particles.Add(p);
            return p;
        }

        // ── spawning ────────────────────────────────────────────────────────────

        private void Spawn(int n)
        {
            switch (Spec.Kind)
            {
                case FieldWeather.Kind.Rain: SpawnRain(n); break;
                case FieldWeather.Kind.HeavyRain:
                case FieldWeather.Kind.Thunderstorm: if (Dp) SpawnHeavyRainDp(n); else SpawnHeavyRain(n); break;
                case FieldWeather.Kind.Snow: SpawnSnow(n); break;
                case FieldWeather.Kind.HeavySnow: SpawnHeavySnow(n); break;
                case FieldWeather.Kind.Blizzard: SpawnBlizzard(n); break;
                case FieldWeather.Kind.Sandstorm:
                case FieldWeather.Kind.Storm: SpawnSand(n); break;
                case FieldWeather.Kind.Ash: SpawnAsh(n); break;
                case FieldWeather.Kind.DiamondDust: SpawnDust(n); break;
                case FieldWeather.Kind.Spirits: SpawnSpirits(n); break;
            }
        }

        private void SpawnRain(int n)
        {
            for (int i = 0; i < n; i++)
            {
                Particle p = Add(); if (p == null) break;
                uint r = Next();
                int t = (int)(r % 3), e = (int)(r % 20);
                p.Frame = t;
                p.W[2] = 10 * (t + 1) + e + (t == 2 ? 10 : 0);
                p.W[4] = -5 * (t + 1) + e / -5 + (t == 2 ? -5 : 0);
                p.W[1] = 1 + (int)(r % 3);
                p.X = 15 * t + (int)(r % 270); p.Y = -96;
                Wrap(p);
            }
        }

        private static readonly int[] Gust = { 1, 1, 2, 1, 3 };

        private void SpawnHeavyRain(int n)
        {
            int m = Gust[_w2 / 60];
            for (int i = 0; i < n; i++)
            {
                Particle p = Add(); if (p == null) break;
                uint r = Next();
                int t = (int)(r % 3);
                p.Frame = t;
                p.W[4] = -24 * (t + 1) * m; p.W[2] = 24 * (t + 1) * m;
                p.W[1] = (int)(r % 4) / m;
                p.X = (int)(r % 512); p.Y = -80 + (int)(r % 48);
                Wrap(p);
            }
        }

        // Diamond scales by a percentage and draws each value separately.
        private void SpawnHeavyRainDp(int n)
        {
            int pct = Gust[_w2 / 60] * 100;
            for (int i = 0; i < n; i++)
            {
                Particle p = Add(); if (p == null) break;
                int t = R(3);
                p.Frame = t;
                p.W[4] = -24 * (t + 1) * pct / 100; p.W[2] = 24 * (t + 1) * pct / 100;
                p.W[1] = (int)(Next() & 3) * 100 / pct;
                p.X = (int)(Next() & 511); p.Y = -80 + R(48);
                Wrap(p);
            }
        }

        private void SpawnSnow(int n)
        {
            if (_double) n *= 2;
            for (int i = 0; i < n; i++)
            {
                Particle p = Add(); if (p == null) break;
                p.Frame = R(4);
                p.W[4] = 10; p.W[5] = 0; p.W[6] = 0; p.W[8] = 0;
                uint r = Next();
                p.W[9] = 4 + (int)(r % 60);
                p.W[1] = r % 2 == 0 ? 1 : -1;
                p.W[2] = 4 + R(2);
                p.W[3] = 1 + R(2);
                p.X = -32 + R(414);
                p.Y = _double && i >= n / 2 ? -40 - R(20) : -8 - R(20);
                Wrap(p);
            }
        }

        private void SpawnHeavySnow(int n)
        {
            int[] periods = { 16, 32, 16, 10 };
            for (int i = 0; i < n; i++)
            {
                Particle p = Add(); if (p == null) break;
                if (++_w1 >= 800) _w1 = 0;
                int ph = _w1 / 200;
                p.W[5] = periods[ph];
                p.W[0] = 0;
                p.W[1] = 4 + R(42);
                int t = (p.W[1] - 4) / 15;
                p.Frame = t;
                p.W[4] = -(t + 1); p.W[2] = 2 * (t + 1); p.W[3] = 0;
                p.X = -20 + t * 20 + R(420); p.Y = -8;
                Wrap(p);
            }
        }

        private static readonly int[] BlizzardX = { -4, -6, -8, -10 }, BlizzardY = { 2, 4, 2, 4 };

        private void SpawnBlizzard(int n)
        {
            if (++_w1 >= 2048) _w1 = 0;
            int ph = _w1 / 512;
            for (int i = 0; i < n * 4; i++)
            {
                Particle p = Add(); if (p == null) break;
                p.W[0] = 0;
                p.W[1] = 18 + R(6);
                int t = R(4);
                p.Frame = t;
                p.W[4] = BlizzardX[ph] * (t + 1); p.W[2] = BlizzardY[ph] * (t + 1); p.W[3] = 0;
                if (t == 3) { p.W[4] += BlizzardX[ph]; p.W[2] += BlizzardY[ph]; }
                p.W[5] = BlizzardX[ph];
                p.X = 256 + R(24); p.Y = -32 + R(168);
                Wrap(p);
            }
        }

        private static readonly int[] SandX = { -3, -5, -5, -4, -5, -6, -10, -6 }, SandY = { 2, 2, 2, 4, 4, 2, 2, 2 };

        private void SpawnSand(int n)
        {
            _w1 = (_w1 + 1) % 320;
            int ph = _w1 / 40;
            for (int i = 0; i < n; i++)
            {
                Particle p = Add(); if (p == null) break;
                p.W[0] = 0;
                p.W[1] = 15 + R(20);
                int t = 3 - (p.W[1] - 15) / 6;
                p.W[2] = SandY[ph] * (t + 1); p.W[4] = SandX[ph] * (t + 1); p.W[3] = 0;
                p.W[5] = SandX[ph];
                if (R(1000) == 777) { t = 4; p.W[2] += p.W[2] / 2; }
                p.Frame = t;
                p.X = 262 + R(24); p.Y = -64 + R(192);
                Wrap(p);
            }
        }

        private void SpawnAsh(int n)
        {
            if (_double) n *= 2;
            for (int i = 0; i < n; i++)
            {
                Particle p = Add(); if (p == null) break;
                p.Frame = R(4);
                p.W[4] = 10; p.W[5] = 0;
                p.W[1] = Next() % 2 == 0 ? 1 : -1;
                p.W[3] = 1 + R(1);
                p.W[7] = 10 + R(20);
                p.X = -32 + R(414);
                p.Y = _double && i >= n / 2 ? -40 - R(20) : -8 - R(20);
                Wrap(p);
            }
        }

        private void SpawnDust(int n)
        {
            for (int i = 0; i < n; i++)
            {
                Particle p = Add(); if (p == null) break;
                p.W[0] = 0;
                p.W[1] = 7 + R(5);
                R(1000); R(6); R(5);   // a drift direction and pace the games draw and never use
                int big = R(20);
                p.X = -64 + R(384); p.Y = -8 + R(256);
                Wrap(p);
                int lo = 50 - p.X / 3, w = 206 - p.X / 3;
                int hi = w < 0 ? lo - R(-w) : lo + R(Math.Max(1, w));
                if (lo <= p.Y && hi >= p.Y) { p.W[1] *= 2; p.Frame = big; }
                else p.Frame = R(4);
            }
        }

        private void SpawnSpirits(int n)
        {
            for (int i = 0; i < n; i++)
            {
                Particle p = Add(); if (p == null) break;
                int f = R(14);
                p.Frame = f;
                int s = f / 4 + 1;
                p.W[0] = (8 + R(25)) * s;
                p.W[1] = 16 / s;
                p.W[2] = 0;
                p.W[3] = R(2);
                p.W[4] = 1;
                p.X = -128 + R(512);
                p.Y = s <= 2 ? 8 + R(192) : s == 3 ? 64 + R(128) : 160 + R(32);
                Wrap(p);
            }
        }

        // ── moving ──────────────────────────────────────────────────────────────

        private void Move(Particle p)
        {
            switch (Spec.Kind)
            {
                case FieldWeather.Kind.Rain: MoveRain(p, 7); break;
                case FieldWeather.Kind.HeavyRain:
                case FieldWeather.Kind.Thunderstorm: MoveRain(p, 5); break;
                case FieldWeather.Kind.Snow: MoveSnow(p); break;
                case FieldWeather.Kind.HeavySnow: MoveHeavySnow(p); break;
                case FieldWeather.Kind.Blizzard: MoveBlizzard(p); break;
                case FieldWeather.Kind.Sandstorm:
                case FieldWeather.Kind.Storm: MoveSand(p); break;
                case FieldWeather.Kind.Ash: MoveAsh(p); break;
                case FieldWeather.Kind.DiamondDust: if (++p.W[0] >= p.W[1]) p.Gone = true; break;
                case FieldWeather.Kind.Spirits: MoveSpirit(p); break;
            }
        }

        // A drop ends after its lifetime: some vanish on the next tick, the rest show the splash frame first.
        private void MoveRain(Particle p, int vanishBelow)
        {
            switch (p.W[3])
            {
                case 0:
                    if (TwoHalfSteps)
                    {
                        for (int k = 0; k < 2; k++)
                        {
                            p.X += p.W[4]; p.Y += p.W[2];
                            if (p.W[0]++ > p.W[1]) Splash(p, vanishBelow);
                        }
                    }
                    else
                    {
                        p.X += p.W[4] * 2; p.Y += p.W[2] * 2;
                        p.W[0] += 2;
                        if (p.W[0] > p.W[1]) Splash(p, vanishBelow);
                    }
                    Wrap(p);
                    break;
                case 1:
                    if (p.W[0]-- <= 0) p.W[3] = 2;
                    break;
                default:
                    p.Gone = true;
                    break;
            }
        }

        private void Splash(Particle p, int vanishBelow)
        {
            if (R(10) < vanishBelow) p.W[3] = 2;
            else { p.W[3] = 1; p.W[0] = 4; p.Frame = 3; }
        }

        private void MoveSnow(Particle p)
        {
            if ((p.W[5] & 0xFFFF) >= p.W[2])
            {
                p.X += p.W[1];
                p.W[4]++;
                p.W[5] &= unchecked((int)0xFFFF0000);
                if (p.W[4] < 10) p.W[2]--; else p.W[2]++;
                if (p.W[4] >= 20) { p.W[4] = 0; p.W[1] = -p.W[1]; }
            }
            if ((p.W[5] >> 16) >= p.W[3])
            {
                p.Y++;
                p.W[5] &= 0xFFFF;
            }
            Wrap(p);
            p.W[6] = (p.W[6] + 1) % 100;
            p.W[5]++;
            p.W[5] += 0x10000;
            if ((p.Y < -284 && p.Y > -296) || (p.Y > 212 && p.Y < 232)) { _double = true; p.Gone = true; }
        }

        private void MoveHeavySnow(Particle p)
        {
            if (p.W[3] != 0) { p.Gone = true; return; }
            p.X += p.W[4]; p.Y += p.W[2];
            if (p.W[0]++ > p.W[1]) p.W[3] = 1;
            if (p.W[0] % p.W[5] == 0) { p.W[4]--; if (p.W[2] > 1) p.W[2]--; }
            Wrap(p);
        }

        private void MoveBlizzard(Particle p)
        {
            if (p.W[3] != 0) { p.Gone = true; return; }
            p.X += p.W[4]; p.Y += p.W[2];
            if (p.W[0]++ > p.W[1]) p.W[3] = 1;
            if (p.W[0] % 4 == 0) { p.W[4] += p.W[5]; if (p.W[2] > 1) p.W[2]--; }
            Wrap(p);
        }

        private void MoveSand(Particle p)
        {
            if (p.W[3] != 0) { p.Gone = true; return; }
            p.X += p.W[4]; p.Y += p.W[2];
            if (p.W[0] % 5 == 0) p.W[4] += p.W[5];
            if (p.W[0]++ > p.W[1]) p.W[3] = 1;
            Wrap(p);
        }

        private void MoveAsh(Particle p)
        {
            if (p.W[5] >= p.W[3])
            {
                p.Y++;
                p.W[5] = 0;
                Wrap(p);
            }
            p.W[5]++;
            if ((p.Y < -284 && p.Y > -296) || (p.Y > 212 && p.Y < 232)) { _double = true; p.Gone = true; }
        }

        private void MoveSpirit(Particle p)
        {
            p.W[2] += p.W[1];
            if (p.W[0] > 0) { p.W[0]--; p.Y -= p.W[2] / 100; }
            else p.Y -= p.W[2] / 50;
            if (--p.W[4] <= 0)
            {
                p.W[4] = 1;
                if (p.W[3] == 0) { p.X += 2; p.W[3] = 1; } else { p.X -= 2; p.W[3] = 0; }
            }
            Wrap(p);
            if (p.Y <= -16) p.Gone = true;
        }

        // ── backgrounds ─────────────────────────────────────────────────────────

        private static readonly sbyte[] Canopy =
        {
            -1, -1, -2, -2, -1, -1, 0, 0, -1, -1, -2, -2, -1, -1, 0, 0, -1, -1, -2, -2, -1, -1, 0, 0, -1, -1, 0, 0,
            -1, -1, -2, -2, -1, -1, 0, 0, 0, 0, 0, 0, -1, -1, -2, -2, -1, -1, 0, 0, -1, -1, 0, 0, -1, -1, -2, -2,
            -1, -1, 0, 0, -1, -1, 0, 0,
        };

        private void Layers()
        {
            switch (Spec.Kind)
            {
                case FieldWeather.Kind.Blizzard:
                    _w4 = (_w4 + 12) % 256;
                    BgX = _w4 * 2; BgY = -_w4;
                    break;
                case FieldWeather.Kind.Sandstorm:
                    _w4 = (_w4 + 6) % 256;
                    BgX = _w4 * 2; BgY = -_w4;
                    break;
                case FieldWeather.Kind.Ash:
                {
                    // Y is kept five times over, as the game does; a still camera adds nothing to either.
                    int x = _state[0], y5 = _state[1];
                    _state[2] += 2;
                    if (_state[2] > 60) { _state[2] = 0; x = (x + 32) % 256; }
                    y5 = (y5 + 2) % 2048;
                    BgX = x; BgY = -y5 / 5;
                    _state[0] = x; _state[1] = y5;
                    break;
                }
                case FieldWeather.Kind.SunThroughLeaves:
                    if (++_state[0] >= 8 * 64) _state[0] = 0;
                    BgX = Canopy[_state[0] / 8];
                    break;
                case FieldWeather.Kind.CaveDark:
                {
                    int bas = Spec.Id == 25 ? 6 : 4, down = Spec.Id == 25 ? 1 : 4, top = 2 * 128;
                    ref int v = ref _state[0];
                    if (_state[1] == 0) { v -= down; if (v <= 0) _state[1] = 1; }
                    else if (_state[1] == 1) { v++; if (v >= top) _state[1] = 2; }
                    else { v--; if (v <= 0) _state[1] = 1; }
                    int eva = bas + v / 128;
                    Blend = (eva, 16 - eva);
                    break;
                }
                case FieldWeather.Kind.Rainbow:
                    if (++_state[0] >= _state[2])
                    {
                        _state[0] = 0;
                        if (_state[4] == 1)
                        {
                            _state[1]--;
                            if (_state[1] <= _state[3]) { _state[2] = 10 + R(15); _state[3] = 7 + R(3); _state[4] = 0; }
                        }
                        else
                        {
                            _state[1]++;
                            if (_state[1] >= _state[3]) { _state[2] = 10 + R(20); _state[3] = 5 + R(3); _state[4] = 1; }
                        }
                    }
                    Blend = (_state[1], 16);
                    break;
            }
        }

        // ── lightning ───────────────────────────────────────────────────────────

        private int _boltWait, _boltPhase, _boltRise, _boltDecay, _boltSum, _boltTicks;

        private void Lightning()
        {
            if (_boltWait >= 0) { _boltWait--; return; }
            switch (_boltPhase)
            {
                case 0:
                    _boltPhase = 1; _boltTicks = 0; _boltRise = 200 + R(480); _boltSum = 0;
                    _boltDecay = R(3) != 0 ? 36 : 200;
                    break;
                case 1:
                    _boltTicks++;
                    _boltSum += _boltRise;
                    if (_boltTicks >= 2) _boltPhase = 2;
                    LightningEva = _boltSum / 100;
                    break;
                default:
                    _boltSum -= _boltDecay;
                    if (_boltSum <= 0)
                    {
                        _boltPhase = 0; _boltSum = 0;
                        _boltWait = _boltDecay == 200 ? R(15) : _boltRise * 50 / 100 + R(120);
                    }
                    LightningEva = _boltSum / 100;
                    break;
            }
        }
    }
}
