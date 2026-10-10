using System;
using SpaParticlePreview = DSPRE.Avalonia.SpaParticlePreview;

namespace DSPRE.Avalonia.Data
{
    public sealed partial class VsIntroPreview
    {
        // The League intro's two particle files, drawn with an orthographic camera from the screen centre.
        private const int FireEmitters = 3, BarEmitters = 4;

        private sealed class ParticleRun
        {
            public int Member = -1;
            public SpaArchive Arc;
            public SpaParticlePreview Preview;
            public int At;
        }

        private readonly ParticleRun _fire = new ParticleRun(), _bars = new ParticleRun();

        private SpaArchive Particles(ParticleRun run, int member)
        {
            if (run.Member == member) return run.Arc;
            run.Member = member;
            run.Preview = null;
            byte[] bytes = _member(member);
            try { run.Arc = bytes != null ? SpaArchive.Parse(bytes) : null; }
            catch (Exception e) when (e is ArgumentException || e is IndexOutOfRangeException || e is System.IO.InvalidDataException) { run.Arc = null; }
            return run.Arc;
        }

        private static SpaParticlePreview Start(SpaArchive arc, int emitters)
        {
            SpaParticlePreview p = new SpaParticlePreview(256, 192) { DsRaster = true };
            for (int i = 0; i < Math.Min(emitters, arc.Emitters.Count); i++)
            {
                SpaEmitter em = arc.Emitters[i];
                SpaTexture tex = em.TexNo >= 0 && em.TexNo < arc.Textures.Count ? arc.Textures[em.TexNo] : null;
                double cx = 128 + em.PosX, cy = 96 - em.PosY;
                // A fixed seed, so a paused or scrubbed frame always looks the same.
                SpaSimulator sim = new SpaSimulator(em, em.AxisX, em.AxisY, seed: 0x5EED + i) { AnchorX = cx, AnchorY = cy };
                p.AddLayer(new SpaParticlePreview.Layer(sim, arc.Textures, tex, cx, cy, em.DrawType,
                    em.RepeatS, em.RepeatT, em.Aspect, em.DbbScale, em.OffsetX, em.OffsetY,
                    baseZ: em.PosZ, viewReversed: false, flipS: em.FlipS, flipT: em.FlipT, em: em, orthographic: true));
            }
            return p;
        }

        /// <summary>Draws a particle file <paramref name="ticks"/> ticks after its emitters started, over the picture.</summary>
        private void DrawParticles(byte[] rgba, ParticleRun run, int member, int emitters, int ticks)
        {
            if (member < 0 || ticks < 0) return;
            SpaArchive arc = Particles(run, member);
            if (arc == null || arc.Emitters.Count == 0) return;
            // Frames usually come in order; going back starts the run again.
            if (run.Preview == null || ticks < run.At) { run.Preview = Start(arc, emitters); run.At = 0; }
            while (run.At < ticks) { run.Preview.Step(); run.At++; }

            byte[] bgra = run.Preview.RenderBgra();
            for (int i = 0; i + 3 < bgra.Length && i + 3 < rgba.Length; i += 4)
            {
                int a = bgra[i + 3];
                if (a == 0) continue;
                int inv = 255 - a;
                rgba[i] = (byte)Math.Min(255, bgra[i + 2] + rgba[i] * inv / 255);
                rgba[i + 1] = (byte)Math.Min(255, bgra[i + 1] + rgba[i + 1] * inv / 255);
                rgba[i + 2] = (byte)Math.Min(255, bgra[i] + rgba[i + 2] * inv / 255);
            }
        }

        /// <summary>How many ticks the fire runs before every emitter is done, or -1 without it.</summary>
        public int FireLength(int member)
        {
            SpaArchive arc = member >= 0 ? Particles(new ParticleRun(), member) : null;
            if (arc == null || arc.Emitters.Count == 0) return -1;
            SpaParticlePreview p = Start(arc, FireEmitters);
            int ticks = 0;
            while (!p.AllFinished && ticks < 600) { p.Step(); ticks++; }
            return ticks;
        }
    }
}
