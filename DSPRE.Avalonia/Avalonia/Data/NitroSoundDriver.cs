using System;
using System.Collections.Generic;

namespace DSPRE.Avalonia.Data
{
    /// <summary>
    /// An offline copy of the DS sound driver playing one sequence: tracks, the 16 channels with their
    /// envelopes, sweeps and LFOs, and the mixer, run update by update at the driver's own rate.
    /// </summary>
    internal sealed class NitroSoundDriver
    {
        internal sealed class Settings
        {
            public int SampleRate = 32000;
            public double MaxSeconds = 8.0;

            /// <summary>False runs the timing only, without making sound, for reading notes.</summary>
            public bool Mix = true;

            /// <summary>False ends a track at a backward jump or an endless loop, so a tune is read once.</summary>
            public bool FollowLoops = true;

            /// <summary>The starting volume the game gives the player, or -1 for the archive's own.</summary>
            public int InitialVolume = -1;

            /// <summary>Pan and pitch added to every track.</summary>
            public int TrackPan, TrackPitch;

            /// <summary>Interpolates between wave samples. Off holds each sample, as the hardware does.</summary>
            public bool Interpolate;

            /// <summary>Stops the sequence this far in, letting notes release. Zero or less never stops it.</summary>
            public double StopSeconds;
        }

        internal struct NoteEvent
        {
            public int Update, Tick, Track, Key, Velocity, Length, Program, Pan, Volume;
        }

        private enum Kind { Pcm, Psg, Noise }
        private enum Env { Attack, Decay, Sustain, Release }
        private enum Arg { U8, S16, VarLen, Random, Variable }

        private const int TrackCount = 16, CallDepthMax = 3;
        private const int SyncStart = 1, SyncStop = 2, SyncTimer = 4, SyncVolume = 8, SyncPan = 16;
        private const int PrioRelease = 1, PrioStop = 0;
        private const int PcmMask = 0xFFFF, PsgMask = 0x3F00, NoiseMask = 0xC000;
        private const int EnvInit = NitroSoundTables.VolumeDbMin << 7;
        private const int BaseTempo = 240;

        // Free channels are searched in this order: PCM-only first, the capture pairs next, the channels that
        // can also play square waves or noise last.
        private static readonly int[] ChannelOrder = { 4, 5, 6, 7, 2, 0, 3, 1, 8, 9, 10, 11, 14, 12, 15, 13 };
        private static readonly int[] ShiftDiv = { 1, 2, 4, 16 };

        // Tone generators run eight steps per cycle and play middle C on their instrument's own key.
        private static readonly int PsgTimerC4 = (int)(NitroSoundTables.TimerClock / (8 * 261.6255653));

        private struct Lfo
        {
            public int Target, Speed, Depth, Range, Delay;
            public static Lfo Default => new Lfo { Target = 0, Depth = 0, Range = 1, Speed = 16, Delay = 0 };
        }

        private sealed class Channel
        {
            public int No;
            public bool Active, StartFlag, AutoSweep;
            public int Sync;
            public int Prio;
            public Kind Kind;
            public int Key, OriginalKey, Velocity, InitPan;
            public int UserDecay, UserDecay2, UserPitch, UserPan, PanRange;
            public int SweepPitch, SweepLength, SweepCounter;
            public int Attack, Decay, Sustain, Release;
            public int EnvDecay;
            public Env Env;
            public int Length;
            public Lfo Lfo;
            public int LfoCounter, LfoDelayCounter;
            public Track Owner;
            public SwavSample Wave;
            public int WaveTimer, Duty;
            public int Volume, Timer, Pan;

            // What the hardware channel is actually doing.
            public bool HwActive;
            public Kind HwKind;
            public SwavSample HwWave;
            public int HwDuty, HwVolume, HwShift, HwTimer, HwPan;
            public double HwPos;
            public int Lfsr, NoiseOut;
        }

        private sealed class Track
        {
            public int No;
            public bool Opened;
            public int Cur;
            public bool NoteWait = true, Mute, Tie, NoteFinishWait, Porta, CmpFlag = true, ChannelMaskFlag;
            public readonly int[] CallStack = new int[CallDepthMax];
            public readonly int[] LoopCount = new int[CallDepthMax];
            public int CallDepth;
            public int Prg, Prio = 64, Volume = 127, Volume2 = 127, ExtFader, Pan, ExtPan, PitchBend, ExtPitch;
            public int Attack = 0xFF, Decay = 0xFF, Sustain = 0xFF, Release = 0xFF;
            public int PanRange = 127, BendRange = 2, PortaKey = 60, PortaTime, SweepPitch, Transpose;
            public int ChannelMask = 0xFFFF;
            public Lfo Mod = Lfo.Default;
            public int Wait;
            public readonly List<Channel> Channels = new List<Channel>();
        }

        private readonly Settings _s;
        private readonly byte[] _seq;
        private readonly int _base;
        private readonly List<SbnkInstrument> _instruments;
        private readonly Func<int, List<SwavSample>> _wavesForSlot;

