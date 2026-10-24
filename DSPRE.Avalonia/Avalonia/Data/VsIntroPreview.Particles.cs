using System;
using DSPRE.Avalonia.BattleAnim.Particles;

namespace DSPRE.Avalonia.Data
{
    public sealed partial class VsIntroPreview
    {
        // The League intro's two particle files: the field encounter effect makes their first emitters at the origin,
        // with an orthographic camera (overlay005/encounter_effect.c).
        private const int FireEmitters = 3, BarEmitters = 4;

        private static int Emitters(Scene s, int burst, int fallback) =>
            s.ParticleEmitters != null && burst < s.ParticleEmitters.Length && s.ParticleEmitters[burst] > 0 ? s.ParticleEmitters[burst] : fallback;

        private sealed class ParticleRun
        {
            public int Member = -1;
            public SpaArchive Arc;
            public byte[] Bytes;
            public SplScene Preview;
            public int At;
        }

        private readonly ParticleRun _fire = new ParticleRun(), _bars = new ParticleRun();

        private SpaArchive Particles(ParticleRun run, int member)
        {
            if (run.Member == member) return run.Arc;
            run.Member = member;
            run.Preview = null;
            byte[] bytes = _member(member);
            run.Bytes = bytes;
            try { run.Arc = bytes != null ? SpaArchive.Parse(bytes) : null; }
            catch (Exception e) when (e is ArgumentException || e is IndexOutOfRangeException || e is System.IO.InvalidDataException) { run.Arc = null; }
            return run.Arc;
        }

        private static SplScene Start(ParticleRun run, int emitters)
        {
            SplScene scene = new SplScene();
            // A fixed seed, so a paused or scrubbed frame always looks the same.
            SplManager mgr = scene.Add(run.Bytes, new SplRandom(BattleAnimPlayer.DefaultSeed), orthographic: true, run.Arc.Textures);
            if (mgr == null) return scene;
            for (int i = 0; i < Math.Min(emitters, mgr.Resources.Count); i++) mgr.CreateEmitter(i, e => e.SetPos(0, 0, 0));
            return scene;
        }

        /// <summary>Draws a particle file <paramref name="ticks"/> ticks after its emitters started, over the picture.</summary>
        private void DrawParticles(byte[] rgba, ParticleRun run, int member, int emitters, int ticks)
        {
            if (member < 0 || ticks < 0) return;
            SpaArchive arc = Particles(run, member);
            if (arc == null || arc.Emitters.Count == 0) return;
            // Frames usually come in order; going back starts the run again.
            if (run.Preview == null || ticks < run.At) { run.Preview = Start(run, emitters); run.At = 0; }
            while (run.At < ticks) { run.Preview.Update(); run.At++; }

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
            ParticleRun run = new ParticleRun();
            SpaArchive arc = member >= 0 ? Particles(run, member) : null;
            if (arc == null || arc.Emitters.Count == 0) return -1;
            SplScene p = Start(run, FireEmitters);
            int ticks = 0;
            while (p.Active && ticks < 600) { p.Update(); ticks++; }
            return ticks;
        }
    }
}
