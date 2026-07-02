using System;
using System.Collections.Generic;

namespace DSPRE.Avalonia.Data
{
    /// <summary>A live particle's render state for one frame (world units; the renderer maps to screen pixels).</summary>
    public struct SpaParticleState
    {
        public double X, Y;     // world position
        public double VX, VY;   // velocity (for directional/line billboards)
        public double Scale;    // world scale
        public double Alpha;    // 0..1
        public byte R, G, B;
        public int TexNo;       // current texture index (SPLResTexAnm picks this per particle over its life)
        public double Rotation; // billboard rotation (radians), from init_rtt + rotation-anim spin
    }

    /// <summary>
    /// Simulates a single SPA emitter's particles frame-by-frame, faithfully to the leaked spl library
    /// (spl_gen.c emission + spl_emitter.c update): each emission tick spawns <c>gen_num</c> particles (fractional
    /// accumulation) while the emitter is alive; each particle starts on the emission shape (× radius) with velocity
    /// = direction·init_vel_pos, then every frame <c>vel = vel·(air_resist+0.09375)/512; pos += vel; age++</c> and
    /// dies when <c>age &gt; ptcl_life</c>. A pragmatic 2D reduction (X/Y plane) for the preview; fields/child/3D
    /// projection are omitted, colour/alpha fade is linear unless extended later.
    /// </summary>
    public sealed class SpaSimulator
    {
        private struct P { public double X, Y, Z, VX, VY, VZ; public int Age, Life, RndTex, ClrRnd; public double OVX, OVY, Phase, Rot0, RotRate; }
        // SPLResChld: a child particle spawned by a parent (trail/spark) — its own life, decaying scale/alpha.
        private struct Child { public double X, Y, VX, VY; public int Age, Life; public double Scale0; }

        private readonly SpaEmitter _e;
        private readonly List<P> _ptcls = new List<P>();
        private readonly List<Child> _children = new List<Child>();
        private readonly Random _rng;
        private readonly double _air;
        private readonly double _axisX, _axisY;   // unit travel direction (attacker↔defender) for init_vel_axis
        private readonly double _driftX, _driftY;  // constant per-particle drift (operator projectiles crossing to target)
        private int _frame;
        private double _genAccum;

        // FIELD_OPERATOR FLD_MAGNET / FLD_CONVERGENCE override: the SPA emitter's own field target is a local
        // placeholder; the operator retargets it to a mon (FLD_AT/DF/SET_DF) — Mega Drain magnet, BubbleBeam/Aurora
        // convergence. NaN = keep the SPA's own target.
        private readonly bool _magOverride; private readonly double _magX, _magY;
        private readonly bool _convOverride; private readonly double _convX, _convY;