        private readonly Channel[] _channels = new Channel[16];
        private readonly Track[] _tracks = new Track[TrackCount];
        private bool _playerActive;
        private int _tempo = 120, _tempoRatio = 256, _tempoCounter = BaseTempo;
        private int _playerVolume = 127, _playerExtFader, _playerPrio;
        private readonly short[] _vars = new short[32];
        private uint _random = 0x12345678;
        private int _update, _ticks;
        private bool _progress;

        public readonly List<NoteEvent> Notes = new List<NoteEvent>();
        public readonly List<int> TickUpdates = new List<int>();
        public int LastTempo => _tempo;

        public NitroSoundDriver(Settings settings, byte[] sseq, List<SbnkInstrument> instruments,
            Func<int, List<SwavSample>> wavesForSlot, int infoVolume, int channelPrio, int playerChannelMask)
        {
            _s = settings;
            _seq = sseq;
            _instruments = instruments;
            _wavesForSlot = wavesForSlot;
            for (int i = 0; i < _channels.Length; i++) _channels[i] = new Channel { No = i };
            for (int i = 0; i < _vars.Length; i++) _vars[i] = -1;

            int baseOffset = sseq.Length >= 0x1C ? sseq[0x18] | (sseq[0x19] << 8) | (sseq[0x1A] << 16) | (sseq[0x1B] << 24) : 0x1C;
            _base = baseOffset > 0 && baseOffset < sseq.Length ? baseOffset : 0x1C;

            int volume = settings.InitialVolume >= 0 ? settings.InitialVolume : infoVolume;
            _playerExtFader = Math.Max(-32768, NitroSoundTables.CalcDecibel(volume));
            _playerPrio = channelPrio & 0xFF;

            Prepare(playerChannelMask);
        }

        /// <summary>Opens track 0 at the start of the data and allocates the tracks its first command asks for.</summary>
        private void Prepare(int playerChannelMask)
        {
            var t0 = NewTrack(0);
            t0.Opened = true;
            t0.Cur = _base;
            _tracks[0] = t0;
            if (_base < _seq.Length && _seq[_base] == 0xFE && _base + 2 < _seq.Length)
            {
                int mask = _seq[_base + 1] | (_seq[_base + 2] << 8);
                t0.Cur = _base + 3;
                mask >>= 1;
                for (int n = 1; mask != 0 && n < TrackCount; n++, mask >>= 1)
                    if ((mask & 1) != 0) _tracks[n] = NewTrack(n);
            }
            if (playerChannelMask != 0)
                foreach (var t in _tracks)
                    if (t != null) { t.ChannelMask = playerChannelMask & 0xFFFF; t.ChannelMaskFlag = true; }
            _playerActive = true;
        }

        // ── the run ─────────────────────────────────────────────────────────────────────────────────────

        /// <summary>Runs to the end of the sequence and its last release, or to the time limit.</summary>
        public short[] Run()
        {
            int rate = _s.SampleRate;
            long maxUpdates = (long)Math.Ceiling(_s.MaxSeconds / NitroSoundTables.UpdateSeconds);
            int samplesPerUpdate = (int)Math.Ceiling(rate * NitroSoundTables.UpdateSeconds) + 1;
            var mixL = new float[samplesPerUpdate];
            var mixR = new float[samplesPerUpdate];
            int maxSamples = (int)Math.Min(int.MaxValue / 2 - 1, (long)(_s.MaxSeconds * rate));

            short[] pcm = _s.Mix ? new short[Math.Min(maxSamples, rate * 2) * 2] : null;
            int produced = 0;
            int idleUpdates = 0;
            int idleLimit = (int)(600.0 / NitroSoundTables.UpdateSeconds);
            bool cut = false;

            for (_update = 0; _update < maxUpdates; _update++)
            {
                UpdateExChannel();
                if (_playerActive && _s.StopSeconds > 0 && _update * NitroSoundTables.UpdateSeconds >= _s.StopSeconds) FinishPlayer();
                if (_playerActive) { _progress = false; PlayerTempoMain(); UpdatePlayerChannel(); }
                ExChannelMain();
                CalcRandom();

                int target = (int)Math.Min(maxSamples, (long)((_update + 1) * NitroSoundTables.UpdateSeconds * rate));
                int n = target - produced;
                if (n > 0)
                {
                    if (_s.Mix)
                    {
                        Array.Clear(mixL, 0, n);
                        Array.Clear(mixR, 0, n);
                        foreach (var c in _channels) if (c.HwActive) MixChannel(c, mixL, mixR, n, rate);
                        if ((produced + n) * 2 > pcm.Length)
                            Array.Resize(ref pcm, (int)Math.Min((long)maxSamples * 2, Math.Max((long)pcm.Length * 2, (produced + n) * 2L)));
                        for (int i = 0; i < n; i++)
                        {
                            pcm[(produced + i) * 2] = Clip(mixL[i]);
                            pcm[(produced + i) * 2 + 1] = Clip(mixR[i]);
                        }
                    }
                    else
                    {
                        foreach (var c in _channels) if (c.HwActive) Advance(c, n, rate);
                    }
                    produced += n;
                }
                if (produced >= maxSamples) { cut = true; break; }

                if (!_playerActive && !AnythingSounding()) break;
                if (!_s.Mix)
                {
                    if (!_playerActive) break;
                    idleUpdates = _progress ? 0 : idleUpdates + 1;
                    if (idleUpdates > idleLimit) break;
                }
            }

            if (!_s.Mix) return null;
            if (cut && AnythingSounding()) FadeTail(pcm, produced, rate);
            Array.Resize(ref pcm, Math.Max(1, produced) * 2);
            return pcm;
        }

