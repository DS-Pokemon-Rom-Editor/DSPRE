using System;
using System.Collections.Generic;

namespace DSPRE.Avalonia.Data
{
    /// <summary>A live particle's render state for one frame (world units; the renderer maps to screen pixels).</summary>
    public struct SpaParticleState
    {
        public double X, Y;     // world position
        public double Z;        // depth offset from the emitter's plane (px-units, +z toward the camera)
        public double VX, VY;   // velocity (for directional/line billboards)
        public double VZ;       // depth velocity (directional polygons orient in 3D)
        public double Scale;    // world scale (base × anim; used by the dot fallback)
        // Per-axis scales per the hardware billboard-build step: the scale anim applies to X / Y / both according to
        // misc.scaleAnimDir; the renderer must use these (then × aspect on X), not Scale, for the quad.
        public double ScaleForX, ScaleForY;
        public double Alpha;    // 0..1
        public byte R, G, B;
        public int TexNo;       // current texture index (the texture-animation resource block picks this per particle over its life)
        public double Rotation; // billboard rotation (radians), from init_rtt + rotation-anim spin
        public bool IsChild;    // child particles use the the child-particle resource flags draw configuration
    }

    /// <summary>
    /// Simulates a single SPA emitter's particles frame-by-frame, matching the NDS particle-library runtime:
    /// each emission tick spawns <c>gen_num</c> particles (fractional accumulation) while the emitter is alive;
    /// each particle starts on the emission shape (× radius) with velocity = direction·init_vel_pos, then every
    /// frame <c>vel = vel·(air_resist+0.09375)/512; pos += vel; age++</c> and dies when <c>age &gt; ptcl_life</c>.
    /// The full behaviour is reproduced: 3D position/velocity, the gravity/random/magnet/spin/collision/
    /// convergence fields, child particles, and per-particle randomisation of scale/lifetime/velocity.
    /// </summary>
    public sealed class SpaSimulator
    {
        private struct P { public double X, Y, Z, VX, VY, VZ; public int Age, Life, RndTex, ClrRnd, LrOff; public double OVX, OVY, Phase, Rot0, RotRate, Scl; }
        // the child-resource block: a child particle spawned by a parent (trail/spark) — its own life, decaying scale/alpha.
        private struct Child { public double X, Y, Z, VX, VY, VZ; public int Age, Life; public double Scale0, Rot, RotRate, Alpha0; }

        // the random-range helper: uniform in [−num, num).
        private double Rng(double num) => num == 0 ? 0 : num * (_rng.NextDouble() * 2.0 - 1.0);

        private readonly SpaEmitter _e;
        private readonly List<P> _ptcls = new List<P>();
        private readonly List<Child> _children = new List<Child>();
        private readonly Random _rng;
        private readonly double _air;
        private readonly double _axisX, _axisY;   // unit travel direction (attacker↔defender) for init_vel_axis
        private readonly double _axisZ;           // depth component (only when the SPA's own 3D axis is in effect)
        private readonly double _driftX, _driftY;  // constant per-particle drift (operator projectiles crossing to target)
        private int _frame;
        private double _genAccum;

        // FIELD_OPERATOR FLD_MAGNET / FLD_CONVERGENCE override: the SPA emitter's own field target is a local
        // placeholder; the operator retargets it to a mon (FLD_AT/DF/SET_DF) — Mega Drain magnet, BubbleBeam/Aurora
        // convergence. NaN = keep the SPA's own target.
        private readonly bool _magOverride; private readonly double _magX, _magY, _magZ;
        private readonly bool _convOverride; private readonly double _convX, _convY, _convZ;

        public SpaSimulator(SpaEmitter e, double axisX = 0, double axisY = 0, double driftX = 0, double driftY = 0,
                            double magOverrideX = double.NaN, double magOverrideY = double.NaN,
                            double convOverrideX = double.NaN, double convOverrideY = double.NaN, int seed = 0x5EED,
                            double axisZ = double.NaN, double magOverrideZ = double.NaN, double convOverrideZ = double.NaN)
        {
            _e = e;
            _rng = new Random(seed);
            _air = AirResistMultiplier(e.AirResist);
            _delay = Math.Max(0, e.StartOffset);
            _axisX = axisX; _axisY = axisY;
            // A callback-provided axis is a screen-plane direction (z 0); when the SPA's own axis is in
            // effect its 3D z component applies (NaN = "derive": use e.AxisZ only if the x/y ARE e.Axis).
            _axisZ = !double.IsNaN(axisZ) ? axisZ
                   : (axisX == e.AxisX && axisY == e.AxisY ? e.AxisZ : 0.0);
            _driftX = driftX; _driftY = driftY;
            _magOverride = !double.IsNaN(magOverrideX); _magX = magOverrideX; _magY = magOverrideY;
            _convOverride = !double.IsNaN(convOverrideX); _convX = convOverrideX; _convY = convOverrideY;
            _magZ = double.IsNaN(magOverrideZ) ? 0 : magOverrideZ;
            _convZ = double.IsNaN(convOverrideZ) ? 0 : convOverrideZ;
            if (e.SpinRadian != 0)
            {
                double ang = e.SpinRadian / 65536.0 * 2.0 * Math.PI;   // spin field: rotate the particle per frame
                _spinCos = Math.Cos(ang); _spinSin = Math.Sin(ang); _spin = true;
            }
        }

