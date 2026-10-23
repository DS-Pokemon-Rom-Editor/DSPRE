using System;
using System.Collections.Generic;

namespace DSPRE.Avalonia.Data
{
    /// <summary>A live particle's render state for one frame (world units; the renderer maps to screen pixels).</summary>
    public struct SpaParticleState
    {
        public double X, Y;     // world position
        public double Z;        // depth offset from the emitter's plane (px units, +z toward the camera)
        public double VX, VY;   // velocity, for directional and line billboards
        public double VZ;       // depth velocity, so directional polygons turn in 3D
        public double Scale;    // world scale (base × animation), for the dot fallback
        // Per-axis scales as the billboard build applies them: the scale animation drives X, Y or both by misc.scaleAnimDir.
        // The renderer draws the quad with these (then × aspect on X), not Scale.
        public double ScaleForX, ScaleForY;
        public double Alpha;    // 0..1
        public byte R, G, B;
        public int TexNo;       // current texture index, which the texture animation picks over the particle's life
        public double Rotation; // billboard rotation (radians), from init_rtt plus the spin
        public bool IsChild;    // children are drawn with the child flags' draw settings
    }

    /// <summary>
    /// Runs one SPA emitter's particles frame by frame as the game does: each tick spawns <c>gen_num</c> particles while the
    /// emitter lives, each starts on the emission shape moving at init_vel_pos, then every frame
    /// <c>vel = vel·(air_resist+0.09375)/512; pos += vel; age++</c> until <c>age &gt; ptcl_life</c>. The fields, children
    /// and per-particle randomness all run.
    /// </summary>
    public sealed class SpaSimulator
    {
        private struct P { public double X, Y, Z, VX, VY, VZ; public int Age, Life, RndTex, ClrRnd, LrOff; public double OVX, OVY, Rot0, RotRate, Scl, AlpK; }
        // A child particle spawned by a parent, for trails and sparks, with its own life and fading scale and alpha.
        private struct Child { public double X, Y, Z, VX, VY, VZ; public int Age, Life; public double Scale0, Rot, RotRate, Alpha0; }

        private readonly SpaEmitter _e;
        private readonly List<P> _ptcls = new List<P>();
        private readonly List<Child> _children = new List<Child>();
        private readonly SplRandom _rng;
        private readonly double _air;
        private readonly double _axisX, _axisY;   // travel direction (attacker to defender) for init_vel_axis
        private readonly double _axisZ;           // depth component, only when the SPA's 3D axis is in use
        private readonly double _driftX, _driftY;  // steady drift per particle, for projectiles crossing to the target
        private int _frame;
        private double _genAccum;

        // Magnet and convergence override: the SPA's field target is a placeholder, and the move points it at a Pokémon,
        // as the Mega Drain magnet and the BubbleBeam and Aurora Beam convergence do. NaN keeps the SPA's target.
        private readonly bool _magOverride; private readonly double _magX, _magY, _magZ;
        private readonly bool _convOverride; private readonly double _convX, _convY, _convZ;

        public SpaSimulator(SpaEmitter e, double axisX = 0, double axisY = 0, double driftX = 0, double driftY = 0,
                            double magOverrideX = double.NaN, double magOverrideY = double.NaN,
                            double convOverrideX = double.NaN, double convOverrideY = double.NaN, int seed = 0x5EED,
                            double axisZ = double.NaN, double magOverrideZ = double.NaN, double convOverrideZ = double.NaN,
                            SplRandom rng = null)
        {
            _e = e;
            // The game has one random generator for every emitter; a shared one keeps emitters from mirroring each other.
            _rng = rng ?? new SplRandom(unchecked((uint)seed));
            _air = AirResistMultiplier(e.AirResist);
            _delay = Math.Max(0, e.StartOffset);
            _axisX = axisX; _axisY = axisY;
            // An axis from the callback lies in the screen plane (z 0); with the SPA's own axis its z applies (NaN: use
            // e.AxisZ only when x and y are e.Axis).
            _axisZ = !double.IsNaN(axisZ) ? axisZ
                   : (axisX == e.AxisX && axisY == e.AxisY ? e.AxisZ : 0.0);
            _driftX = driftX; _driftY = driftY;
            _magOverride = !double.IsNaN(magOverrideX); _magX = magOverrideX; _magY = magOverrideY;
            _convOverride = !double.IsNaN(convOverrideX); _convX = convOverrideX; _convY = convOverrideY;
            _magZ = double.IsNaN(magOverrideZ) ? 0 : magOverrideZ;
            _convZ = double.IsNaN(convOverrideZ) ? 0 : convOverrideZ;
            if (e.SpinRadian != 0)
            {
                double ang = e.SpinRadian / 65536.0 * 2.0 * Math.PI;   // spin field: turn the particle each frame
                _spinCos = Math.Cos(ang); _spinSin = Math.Sin(ang); _spin = true;
            }
        }

