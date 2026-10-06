using System;
using System.Collections.Generic;
using Avalonia.Threading;
using DSPRE.Avalonia.Data;
using DSPRE.Avalonia.Gl;
using NarcAPI;

namespace DSPRE.Avalonia.Views.Controls
{
    /// <summary>
    /// Runs a field weather's 2D part, its particles and background layer, at the field's tick rate and hands
    /// each tick's pictures to a 3D view, which mixes them in the way the DS does.
    /// </summary>
    public sealed class WeatherLayer
    {
        // The field's main loop waits two vertical blanks a turn.
        private const double TicksPerSecond = 30;

        private readonly NsbmdGlControl _gl;
        private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1 / TicksPerSecond) };
        private FieldWeatherSim _sim;
        private Graphics _particles;
        private byte[] _background, _lightning;   // 256 by 256 straight RGBA, see-through where colour 0
        private bool _running;

        public WeatherLayer(NsbmdGlControl gl)
        {
            _gl = gl;
            _timer.Tick += (_, _) => Tick();
        }

        /// <summary>Starts or stops the clock, for when the view leaves the screen.</summary>
        public bool Running
        {
            get => _running;
            set { _running = value; if (value && _sim != null) _timer.Start(); else _timer.Stop(); }
        }

        /// <summary>Runs the weather a header value gives in the open game; clear weather draws nothing.</summary>
        public void Show(int headerWeather)
        {
            _timer.Stop();
            _sim = null;
            _particles = null;
            _background = _lightning = null;
            _gl.SetScreenLayers(null);

            FieldWeather.Spec spec = FieldWeather.For(RomInfo.gameFamily, headerWeather);
            if (spec.Particles < 0 && spec.Background < 0 && spec.Companion == null) return;

            try
            {
                Narc narc = Narc.Open(RomInfo.WeatherSysNarcPath);
                if (narc == null) return;
                try
                {
                    if (spec.Particles >= 0) _particles = Graphics.Load(narc, FieldWeather.ParticleSets[spec.Particles]);
                    if (spec.Background >= 0)
                    {
                        byte[] layer = Layer(narc, FieldWeather.BackgroundSets[spec.Background]);
                        if (spec.Kind == FieldWeather.Kind.Lightning) _lightning = layer; else _background = layer;
                    }
                    if (spec.Companion?.Background is int bolt and >= 0)
                        _lightning = Layer(narc, FieldWeather.BackgroundSets[bolt]);
                }
                finally { narc.Free(); }
            }
            catch (Exception ex)
            {
                AppLogger.Warn($"Weather {headerWeather} graphics could not be read: {ex.Message}");
                return;
            }

            _sim = new FieldWeatherSim(RomInfo.gameFamily, spec);
            Publish(_sim);
            if (_running) _timer.Start();
        }

        private void Tick()
        {
            FieldWeatherSim sim = _sim;
            if (sim == null) return;
            sim.Step();
            Publish(sim);
        }

        // ── composing ────────────────────────────────────────────────────────────

        private void Publish(FieldWeatherSim sim)
        {
            List<NsbmdGlControl.ScreenLayer> layers = new List<NsbmdGlControl.ScreenLayer>();
            if (_background != null)
            {
                (int eva, int evb) = sim.Blend ?? (16, 0);
                layers.Add(new NsbmdGlControl.ScreenLayer
                {
                    Rgba = Scroll(_background, sim.BgX, sim.BgY), Eva = eva, Evb = evb, Behind = sim.Spec.BackgroundBehind,
                });
            }
            if (_lightning != null && sim.LightningEva > 0)
                layers.Add(new NsbmdGlControl.ScreenLayer { Rgba = Scroll(_lightning, 0, 0), Eva = sim.LightningEva, Evb = 31 });

            Graphics g = _particles;
            if (g != null)
            {
                byte[] sprites = new byte[DsBgScreen.Width * DsBgScreen.Height * 4];
                foreach (FieldWeatherSim.Particle p in sim.Particles)
                {
                    DsBgScreen.Oam[] cell = g.Cell(p.Frame);
                    if (cell != null) DsBgScreen.DrawCell(sprites, cell, g.Characters, g.PaletteFor, p.X, p.Y);
                }
                layers.Add(new NsbmdGlControl.ScreenLayer { Rgba = sprites });
            }
            _gl.SetScreenLayers(layers);
        }

        // The visible 256 by 192 of a 256 by 256 layer at a scroll, which wraps the way BG2 does.
        private static byte[] Scroll(byte[] layer, int scrollX, int scrollY)
        {
            byte[] frame = new byte[DsBgScreen.Width * DsBgScreen.Height * 4];
            for (int y = 0; y < DsBgScreen.Height; y++)
            {
                int ly = ((y + scrollY) % 256 + 256) % 256;
                for (int x = 0; x < DsBgScreen.Width; x++)
                {
                    int lx = ((x + scrollX) % 256 + 256) % 256;
                    Buffer.BlockCopy(layer, (ly * 256 + lx) * 4, frame, (y * DsBgScreen.Width + x) * 4, 4);
                }
            }
            return frame;
        }

        // ── graphics ─────────────────────────────────────────────────────────────

        // The game copies the background's first sixteen colours into slot 6 and points every tile at it.
        private static byte[] Layer(Narc narc, (int Nclr, int Ncgr, int Nscr) set)
        {
            ushort[] colours = DsBgScreen.Row(DsBgScreen.ReadColours(narc.GetElementBytes(set.Nclr)), 0);
            byte[] chars = DsBgScreen.ReadCharacters(narc.GetElementBytes(set.Ncgr));
            (int widthTiles, ushort[] entries) = DsBgScreen.ReadMap(narc.GetElementBytes(set.Nscr));
            byte[] rgba = new byte[256 * 256 * 4];
            if (widthTiles <= 0) return rgba;
            for (int i = 0; i < entries.Length; i++)
            {
                int tx = i % widthTiles, ty = i / widthTiles;
                if (tx >= 32 || ty >= 32) continue;
                int e = entries[i], tile = e & 0x3FF;
                bool flipH = (e & 0x400) != 0, flipV = (e & 0x800) != 0;
                for (int py = 0; py < 8; py++)
                    for (int px = 0; px < 8; px++)
                    {
                        int sx = flipH ? 7 - px : px, sy = flipV ? 7 - py : py;
                        int at = tile * 32 + (sy * 8 + sx) / 2;
                        if (at >= chars.Length) continue;
                        int index = ((sy * 8 + sx) & 1) != 0 ? chars[at] >> 4 : chars[at] & 0xF;
                        if (index == 0) continue;
                        ushort c = colours[index];
                        int to = ((ty * 8 + py) * 256 + tx * 8 + px) * 4;
                        rgba[to] = DsBgScreen.Expand(c & 31); rgba[to + 1] = DsBgScreen.Expand((c >> 5) & 31); rgba[to + 2] = DsBgScreen.Expand((c >> 10) & 31);
                        rgba[to + 3] = 255;
                    }
            }
            return rgba;
        }

        private sealed class Graphics
        {
            public byte[] Characters;
            public List<DsBgScreen.Oam[]> Cells;
            public ushort[] Colours;
            public readonly List<int> FrameCells = new();

            public ushort[] PaletteFor(int row) =>
                DsBgScreen.Row(Colours, Colours.Length >= (row + 1) * 16 ? row : 0);

            /// <summary>The cell an animation frame shows; the games set frames by hand and never play them.</summary>
            public DsBgScreen.Oam[] Cell(int frame)
            {
                int cell = frame >= 0 && frame < FrameCells.Count ? FrameCells[frame] : frame;
                return cell >= 0 && cell < Cells.Count ? Cells[cell] : null;
            }

            public static Graphics Load(Narc narc, (int Ncer, int Nanr, int Ncgr, int Nclr) set)
            {
                Graphics g = new Graphics
                {
                    Characters = DsBgScreen.ReadCharacters(narc.GetElementBytes(set.Ncgr)),
                    Cells = DsBgScreen.ReadCells(narc.GetElementBytes(set.Ncer)),
                    Colours = DsBgScreen.ReadColours(narc.GetElementBytes(set.Nclr)),
                };
                NanrFile anim = NanrFile.Read(narc.GetElementBytes(set.Nanr));
                if (anim != null && anim.Sequences.Count > 0)
                    foreach (NanrFile.Frame f in anim.Sequences[0].Frames)
                    {
                        int cell = -1;
                        foreach (NanrFile.Result r in anim.Results) if (r.Offset == f.ResultAt) { cell = r.Cell; break; }
                        g.FrameCells.Add(cell);
                    }
                return g;
            }
        }
    }
}