        private static short Clip(float v) => v >= 32767f ? (short)32767 : v <= -32768f ? (short)-32768 : (short)v;

        /// <summary>A tune cut off by the time limit ends on a short fade rather than a click.</summary>
        private static void FadeTail(short[] pcm, int produced, int rate)
        {
            int n = Math.Min(produced, rate / 100);
            for (int i = 0; i < n; i++)
            {
                double g = i / (double)n;
                int at = (produced - 1 - i) * 2;
                pcm[at] = (short)(pcm[at] * g);
                pcm[at + 1] = (short)(pcm[at + 1] * g);
            }
        }

        private bool AnythingSounding()
        {
            foreach (var c in _channels) if (c.Active || c.HwActive || c.Sync != 0) return true;
            return false;
        }

        private int CalcRandom()
        {
            _random = unchecked(_random * 1664525 + 1013904223);
            return (int)(_random >> 16);
        }

        // ── hardware channels ───────────────────────────────────────────────────────────────────────────

        private void UpdateExChannel()
        {
            foreach (var c in _channels)
            {
                if (c.Sync == 0) continue;
                if ((c.Sync & SyncStop) != 0) c.HwActive = false;
                if ((c.Sync & SyncStart) != 0)
                {
                    c.HwKind = c.Kind;
                    c.HwWave = c.Wave;
                    c.HwDuty = c.Duty;
                    c.HwVolume = c.Volume & 0xFF; c.HwShift = (c.Volume >> 8) & 3;
                    c.HwTimer = c.Timer; c.HwPan = c.Pan;
                    // The hardware counts a few samples of silence before the first real one.
                    c.HwPos = c.Kind == Kind.Pcm ? -3 : -1;
                    c.Lfsr = 0x7FFF; c.NoiseOut = 0;
                }
                else
                {
                    if ((c.Sync & SyncTimer) != 0) c.HwTimer = c.Timer;
                    if ((c.Sync & SyncVolume) != 0) { c.HwVolume = c.Volume & 0xFF; c.HwShift = (c.Volume >> 8) & 3; }
                    if ((c.Sync & SyncPan) != 0) c.HwPan = c.Pan;
                }
            }
            foreach (var c in _channels)
            {
                if (c.Sync == 0) continue;
                if ((c.Sync & SyncStart) != 0)
                    c.HwActive = c.HwKind != Kind.Pcm || (c.HwWave?.Pcm != null && c.HwWave.Pcm.Length > 0);
                c.Sync = 0;
            }
        }

        private void MixChannel(Channel c, float[] mixL, float[] mixR, int n, int rate)
        {
            int vol = c.HwVolume == 127 ? 128 : c.HwVolume;
            int pan = c.HwPan == 127 ? 128 : c.HwPan;
            double g = vol / 128.0 / ShiftDiv[c.HwShift];
            float gl = (float)(g * (128 - pan) / 128.0), gr = (float)(g * pan / 128.0);
            double step = (double)NitroSoundTables.TimerClock / Math.Max(1, c.HwTimer) / rate;
            double pos = c.HwPos;

            switch (c.HwKind)
            {
                case Kind.Pcm:
                {
                    var w = c.HwWave;
                    short[] d = w.Pcm;
                    int len = d.Length;
                    int loopStart = Math.Clamp(w.LoopStartSample, 0, len - 1);
                    int loopLen = len - loopStart;
                    bool loop = w.Loop && loopLen > 0;
                    bool lerp = _s.Interpolate;
                    for (int i = 0; i < n; i++)
                    {
                        if (pos >= len)
                        {
                            if (!loop) { c.HwActive = false; break; }
                            pos = loopStart + (pos - len) % loopLen;
                        }
                        if (pos >= 0)
                        {
                            int i0 = (int)pos;
                            float v = d[i0];
                            if (lerp)
                            {
                                int i1 = i0 + 1 < len ? i0 + 1 : loop ? loopStart : i0;
                                v += (d[i1] - v) * (float)(pos - i0);
                            }
                            mixL[i] += v * gl;
                            mixR[i] += v * gr;
                        }
                        pos += step;
                    }
                    break;
                }
                case Kind.Psg:
                {
                    int duty = c.HwDuty & 7;
                    for (int i = 0; i < n; i++)
                    {
                        if (pos >= 0)
                        {
                            int idx = (int)pos;
                            float v = duty == 7 || idx < 7 - duty ? -32767f : 32767f;
                            mixL[i] += v * gl;
                            mixR[i] += v * gr;
                        }
                        pos += step;
                        if (pos >= 8) pos %= 8;
                    }
                    break;
                }
                default:
                {
                    for (int i = 0; i < n; i++)
                    {
                        mixL[i] += c.NoiseOut * gl;
                        mixR[i] += c.NoiseOut * gr;
                        pos += step;
                        while (pos >= 1)
                        {
                            pos -= 1;
                            if ((c.Lfsr & 1) != 0) { c.Lfsr = (c.Lfsr >> 1) ^ 0x6000; c.NoiseOut = -32767; }
                            else { c.Lfsr >>= 1; c.NoiseOut = 32767; }
                        }
                    }
                    break;
                }
            }
            c.HwPos = pos;
        }