        private readonly bool _spin;
        private readonly double _spinCos = 1, _spinSin;

        // Emitter motion (EMIT_ROTATION / STRAIGHT / PARABOLIC): the emitter's offset at a given frame. Captured at
        // spawn so particles are left along the moving emitter's path (orbit / stream / arc) and then move on their own.
        private Func<int, (double, double)> _emitterMotion;
        public void SetEmitterMotion(Func<int, (double, double)> m) => _emitterMotion = m;
        public double AnchorX, AnchorY;   // the emitter's spawn screen position (so EMIT_ROTATION can re-centre its orbit)
        // The anchor's WORLD y in px-units (+Y up; particle-space origin projects to screen y 96): the
        // collision plane is a WORLD plane (the game tests emitterPos.y + particle.y), so local ys must
        // be offset by this. Derived from the screen anchor at the ≈1:1 plane.
        public double AnchorWorldY => 96.0 - AnchorY;

        /// <summary>Velocity multiplier applied each frame: <c>(air_resist + FX32_CONST(0.09375)) / 512</c> where
        /// FX32_CONST(0.09375) = 384, so air_resist 128 → ×1.0 (no damping), &lt;128 damps, &gt;128 accelerates.</summary>
        public static double AirResistMultiplier(int airResist) => (airResist + 384.0) / 512.0;

        public int AliveCount => _ptcls.Count;

        private bool _stopped;
        /// <summary>WEST_EXIT_PARTICLE (the emitter-stop routine): stop emitting now and let the live particles die out. Also the only
        /// way an "emit forever" emitter (emtr_life == 0) ever finishes.</summary>
        public void Stop() => _stopped = true;

        /// <summary>True once the emitter has stopped emitting and all its particles have died.
        /// A pending start_offset counts as not-finished (the emitter simply hasn't begun yet).</summary>
        public bool Finished => (_stopped || _delay <= 0) && _ptcls.Count == 0 && _children.Count == 0 && (_stopped || (_e.EmitterLife != 0 && _frame >= _e.EmitterLife));

        // base.start_offset: the emitter idles this many frames before its own clock starts.
        // This is what sequences e.g. Seed Flare's big slashes after its small particles without any script waits.
        private int _delay;