        public SpaSimulator(SpaEmitter e, double axisX = 0, double axisY = 0, double driftX = 0, double driftY = 0,
                            double magOverrideX = double.NaN, double magOverrideY = double.NaN,
                            double convOverrideX = double.NaN, double convOverrideY = double.NaN, int seed = 0x5EED)
        {
            _e = e;
            _rng = new Random(seed);
            _air = AirResistMultiplier(e.AirResist);
            _axisX = axisX; _axisY = axisY;
            _driftX = driftX; _driftY = driftY;
            _magOverride = !double.IsNaN(magOverrideX); _magX = magOverrideX; _magY = magOverrideY;
            _convOverride = !double.IsNaN(convOverrideX); _convX = convOverrideX; _convY = convOverrideY;
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

        /// <summary>Velocity multiplier applied each frame: <c>(air_resist + FX32_CONST(0.09375)) / 512</c> where
        /// FX32_CONST(0.09375) = 384, so air_resist 128 → ×1.0 (no damping), &lt;128 damps, &gt;128 accelerates.</summary>
        public static double AirResistMultiplier(int airResist) => (airResist + 384.0) / 512.0;

        public int AliveCount => _ptcls.Count;

        private bool _stopped;
        /// <summary>WEST_EXIT_PARTICLE (Wp_Exit): stop emitting now and let the live particles die out. Also the only
        /// way an "emit forever" emitter (emtr_life == 0) ever finishes.</summary>
        public void Stop() => _stopped = true;

        /// <summary>True once the emitter has stopped emitting and all its particles have died.</summary>
        public bool Finished => _ptcls.Count == 0 && _children.Count == 0 && (_stopped || (_e.EmitterLife != 0 && _frame >= _e.EmitterLife));

        public void Step()
        {
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
                p.VX *= _air; p.VY *= _air;
                // Field accelerations accumulate, then vel += acc (spl_emitter.c: vel*=air; vel+=acc).
                double accX = _e.GravityX, accY = _e.GravityY;
                if (_e.UseMagnet || _magOverride)   // spl_calc_magnet: acc += mag·((target − pos) − vel) → spring-pull
                {
                    double mtX = _magOverride ? _magX : _e.MagnetX, mtY = _magOverride ? _magY : _e.MagnetY;
                    double mag = _e.UseMagnet ? _e.MagnetMag : 0.02;   // keep the SPA's spring strength; default if none
                    accX += mag * ((mtX - p.X) - p.VX);
                    accY += mag * ((mtY - p.Y) - p.VY);
                }
                if (_e.RandIntvl > 0 && p.Age % _e.RandIntvl == 0)   // spl_calc_random: a velocity kick every intvl frames
                {
                    accX += (_rng.NextDouble() * 2.0 - 1.0) * _e.RandMagX;
                    accY += (_rng.NextDouble() * 2.0 - 1.0) * _e.RandMagY;
                }
                p.VX += accX; p.VY += accY;
                p.VZ *= _air;   // the depth velocity damps like the others; no field acts on it in this 2D reduction
                p.X += p.VX; p.Y += p.VY; p.Z += p.VZ;
                if (_e.UseConv || _convOverride)   // spl_calc_convergence: lerp the POSITION toward the convergence point
                {
                    double ctX = _convOverride ? _convX : _e.ConvX, ctY = _convOverride ? _convY : _e.ConvY;
                    double ratio = _e.UseConv ? _e.ConvRatio : 0.1;   // operator keeps the SPA's ratio; default if none
                    p.X += ratio * (ctX - p.X);
                    p.Y += ratio * (ctY - p.Y);
                }
                if (_e.UseColl)   // spl_calc_scfield: a horizontal plane at CollY — kill (0) or bounce (1) on crossing
                {
                    double prevY = p.Y - p.VY;
                    if ((prevY > _e.CollY) != (p.Y > _e.CollY))   // crossed the plane this frame
                    {
                        p.Y = _e.CollY;
                        if (_e.CollEvent == 1) p.VY = -p.VY * _e.CollBounce; else p.Age = p.Life;
                    }
                }
                if (_spin)   // spl_calc_spin: rotate ptcl_pos around axis_type (0=X,1=Y,2=Z). Only Z spins the screen
                {            // plane; X/Y spins involve depth (Z) and leave the other screen axis free (Mist falls under gravity).
                    if (_e.SpinAxis == 2) { double nx = p.X * _spinCos - p.Y * _spinSin, ny = p.X * _spinSin + p.Y * _spinCos; p.X = nx; p.Y = ny; }      // Z: X↔Y
                    else if (_e.SpinAxis == 1) { double nx = p.Z * _spinSin + p.X * _spinCos, nz = p.Z * _spinCos - p.X * _spinSin; p.X = nx; p.Z = nz; } // Y: Z↔X (Y free)
                    else { double ny = p.Y * _spinCos - p.Z * _spinSin, nz = p.Y * _spinSin + p.Z * _spinCos; p.Y = ny; p.Z = nz; }                       // X: Y↔Z (X free)
                }
                // SPLResChld: spawn child particles from this parent (gen_num every gen_intvl, after gen_start).
                if (_e.UseChild && p.Age >= _e.ChildGenStart && (p.Age - _e.ChildGenStart) % _e.ChildGenIntvl == 0 && _children.Count < 4000)
                    for (int k = 0; k < _e.ChildGenNum; k++)
                        _children.Add(new Child { X = p.X, Y = p.Y, VX = p.VX * _e.ChildVelRatio, VY = p.VY * _e.ChildVelRatio,
                                                  Age = 0, Life = _e.ChildLife, Scale0 = _e.BaseScale * _e.ChildSclRatio });
                p.Age++;
                if (p.Age > p.Life) _ptcls.RemoveAt(i);
                else _ptcls[i] = p;
            }
            // Children: air-damped drift + gravity, die at their own life (the parent may already be gone).
            for (int i = _children.Count - 1; i >= 0; i--)
            {
                var c = _children[i];
                c.VX *= _air; c.VY *= _air; c.VX += _e.GravityX; c.VY += _e.GravityY;
                c.X += c.VX; c.Y += c.VY; c.Age++;
                if (c.Age > c.Life) _children.RemoveAt(i); else _children[i] = c;
            }
            _frame++;
        }