        /// <summary>Moves a channel on without making sound, so a one-shot still ends when it would have.</summary>
        private void Advance(Channel c, int n, int rate)
        {
            if (c.HwKind != Kind.Pcm) return;
            var w = c.HwWave;
            int len = w.Pcm.Length;
            double pos = c.HwPos + (double)NitroSoundTables.TimerClock / Math.Max(1, c.HwTimer) / rate * n;
            if (pos >= len)
            {
                int loopStart = Math.Clamp(w.LoopStartSample, 0, len - 1);
                if (!w.Loop || len - loopStart <= 0) c.HwActive = false;
                else pos = loopStart + (pos - len) % (len - loopStart);
            }
            c.HwPos = pos;
        }

        // ── driver channels ─────────────────────────────────────────────────────────────────────────────

        private void ExChannelMain()
        {
            foreach (var c in _channels)
            {
                if (!c.Active) continue;

                if (c.StartFlag)
                {
                    c.Sync |= SyncStart;
                    c.StartFlag = false;
                }
                else if (!c.HwActive)
                {
                    Finish(c);
                    continue;
                }

                int decay = NitroSoundTables.CalcDecibelSquare(c.Velocity);
                int pitch = (c.Key - c.OriginalKey) << NitroSoundTables.PitchDivisionBit;
                decay += UpdateEnvelope(c);
                pitch += SweepMain(c);
                decay += c.UserDecay + c.UserDecay2;
                pitch += c.UserPitch;

                int lfo = LfoMain(c);
                int pan = 0;
                switch (c.Lfo.Target)
                {
                    case 1: if (decay > -32768) decay += lfo; break;
                    case 0: pitch += lfo; break;
                    case 2: pan += lfo; break;
                }

                pan += c.InitPan;
                if (c.PanRange != 127) pan = (pan * c.PanRange + 64) >> 7;
                pan += c.UserPan;

                if (c.Env == Env.Release && decay <= NitroSoundTables.VolumeDbMin)
                {
                    c.Sync = SyncStop;
                    Finish(c);
                    continue;
                }

                NitroSoundTables.CalcChannelVolume(decay, out int vol, out int shift);
                int volume = (shift << 8) | vol;
                int timer = NitroSoundTables.CalcTimer(c.WaveTimer, pitch);
                if (c.Kind == Kind.Psg) timer &= 0xFFFC;
                pan = Math.Clamp(pan + 64, 0, 127);

                if (volume != c.Volume) { c.Volume = volume; c.Sync |= SyncVolume; }
                if (timer != c.Timer) { c.Timer = timer; c.Sync |= SyncTimer; }
                if (pan != c.Pan) { c.Pan = pan; c.Sync |= SyncPan; }
            }
        }

        private void Finish(Channel c)
        {
            if (c.Owner != null) { c.Owner.Channels.Remove(c); c.Owner = null; }
            c.Prio = PrioStop;
            c.Volume = 0;
            c.Active = false;
        }

        private static int UpdateEnvelope(Channel c)
        {
            switch (c.Env)
            {
                case Env.Attack:
                    c.EnvDecay = -((-c.EnvDecay * c.Attack) >> 8);
                    if (c.EnvDecay == 0) c.Env = Env.Decay;
                    break;
                case Env.Decay:
                {
                    int sustainDecay = NitroSoundTables.CalcDecibelSquare(c.Sustain) << 7;
                    c.EnvDecay -= c.Decay;
                    if (c.EnvDecay > sustainDecay) break;
                    c.EnvDecay = sustainDecay;
                    c.Env = Env.Sustain;
                    break;
                }
                case Env.Release:
                    c.EnvDecay -= c.Release;
                    break;
            }
            return c.EnvDecay >> 7;
        }

        private static int SweepMain(Channel c)
        {
            if (c.SweepPitch == 0 || c.SweepCounter >= c.SweepLength) return 0;
            long sweep = (long)c.SweepPitch * (c.SweepLength - c.SweepCounter) / c.SweepLength;
            if (c.AutoSweep) c.SweepCounter++;
            return (int)sweep;
        }