        public void Step()
        {
            if (_delay > 0 && !_stopped) { _delay--; return; }

            // Emission while the emitter is alive (emtr_life == 0 means "forever") and not EXIT_PARTICLE-stopped.
            bool emitting = !_stopped && (_e.EmitterLife == 0 || _frame < _e.EmitterLife);
            int intvl = Math.Max(1, _e.GenInterval);
            if (emitting && _frame % intvl == 0)
            {
                _genAccum += Math.Max(0, _e.GenNum);
                int n = (int)_genAccum;
                _genAccum -= n;
                for (int i = 0; i < n && _ptcls.Count < 4000; i++) Emit(i, n);
            }

            // Update + cull.
            for (int i = _ptcls.Count - 1; i >= 0; i--)
            {
                var p = _ptcls[i];
                p.VX *= _air; p.VY *= _air; p.VZ *= _air;
                // Field accelerations accumulate in 3D, then vel += acc (vel*=air; vel+=acc).
                double accX = _e.GravityX, accY = _e.GravityY, accZ = _e.GravityZ;
                if (_e.UseMagnet || _magOverride)   // spl_calc_magnet: acc += mag·((target − pos) − vel) → spring-pull
                {
                    double mtX = _magOverride ? _magX : _e.MagnetX, mtY = _magOverride ? _magY : _e.MagnetY;
                    double mtZ = _magOverride ? _magZ : _e.MagnetZ;
                    double mag = _e.UseMagnet ? _e.MagnetMag : 0.02;   // keep the SPA's spring strength; default if none
                    accX += mag * ((mtX - p.X) - p.VX);
                    accY += mag * ((mtY - p.Y) - p.VY);
                    accZ += mag * ((mtZ - p.Z) - p.VZ);
                }
                if (_e.RandIntvl > 0 && p.Age % _e.RandIntvl == 0)   // spl_calc_random: a velocity kick every intvl frames
                {
                    accX += (_rng.NextDouble() * 2.0 - 1.0) * _e.RandMagX;
                    accY += (_rng.NextDouble() * 2.0 - 1.0) * _e.RandMagY;
                    accZ += (_rng.NextDouble() * 2.0 - 1.0) * _e.RandMagZ;
                }
                p.VX += accX; p.VY += accY; p.VZ += accZ;
                p.X += p.VX; p.Y += p.VY; p.Z += p.VZ;
                if (_e.UseConv || _convOverride)   // spl_calc_convergence: lerp the POSITION toward the convergence point
                {
                    double ctX = _convOverride ? _convX : _e.ConvX, ctY = _convOverride ? _convY : _e.ConvY;
                    double ctZ = _convOverride ? _convZ : _e.ConvZ;
                    double ratio = _e.UseConv ? _e.ConvRatio : 0.1;   // operator keeps the SPA's ratio; default if none
                    p.X += ratio * (ctX - p.X);
                    p.Y += ratio * (ctY - p.Y);
                    p.Z += ratio * (ctZ - p.Z);
                }
                if (_e.UseColl)   // the collision-plane behavior step: a WORLD horizontal plane — the game tests
                {                 // emitterPos.y + particle.y against the plane, so include the anchor's world y.
                    double wy = AnchorWorldY + p.Y, wyPrev = wy - p.VY;
                    if ((wyPrev > _e.CollY) != (wy > _e.CollY))   // crossed the plane this frame
                    {
                        p.Y = _e.CollY - AnchorWorldY;
                        if (_e.CollEvent == 1) p.VY = -p.VY * _e.CollBounce; else p.Age = p.Life;
                    }
                }
                if (_spin)   // spl_calc_spin: rotate ptcl_pos around axis_type (0=X,1=Y,2=Z). Only Z spins the screen
                {            // plane; X/Y spins involve depth (Z) and leave the other screen axis free (Mist falls under gravity).
                    if (_e.SpinAxis == 2) { double nx = p.X * _spinCos - p.Y * _spinSin, ny = p.X * _spinSin + p.Y * _spinCos; p.X = nx; p.Y = ny; }      // Z: X↔Y
                    else if (_e.SpinAxis == 1) { double nx = p.Z * _spinSin + p.X * _spinCos, nz = p.Z * _spinCos - p.X * _spinSin; p.X = nx; p.Z = nz; } // Y: Z↔X (Y free)
                    else { double ny = p.Y * _spinCos - p.Z * _spinSin, nz = p.Y * _spinSin + p.Z * _spinCos; p.Y = ny; p.Z = nz; }                       // X: Y↔Z (X free)
                }
                // the child-resource block (EmitChildren): children inherit full 3D position/velocity ×velRatio
                // PLUS a ±randomInitVelMag kick per component; base scale = the parent's CURRENT animated
                // scale × (scaleRatio+1)/64; initial alpha = the parent's CURRENT alpha; rotation per
                // rotationType (1 = frozen at the parent's angle, 2 = keeps the parent's spin).
                if (_e.UseChild && p.Age >= _e.ChildGenStart && (p.Age - _e.ChildGenStart) % _e.ChildGenIntvl == 0 && _children.Count < 4000)
                {
                    int lrNow = Math.Min(255, (int)(255.0 * p.Age / Math.Max(1, p.Life)));
                    int lrLoopNow = (p.LrOff + p.Age * 255 / _e.LoopFrames) & 0xFF;
                    double animNow = _e.UseScaleAnm ? SclCurve(_e.SclLoop ? lrLoopNow : lrNow) : 1.0;
                    double alphaNow = (_e.UseAlphaAnm ? AlpCurve(_e.AlpLoop ? lrLoopNow : lrNow) : _e.BaseAlpha) / 31.0;
                    double childScale = p.Scl * animNow * (_e.ChildSclRatioRaw + 1) / 64.0;
                    for (int k = 0; k < _e.ChildGenNum; k++)
                        _children.Add(new Child { X = p.X, Y = p.Y, Z = p.Z,
                                                  VX = p.VX * _e.ChildVelRatio + Rng(_e.ChildRandVel),
                                                  VY = p.VY * _e.ChildVelRatio + Rng(_e.ChildRandVel),
                                                  VZ = p.VZ * _e.ChildVelRatio + Rng(_e.ChildRandVel),
                                                  Rot = _e.ChildRotType != 0 ? p.Rot0 + p.RotRate * p.Age : 0,
                                                  RotRate = _e.ChildRotType == 2 ? p.RotRate : 0,
                                                  Alpha0 = alphaNow,
                                                  Age = 0, Life = _e.ChildLife, Scale0 = childScale });
                }
                p.Age++;
                if (p.Age > p.Life) _ptcls.RemoveAt(i);
                else _ptcls[i] = p;
            }
            // Children: air-damped drift, die at their own life (the parent may already be gone). The
            // behavior fields apply to children ONLY when the child-particle resource flags.usesBehaviors is set
            // (zeroes behaviorCount otherwise).
            for (int i = _children.Count - 1; i >= 0; i--)
            {
                var c = _children[i];
                c.VX *= _air; c.VY *= _air; c.VZ *= _air;
                if (_e.ChildUsesBehaviors)
                {
                    double aX = _e.GravityX, aY = _e.GravityY, aZ = _e.GravityZ;
                    if (_e.UseMagnet)
                    {
                        aX += _e.MagnetMag * ((_e.MagnetX - c.X) - c.VX);
                        aY += _e.MagnetMag * ((_e.MagnetY - c.Y) - c.VY);
                        aZ += _e.MagnetMag * ((_e.MagnetZ - c.Z) - c.VZ);
                    }
                    if (_e.RandIntvl > 0 && c.Age % _e.RandIntvl == 0)
                    {
                        aX += Rng(_e.RandMagX); aY += Rng(_e.RandMagY); aZ += Rng(_e.RandMagZ);
                    }
                    c.VX += aX; c.VY += aY; c.VZ += aZ;
                    if (_e.UseConv)
                    {
                        c.X += _e.ConvRatio * (_e.ConvX - c.X);
                        c.Y += _e.ConvRatio * (_e.ConvY - c.Y);
                        c.Z += _e.ConvRatio * (_e.ConvZ - c.Z);
                    }
                    if (_e.UseColl)
                    {
                        double wy = AnchorWorldY + c.Y, wyPrev = wy - c.VY;
                        if ((wyPrev > _e.CollY) != (wy > _e.CollY))
                        {
                            c.Y = _e.CollY - AnchorWorldY;
                            if (_e.CollEvent == 1) c.VY = -c.VY * _e.CollBounce; else c.Age = c.Life;
                        }
                    }
                }
                c.X += c.VX; c.Y += c.VY; c.Z += c.VZ; c.Age++;
                if (c.Age > c.Life) _children.RemoveAt(i); else _children[i] = c;
            }
            _frame++;
        }