        // Spawn one particle. emIdx/emCount are this tick's index/total so CIRCLE_RI rings come out evenly spaced
        // (spl_gen.c uses idx = emission·16/total), instead of clumping into a wedge with random angles.
        private void Emit(int emIdx, int emCount)
        {
            // The SPL shapes are 3D; the preview camera is orthographic facing −Z, so we emit in 3D then keep (x,y).
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
            int life = _e.ParticleLife <= 0 ? 1 : _e.ParticleLife;
            (double mx, double my) = _emitterMotion?.Invoke(_frame) ?? (0.0, 0.0);   // emitter's path offset now
            // The emitter's travel direction at spawn — used to orient a DIRECTIONAL billboard (the needle/wave) whose
            // own velocity is ~0 because it rides the moving emitter (Pin Missile/Sonic Boom/Horn Drill: EMIT_STRAIGHT/
            // PARABOLIC sweep the emitter attacker→defender). Without this the needle has no velocity and points up.
            (double pmx, double pmy) = _emitterMotion?.Invoke(Math.Max(0, _frame - 1)) ?? (0.0, 0.0);
            double ovx = mx - pmx, ovy = my - pmy;
            double rot0 = _e.UseInitRttRndm ? (_e.RttMinRot + _rng.NextDouble() * (_e.RttMaxRot - _e.RttMinRot)) : _e.InitRot;
            // spl_gen.c: a random tex-anim picks one tex_no at birth; otherwise the texture is chosen per frame by age.
            int rndTex = (_e.UseTexAnm && _e.TexUseRndm && _e.TexSeq != null)
                ? _e.TexSeq[_rng.Next(Math.Max(1, _e.TexUseNum)) % _e.TexSeq.Length] : _e.TexNo;
            _ptcls.Add(new P
            {
                RndTex = rndTex,
                // Emitter position comes from the ADD_PARTICLE callback (the layer centre), which OVERRIDES the
                // SPA's own base pos — so particles start at the shape offset (+ the moving emitter's path offset).
                // follow_emtr particles TRACK the moving emitter (the current offset is added at render), so DON'T
                // bake the spawn offset in — otherwise they'd double up. (Pin Missile/Sonic Boom needles travel this way.)
                X = ux * r + lox + (_e.FollowEmtr ? 0 : mx),
                Y = uy * r + loy + (_e.FollowEmtr ? 0 : my),
                Z = uz * r,
                // velocity = outward radial (init_vel_pos) + along the emitter axis (init_vel_axis): the latter is
                // what carries "travelling" moves (beams/projectiles) from the attacker toward the defender.
                VX = ux * _e.InitVelPos + _axisX * _e.InitVelAxis + _driftX,
                VY = uy * _e.InitVelPos + _axisY * _e.InitVelAxis + _driftY,
                VZ = uz * _e.InitVelPos,
                OVX = ovx, OVY = ovy,   // emitter travel direction at spawn (orientation only)
                Phase = _e.RandomLoopAnm ? _rng.NextDouble() : 0.0,   // random anim start phase for looping clr/tex anims
                ClrRnd = _e.ClrRndm ? _rng.Next(256) : 0,             // fixed random gradient point (colour use_rndm)
                Rot0 = rot0, RotRate = _e.RotRate,                    // billboard initial rotation + spin (init_rtt / rtt_anm)
                Age = 0,
                Life = life,
            });
        }

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
        /// each particle's life (spl_anm.c). Without a curve a field stays at its base value (the game holds alpha
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
            foreach (var p in _ptcls)
            {
                int lr = (int)(255.0 * p.Age / Math.Max(1, p.Life));   // lifeRate 0..255
                if (lr > 255) lr = 255;
                // A LOOPING colour/texture anim with ptcl_random_loop_anm advances from a random per-particle phase and
                // wraps, so the emitter shows the whole cycle simultaneously (Aurora Beam's drifting rainbow). Scale &
                // alpha keep the normal once-through lifeRate (the particle still fades and dies on schedule).
                double frac = (double)p.Age / Math.Max(1, p.Life);
                int lrClr = (_e.ClrLoop && p.Phase != 0.0) ? (int)(255.0 * ((frac + p.Phase) % 1.0)) : lr;
                int lrTex = (_e.TexLoop && p.Phase != 0.0) ? (int)(255.0 * ((frac + p.Phase) % 1.0)) : lr;

                double scale = _e.BaseScale * (_e.UseScaleAnm ? SclCurve(lr) : 1.0);
                byte r = _e.ColorR, g = _e.ColorG, b = _e.ColorB;
                // colour use_rndm (SPLResClrAnm): each particle picks a FIXED random point on the gradient at birth, so
                // the emitter shows the whole clr_s→clr_n→clr_e spread at once (Aurora Beam's pink/cyan/yellow). Always
                // interpolate for the random spread; otherwise follow the curve's own interpolation flag.
                if (_e.UseColorAnm) (r, g, b) = _e.ClrRndm ? ClrCurve(p.ClrRnd, true) : ClrCurve(lrClr, _e.ClrInterp);
                double alpha = (_e.UseAlphaAnm ? AlpCurve(lr) : _e.BaseAlpha) / 31.0;

                // Billboard orientation vector (renderer uses this ONLY for the directional quad angle, not motion):
                // follow → the emitter's current travel; a needle riding a moving emitter (own velocity ~0) → its
                // baked spawn travel direction; otherwise the particle's own velocity.
                double ovx = follow ? emVX : p.VX, ovy = follow ? emVY : p.VY;
                if (!follow && Math.Abs(p.VX) < 1e-3 && Math.Abs(p.VY) < 1e-3 && (Math.Abs(p.OVX) > 1e-9 || Math.Abs(p.OVY) > 1e-9))
                { ovx = p.OVX; ovy = p.OVY; }
                yield return new SpaParticleState
                {
                    X = p.X + emx, Y = p.Y + emy,
                    VX = ovx, VY = ovy,   // directional billboards orient by this
                    Scale = scale, Alpha = Math.Clamp(alpha, 0, 1),
                    R = r, G = g, B = b,
                    TexNo = TexNoFor(lrTex, p),
                    Rotation = p.Rot0 + p.RotRate * p.Age,   // init_rtt + spin·age
                };
            }
            // Children (SPLResChld): scale 1→scl_e, alpha fades out over life, own colour if use_chld_clr.
            foreach (var c in _children)
            {
                double t = (double)c.Age / Math.Max(1, c.Life);
                double cscale = c.Scale0 * (1.0 + (_e.ChildSclEnd - 1.0) * t);
                double calpha = (1.0 - t) * (_e.BaseAlpha / 31.0);
                yield return new SpaParticleState
                {
                    X = c.X, Y = c.Y, VX = c.VX, VY = c.VY,
                    Scale = cscale, Alpha = Math.Clamp(calpha, 0, 1),
                    R = _e.ChildUseClr ? _e.ChildR : _e.ColorR,
                    G = _e.ChildUseClr ? _e.ChildG : _e.ColorG,
                    B = _e.ChildUseClr ? _e.ChildB : _e.ColorB,
                    TexNo = _e.ChildTexNo,
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