        private readonly bool _spin;
        private readonly double _spinCos = 1, _spinSin;

        // Emitter motion (RevolveEmitter, MoveEmitterA2BLinear, MoveEmitterA2BParabolic): the emitter's offset at a frame,
        // taken at spawn so particles trail along its path (orbit, stream or arc) and then move by themselves.
        private Func<int, (double, double)> _emitterMotion;
        public void SetEmitterMotion(Func<int, (double, double)> m) => _emitterMotion = m;
        public double AnchorX, AnchorY;   // the emitter's spawn position on screen, so RevolveEmitter can centre its orbit again
        // The anchor's world y in px units (+Y up; particle space projects to screen y 96). The collision plane is in world
        // space (the game tests emitterPos.y + particle.y), so local ys are offset by this, taken from the screen anchor.
        public double AnchorWorldY => 96.0 - AnchorY;

        /// <summary>Velocity multiplier each frame: <c>(air_resist + FX32_CONST(0.09375)) / 512</c>, with FX32_CONST(0.09375) = 384,
        /// so air_resist 128 keeps speed, less damps and more speeds up.</summary>
        public static double AirResistMultiplier(int airResist) => (airResist + 384.0) / 512.0;

        public int AliveCount => _ptcls.Count;

        private bool _stopped;
        /// <summary>UnloadParticleSystem: stops emitting and lets the live particles die out. It's the only way an emitter
        /// with emtr_life 0, which emits forever, finishes.</summary>
        public void Stop() => _stopped = true;

        /// <summary>True once the emitter has stopped and all its particles have died. A start_offset still waiting counts as
        /// not finished.</summary>
        public bool Finished => (_stopped || _delay <= 0) && _ptcls.Count == 0 && _children.Count == 0 && (_stopped || (_e.EmitterLife != 0 && _frame >= _e.EmitterLife));

        // base.start_offset: frames the emitter waits before its clock starts. It's how Seed Flare's big slashes follow its
        // small particles with no script waits.
        private int _delay;