        // Spawn one particle. emIdx/emCount are this tick's index/total so CIRCLE_RI rings come out evenly spaced
        // (uses idx = emission·16/total), instead of clumping into a wedge with random angles.
        private void Emit(int emIdx, int emCount)
        {
            // The the particle library shapes are 3D; the preview camera is orthographic facing −Z, so we emit in 3D then keep (x,y).
            // The circle/cylinder shapes lie in the plane perpendicular to circle_axis (Z = screen plane → a full
            // on-screen ring; Y/X → an edge-on ring that projects to a horizontal/vertical spread, as in-game).
            double ux, uy, uz = 0;         // unit emission direction; z (depth) is kept only to drive an X/Y-axis spin
            double rscale = 1.0;           // radius multiplier (1 = surface, rand = volume)
            double lox = 0, loy = 0;       // cylinder length offset (along the cylinder axis = circle_axis)
            switch (_e.InitPosType)
            {
                case 0:                                                     // ZERO
                    ux = uy = 0; break;
                case 1:                                                     // SPHERE (surface)
                case 4: { var (sx, sy, sz) = Sphere(); ux = sx; uy = sy; uz = sz; if (_e.InitPosType == 4) rscale = _rng.NextDouble(); break; }
                case 2:                                                     // CIRCLE (random angle in plane)
                case 5: { double a = _rng.NextDouble() * Math.PI * 2.0; (ux, uy, uz) = CirclePlane(a); if (_e.InitPosType == 5) rscale = _rng.NextDouble(); break; }
                case 3: { double a = Math.PI * 2.0 * emIdx / Math.Max(1, emCount); (ux, uy, uz) = CirclePlane(a); break; }  // CIRCLE_RI (even)
                case 6: case 7:                                            // CYLINDER (spl_gen): circle⟂axis × radius + random along the axis (length)
                {
                    double a = _rng.NextDouble() * Math.PI * 2.0; (ux, uy, uz) = CirclePlane(a);
                    if (_e.InitPosType == 7) rscale = _rng.NextDouble();   // CYLINDER_RI = volume radius
                    double lz = (_rng.NextDouble() * 2.0 - 1.0) * _e.Length;
                    if (_e.CircleAxis == 1) loy = lz; else if (_e.CircleAxis == 2) lox = lz;   // axis Z → into screen (unseen)
                    break;
                }
                case 8: case 9: { var (sx, sy, sz) = Sphere(); ux = sx; uy = Math.Abs(sy); uz = sz; if (_e.InitPosType == 9) rscale = _rng.NextDouble(); break; }  // SEMISPHERE
                default: { double a = _rng.NextDouble() * Math.PI * 2.0; (ux, uy, uz) = CirclePlane(a); break; }
            }
            double r = _e.Radius * rscale;
            // Spawn position on the emission shape. VOLUME shapes (sphere/circle/hemisphere interiors) scale
            // EACH component by its OWN random factor (FX_MUL(pos.c, radius) × RangeFX32/…) —
            // the shape switch above provides the direction and a per-type rscale; the per-component spread
            // for volume types is applied here.
            double posX = ux * r + lox, posY = uy * r + loy, posZ = uz * r;
            if (_e.InitPosType == 4 || _e.InitPosType == 5)          // SPHERE / CIRCLE interiors: ±rand per component
            {
                posX = ux * _e.Radius * (_rng.NextDouble() * 2.0 - 1.0);
                posY = uy * _e.Radius * (_rng.NextDouble() * 2.0 - 1.0);
                posZ = uz * _e.Radius * (_rng.NextDouble() * 2.0 - 1.0);
            }
            else if (_e.InitPosType == 9)                            // HEMISPHERE interior: rand/2 + 0.5 per component
            {
                posX = ux * _e.Radius * ((_rng.NextDouble() * 2.0 - 1.0) * 0.5 + 0.5);
                posY = uy * _e.Radius * ((_rng.NextDouble() * 2.0 - 1.0) * 0.5 + 0.5);
                posZ = uz * _e.Radius * ((_rng.NextDouble() * 2.0 - 1.0) * 0.5 + 0.5);
            }
            // Radial velocity direction = normalize(spawn position); a POINT emitter (pos 0) gets a RANDOM 3D
            // direction (posNorm) — point bursts are omnidirectional, not static.
            double nx, ny, nz;
            {
                double pl = Math.Sqrt(posX * posX + posY * posY + posZ * posZ);
                if (pl > 1e-9) { nx = posX / pl; ny = posY / pl; nz = posZ / pl; }
                else { var (sx, sy, sz) = Sphere(); nx = sx; ny = sy; nz = sz; }
                if (_e.InitPosType == 6)   // CYLINDER_SURFACE: direction = the circle dir (no length component)
                { nx = ux; ny = uy; nz = uz; }
            }
            // randomAttenuation: velocity magnitudes and base scale spread ±rnd/256 per particle
            // (DoubleScaledRange); lifetime attenuates downward (ScaledRange), minimum 1 frame.
            double magPos = _e.InitVelPos * DoubleScaled(_e.RndVel);
            double magAxis = _e.InitVelAxis * DoubleScaled(_e.RndVel);
            double pScale = _e.BaseScale * DoubleScaled(_e.RndScale);
            int life = Math.Max(1, (int)((_e.ParticleLife <= 0 ? 1 : _e.ParticleLife) * Scaled(_e.RndLife)) + 1);
            (double mx, double my) = _emitterMotion?.Invoke(_frame) ?? (0.0, 0.0);   // emitter's path offset now
            // The emitter's travel direction at spawn — used to orient a DIRECTIONAL billboard (the needle/wave) whose
            // own velocity is ~0 because it rides the moving emitter (Pin Missile/Sonic Boom/Horn Drill: EMIT_STRAIGHT/
            // PARABOLIC sweep the emitter attacker→defender). Without this the needle has no velocity and points up.
            (double pmx, double pmy) = _emitterMotion?.Invoke(Math.Max(0, _frame - 1)) ?? (0.0, 0.0);
            double ovx = mx - pmx, ovy = my - pmy;
            // Rotation: randomInitAngle → a FULLY random angle (not a min..max pick); the spin
            // rate (hasRotation) is random in [minRotation, maxRotation] PER PARTICLE.
            double rot0 = _e.UseInitRttRndm ? _rng.NextDouble() * 2.0 * Math.PI : _e.InitRot;
            double rotRate = _e.UseRttAnm ? _e.RttMinRot + _rng.NextDouble() * (_e.RttMaxRot - _e.RttMinRot) : 0.0;
            //: a random tex-anim picks one tex_no at birth; otherwise the texture is chosen per frame by age.
            int rndTex = (_e.UseTexAnm && _e.TexUseRndm && _e.TexSeq != null)
                ? _e.TexSeq[_rng.Next(Math.Max(1, _e.TexUseNum)) % _e.TexSeq.Length] : _e.TexNo;
            _ptcls.Add(new P
            {
                RndTex = rndTex,
                // Emitter position comes from the ADD_PARTICLE callback (the layer centre), which OVERRIDES the
                // SPA's own base pos — so particles start at the shape offset (+ the moving emitter's path offset).
                // follow_emtr particles TRACK the moving emitter (the current offset is added at render), so DON'T
                // bake the spawn offset in — otherwise they'd double up. (Pin Missile/Sonic Boom needles travel this way.)
                X = posX + (_e.FollowEmtr ? 0 : mx),
                Y = posY + (_e.FollowEmtr ? 0 : my),
                Z = posZ,
                // velocity = outward radial (init_vel_pos, along the SPAWN-POSITION normal) + along the emitter
                // axis (init_vel_axis): the latter carries "travelling" moves toward the defender.
                VX = nx * magPos + _axisX * magAxis + _driftX,
                VY = ny * magPos + _axisY * magAxis + _driftY,
                VZ = nz * magPos + _axisZ * magAxis,
                OVX = ovx, OVY = ovy,   // emitter travel direction at spawn (orientation only)
                Phase = _e.RandomLoopAnm ? _rng.NextDouble() : 0.0,
                LrOff = _e.RandomLoopAnm ? _rng.Next(256) : 0,        // lifeRateOffset for LOOPING anims
                // colour use_rndm: pick ONE of {start, base, end} at birth — a discrete 3-way
                // pick, and the colour anim is NOT registered for such emitters (the pick stays for life).
                ClrRnd = _e.ClrRndm ? _rng.Next(3) : 0,
                Rot0 = rot0, RotRate = rotRate,
                Scl = pScale,
                Age = 0,
                Life = life,
            });
        }