        private static int LfoMain(Channel c)
        {
            long value = 0;
            if (c.Lfo.Depth != 0 && c.LfoDelayCounter >= c.Lfo.Delay)
                value = NitroSoundTables.SinIdx(c.LfoCounter >> 8) * c.Lfo.Depth * c.Lfo.Range;
            if (value != 0)
            {
                switch (c.Lfo.Target)
                {
                    case 1: value *= 60; break;
                    case 0: value *= 1 << NitroSoundTables.PitchDivisionBit; break;
                    case 2: value *= 64; break;
                }
                value >>= 14;
            }

            if (c.LfoDelayCounter < c.Lfo.Delay)
            {
                c.LfoDelayCounter++;
            }
            else
            {
                int offset = (c.LfoCounter + (c.Lfo.Speed << 6)) >> 8;
                while (offset >= NitroSoundTables.SinPeriod) offset -= NitroSoundTables.SinPeriod;
                c.LfoCounter = (((c.LfoCounter + (c.Lfo.Speed << 6)) & 0xFF) | (offset << 8)) & 0xFFFF;
            }
            return (int)value;
        }

        private Channel AllocChannel(int mask, int prio, bool strong, Track owner)
        {
            Channel best = null;
            foreach (int no in ChannelOrder)
            {
                if ((mask & (1 << no)) == 0) continue;
                var c = _channels[no];
                if (best == null) { best = c; continue; }
                if (c.Prio > best.Prio) continue;
                if (c.Prio == best.Prio && CompareVolume(best, c) >= 0) continue;
                best = c;
            }
            if (best == null || prio < best.Prio) return null;

            if (best.Owner != null) best.Owner.Channels.Remove(best);
            best.Sync = SyncStop;
            best.Active = false;

            best.Owner = owner;
            best.Length = 0;
            best.Prio = prio & 0xFF;
            best.Volume = 127;
            best.StartFlag = false;
            best.AutoSweep = true;
            best.Key = 60; best.OriginalKey = 60; best.Velocity = 127; best.InitPan = 0;
            best.UserDecay = 0; best.UserDecay2 = 0; best.UserPitch = 0; best.UserPan = 0; best.PanRange = 127;
            best.SweepPitch = 0; best.SweepLength = 0; best.SweepCounter = 0;
            best.Attack = NitroSoundTables.AttackRate(127);
            best.Decay = NitroSoundTables.FallRate(127);
            best.Sustain = 127;
            best.Release = NitroSoundTables.FallRate(127);
            best.Lfo = Lfo.Default;
            return best;
        }

        private static int CompareVolume(Channel a, Channel b)
        {
            int[] shift = { 0, 1, 2, 4 };
            int va = ((a.Volume & 0xFF) << 4) >> shift[(a.Volume >> 8) & 3];
            int vb = ((b.Volume & 0xFF) << 4) >> shift[(b.Volume >> 8) & 3];
            return va != vb ? (va < vb ? 1 : -1) : 0;
        }

        // ── the sequence player ─────────────────────────────────────────────────────────────────────────

        private void PlayerTempoMain()
        {
            int ticks = 0;
            while (_tempoCounter >= BaseTempo) { _tempoCounter -= BaseTempo; ticks++; }

            for (int i = 0; i < ticks; i++)
            {
                if (!_s.Mix) TickUpdates.Add(_update);
                if (PlayerSeqMain())
                {
                    FinishPlayer();
                    break;
                }
                _ticks++;
            }

            _tempoCounter = (_tempoCounter + ((_tempo * _tempoRatio) >> 8)) & 0xFFFF;
        }

        private bool PlayerSeqMain()
        {
            bool active = false;
            for (int n = 0; n < TrackCount; n++)
            {
                var t = _tracks[n];
                if (t == null || !t.Opened) continue;
                if (TrackSeqMain(t) == 0) active = true;
                else ClosePlayerTrack(n);
            }
            return !active;
        }

        private void FinishPlayer()
        {
            for (int n = 0; n < TrackCount; n++) ClosePlayerTrack(n);
            _playerActive = false;
        }

        private void ClosePlayerTrack(int n)
        {
            var t = _tracks[n];
            if (t == null) return;
            CloseTrack(t);
            _tracks[n] = null;
        }

        private void CloseTrack(Track t)
        {
            ReleaseTrackChannelAll(t, -1);
            FreeTrackChannelAll(t);
        }

        private void ReleaseTrackChannelAll(Track t, int release)
        {
            UpdateTrackChannel(t, false);
            foreach (var c in t.Channels)
            {
                if (!c.Active) continue;
                if (release >= 0) c.Release = NitroSoundTables.FallRate(release);
                c.Prio = PrioRelease;
                c.Env = Env.Release;
            }
        }

        private static void FreeTrackChannelAll(Track t)
        {
            foreach (var c in t.Channels) c.Owner = null;
            t.Channels.Clear();
        }

        private void UpdatePlayerChannel()
        {
            foreach (var t in _tracks) if (t != null) UpdateTrackChannel(t, true);
        }