        public void Step()
        {
            if (_delay > 0 && !_stopped) { _delay--; return; }

            // Emit while the emitter lives (emtr_life 0 is forever) and UnloadParticleSystem hasn't stopped it.
            bool emitting = !_stopped && (_e.EmitterLife == 0 || _frame < _e.EmitterLife);
            int intvl = Math.Max(1, _e.GenInterval);
            if (emitting && _frame % intvl == 0)
            {
                _genAccum += Math.Max(0, _e.GenNum);
                int n = (int)_genAccum;
                _genAccum -= n;
                for (int i = 0; i < n && _ptcls.Count < 4000; i++) Emit(i, n);
            }

            // Update and cull.
            for (int i = _ptcls.Count - 1; i >= 0; i--)
            {
                P p = _ptcls[i];
                // SPLAnim_Alpha draws once per frame for every alpha-animated particle, before the behaviours.
                if (_e.UseAlphaAnm) p.AlpK = _rng.ScaledRange(_e.AlpFlick);
                // Behaviours read the undamped velocity, then vel = vel*air + acc (SPLEmitter_Update).
                double accX = _e.GravityX, accY = _e.GravityY, accZ = _e.GravityZ;
                if (_e.UseMagnet || _magOverride)   // spl_calc_magnet: acc += mag·((target − pos) − vel), a spring pull
                {
                    double mtX = _magOverride ? _magX : _e.MagnetX, mtY = _magOverride ? _magY : _e.MagnetY;
                    double mtZ = _magOverride ? _magZ : _e.MagnetZ;
                    double mag = _e.UseMagnet ? _e.MagnetMag : 0.02;   // keep the SPA's spring strength, or a default
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
                if (_e.UseConv || _convOverride)   // spl_calc_convergence: moves the position toward the convergence point
                {
                    double ctX = _convOverride ? _convX : _e.ConvX, ctY = _convOverride ? _convY : _e.ConvY;
                    double ctZ = _convOverride ? _convZ : _e.ConvZ;
                    double ratio = _e.UseConv ? _e.ConvRatio : 0.1;   // keep the SPA's ratio, or a default
                    p.X += ratio * (ctX - p.X);
                    p.Y += ratio * (ctY - p.Y);
                    p.Z += ratio * (ctZ - p.Z);
                }
                if (_e.UseColl)   // collision plane: a horizontal plane in world space; the game tests
                {                 // emitterPos.y + particle.y against it, so the anchor's world y is added.
                    double wy = AnchorWorldY + p.Y, wyPrev = wy - p.VY;
                    if ((wyPrev > _e.CollY) != (wy > _e.CollY))   // crossed the plane this frame
                    {
                        p.Y = _e.CollY - AnchorWorldY;
                        if (_e.CollEvent == 1) p.VY = -p.VY * _e.CollBounce; else p.Age = p.Life;
                    }
                }
                if (_spin)   // spl_calc_spin: turns ptcl_pos about axis_type (0 = X, 1 = Y, 2 = Z). Only Z spins in the
                {            // screen plane; X and Y spins move depth and leave the other axis free (Mist falls by gravity).
                    if (_e.SpinAxis == 2) { double nx = p.X * _spinCos - p.Y * _spinSin, ny = p.X * _spinSin + p.Y * _spinCos; p.X = nx; p.Y = ny; }      // Z: X and Y
                    else if (_e.SpinAxis == 1) { double nx = p.Z * _spinSin + p.X * _spinCos, nz = p.Z * _spinCos - p.X * _spinSin; p.X = nx; p.Z = nz; } // Y: Z and X (Y free)
                    else { double ny = p.Y * _spinCos - p.Z * _spinSin, nz = p.Y * _spinSin + p.Z * _spinCos; p.Y = ny; p.Z = nz; }                       // X: Y and Z (X free)
                }
                // Children (EmitChildren) take the parent's 3D position and velocity × velRatio plus a ± randomInitVelMag kick per
                // component; base scale = the parent's current scale × (scaleRatio+1)/64; alpha = the parent's current alpha;
                // rotation by rotationType (1 holds the parent's angle, 2 keeps its spin).
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
            // Children drift with air damping and die at their own life, even after the parent. The behaviour fields apply
            // to children only when the child flags' usesBehaviors bit is set.
            for (int i = _children.Count - 1; i >= 0; i--)
            {
                Child c = _children[i];
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

        // Spawns one particle. emIdx and emCount are this tick's index and total, so a ring comes out evenly spaced
        // (idx = emission·16/total) and not clumped by random angles.
        private void Emit(int emIdx, int emCount)
        {
            // Circles, cylinders and hemispheres lie on the two axes across circle_axis; spheres ignore it. The random draws
            // follow SPLEmitter_EmitParticles: shape, speeds, direction, scale, colour, angle, spin, life, texture, loop offset.
            ((double X, double Y, double Z) c1, (double X, double Y, double Z) c2, (double X, double Y, double Z) up) = OrthogonalAxes();
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
                    (double sx, double sy, double sz) = _rng.Vec();
                    posX = sx * _e.Radius; posY = sy * _e.Radius; posZ = sz * _e.Radius;
                    break;
                }
                case 2:   // circle border
                {
                    (double cx, double cy) = _rng.VecXY();
                    (posX, posY, posZ) = Tilt(cx * _e.Radius, cy * _e.Radius, 0);
                    break;
                }
                case 3:   // circle border, uniform: evenly spaced, sine on the first axis
                {
                    double a = Math.PI * 2.0 * emIdx / Math.Max(1, emCount);
                    (posX, posY, posZ) = Tilt(Math.Sin(a) * _e.Radius, Math.Cos(a) * _e.Radius, 0);
                    break;
                }
                case 4:   // sphere: each component scaled by its own random factor
                {
                    (double sx, double sy, double sz) = _rng.Vec();
                    posX = sx * _e.Radius * R(); posY = sy * _e.Radius * R(); posZ = sz * _e.Radius * R();
                    break;
                }
                case 5:   // circle
                {
                    (double cx, double cy) = _rng.VecXY();
                    double lx = cx * _e.Radius * R(), ly = cy * _e.Radius * R();
                    (posX, posY, posZ) = Tilt(lx, ly, 0);
                    break;
                }
                case 6:   // cylinder surface
                {
                    (double cx, double cy) = _rng.VecXY();
                    (posX, posY, posZ) = Tilt(cx * _e.Radius, cy * _e.Radius, _rng.Range(_e.Length));
                    (tanX, tanY, tanZ) = Tilt(cx, cy, 0);
                    ringVelocity = true;
                    break;
                }
                case 7:   // cylinder
                {
                    (double cx, double cy) = _rng.VecXY();
                    double lx = cx * _e.Radius * R(), ly = cy * _e.Radius * R();
                    (posX, posY, posZ) = Tilt(lx, ly, _rng.Range(_e.Length));
                    break;
                }
                case 8:   // hemisphere surface: flipped onto the side the axes face
                case 9:   // hemisphere
                {
                    (double sx, double sy, double sz) = _rng.Vec();
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
                default:  // point
                    posX = posY = posZ = 0;
                    break;
            }
            // randomAttenuation: speeds and base scale spread around the base (DoubleScaledRange); lifetime only goes down
            // (ScaledRange) and is at least 1 frame.
            double magPos = _e.InitVelPos * _rng.DoubleScaledRange(_e.RndVel);
            double magAxis = _e.InitVelAxis * _rng.DoubleScaledRange(_e.RndVel);
            // Velocity points away from the centre; a particle born exactly at the centre gets a random direction.
            double nx, ny, nz;
            if (ringVelocity) { (nx, ny, nz) = Norm(tanX, tanY, tanZ); }
            else if (posX == 0 && posY == 0 && posZ == 0) { (nx, ny, nz) = _rng.Vec(); }
            else { (nx, ny, nz) = Norm(posX, posY, posZ); }
            double pScale = _e.BaseScale * _rng.DoubleScaledRange(_e.RndScale);
            // colour use_rndm picks one of start, base and end at birth, and the colour animation doesn't run for such
            // emitters, so the pick stays.
            int clrRnd = _e.UseColorAnm && _e.ClrRndm ? (int)(_rng.U32(12) % 3) : 1;
            // randomInitAngle takes a full u16 angle; the spin rate is per particle in [minRotation, maxRotation).
            double rot0 = _e.UseInitRttRndm ? (_rng.Next() & 0xFFFF) / 65536.0 * 2.0 * Math.PI : _e.InitRot;
            double rotRate = _e.UseRttAnm ? _rng.Between(_e.RttMinRot, _e.RttMaxRot) : 0.0;
            int life = _rng.ScaledRange(Math.Max(0, _e.ParticleLife), _e.RndLife) + 1;
            // A random texture animation picks one tex_no at birth; otherwise age picks the texture each frame.
            int rndTex = (_e.UseTexAnm && _e.TexUseRndm && _e.TexSeq != null)
                ? _e.TexSeq[(int)(_rng.U32(12) % (uint)Math.Max(1, _e.TexUseNum)) % _e.TexSeq.Length] : _e.TexNo;
            int lrOff = _e.RandomLoopAnm ? (int)_rng.U32(8) : 0;   // lifeRateOffset for looping animations
            (double mx, double my) = _emitterMotion?.Invoke(_frame) ?? (0.0, 0.0);   // the emitter's path offset now
            // The emitter's travel direction at spawn, to turn a directional billboard riding a moving emitter, whose own
            // velocity is near 0 (the Pin Missile, Sonic Boom and Horn Drill needles); without it the needle points up.
            (double pmx, double pmy) = _emitterMotion?.Invoke(Math.Max(0, _frame - 1)) ?? (0.0, 0.0);
            double ovx = mx - pmx, ovy = my - pmy;
            _ptcls.Add(new P
            {
                RndTex = rndTex,
                // The CreateEmitter callback sets the emitter's position (the layer centre) over the SPA's base pos, so particles
                // start at the shape offset. follow_emtr particles track the moving emitter (its offset is added when drawing),
                // so the spawn offset isn't baked in here, or they would move twice as far.
                X = posX + (_e.FollowEmtr ? 0 : mx),
                Y = posY + (_e.FollowEmtr ? 0 : my),
                Z = posZ,
                // Velocity: outward from the spawn position (init_vel_pos) plus along the emitter axis (init_vel_axis), which
                // carries moves that travel to the defender.
                VX = nx * magPos + _axisX * magAxis + _driftX,
                VY = ny * magPos + _axisY * magAxis + _driftY,
                VZ = nz * magPos + _axisZ * magAxis,
                OVX = ovx, OVY = ovy,   // the emitter's travel direction at spawn, for turning only
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
        /// Whether a parent at <paramref name="age"/> emits children this frame: from age &gt;= life * delay / 256 of its
        /// randomised life, then every interval frames (SPLEmitter_Update).
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
                default:                                 // the emitter's axis
                    (ax, ay, az) = Norm(_e.AxisX, _e.AxisY, _e.AxisZ);
                    if (ax == 0 && ay == 0 && az == 0) ay = 1;
                    break;
            }
            // The world up vector, unless the axis is up itself.
            double vx = 0, vy = 1, vz = 0;
            if (Math.Abs(Math.Abs(ay) - 1.0) < 1e-9) { vx = 1; vy = 0; }
            (double X, double Y, double Z) c1 = Norm(ay * vz - az * vy, az * vx - ax * vz, ax * vy - ay * vx);
            (double X, double Y, double Z) c2 = Norm(ay * c1.Z - az * c1.Y, az * c1.X - ax * c1.Z, ax * c1.Y - ay * c1.X);
            (double X, double Y, double Z) up = Norm(c1.Y * c2.Z - c1.Z * c2.Y, c1.Z * c2.X - c1.X * c2.Z, c1.X * c2.Y - c1.Y * c2.X);
            return (c1, c2, up);
        }

        private static (double X, double Y, double Z) Norm(double x, double y, double z)
        {
            double l = Math.Sqrt(x * x + y * y + z * z);
            return l < 1e-12 ? (0.0, 0.0, 0.0) : (x / l, y / l, z / l);
        }

        /// <summary>The live particles this frame, with the scale, colour and alpha curves applied over each one's life. A
        /// field without a curve stays at its base value; the game ends a particle at death with no fade.</summary>
        public IEnumerable<SpaParticleState> Particles()
        {
            // follow_emtr: the particle tracks the emitter's current path offset, added here. It has almost no velocity of
            // its own, so a directional billboard turns along the emitter's motion, or the needle points up (Pin Missile).
            bool follow = _e.FollowEmtr && _emitterMotion != null;
            (double emx, double emy) = follow ? _emitterMotion(_frame) : (0.0, 0.0);
            (double pemx, double pemy) = follow ? _emitterMotion(Math.Max(0, _frame - 1)) : (0.0, 0.0);
            double emVX = emx - pemx, emVY = emy - pemy;   // emitter path velocity this frame (sim space)
            // drawChildrenFirst and hideParent (resource flag bits 21 and 22): parent and child draw order, and whether only
            // children are drawn, leaving the parent an invisible spawner.
            if (_e.DrawChildrenFirst)
                foreach (SpaParticleState s in ChildStates()) yield return s;
            if (!_e.HideParent)
            foreach (P p in _ptcls)
            {
                int lr = (int)(255.0 * p.Age / Math.Max(1, p.Life));   // lifeRate 0..255, once over the life
                if (lr > 255) lr = 255;
                // Looping animations run on the loopFrames clock, offset by lifeRateOffset and wrapping as a u8
                // (lifeRateOffset + loopTimeFactor*age), not on the share of the life. Each picks its clock by its loop flag.
                int lrLoop = (p.LrOff + p.Age * 255 / _e.LoopFrames) & 0xFF;
                int lrScl = _e.SclLoop ? lrLoop : lr;
                int lrClr = _e.ClrLoop ? lrLoop : lr;
                int lrTex = _e.TexLoop ? lrLoop : lr;
                int lrAlp = _e.AlpLoop ? lrLoop : lr;

                double anim = _e.UseScaleAnm ? SclCurve(lrScl) : 1.0;
                double scale = p.Scl * anim;
                // misc.scaleAnimDir: 0 = both axes, 1 = X only, 2 = Y only.
                double scaleForX = p.Scl * (_e.ScaleAnimDir == 2 ? 1.0 : anim);
                double scaleForY = p.Scl * (_e.ScaleAnimDir == 1 ? 1.0 : anim);
                byte r = _e.ColorR, g = _e.ColorG, b = _e.ColorB;
                // colour use_rndm (randomStartColor): each particle picks one of start, base and end at birth and keeps it;
                // otherwise the in, peak and out curve runs.
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

                // The billboard's facing, used only to angle the directional quad: the emitter's current travel when
                // following; the spawn travel for a needle riding a moving emitter; otherwise the particle's velocity.
                double ovx = follow ? emVX : p.VX, ovy = follow ? emVY : p.VY;
                if (!follow && Math.Abs(p.VX) < 1e-3 && Math.Abs(p.VY) < 1e-3 && (Math.Abs(p.OVX) > 1e-9 || Math.Abs(p.OVY) > 1e-9))
                { ovx = p.OVX; ovy = p.OVY; }
                yield return new SpaParticleState
                {
                    X = p.X + emx, Y = p.Y + emy, Z = p.Z,
                    VX = ovx, VY = ovy, VZ = follow ? 0 : p.VZ,   // directional billboards and polygons turn by this
                    Scale = scale, ScaleForX = scaleForX, ScaleForY = scaleForY,
                    Alpha = Math.Clamp(alpha, 0, 1),
                    R = r, G = g, B = b,
                    TexNo = TexNoFor(lrTex, p),
                    Rotation = p.Rot0 + p.RotRate * p.Age,   // init_rtt + spin·age
                };
            }
            if (!_e.DrawChildrenFirst)
                foreach (SpaParticleState s in ChildStates()) yield return s;
        }

        // Children: scale goes from 1 to scl_e, alpha fades out over the life, own colour with use_chld_clr; rotation by
        // rotationType, set at spawn and maybe still spinning.
        private IEnumerable<SpaParticleState> ChildStates()
        {
            foreach (Child c in _children)
            {
                double t = (double)c.Age / Math.Max(1, c.Life);
                // The child scale and alpha animations run only when the child flags ask for them; otherwise the child keeps
                // its spawn scale and the parent alpha it took.
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
            return n > 0 ? _e.TexSeq[n - 1] : _e.TexNo;   // past the last threshold, hold the last frame
        }

        private double SclCurve(int lr)
        {
            if (lr < _e.SclIn) return _e.SclS + lr * (_e.SclN - _e.SclS) / Math.Max(1, _e.SclIn);
            if (lr < _e.SclOut) return _e.SclN;
            return _e.SclE + (lr - 255) * (_e.SclE - _e.SclN) / Math.Max(1, 255 - _e.SclOut);
        }

        // SPLAnim_Alpha: the curve value through ScaledRange(value, randomRange), k drawn once per frame.
        private double AnimAlpha(int lr, double k) => AlpCurve(lr) * k;

        private double AlpCurve(int lr)   // 0..31
        {
            if (lr < _e.AlpIn) return _e.AlpS + (double)(lr * (_e.AlpN - _e.AlpS)) / Math.Max(1, _e.AlpIn);
            if (lr < _e.AlpOut) return _e.AlpN;
            return _e.AlpE + (double)((lr - 255) * (_e.AlpE - _e.AlpN)) / Math.Max(1, 255 - _e.AlpOut);
        }

        // Three keyframes over the life: clr_s at in, clr_n (the base) at peak, clr_e at out. interp blends between them;
        // without it each keyframe holds until the next (clr_s to peak, clr_n to out, then clr_e).
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
