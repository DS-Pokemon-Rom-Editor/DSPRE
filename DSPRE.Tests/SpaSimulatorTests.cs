using System.Linq;
using DSPRE.Avalonia.Data;
using Xunit;

namespace DSPRE.Tests
{
    /// <summary>
    /// The SPA simulator reproduces the leaked spl emission/update model: emit gen_num/tick while alive, age each
    /// particle, kill at age &gt; ptcl_life, damp velocity by (air_resist+0.09375)/512 per frame.
    /// </summary>
    public class SpaSimulatorTests
    {
        [Fact]
        public void AirResistMultiplier_128IsNoDamping()
        {
            Assert.Equal(1.0, SpaSimulator.AirResistMultiplier(128), 3);
            Assert.True(SpaSimulator.AirResistMultiplier(0) < 1.0);    // full damping
        }

        [Fact]
        public void Emits_ThenAllParticlesDie()
        {
            // One emission of 3 particles, each living 4 frames, then the emitter is done.
            var e = new SpaEmitter
            {
                InitPosType = 1, Radius = 4, GenNum = 3, EmitterLife = 1, GenInterval = 1,
                ParticleLife = 4, InitVelPos = 0.5, AirResist = 128, BaseAlpha = 31, BaseScale = 1,
            };
            var sim = new SpaSimulator(e);

            sim.Step();                       // emit + first update
            Assert.Equal(3, sim.AliveCount);
            Assert.False(sim.Finished);

            for (int i = 0; i < 10; i++) sim.Step();
            Assert.Equal(0, sim.AliveCount);  // all aged out
            Assert.True(sim.Finished);
        }

        [Fact]
        public void Particles_CarryColourAndScale_ConstantAlphaWithoutAnim()
        {
            // No alpha anim → alpha stays constant (the game holds it and the particle just ends; no synthetic fade).
            var e = new SpaEmitter
            {
                InitPosType = 1, Radius = 2, GenNum = 1, EmitterLife = 1, GenInterval = 1,
                ParticleLife = 10, InitVelPos = 1, AirResist = 128, BaseAlpha = 31, BaseScale = 2,
                ColorR = 200, ColorG = 50, ColorB = 10,
            };
            var sim = new SpaSimulator(e);
            sim.Step();
            var p0 = sim.Particles().Single();
            Assert.Equal(200, p0.R);
            Assert.Equal(2.0, p0.Scale);
            double a0 = p0.Alpha;

            for (int i = 0; i < 5; i++) sim.Step();
            Assert.Equal(a0, sim.Particles().Single().Alpha, 3);   // constant
        }

        [Fact]
        public void Particles_AlphaCurve_FadesOverLife()
        {
            // Alpha anim 31 → 31 → 0 (in=0, out=0 → straight ramp to e=0 across life).
            var e = new SpaEmitter
            {
                InitPosType = 1, Radius = 0, GenNum = 1, EmitterLife = 1, GenInterval = 1,
                ParticleLife = 10, InitVelPos = 0, AirResist = 128, BaseAlpha = 31, BaseScale = 1,
                ColorR = 255, ColorG = 255, ColorB = 255,
                UseAlphaAnm = true, AlpS = 31, AlpN = 31, AlpE = 0, AlpIn = 0, AlpOut = 0,
            };
            var sim = new SpaSimulator(e);
            sim.Step();
            double a0 = sim.Particles().Single().Alpha;
            for (int i = 0; i < 5; i++) sim.Step();
            Assert.True(sim.Particles().Single().Alpha < a0);   // fades via the curve
        }

        [Fact]
        public void Damping_ShrinksPerFrameTravel()
        {
            // A single particle with strong damping should move less each successive frame.
            var e = new SpaEmitter
            {
                InitPosType = 1, Radius = 0, GenNum = 1, EmitterLife = 1, GenInterval = 1,
                ParticleLife = 30, InitVelPos = 10, AirResist = 0 /* heavy damping */, BaseAlpha = 31, BaseScale = 1,
            };
            var sim = new SpaSimulator(e);
            sim.Step();
            var a = sim.Particles().Single();
            sim.Step();
            var b = sim.Particles().Single();
            sim.Step();
            var c = sim.Particles().Single();

            double d1 = System.Math.Sqrt((b.X - a.X) * (b.X - a.X) + (b.Y - a.Y) * (b.Y - a.Y));
            double d2 = System.Math.Sqrt((c.X - b.X) * (c.X - b.X) + (c.Y - b.Y) * (c.Y - b.Y));
            Assert.True(d2 < d1);
        }
    }
}