        /// <summary>Updates the track's sounding notes and keys off the ones whose length has run out.</summary>
        private void UpdateTrackChannel(Track t, bool doRelease)
        {
            int decay = NitroSoundTables.CalcDecibelSquare(t.Volume) + NitroSoundTables.CalcDecibelSquare(t.Volume2)
                      + NitroSoundTables.CalcDecibelSquare(_playerVolume);
            int decay2 = t.ExtFader + _playerExtFader;
            int pitch = ((t.PitchBend * (t.BendRange << NitroSoundTables.PitchDivisionBit)) >> 7) + t.ExtPitch;
            int pan = t.Pan;
            if (t.PanRange != 127) pan = (pan * t.PanRange + 64) >> 7;
            pan += t.ExtPan;

            decay = Math.Max(decay, -32768);
            decay2 = Math.Max(decay2, -32768);
            pan = Math.Clamp(pan, -128, 127);

            for (int i = 0; i < t.Channels.Count; i++)
            {
                var c = t.Channels[i];
                c.UserDecay2 = (short)decay2;
                if (c.Env == Env.Release) continue;
                c.UserDecay = (short)decay;
                c.UserPitch = (short)pitch;
                c.UserPan = pan;
                c.PanRange = t.PanRange;
                c.Lfo = t.Mod;
                if (c.Length == 0 && doRelease)
                {
                    c.Prio = PrioRelease;
                    c.Env = Env.Release;
                }
            }
        }

        private Track NewTrack(int n) => new Track { No = n, ExtPan = Math.Clamp(_s.TrackPan, -128, 127), ExtPitch = _s.TrackPitch };

        private int ReadByte(Track t)
        {
            if (t.Cur >= 0 && t.Cur < _seq.Length) return _seq[t.Cur++];
            t.Cur++;
            return 0xFF;
        }

        private int Read16(Track t) { int a = ReadByte(t); return a | (ReadByte(t) << 8); }

        private int Read24(Track t) { int a = ReadByte(t); a |= ReadByte(t) << 8; return a | (ReadByte(t) << 16); }

        private int ReadVar(Track t)
        {
            int value = 0;
            for (int i = 0; i < 4; i++)
            {
                int b = ReadByte(t);
                value = (value << 7) | (b & 0x7F);
                if ((b & 0x80) == 0) break;
            }
            return value;
        }

        private int ReadArg(Track t, Arg type)
        {
            switch (type)
            {
                case Arg.U8: return ReadByte(t);
                case Arg.S16: return Read16(t);
                case Arg.VarLen: return ReadVar(t);
                case Arg.Variable:
                {
                    int no = ReadByte(t);
                    return no < _vars.Length ? _vars[no] : 0;
                }
                default:
                {
                    int min = (short)Read16(t), max = (short)Read16(t);
                    int r = unchecked(CalcRandom() * (max - min + 1));
                    return (r >> 16) + min;
                }
            }
        }

