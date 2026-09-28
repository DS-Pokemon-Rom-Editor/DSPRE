using System.Linq;
using DSPRE.Avalonia.Data;
using Xunit;

namespace DSPRE.Tests
{
    /// <summary>
    /// The SPA simulator reproduces the NDS particle-library emission/update model: emit gen_num/tick while alive, age each
    /// particle, kill at age &gt; ptcl_life, damp velocity by (air_resist+0.09375)/512 per frame.
    /// </summary>
    public class SpaSimulatorTests
    {
        [Fact]
        public void ACircleOnTheEmitterAxisLiesAcrossThatAxis()
        {
            // circle_axis 3 uses the emitter's own axis; an upright axis lays the ring flat.
            var e = new SpaEmitter
            {
                InitPosType = 2, CircleAxis = 3, AxisY = 1, Radius = 10, GenNum = 24, EmitterLife = 1, GenInterval = 1,
                ParticleLife = 5, AirResist = 128, BaseAlpha = 31, BaseScale = 1,
            };
            var sim = new SpaSimulator(e);
            sim.Step();
            var ps = sim.Particles().ToList();
            Assert.Equal(24, ps.Count);
            Assert.All(ps, p => Assert.Equal(0.0, p.Y, 6));
            Assert.All(ps, p => Assert.Equal(10.0, System.Math.Sqrt(p.X * p.X + p.Z * p.Z), 6));
            Assert.Contains(ps, p => System.Math.Abs(p.Z) > 5);
        }

        [Fact]
        public void AHemisphereAroundTheScreenAxisOpensTowardTheCamera()
        {
            // The hemisphere faces the normal of the axes across circle_axis: +Z for the Z axis.
            var e = new SpaEmitter
            {
                InitPosType = 8, CircleAxis = 0, Radius = 5, GenNum = 40, EmitterLife = 1, GenInterval = 1,
                ParticleLife = 5, AirResist = 128, BaseAlpha = 31, BaseScale = 1,
            };
            var sim = new SpaSimulator(e);
            sim.Step();
            var ps = sim.Particles().ToList();
            Assert.Equal(40, ps.Count);
            Assert.All(ps, p => Assert.True(p.Z >= 0, $"particle at z {p.Z} is behind the emitter"));
            Assert.Contains(ps, p => p.Y < -1);
        }

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
            Assert.Equal(2.0 * 255 / 256, p0.Scale, 9);   // DoubleScaledRange with no spread is 255/256
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

        [Theory]
        [InlineData(30, 128, 1, 15)]
        [InlineData(31, 128, 1, 16)]   // 15.5 frames: the first whole frame past it
        [InlineData(10, 0, 1, 0)]
        [InlineData(40, 255, 1, 40)]
        public void ChildrenStartAtTheDelayFractionOfTheParentsLife(int life, int delay, int interval, int firstAge)
        {
            int first = Enumerable.Range(0, life + 1).First(age => SpaSimulator.ChildEmitsAt(age, life, delay, interval));
            Assert.Equal(firstAge, first);
        }

        [Fact]
        public void ChildrenRepeatEveryIntervalAfterTheDelay()
        {
            var ages = Enumerable.Range(0, 31).Where(age => SpaSimulator.ChildEmitsAt(age, 30, 128, 4)).ToArray();
            Assert.Equal(new[] { 15, 19, 23, 27 }, ages);
        }

        [Fact]
        public void EveryParentEmitsChildrenWhateverItsRandomisedLife()
        {
            // Lives spread down to a frame or two; the delay is half of each particle's own life, so even
            // parents far shorter than the emitter's life emit their child.
            var e = new SpaEmitter
            {
                InitPosType = 0, GenNum = 40, EmitterLife = 1, GenInterval = 1, ParticleLife = 40, RndLife = 255,
                AirResist = 128, BaseAlpha = 31, BaseScale = 1,
                UseChild = true, ChildGenNum = 1, ChildGenDelay = 128, ChildGenIntvl = 255, ChildLife = 200,
            };
            var sim = new SpaSimulator(e);
            sim.Step();
            int parents = sim.Particles().Count(p => !p.IsChild);
            for (int i = 0; i < 60; i++) sim.Step();
            Assert.Equal(40, parents);
            Assert.Equal(parents, sim.Particles().Count(p => p.IsChild));
        }

        [Fact]
        public void TheSameSeedReplaysTheSameParticles()
        {
            var e = new SpaEmitter
            {
                InitPosType = 4, Radius = 8, GenNum = 5, EmitterLife = 3, GenInterval = 1, ParticleLife = 20,
                InitVelPos = 1, RndVel = 100, RndScale = 100, AirResist = 128, BaseAlpha = 31, BaseScale = 1,
            };
            SpaParticleState[] Run()
            {
                var sim = new SpaSimulator(e);
                for (int i = 0; i < 6; i++) sim.Step();
                return sim.Particles().ToArray();
            }
            var a = Run(); var b = Run();
            Assert.Equal(15, a.Length);
            Assert.Equal(a.Select(p => (p.X, p.Y, p.Z, p.Scale)), b.Select(p => (p.X, p.Y, p.Z, p.Scale)));
        }

        [Fact]
        public void EmittersSharingTheGeneratorDoNotMirrorEachOther()
        {
            var e = new SpaEmitter
            {
                InitPosType = 1, Radius = 8, GenNum = 3, EmitterLife = 1, GenInterval = 1, ParticleLife = 20,
                AirResist = 128, BaseAlpha = 31, BaseScale = 1,
            };
            var rng = new SplRandom(0x5EED);
            var first = new SpaSimulator(e, rng: rng); first.Step();
            var second = new SpaSimulator(e, rng: rng); second.Step();
            var a = first.Particles().Select(p => (p.X, p.Y, p.Z)).ToArray();
            var b = second.Particles().Select(p => (p.X, p.Y, p.Z)).ToArray();
            Assert.Equal(3, a.Length);
            Assert.NotEqual(a, b);
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
