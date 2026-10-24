using System;
using System.Collections.Generic;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using DSPRE.Avalonia.Data;

namespace DSPRE.Avalonia.BattleAnim.Particles
{
    /// <summary>Particle files played outside a move animation, each with its own particle system and camera, drawn
    /// together. Each frame draws first and then moves the particles on, as the game does.</summary>
    public sealed class SplScene
    {
        private sealed class PSystem
        {
            public SplManager Mgr;
            public SplCamera Cam;
            public IReadOnlyList<SpaTexture> Textures;
        }

        /// <summary>Screen pixels per world unit, for both projections at the default camera.</summary>
        public const int FxPerPixel = 4096 * 4 / 96;

        private readonly List<PSystem> _systems = new List<PSystem>();
        private readonly SplRenderer _renderer = new SplRenderer();

        /// <summary>Loads a particle file as a new system. Null when the file can't be read.</summary>
        public SplManager Add(byte[] spa, SplRandom rng, bool orthographic, IReadOnlyList<SpaTexture> textures = null)
        {
            if (spa == null) return null;
            SplManager mgr;
            try { mgr = new SplManager(spa, rng); textures ??= SpaArchive.Parse(spa).Textures; }
            catch (Exception ex) { AppLogger.WarnOnce("SplScene.Add", ex); return null; }
            _systems.Add(new PSystem { Mgr = mgr, Cam = new SplCamera { Orthographic = orthographic }, Textures = textures });
            return mgr;
        }

        /// <summary>Any emitter is still alive.</summary>
        public bool Active => _systems.Exists(s => s.Mgr.Active.Count > 0);

        public void Update() { foreach (PSystem s in _systems) s.Mgr.Update(); }

        /// <summary>This frame as premultiplied BGRA rows, 256 by 192. The buffer is reused.</summary>
        public byte[] RenderBgra()
        {
            foreach (PSystem s in _systems) _renderer.Draw(s.Mgr, s.Cam, s.Textures);
            return _renderer.Flush();
        }

        public WriteableBitmap RenderFrame()
        {
            byte[] px = RenderBgra();
            WriteableBitmap wb = new WriteableBitmap(new global::Avalonia.PixelSize(SplRenderer.W, SplRenderer.H), new global::Avalonia.Vector(96, 96),
                PixelFormat.Bgra8888, AlphaFormat.Premul);
            using (ILockedFramebuffer fb = wb.Lock())
                for (int y = 0; y < SplRenderer.H; y++)
                    System.Runtime.InteropServices.Marshal.Copy(px, y * SplRenderer.W * 4, fb.Address + y * fb.RowBytes, SplRenderer.W * 4);
            return wb;
        }

        /// <summary>Draws this frame, then moves the particles on. Null once every emitter has ended.</summary>
        public WriteableBitmap Step()
        {
            if (!Active) return null;
            WriteableBitmap frame = RenderFrame();
            Update();
            return frame;
        }
    }
}