        // the double-scaled random-range helper: uniform multiplier in [1 − r/256, 1 + r/256).
        private double DoubleScaled(int rnd) => rnd == 0 ? 1.0 : 1.0 + (rnd / 256.0) * (_rng.NextDouble() * 2.0 - 1.0);
        // the scaled random-range helper: uniform multiplier in [1 − r/256, 1].
        private double Scaled(int rnd) => rnd == 0 ? 1.0 : 1.0 - (rnd / 256.0) * _rng.NextDouble();

        // A point on the unit circle in the plane perpendicular to circle_axis → (x, y, z); z is the depth component.
        private (double, double, double) CirclePlane(double ang)
        {
            double c = Math.Cos(ang), s = Math.Sin(ang);
            switch (_e.CircleAxis)
            {
                case 1: return (c, 0, s);   // axis Y → ring in XZ plane (horizontal on screen, z = depth)
                case 2: return (0, c, s);   // axis X → ring in YZ plane (vertical on screen, z = depth)
                default: return (c, s, 0);  // axis Z (screen plane) / arbitrary → full on-screen ring
            }
        }

        // A random unit vector on a 3D sphere → (x, y, z).
        private (double, double, double) Sphere()
        {
            double z = _rng.NextDouble() * 2.0 - 1.0;
            double t = _rng.NextDouble() * Math.PI * 2.0;
            double rr = Math.Sqrt(Math.Max(0, 1.0 - z * z));
            return (rr * Math.Cos(t), rr * Math.Sin(t), z);
        }

