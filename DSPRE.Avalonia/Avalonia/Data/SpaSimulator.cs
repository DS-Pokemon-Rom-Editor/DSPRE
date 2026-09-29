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
        private struct P { public double X, Y, Z, VX, VY, VZ; public int Age, Life, RndTex, ClrRnd, LrOff; public double OVX, OVY, Rot0, RotRate, Scl, AlpK; }
        // the child-resource block: a child particle spawned by a parent (trail/spark), its own life, decaying scale/alpha.
        private struct Child { public double X, Y, Z, VX, VY, VZ; public int Age, Life; public double Scale0, Rot, RotRate, Alpha0; }

        private readonly SpaEmitter _e;
        private readonly List<P> _ptcls = new List<P>();
        private readonly List<Child> _children = new List<Child>();
        private readonly SplRandom _rng;
        private readonly double _air;
        private readonly double _axisX, _axisY;   // unit travel direction (attacker↔defender) for init_vel_axis
        private readonly double _axisZ;           // depth component (only when the SPA's own 3D axis is in effect)
        private readonly double _driftX, _driftY;  // constant per-particle drift (operator projectiles crossing to target)
        private int _frame;
        private double _genAccum;

        // Magnet and convergence override: the SPA emitter's own field target is a local
        // placeholder; the operator retargets it to a mon : Mega Drain magnet, BubbleBeam/Aurora
        // convergence. NaN = keep the SPA's own target.
        private readonly bool _magOverride; private readonly double _magX, _magY, _magZ;
        private readonly bool _convOverride; private readonly double _convX, _convY, _convZ;

        public SpaSimulator(SpaEmitter e, double axisX = 0, double axisY = 0, double driftX = 0, double driftY = 0,
                            double magOverrideX = double.NaN, double magOverrideY = double.NaN,
                            double convOverrideX = double.NaN, double convOverrideY = double.NaN, int seed = 0x5EED,
                            double axisZ = double.NaN, double magOverrideZ = double.NaN, double convOverrideZ = double.NaN,
                            SplRandom rng = null)
        {
            _e = e;
            // The library has one generator for every emitter; pass a shared one so emitters don't mirror each other.
            _rng = rng ?? new SplRandom(unchecked((uint)seed));
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

        // Emitter motion (RevolveEmitter, MoveEmitterA2BLinear, MoveEmitterA2BParabolic): the emitter's offset at a given frame. Captured at
        // spawn so particles are left along the moving emitter's path (orbit / stream / arc) and then move on their own.
        private Func<int, (double, double)> _emitterMotion;
        public void SetEmitterMotion(Func<int, (double, double)> m) => _emitterMotion = m;
        public double AnchorX, AnchorY;   // the emitter's spawn screen position (so RevolveEmitter can re-centre its orbit)
        // The anchor's WORLD y in px-units (+Y up; particle-space origin projects to screen y 96): the
        // collision plane is a WORLD plane (the game tests emitterPos.y + particle.y), so local ys must
        // be offset by this. Derived from the screen anchor at the ≈1:1 plane.
        public double AnchorWorldY => 96.0 - AnchorY;

        /// <summary>Velocity multiplier applied each frame: <c>(air_resist + FX32_CONST(0.09375)) / 512</c> where
        /// FX32_CONST(0.09375) = 384, so air_resist 128 → ×1.0 (no damping), &lt;128 damps, &gt;128 accelerates.</summary>
        public static double AirResistMultiplier(int airResist) => (airResist + 384.0) / 512.0;

        public int AliveCount => _ptcls.Count;

        private bool _stopped;
        /// <summary>UnloadParticleSystem (the emitter-stop routine): stop emitting now and let the live particles die out. Also the only
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

            // Emission while the emitter is alive (emtr_life == 0 means "forever") and not stopped by UnloadParticleSystem.
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
                // SPLAnim_Alpha draws once per frame for every alpha-animated particle, before the behaviours.
                if (_e.UseAlphaAnm) p.AlpK = _rng.ScaledRange(_e.AlpFlick);
                // Behaviours read the undamped velocity; then vel = vel*air + acc (SPLEmitter_Update).
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
                    accX += _rng.Range(_e.RandMagX);
                    accY += _rng.Range(_e.RandMagY);
                    accZ += _rng.Range(_e.RandMagZ);
                }
                p.VX *= _air; p.VY *= _air; p.VZ *= _air;
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
                if (_e.UseColl)   // the collision-plane behavior step: a WORLD horizontal plane; the game tests
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
                if (_e.UseChild && ChildEmitsAt(p.Age, p.Life, _e.ChildGenDelay, _e.ChildGenIntvl) && _children.Count < 4000)
                {
                    int lrNow = Math.Min(255, (int)(255.0 * p.Age / Math.Max(1, p.Life)));
                    int lrLoopNow = (p.LrOff + p.Age * 255 / _e.LoopFrames) & 0xFF;
                    double animNow = _e.UseScaleAnm ? SclCurve(_e.SclLoop ? lrLoopNow : lrNow) : 1.0;
                    double alphaNow = (_e.UseAlphaAnm ? AnimAlpha(_e.AlpLoop ? lrLoopNow : lrNow, p.AlpK) : _e.BaseAlpha) / 31.0;
                    double childScale = p.Scl * animNow * (_e.ChildSclRatioRaw + 1) / 64.0;
                    for (int k = 0; k < _e.ChildGenNum; k++)
                    {
                        // x, y, z draw in that order (SPLEmitter_EmitChildren).
                        double cvx = p.VX * _e.ChildVelRatio + _rng.Range(_e.ChildRandVel);
                        double cvy = p.VY * _e.ChildVelRatio + _rng.Range(_e.ChildRandVel);
                        double cvz = p.VZ * _e.ChildVelRatio + _rng.Range(_e.ChildRandVel);
                        _children.Add(new Child { X = p.X, Y = p.Y, Z = p.Z, VX = cvx, VY = cvy, VZ = cvz,
                                                  Rot = _e.ChildRotType != 0 ? p.Rot0 + p.RotRate * p.Age : 0,
                                                  RotRate = _e.ChildRotType == 2 ? p.RotRate : 0,
                                                  Alpha0 = alphaNow,
                                                  Age = 0, Life = _e.ChildLife, Scale0 = childScale });
                    }
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
                double aX = 0, aY = 0, aZ = 0;
                if (_e.ChildUsesBehaviors)
                {
                    aX = _e.GravityX; aY = _e.GravityY; aZ = _e.GravityZ;
                    if (_e.UseMagnet)
                    {
                        aX += _e.MagnetMag * ((_e.MagnetX - c.X) - c.VX);
                        aY += _e.MagnetMag * ((_e.MagnetY - c.Y) - c.VY);
                        aZ += _e.MagnetMag * ((_e.MagnetZ - c.Z) - c.VZ);
                    }
                    if (_e.RandIntvl > 0 && c.Age % _e.RandIntvl == 0)
                    {
                        aX += _rng.Range(_e.RandMagX); aY += _rng.Range(_e.RandMagY); aZ += _rng.Range(_e.RandMagZ);
                    }
                }
                c.VX = c.VX * _air + aX; c.VY = c.VY * _air + aY; c.VZ = c.VZ * _air + aZ;
                if (_e.ChildUsesBehaviors)
                {
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

        // Spawn one particle. emIdx/emCount are this tick's index/total so ring emissions come out evenly spaced
        // (uses idx = emission·16/total), instead of clumping into a wedge with random angles.
        private void Emit(int emIdx, int emCount)
        {
            // Circles, cylinders and hemispheres lie on two axes across circle_axis; spheres ignore it.
            // Draws follow SPLEmitter_EmitParticles: shape, velocity magnitudes, centre direction, scale,
            // colour, angle, spin, life, texture, loop offset.
            var (c1, c2, up) = OrthogonalAxes();
            double R() => _rng.Range(1.0);
            (double, double, double) Tilt(double lx, double ly, double lz) =>
                (lx * c1.X + ly * c2.X + lz * up.X, lx * c1.Y + ly * c2.Y + lz * up.Y, lx * c1.Z + ly * c2.Z + lz * up.Z);
            double posX, posY, posZ;
            double tanX = 0, tanY = 0, tanZ = 0;   // cylinder surface: its ring direction sets the velocity
            bool ringVelocity = false;
            switch (_e.InitPosType)
            {
                case 1:   // sphere surface
                {
                    var (sx, sy, sz) = _rng.Vec();
                    posX = sx * _e.Radius; posY = sy * _e.Radius; posZ = sz * _e.Radius;
                    break;
                }
                case 2:   // circle border
                {
                    var (cx, cy) = _rng.VecXY();
                    (posX, posY, posZ) = Tilt(cx * _e.Radius, cy * _e.Radius, 0);
                    break;
                }
                case 3:   // circle border, uniform: evenly spaced, sine on the first axis
                {
                    double a = Math.PI * 2.0 * emIdx / Math.Max(1, emCount);
                    (posX, posY, posZ) = Tilt(Math.Sin(a) * _e.Radius, Math.Cos(a) * _e.Radius, 0);
                    break;
                }
                case 4:   // SPHERE: each component scaled by its own random factor
                {
                    var (sx, sy, sz) = _rng.Vec();
                    posX = sx * _e.Radius * R(); posY = sy * _e.Radius * R(); posZ = sz * _e.Radius * R();
                    break;
                }
                case 5:   // CIRCLE
                {
                    var (cx, cy) = _rng.VecXY();
                    double lx = cx * _e.Radius * R(), ly = cy * _e.Radius * R();
                    (posX, posY, posZ) = Tilt(lx, ly, 0);
                    break;
                }
                case 6:   // cylinder surface
                {
                    var (cx, cy) = _rng.VecXY();
                    (posX, posY, posZ) = Tilt(cx * _e.Radius, cy * _e.Radius, _rng.Range(_e.Length));
                    (tanX, tanY, tanZ) = Tilt(cx, cy, 0);
                    ringVelocity = true;
                    break;
                }
                case 7:   // CYLINDER
                {
                    var (cx, cy) = _rng.VecXY();
                    double lx = cx * _e.Radius * R(), ly = cy * _e.Radius * R();
                    (posX, posY, posZ) = Tilt(lx, ly, _rng.Range(_e.Length));
                    break;
                }
                case 8:   // hemisphere surface: flipped onto the side the axes face
                case 9:   // HEMISPHERE
                {
                    var (sx, sy, sz) = _rng.Vec();
                    double d = sx * up.X + sy * up.Y + sz * up.Z;
                    if (_e.InitPosType == 8 ? d <= 0 : d < 0) { sx = -sx; sy = -sy; sz = -sz; }
                    if (_e.InitPosType == 8) { posX = sx * _e.Radius; posY = sy * _e.Radius; posZ = sz * _e.Radius; }
                    else
                    {
                        posX = sx * _e.Radius * (R() * 0.5 + 0.5);
                        posY = sy * _e.Radius * (R() * 0.5 + 0.5);
                        posZ = sz * _e.Radius * (R() * 0.5 + 0.5);
                    }
                    break;
                }
                default:  // POINT
                    posX = posY = posZ = 0;
                    break;
            }
            // randomAttenuation: velocity magnitudes and base scale spread around the base (DoubleScaledRange);
            // lifetime attenuates downward (ScaledRange) and is at least 1 frame.
            double magPos = _e.InitVelPos * _rng.DoubleScaledRange(_e.RndVel);
            double magAxis = _e.InitVelAxis * _rng.DoubleScaledRange(_e.RndVel);
            // Velocity points away from the centre; a particle born exactly at the centre gets a random direction.
            double nx, ny, nz;
            if (ringVelocity) { (nx, ny, nz) = Norm(tanX, tanY, tanZ); }
            else if (posX == 0 && posY == 0 && posZ == 0) { (nx, ny, nz) = _rng.Vec(); }
            else { (nx, ny, nz) = Norm(posX, posY, posZ); }
            double pScale = _e.BaseScale * _rng.DoubleScaledRange(_e.RndScale);
            // colour use_rndm: pick ONE of {start, base, end} at birth; the colour anim is not registered for
            // such emitters, so the pick stays for life.
            int clrRnd = _e.UseColorAnm && _e.ClrRndm ? (int)(_rng.U32(12) % 3) : 1;
            // randomInitAngle takes a full u16 angle; the spin rate is per particle in [minRotation, maxRotation).
            double rot0 = _e.UseInitRttRndm ? (_rng.Next() & 0xFFFF) / 65536.0 * 2.0 * Math.PI : _e.InitRot;
            double rotRate = _e.UseRttAnm ? _rng.Between(_e.RttMinRot, _e.RttMaxRot) : 0.0;
            int life = _rng.ScaledRange(Math.Max(0, _e.ParticleLife), _e.RndLife) + 1;
            // A random tex-anim picks one tex_no at birth; otherwise the texture is chosen per frame by age.
            int rndTex = (_e.UseTexAnm && _e.TexUseRndm && _e.TexSeq != null)
                ? _e.TexSeq[(int)(_rng.U32(12) % (uint)Math.Max(1, _e.TexUseNum)) % _e.TexSeq.Length] : _e.TexNo;
            int lrOff = _e.RandomLoopAnm ? (int)_rng.U32(8) : 0;   // lifeRateOffset for LOOPING anims
            (double mx, double my) = _emitterMotion?.Invoke(_frame) ?? (0.0, 0.0);   // emitter's path offset now
            // The emitter's travel direction at spawn, used to orient a DIRECTIONAL billboard (the needle/wave) whose
            // own velocity is ~0 because it rides the moving emitter (Pin Missile/Sonic Boom/Horn Drill: the linear and parabolic
            // routines sweep the emitter attacker→defender). Without this the needle has no velocity and points up.
            (double pmx, double pmy) = _emitterMotion?.Invoke(Math.Max(0, _frame - 1)) ?? (0.0, 0.0);
            double ovx = mx - pmx, ovy = my - pmy;
            _ptcls.Add(new P
            {
                RndTex = rndTex,
                // Emitter position comes from the CreateEmitter callback (the layer centre), which OVERRIDES the
                // SPA's own base pos, so particles start at the shape offset (+ the moving emitter's path offset).
                // follow_emtr particles TRACK the moving emitter (the current offset is added at render), so DON'T
                // bake the spawn offset in, otherwise they'd double up. (Pin Missile/Sonic Boom needles travel this way.)
                X = posX + (_e.FollowEmtr ? 0 : mx),
                Y = posY + (_e.FollowEmtr ? 0 : my),
                Z = posZ,
                // velocity = outward radial (init_vel_pos, along the SPAWN-POSITION normal) + along the emitter
                // axis (init_vel_axis): the latter carries "travelling" moves toward the defender.
                VX = nx * magPos + _axisX * magAxis + _driftX,
                VY = ny * magPos + _axisY * magAxis + _driftY,
                VZ = nz * magPos + _axisZ * magAxis,
                OVX = ovx, OVY = ovy,   // emitter travel direction at spawn (orientation only)
                LrOff = lrOff,
                ClrRnd = clrRnd,
                Rot0 = rot0, RotRate = rotRate,
                Scl = pScale,
                AlpK = 1.0,
                Age = 0,
                Life = life,
            });
        }

        /// <summary>
        /// Whether a parent at <paramref name="age"/> emits children this frame: from
        /// age &gt;= life * delay / 256 of its own randomised life, then every interval frames (SPLEmitter_Update).
        /// </summary>
        public static bool ChildEmitsAt(int age, int life, int delay, int interval)
        {
            long diff = (long)age * 4096 - (long)life * delay * 16;   // fx32: age - life*delay/256
            return diff >= 0 && (diff >> 12) % Math.Max(1, interval) == 0;
        }

        // Two unit axes spanning the plane across circle_axis, and their normal.
        private ((double X, double Y, double Z) c1, (double X, double Y, double Z) c2, (double X, double Y, double Z) up) OrthogonalAxes()
        {
            double ax, ay, az;
            switch (_e.CircleAxis)
            {
                case 0: ax = 0; ay = 0; az = 1; break;
                case 1: ax = 0; ay = 1; az = 0; break;
                case 2: ax = 1; ay = 0; az = 0; break;
                default:                                 // the emitter's own axis
                    (ax, ay, az) = Norm(_e.AxisX, _e.AxisY, _e.AxisZ);
                    if (ax == 0 && ay == 0 && az == 0) ay = 1;
                    break;
            }
            // The world up vector, unless the axis is up itself.
            double vx = 0, vy = 1, vz = 0;
            if (Math.Abs(Math.Abs(ay) - 1.0) < 1e-9) { vx = 1; vy = 0; }
            var c1 = Norm(ay * vz - az * vy, az * vx - ax * vz, ax * vy - ay * vx);
            var c2 = Norm(ay * c1.Z - az * c1.Y, az * c1.X - ax * c1.Z, ax * c1.Y - ay * c1.X);
            var up = Norm(c1.Y * c2.Z - c1.Z * c2.Y, c1.Z * c2.X - c1.X * c2.Z, c1.X * c2.Y - c1.Y * c2.X);
            return (c1, c2, up);
        }

        private static (double X, double Y, double Z) Norm(double x, double y, double z)
        {
            double l = Math.Sqrt(x * x + y * y + z * z);
            return l < 1e-12 ? (0.0, 0.0, 0.0) : (x / l, y / l, z / l);
        }

        /// <summary>The alive particles this frame, with the SPA scale/colour/alpha animation curves applied over
        /// each particle's life. Without a curve a field stays at its base value (the game holds alpha
        /// constant and the particle simply ends at death, no synthetic fade).</summary>
        public IEnumerable<SpaParticleState> Particles()
        {
            // follow_emtr: the particle tracks the emitter's CURRENT path offset (added here, not baked at spawn). It
            // also has ~no velocity of its own, so a DIRECTIONAL billboard must orient along the emitter's MOTION
            // (its travel direction), otherwise the needle/wave just points up (Pin Missile / Sonic Boom).
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
                // WRAPPING (lifeRateOffset + loopTimeFactor*age, u8 wrap), NOT on the life
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
                // The alpha curve's randomRange flicker was drawn in Step, so re-enumerating a frame is stable.
                double alpha = (_e.UseAlphaAnm ? AnimAlpha(lrAlp, p.AlpK) : _e.BaseAlpha) / 31.0;

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

        // SPLAnim_Alpha: the curve value through ScaledRange(value, randomRange), k drawn once per frame.
        private double AnimAlpha(int lr, double k) => AlpCurve(lr) * k;

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