        /// <summary>One sequence tick of one track. Returns -1 when the track has ended.</summary>
        private int TrackSeqMain(Track t)
        {
            foreach (var c in t.Channels)
            {
                if (c.Length > 0) c.Length--;
                if (!c.AutoSweep && c.SweepCounter < c.SweepLength) c.SweepCounter++;
            }

            if (t.NoteFinishWait)
            {
                if (t.Channels.Count > 0) return 0;
                t.NoteFinishWait = false;
            }
            if (t.Wait > 0)
            {
                t.Wait--;
                if (t.Wait > 0) return 0;
            }

            int guard = 0;
            while (t.Wait == 0 && !t.NoteFinishWait)
            {
                // A loop that never waits would lock the console up; here it just ends the track.
                if (t.Cur < 0 || t.Cur >= _seq.Length || ++guard > 65536) return -1;
                _progress = true;

                bool exec = true;
                Arg? argType = null;
                int cmd = ReadByte(t);
                if (cmd == 0xA2) { cmd = ReadByte(t); exec = t.CmpFlag; }
                if (cmd == 0xA0) { cmd = ReadByte(t); argType = Arg.Random; }
                if (cmd == 0xA1) { cmd = ReadByte(t); argType = Arg.Variable; }

                if ((cmd & 0x80) == 0)
                {
                    int velocity = ReadByte(t);
                    int length = ReadArg(t, argType ?? Arg.VarLen);
                    int key = cmd + t.Transpose;
                    if (!exec) continue;
                    key = Math.Clamp(key, 0, 127);

                    if (!t.Mute)
                    {
                        if (!_s.Mix)
                            Notes.Add(new NoteEvent
                            {
                                Update = _update, Tick = _ticks, Track = t.No, Key = key, Velocity = velocity,
                                Length = length, Program = t.Prg, Pan = t.Pan + 64, Volume = t.Volume,
                            });
                        NoteOnCommandProc(t, key, velocity, length > 0 ? length : -1);
                    }
                    t.PortaKey = key;
                    if (t.NoteWait)
                    {
                        t.Wait = length;
                        if (length == 0) t.NoteFinishWait = true;
                    }
                    continue;
                }

                switch (cmd & 0xF0)
                {
                    case 0x80:
                    {
                        int arg = ReadArg(t, argType ?? Arg.VarLen);
                        if (!exec) break;
                        if (cmd == 0x80) t.Wait = arg;
                        else if (cmd == 0x81 && arg < 0x10000) t.Prg = arg & 0xFFFF;
                        break;
                    }

                    case 0x90:
                        switch (cmd)
                        {
                            case 0x93:
                            {
                                int no = ReadByte(t);
                                int offset = Read24(t);
                                if (!exec) break;
                                var other = no < TrackCount ? _tracks[no] : null;
                                if (other == null || other == t) break;
                                CloseTrack(other);
                                other.Cur = _base + offset;
                                other.Opened = true;
                                break;
                            }
                            case 0x94:
                            {
                                int offset = Read24(t);
                                if (!exec) break;
                                if (!_s.FollowLoops && _base + offset <= t.Cur) return -1;
                                t.Cur = _base + offset;
                                break;
                            }
                            case 0x95:
                            {
                                int offset = Read24(t);
                                if (!exec || t.CallDepth >= CallDepthMax) break;
                                t.CallStack[t.CallDepth++] = t.Cur;
                                t.Cur = _base + offset;
                                break;
                            }
                        }
                        break;

                    case 0xC0:
                    case 0xD0:
                    {
                        int arg = ReadArg(t, argType ?? Arg.U8) & 0xFF;
                        if (!exec) break;
                        switch (cmd)
                        {
                            case 0xC0: t.Pan = (sbyte)(arg - 64); break;
                            case 0xC1: t.Volume = arg; break;
                            case 0xC2: _playerVolume = arg; break;
                            case 0xC3: t.Transpose = (sbyte)arg; break;
                            case 0xC4: t.PitchBend = (sbyte)arg; break;
                            case 0xC5: t.BendRange = arg; break;
                            case 0xC6: t.Prio = arg; break;
                            // The track's flags are single bits, so only the low bit of the argument counts.
                            case 0xC7: t.NoteWait = (arg & 1) != 0; break;
                            case 0xC8:
                                t.Tie = (arg & 1) != 0;
                                ReleaseTrackChannelAll(t, -1);
                                FreeTrackChannelAll(t);
                                break;
                            case 0xC9: t.PortaKey = (arg + t.Transpose) & 0xFF; t.Porta = true; break;
                            case 0xCA: t.Mod.Depth = arg; break;
                            case 0xCB: t.Mod.Speed = arg; break;
                            case 0xCC: t.Mod.Target = arg; break;
                            case 0xCD: t.Mod.Range = arg; break;
                            case 0xCE: t.Porta = (arg & 1) != 0; break;
                            case 0xCF: t.PortaTime = arg; break;
                            case 0xD0: t.Attack = arg; break;
                            case 0xD1: t.Decay = arg; break;
                            case 0xD2: t.Sustain = arg; break;
                            case 0xD3: t.Release = arg; break;
                            case 0xD4:
                                if (t.CallDepth >= CallDepthMax) break;
                                t.CallStack[t.CallDepth] = t.Cur;
                                t.LoopCount[t.CallDepth] = arg;
                                t.CallDepth++;
                                break;
                            case 0xD5: t.Volume2 = arg; break;
                            case 0xD7: SetTrackMute(t, arg); break;
                        }
                        break;
                    }

                    case 0xE0:
                    {
                        int arg = (short)ReadArg(t, argType ?? Arg.S16);
                        if (!exec) break;
                        switch (cmd)
                        {
                            case 0xE0: t.Mod.Delay = arg & 0xFFFF; break;
                            case 0xE1: _tempo = arg & 0xFFFF; break;
                            case 0xE3: t.SweepPitch = arg; break;
                        }
                        break;
                    }

                    case 0xB0:
                    {
                        int no = ReadByte(t);
                        int arg = (short)ReadArg(t, argType ?? Arg.S16);
                        if (!exec || no >= _vars.Length) break;
                        int v = _vars[no];
                        switch (cmd)
                        {
                            case 0xB0: v = arg; break;
                            case 0xB1: v += arg; break;
                            case 0xB2: v -= arg; break;
                            case 0xB3: v *= arg; break;
                            case 0xB4: if (arg != 0) v /= arg; break;
                            case 0xB5: v = arg >= 0 ? (arg < 32 ? v << arg : 0) : (v >> Math.Min(31, -arg)); break;
                            case 0xB6:
                            {
                                bool negative = arg < 0;
                                if (negative) arg = (short)-arg;
                                int r = (CalcRandom() * (arg + 1)) >> 16;
                                v = negative ? -r : r;
                                break;
                            }
                            case 0xB8: t.CmpFlag = v == arg; break;
                            case 0xB9: t.CmpFlag = v >= arg; break;
                            case 0xBA: t.CmpFlag = v > arg; break;
                            case 0xBB: t.CmpFlag = v <= arg; break;
                            case 0xBC: t.CmpFlag = v < arg; break;
                            case 0xBD: t.CmpFlag = v != arg; break;
                        }
                        _vars[no] = (short)v;
                        break;
                    }

                    case 0xF0:
                        if (!exec) break;
                        switch (cmd)
                        {
                            case 0xFD:
                                if (t.CallDepth == 0) break;
                                t.Cur = t.CallStack[--t.CallDepth];
                                break;
                            case 0xFC:
                            {
                                if (t.CallDepth == 0) break;
                                int count = t.LoopCount[t.CallDepth - 1];
                                if (count > 0)
                                {
                                    count--;
                                    if (count == 0) { t.CallDepth--; break; }
                                }
                                else if (!_s.FollowLoops) return -1;
                                t.LoopCount[t.CallDepth - 1] = count;
                                t.Cur = t.CallStack[t.CallDepth - 1];
                                break;
                            }
                            case 0xFF:
                                return -1;
                        }
                        break;
                }
            }
            return 0;
        }