        /// <summary>The alive particles this frame, with the SPA scale/colour/alpha animation curves applied over
        /// each particle's life. Without a curve a field stays at its base value (the game holds alpha
        /// constant and the particle simply ends at death — no synthetic fade).</summary>
        public IEnumerable<SpaParticleState> Particles()
        {
            // follow_emtr: the particle tracks the emitter's CURRENT path offset (added here, not baked at spawn). It
            // also has ~no velocity of its own, so a DIRECTIONAL billboard must orient along the emitter's MOTION
            // (its travel direction) — otherwise the needle/wave just points up (Pin Missile / Sonic Boom).
            bool follow = _e.FollowEmtr && _emitterMotion != null;
            (double emx, double emy) = follow ? _emitterMotion(_frame) : (0.0, 0.0);
            (double pemx, double pemy) = follow ? _emitterMotion(Math.Max(0, _frame - 1)) : (0.0, 0.0);
            double emVX = emx - pemx, emVY = emy - pemy;   // emitter path velocity this frame (sim space)
            // drawChildrenFirst / hideParent (the resource flags bits 21/22): parent/child render order,
            // and "only children are rendered" (the parent is just an invisible child-spawner).
            if (_e.DrawChildrenFirst)
                foreach (var s in ChildStates()) yield return s;
            if (!_e.HideParent)
            foreach (var p in _ptcls)
            {
                int lr = (int)(255.0 * p.Age / Math.Max(1, p.Life));   // lifeRate 0..255 (once through the life)
                if (lr > 255) lr = 255;
                // LOOPING anims run on the loopFrames clock, offset by the per-particle lifeRateOffset and
                // WRAPPING (lifeRateOffset + loopTimeFactor·age, u8 wrap) — NOT on the life
                // fraction. Each anim picks its clock by its own loop flag.
                int lrLoop = (p.LrOff + p.Age * 255 / _e.LoopFrames) & 0xFF;
                int lrScl = _e.SclLoop ? lrLoop : lr;
                int lrClr = _e.ClrLoop ? lrLoop : lr;
                int lrTex = _e.TexLoop ? lrLoop : lr;
                int lrAlp = _e.AlpLoop ? lrLoop : lr;

                double anim = _e.UseScaleAnm ? SclCurve(lrScl) : 1.0;
                double scale = p.Scl * anim;
                // misc.scaleAnimDir (the hardware billboard-build step): 0 = anim on both axes, 1 = X only, 2 = Y only.
                double scaleForX = p.Scl * (_e.ScaleAnimDir == 2 ? 1.0 : anim);
                double scaleForY = p.Scl * (_e.ScaleAnimDir == 1 ? 1.0 : anim);
                byte r = _e.ColorR, g = _e.ColorG, b = _e.ColorB;
                // colour use_rndm (randomStartColor): each particle picks ONE of {start, base, end}
                // at birth and KEEPS it (the colour anim isn't registered for such emitters). Otherwise the
                // in/peak/out curve runs.
                if (_e.UseColorAnm)
                {
                    if (_e.ClrRndm)
                        (r, g, b) = p.ClrRnd == 0 ? (_e.ClrSR, _e.ClrSG, _e.ClrSB)
                                  : p.ClrRnd == 2 ? (_e.ClrER, _e.ClrEG, _e.ClrEB)
                                  : (_e.ColorR, _e.ColorG, _e.ColorB);
                    else (r, g, b) = ClrCurve(lrClr, _e.ClrInterp);
                }
                double alpha = (_e.UseAlphaAnm ? AlpCurve(lrAlp) : _e.BaseAlpha) / 31.0;
                // the alpha-animation curve randomRange: a per-frame downward jitter of the alpha (flicker). Deterministic
                // per (particle, frame) hash so re-enumerating a frame renders identically.
                if (_e.AlpFlick > 0)
                {
                    uint hh = (uint)(p.Age * 2654435761u + (uint)(p.LrOff * 97 + p.Life * 31 + p.ClrRnd * 13));
                    alpha *= 1.0 - (_e.AlpFlick / 256.0) * (((hh >> 8) & 0xFF) / 255.0);
                }

                // Billboard orientation vector (renderer uses this ONLY for the directional quad angle, not motion):
                // follow → the emitter's current travel; a needle riding a moving emitter (own velocity ~0) → its
                // baked spawn travel direction; otherwise the particle's own velocity.
                double ovx = follow ? emVX : p.VX, ovy = follow ? emVY : p.VY;
                if (!follow && Math.Abs(p.VX) < 1e-3 && Math.Abs(p.VY) < 1e-3 && (Math.Abs(p.OVX) > 1e-9 || Math.Abs(p.OVY) > 1e-9))
                { ovx = p.OVX; ovy = p.OVY; }
                yield return new SpaParticleState
                {
                    X = p.X + emx, Y = p.Y + emy, Z = p.Z,
                    VX = ovx, VY = ovy, VZ = follow ? 0 : p.VZ,   // directional billboards/polygons orient by this
                    Scale = scale, ScaleForX = scaleForX, ScaleForY = scaleForY,
                    Alpha = Math.Clamp(alpha, 0, 1),
                    R = r, G = g, B = b,
                    TexNo = TexNoFor(lrTex, p),
                    Rotation = p.Rot0 + p.RotRate * p.Age,   // init_rtt + spin·age
                };
            }
            if (!_e.DrawChildrenFirst)
                foreach (var s in ChildStates()) yield return s;
        }

        // Children (the child-resource block): scale 1→scl_e, alpha fades out over life, own colour if use_chld_clr;
        // rotation per rotationType (inherited at spawn, optionally still spinning).
        private IEnumerable<SpaParticleState> ChildStates()
        {
            foreach (var c in _children)
            {
                double t = (double)c.Age / Math.Max(1, c.Life);
                // the child scale-animation step/ChildAlpha run ONLY when the child flags request them; otherwise the
                // child keeps its spawn scale and its captured parent alpha (anim registration).
                double cscale = c.Scale0 * (_e.ChildHasSclAnm ? 1.0 + (_e.ChildSclEnd - 1.0) * t : 1.0);
                double calpha = c.Alpha0 * (_e.ChildHasAlpAnm ? 1.0 - t : 1.0);
                yield return new SpaParticleState
                {
                    X = c.X, Y = c.Y, Z = c.Z, VX = c.VX, VY = c.VY, VZ = c.VZ,
                    Scale = cscale, ScaleForX = cscale, ScaleForY = cscale,
                    Alpha = Math.Clamp(calpha, 0, 1),
                    R = _e.ChildUseClr ? _e.ChildR : _e.ColorR,
                    G = _e.ChildUseClr ? _e.ChildG : _e.ColorG,
                    B = _e.ChildUseClr ? _e.ChildB : _e.ColorB,
                    TexNo = _e.ChildTexNo,
                    Rotation = c.Rot + c.RotRate * c.Age,
                    IsChild = true,
                };
            }
        }