        private void SetTrackMute(Track t, int mute)
        {
            switch (mute)
            {
                case 0: t.Mute = false; break;
                case 1: t.Mute = true; break;
                case 2: t.Mute = true; ReleaseTrackChannelAll(t, -1); break;
                case 3: t.Mute = true; ReleaseTrackChannelAll(t, 127); FreeTrackChannelAll(t); break;
            }
        }

        private void NoteOnCommandProc(Track t, int key, int velocity, int length)
        {
            Channel c = null;
            if (t.Tie && t.Channels.Count > 0)
            {
                c = t.Channels[0];
                c.Key = key;
                c.Velocity = velocity;
            }

            if (c == null)
            {
                var region = ResolveRegion(t.Prg, key);
                if (region == null) return;
                int mask = region.Psg == PsgKind.Square ? PsgMask : region.Psg == PsgKind.Noise ? NoiseMask : PcmMask;
                c = AllocChannel(mask & t.ChannelMask, _playerPrio + t.Prio, t.ChannelMaskFlag, t);
                if (c == null) return;
                if (!NoteOn(c, key, velocity, t.Tie ? -1 : length, region))
                {
                    c.Prio = PrioStop;
                    c.Owner = null;
                    return;
                }
                t.Channels.Insert(0, c);
            }

            if (t.Attack != 0xFF) c.Attack = NitroSoundTables.AttackRate(t.Attack);
            if (t.Decay != 0xFF) c.Decay = NitroSoundTables.FallRate(t.Decay);
            if (t.Sustain != 0xFF) c.Sustain = t.Sustain;
            if (t.Release != 0xFF) c.Release = NitroSoundTables.FallRate(t.Release);

            c.SweepPitch = t.SweepPitch;
            if (t.Porta) c.SweepPitch = (short)(c.SweepPitch + (short)((t.PortaKey - key) << NitroSoundTables.PitchDivisionBit));
            if (t.PortaTime == 0)
            {
                // Without a portamento time the sweep runs over the note's written length, counted in ticks.
                c.SweepLength = length;
                c.AutoSweep = false;
            }
            else
            {
                // With one, it runs for a time set by the distance, counted in driver updates.
                c.SweepLength = (t.PortaTime * t.PortaTime * Math.Abs(c.SweepPitch)) >> 11;
            }
            c.SweepCounter = 0;
        }

        private SbnkRegion ResolveRegion(int prg, int key)
        {
            if (prg < 0 || prg >= _instruments.Count) return null;
            var region = _instruments[prg]?.Resolve(key);
            return region == null || region.Silent ? null : region;
        }

        private bool NoteOn(Channel c, int key, int velocity, int length, SbnkRegion region)
        {
            int release = region.Release;
            if (release == SbnkRegion.ReleaseDisabled) { length = -1; release = 0; }

            switch (region.Psg)
            {
                case PsgKind.None:
                {
                    var waves = _wavesForSlot(region.WaveArcSlot);
                    if (waves == null || region.WaveIndex < 0 || region.WaveIndex >= waves.Count) return false;
                    var wave = waves[region.WaveIndex];
                    if (wave?.Pcm == null) return false;
                    c.Kind = Kind.Pcm;
                    c.Wave = wave;
                    c.WaveTimer = wave.Timer > 0 ? wave.Timer : NitroSoundTables.TimerClock / Math.Max(1, wave.SampleRate);
                    break;
                }
                case PsgKind.Square:
                    if (c.No < 8 || c.No > 13) return false;
                    c.Kind = Kind.Psg;
                    c.Duty = region.PsgDuty;
                    c.WaveTimer = PsgTimerC4;
                    break;
                default:
                    if (c.No < 14) return false;
                    c.Kind = Kind.Noise;
                    c.WaveTimer = PsgTimerC4;
                    break;
            }

            c.EnvDecay = EnvInit;
            c.Env = Env.Attack;
            c.Length = length;
            c.LfoCounter = 0;
            c.LfoDelayCounter = 0;
            c.StartFlag = true;
            c.Active = true;

            c.Key = key;
            c.OriginalKey = region.BaseNote & 0xFF;
            c.Velocity = velocity & 0xFF;
            c.Attack = NitroSoundTables.AttackRate(region.Attack);
            c.Decay = NitroSoundTables.FallRate(region.Decay);
            c.Sustain = Math.Clamp(region.Sustain, 0, 127);
            c.Release = NitroSoundTables.FallRate(release);
            c.InitPan = (sbyte)(region.Pan - 64);
            return true;
        }
    }
}