        // spl_tex_ptn_anm: pick tex_no[i] for the first i with lifeRate < diff·(i+1); rndm picks one at birth.
        private int TexNoFor(int lr, P p)
        {
            if (!_e.UseTexAnm || _e.TexSeq == null) return _e.TexNo;
            if (_e.TexUseRndm) return p.RndTex;
            int n = Math.Min(_e.TexUseNum, _e.TexSeq.Length);
            for (int i = 0; i < n; i++)
                if (lr < _e.TexDiff * (i + 1)) return _e.TexSeq[i];
            return n > 0 ? _e.TexSeq[n - 1] : _e.TexNo;   // past the last threshold → hold the last frame
        }

        private double SclCurve(int lr)
        {
            if (lr < _e.SclIn) return _e.SclS + lr * (_e.SclN - _e.SclS) / Math.Max(1, _e.SclIn);
            if (lr < _e.SclOut) return _e.SclN;
            return _e.SclE + (lr - 255) * (_e.SclE - _e.SclN) / Math.Max(1, 255 - _e.SclOut);
        }

        private double AlpCurve(int lr)   // → 0..31
        {
            if (lr < _e.AlpIn) return _e.AlpS + (double)(lr * (_e.AlpN - _e.AlpS)) / Math.Max(1, _e.AlpIn);
            if (lr < _e.AlpOut) return _e.AlpN;
            return _e.AlpE + (double)((lr - 255) * (_e.AlpE - _e.AlpN)) / Math.Max(1, 255 - _e.AlpOut);
        }

        // Three keyframes across the particle life: clr_s at `in`, clr_n (=base/peak) at `peak`, clr_e at `out`.
        // interp=true → piecewise-linear between them; interp=false → STEP: hold the previous keyframe (clr_s until
        // peak, clr_n until out, clr_e after). The earlier code returned the base for [in,peak) when stepping, which
        // dropped clr_s entirely (Aurora Beam lost its magenta → only cyan+yellow showed).
        private (byte, byte, byte) ClrCurve(int lr, bool interp)
        {
            byte pr = _e.ColorR, pg = _e.ColorG, pb = _e.ColorB;   // clr_n (peak/base)
            if (!interp)
            {
                if (lr < _e.ClrPeak) return (_e.ClrSR, _e.ClrSG, _e.ClrSB);
                if (lr < _e.ClrOut) return (pr, pg, pb);
                return (_e.ClrER, _e.ClrEG, _e.ClrEB);
            }
            if (lr <= _e.ClrIn) return (_e.ClrSR, _e.ClrSG, _e.ClrSB);
            if (lr < _e.ClrPeak)
            {
                double a = lr - _e.ClrIn, span = Math.Max(1, _e.ClrPeak - _e.ClrIn);
                return (L(_e.ClrSR, pr, a, span), L(_e.ClrSG, pg, a, span), L(_e.ClrSB, pb, a, span));
            }
            if (lr < _e.ClrOut)
            {
                double a = lr - _e.ClrPeak, span = Math.Max(1, _e.ClrOut - _e.ClrPeak);
                return (L(pr, _e.ClrER, a, span), L(pg, _e.ClrEG, a, span), L(pb, _e.ClrEB, a, span));
            }
            return (_e.ClrER, _e.ClrEG, _e.ClrEB);
            static byte L(byte from, byte to, double a, double b) => (byte)Math.Clamp(from + a * (to - from) / b, 0, 255);
        }
    }
}
