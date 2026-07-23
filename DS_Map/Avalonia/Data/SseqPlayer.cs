using System;
using System.Collections.Generic;

namespace DSPRE.Avalonia.Data
{
    /// <summary>
    /// Renders one SSEQ sequence (a MIDI-like note/control event stream, the public Nitro sound format used to
    /// drive both music and, for short simple sequences, sound effects) to a flat stereo 16-bit PCM buffer for
    /// preview playback. Not a real-time player: since sound effects are short, the whole sequence is simulated
    /// up front and mixed down once, which sidesteps needing a live audio-thread scheduler entirely.
    ///
    /// Renders note timing (variable-length duration/tick format, notewait-mode-gated track clock), program
    /// change → instrument → wave lookup, pitch (note + transpose + real pitch bend), pan, tempo, simple track
    /// subroutines (call/return) and one bounded loop-back, the real per-instrument attack/decay/sustain/release
    /// envelope (SBNK's own bytes, run through the exact real per-tick curve — see NitroEnvelope.AttackGain
    /// for the exponential attack, linear-in-dB decay/release), and velocity/volume/expression/master-volume
    /// gain through the verified squared-dB attenuation curve.
    ///
    /// Also models pitch vibrato (modulation type 0 — 0xCA depth/0xCB speed/0xCD range/0xE0 delay, the
    /// dominant real usage: 180 of 183 real modType settings are type 0) and Sweep Pitch (0xE3, 251 real
    /// occurrences) — both per the REAL Nintendo ARM7 sound driver source, found in the leak at
    /// `PlatPC_src/main/sdk/NitroSDK/build/libraries/snd/{ARM7,common}/src/` (`snd_exchannel.c`'s
    /// `LfoMain`/`SND_GetLfoValue`/`SweepMain`, `snd_util.c`'s `SND_SinIdx`/sine table) — genuinely
    /// Nintendo's own mixer code, not a third-party reimplementation. This directly superseded an earlier,
    /// incorrect attempt sourced from a well-regarded but NOT byte-exact community reimplementation
    /// (kode54/fincs's SSEQPlayer): that project's own sine table and envelope tables matched perfectly
    /// (independently cross-validating both), but its pitch-vibrato scale constant was wrong (used 60,
    /// where the real source's `SND_PITCH_DIVISION_BIT`-derived value is 64), and its claim that Sweep
    /// Pitch requires portamento to have any effect is contradicted by the real source (`sweep_length` is
    /// set from the note's own duration regardless of portamento state — sweep pitch is a real, always-
    /// active per-note pitch ramp toward zero, not dead bytes). Both fixed here against the real formulas.
    ///
    /// One real, disclosed gap remains, quantified against the actual HGSS SEQ_SE_* corpus (1006 sequences):
    /// volume/pan modulation (modType 1/2, only 3 real occurrences, all type 1) is NOT modelled. Portamento
    /// itself (0xC9/0xCE/0xCF) has ZERO occurrences across the whole corpus — confirmed unused by sound
    /// effects, not skipped out of laziness — so its cross-note pitch-glide behavior (distinct from the
    /// always-active Sweep Pitch above) doesn't need modelling for this corpus either.
    /// </summary>
    public static class SseqPlayer
    {
        private const int Ppqn = 48;   // ticks per quarter note

        private sealed class Voice
        {
            public double StartSeconds;
            public double DurationSeconds;
            public int Note;
            public int Velocity;
            public int Program;
            public int Pan;       // 0..127, 64 = centre
            public int Volume;    // 0..127
            public int Expression;    // 0..127, 127 = no attenuation
            public int MasterVolume;  // 0..127, 127 = no attenuation
            public double PitchBendSemitones;
            public int ModType, ModDepth, ModRange, ModSpeed, ModDelayTicks;
            public int SweepPitchRaw;
        }

        private sealed class Track
        {
            public byte[] Data;
            public int Pos;
            public bool Done;
            public int Program;
            public int Pan = 64;
            // Verified against the real Nintendo source (snd_seq.c's InitTrack): SND_TRACK_DEFAULT_VOLUME is
            // 127 for BOTH volume (0xC1) and volume2/expression (0xD5) — a third-party reimplementation this
            // renderer briefly relied on before finding the real source used 64, which turned out wrong.
            public int Volume = 127;
            public int Expression = 127;
            public int MasterVolume = 127;
            public int Transpose;
            // Pitch vibrato defaults, verified against the real SND_InitLfoParam (snd_exchannel.c): depth 0
            // (off), range 1, speed 16, delay 0 ticks, type 0 (pitch). (A third-party reimplementation this
            // renderer briefly relied on had delay defaulting to 10 — wrong; the real default is 0.)
            public int ModType, ModDepth, ModRange = 1, ModSpeed = 16, ModDelayTicks;
            // Sweep Pitch (0xE3): raw signed value, same "1/64 semitone" units as pitch bend/transpose.
            public int SweepPitchRaw;
            // Raw signed pitch-bend byte (-128..127) and the range it's scaled by (semitones). Real corpus
            // usage confirmed (14353 pitch-bend + 981 range events across 1006 real SEQ_SE_* sequences — this
            // is not a rare BGM-only feature, it's used constantly in sound effects too). Default range of 2
            // semitones matches General MIDI's conventional default pitch-bend sensitivity; not independently
            // leak-verified as the NDS engine's own default, but nearly every real track sets it explicitly
            // before bending (981 range-sets across ~1409 tracks), so the default rarely if ever matters.
            public int PitchBend;
            public int PitchBendRange = 2;
            // NOTEWAIT MODE (0xC7): per the real, independently-verified default (VGMTrans's NDSTrack::resetVars
            // sets noteWithDelta = false) a note-on event's own duration does NOT advance the track's clock —
            // the NEXT event fires immediately, and only an explicit Rest (0x80) or notewait-mode-on changes
            // that. Checked directly against the real ROM corpus before trusting this: 1408 of 1409 real tracks
            // (99.9%) explicitly turn this on near the start and never turn it back off, so unconditionally
            // advancing time (this renderer's old behaviour) was accidentally correct for virtually every real
            // SE — but the correct default is still off, for the one track that doesn't set it and for
            // whatever this renders that isn't in this specific corpus.
            public bool NoteWait;
            public double TimeSeconds;
            public readonly Stack<int> CallStack = new Stack<int>();
        }

        /// <summary>Renders sequence <paramref name="seqIndex"/> from <paramref name="sdat"/> to interleaved
        /// stereo 16-bit PCM at <paramref name="sampleRate"/>, or null if the sequence/bank can't be resolved.</summary>
        public static short[] Render(SdatArchive sdat, int seqIndex, int sampleRate = 32000, double maxSeconds = 8.0)
        {
            if (sdat == null || seqIndex < 0 || seqIndex >= sdat.Sequences.Count) return null;
            var seq = sdat.Sequences[seqIndex];
            if (seq == null) return null;
            var seqBytes = sdat.GetFileBytes(seq.FileId);
            if (seqBytes == null || seqBytes.Length < 0x1C) return null;
            if (seq.BankNo < 0 || seq.BankNo >= sdat.Banks.Count || sdat.Banks[seq.BankNo] == null) return null;
            var bank = sdat.Banks[seq.BankNo];
            var instruments = sdat.GetBankInstruments(seq.BankNo);
            if (instruments == null) return null;

            List<SwavSample> WavesForSlot(int slot)
            {
                if (slot < 0 || slot >= 4) return null;
                return sdat.GetWaveArchive(bank.WaveArcNo[slot]);
            }

            var tracks = ParseTrackList(seqBytes);
            double bpm = 120.0;
            var voices = new List<Voice>();

            foreach (var t in tracks)
                RunTrack(t, seqBytes, ref bpm, voices, maxSeconds);

            // Most SEs run a fraction of a second; a hardcoded 8-second buffer allocates ~2MB (a Large Object
            // Heap allocation — confirmed via GC.GetAllocatedBytesForCurrentThread on real ROM sounds, ~6x more
            // than sequences actually need) on every single preview, and LOH churn from repeated calls provokes
            // gen1/gen2 (stop-the-world) collections that stall every thread in the process, including whatever
            // else is mid-playback — audibly "choppy"/"staticky" glitches with no connection to the sound data
            // itself. Size the buffer to what this sequence actually renders instead of the worst-case ceiling.
            // The +1.1s margin covers the mixdown's own release tail (capped at 1s past a note's programmed
            // duration, see Mix) plus a small cushion — still far short of the old flat 8s ceiling.
            double neededSeconds = 0;
            foreach (var v in voices) neededSeconds = Math.Max(neededSeconds, v.StartSeconds + v.DurationSeconds);
            double bufferSeconds = Math.Min(maxSeconds, neededSeconds + 1.1);

            return Mix(voices, instruments, WavesForSlot, sampleRate, bufferSeconds);
        }

        /// <summary>Writes interleaved stereo 16-bit PCM to a standard .wav file, so rendered audio can be
        /// A/B'd in a real media player (independent of this app's own NAudio playback path).</summary>
        public static void WriteWav(string path, short[] interleavedStereoPcm, int sampleRate)
        {
            int dataBytes = interleavedStereoPcm.Length * 2;
            using var fs = new System.IO.FileStream(path, System.IO.FileMode.Create);
            using var w = new System.IO.BinaryWriter(fs);
            void Str(string s) => w.Write(System.Text.Encoding.ASCII.GetBytes(s));
            Str("RIFF"); w.Write(36 + dataBytes); Str("WAVE");
            Str("fmt "); w.Write(16); w.Write((short)1); w.Write((short)2);
            w.Write(sampleRate); w.Write(sampleRate * 2 * 2); w.Write((short)4); w.Write((short)16);
            Str("data"); w.Write(dataBytes);
            foreach (var s in interleavedStereoPcm) w.Write(s);
        }

        // Track pointer discovery: the byte right after the file/block header (offset 0x1C) is either a normal
        // event (single-track sequence — track 0 starts right there) or 0xFE marking that a run of 0x93 "Open
        // Track" pointer records follows (each: opcode, track index, 3-byte little-endian offset from 0x1C).
        // Track 0's own bytecode begins wherever that pointer run ends.
        private static List<Track> ParseTrackList(byte[] d)
        {
            var list = new List<Track>();
            int off = 0x1C;
            if (off >= d.Length) return list;
            if (d[off] == 0xFE)
            {
                int p = off + 3;   // 0xFE + 2 bytes of "valid track" flags
                while (p + 5 <= d.Length && d[p] == 0x93)
                {
                    int trkOff = 0x1C + (d[p + 2] | (d[p + 3] << 8) | (d[p + 4] << 16));
                    list.Add(new Track { Data = d, Pos = trkOff });
                    p += 5;
                }
                off = p;
            }
            list.Insert(0, new Track { Data = d, Pos = off });
            return list;
        }

        private static void RunTrack(Track t, byte[] d, ref double bpmRef, List<Voice> voices, double maxSeconds)
        {
            double bpm = bpmRef;
            int guard = 0;
            while (!t.Done && t.Pos < d.Length && t.TimeSeconds < maxSeconds && guard++ < 200_000)
            {
                int op = d[t.Pos++];
                if (op < 0x80)
                {
                    int velocity = t.Pos < d.Length ? d[t.Pos++] : 0;
                    int durTicks = ReadVarLen(d, ref t.Pos);
                    double durSec = TicksToSeconds(durTicks, bpm);
                    voices.Add(new Voice
                    {
                        StartSeconds = t.TimeSeconds,
                        DurationSeconds = durSec,
                        Note = Math.Clamp(op + t.Transpose, 0, 127),
                        Velocity = velocity,
                        Program = t.Program,
                        Pan = t.Pan,
                        Volume = t.Volume,
                        Expression = t.Expression,
                        MasterVolume = t.MasterVolume,
                        PitchBendSemitones = t.PitchBend * t.PitchBendRange / 128.0,
                        ModType = t.ModType, ModDepth = t.ModDepth, ModRange = t.ModRange,
                        ModSpeed = t.ModSpeed, ModDelayTicks = t.ModDelayTicks,
                        SweepPitchRaw = t.SweepPitchRaw,
                    });
                    // Only Notewait Mode (0xC7) makes a note's own duration also serve as the delay before the
                    // next event — the default (and this is what actually differs per-track) is that notes at
                    // the SAME track-time layer as a chord, and only an explicit Rest (0x80) advances the clock.
                    if (t.NoteWait) t.TimeSeconds += durSec;
                    continue;
                }

                switch (op)
                {
                    case 0x80: t.TimeSeconds += TicksToSeconds(ReadVarLen(d, ref t.Pos), bpm); break;
                    case 0x81: t.Program = ReadVarLen(d, ref t.Pos); break;
                    case 0x93: t.Pos += 4; break;                       // Open Track (mid-stream, rare — skip its args)
                    case 0x94:                                          // Jump
                    {
                        int target = 0x1C + Read24(d, t.Pos); t.Pos += 3;
                        // A forward jump is normal control flow (e.g. skipping a section) and is followed as
                        // usual. A backward jump is the sequence's own loop mechanism (normal for BGM, which
                        // loops forever until something explicitly stops the channel) — a one-shot sound
                        // effect has no such "stop now" signal available to a static preview render, and
                        // repeating the loop body played back as an audible doubled/"echoed" copy of content
                        // that's only meant to sustain for as long as the real game keeps the channel open.
                        // Playing it through once (not looping) is the cleaner, more accurate preview.
                        if (target > t.Pos) t.Pos = target; else t.Done = true;
                        break;
                    }
                    case 0x95:                                          // Call
                    {
                        int target = 0x1C + Read24(d, t.Pos); t.Pos += 3;
                        t.CallStack.Push(t.Pos);
                        t.Pos = target;
                        break;
                    }
                    case 0xA0: t.Pos += 5; break;
                    case 0xA1: t.Pos += 2; break;
                    case 0xA2: break;
                    case >= 0xB0 and <= 0xBD: t.Pos += 3; break;
                    case 0xC0: t.Pan = d[t.Pos++]; break;
                    case 0xC1: t.Volume = d[t.Pos++]; break;
                    case 0xC2: t.MasterVolume = d[t.Pos++]; break;   // real corpus: 0 occurrences in SE data, kept for completeness/other data
                    case 0xC3: t.Transpose = (sbyte)d[t.Pos++]; break;
                    case 0xC4: t.PitchBend = (sbyte)d[t.Pos++]; break;             // 14353 real occurrences — pervasive, not a rare feature
                    case 0xC5: t.PitchBendRange = d[t.Pos++]; break;               // 981 real occurrences
                    case 0xC6: t.Pos += 1; break;                                  // Priority (voice-stealing only, not audible)
                    case 0xC7: t.NoteWait = d[t.Pos++] != 0; break;                // see Track.NoteWait doc
                    case 0xC8: t.Pos += 1; break;                                  // Tie (0 real occurrences)
                    // Portamento (0xC9 key / 0xCE on-off / 0xCF time): confirmed ZERO occurrences across all
                    // 1006 real SEQ_SE_* sequences (scanned directly) — genuinely unused by sound effects in
                    // this game, not silently skipped out of laziness. Left structurally parsed (byte counts
                    // still correct, so track parsing doesn't desync) but not modelled.
                    case 0xC9: t.Pos += 1; break;
                    // Modulation LFO — 180/183 real modType settings are 0 (pitch vibrato, implemented in
                    // Mix's per-sample loop via the real per-tick formula from a hardware-accurate ARM7
                    // reimplementation); modType 1/2 (volume/pan, 3/0 real occurrences) are NOT modelled — see
                    // the class doc comment for why (even that reference's own author couldn't decode the
                    // formula for those two types).
                    case 0xCA: t.ModDepth = d[t.Pos++]; break;
                    case 0xCB: t.ModSpeed = d[t.Pos++]; break;
                    case 0xCC: t.ModType = d[t.Pos++]; break;
                    case 0xCD: t.ModRange = d[t.Pos++]; break;
                    case 0xCE: case 0xCF: t.Pos += 1; break;
                    case 0xD0: case 0xD1: case 0xD2: case 0xD3: t.Pos += 1; break;   // per-note ADSR override: 0 real occurrences
                    case 0xD4: t.Pos += 1; break;                       // Loop Start marker
                    case 0xD5: t.Expression = d[t.Pos++]; break;        // 197 real occurrences
                    case 0xD6: t.Pos += 1; break;
                    case 0xE0: t.ModDelayTicks = d[t.Pos] | (d[t.Pos + 1] << 8); t.Pos += 2; break;
                    case 0xE1: bpm = d[t.Pos] | (d[t.Pos + 1] << 8); t.Pos += 2; break;
                    // Sweep Pitch: a real, always-active per-note pitch ramp (see class doc comment — an
                    // earlier claim that this needs portamento to matter was wrong, corrected against the
                    // real source). Raw signed value, applied in Mix.
                    case 0xE3: t.SweepPitchRaw = (short)(d[t.Pos] | (d[t.Pos + 1] << 8)); t.Pos += 2; break;
                    case 0xFC: break;                                   // Loop End marker (looping itself is via Jump)
                    case 0xFD:                                          // Return
                        if (t.CallStack.Count > 0) t.Pos = t.CallStack.Pop(); else t.Done = true;
                        break;
                    case 0xFE: t.Pos += 2; break;
                    case 0xFF: t.Done = true; break;
                    default: t.Done = true; break;                      // unknown opcode: stop rather than misread the rest
                }
            }
            bpmRef = bpm;
        }

        private static int Read24(byte[] d, int p) => d[p] | (d[p + 1] << 8) | (d[p + 2] << 16);
        private static double TicksToSeconds(int ticks, double bpm) => ticks * (60.0 / bpm) / Ppqn;

        private static int ReadVarLen(byte[] d, ref int pos)
        {
            int value = 0;
            while (pos < d.Length)
            {
                int c = d[pos++];
                value = (value << 7) | (c & 0x7F);
                if ((c & 0x80) == 0) break;
            }
            return value;
        }

        private static short[] Mix(List<Voice> voices, List<SbnkInstrument> instruments,
            Func<int, List<SwavSample>> wavesForSlot, int sampleRate, double maxSeconds)
        {
            int totalSamples = (int)(maxSeconds * sampleRate);
            var buf = new float[totalSamples * 2];
            double endSeconds = 0;

            foreach (var v in voices)
            {
                if (v.Program < 0 || v.Program >= instruments.Count) continue;
                var inst = instruments[v.Program];
                var region = inst?.Resolve(v.Note);
                if (region == null) continue;
                var waves = wavesForSlot(region.WaveArcSlot);
                if (waves == null || region.WaveIndex >= waves.Count || waves[region.WaveIndex] == null) continue;
                var wav = waves[region.WaveIndex];
                if (wav.Pcm == null || wav.Pcm.Length == 0) continue;

                // Pitch bend (0xC4/0xC5) folds straight into the same semitone-to-ratio math as the note's own
                // transposition — confirmed pervasive in real data (14353 real occurrences across the SE
                // corpus), not a rare BGM-only feature worth skipping.
                double semitones = v.Note - region.BaseNote + v.PitchBendSemitones;
                double basePitchRatio = Math.Pow(2.0, semitones / 12.0) * wav.SampleRate / sampleRate;

                // Pitch vibrato (modType 0 only — the dominant real usage; see class doc comment for why
                // modType 1/2 aren't modelled). Formula verified against the real Nintendo source
                // (`LfoMain`/`SND_GetLfoValue` in snd_exchannel.c): each envelope tick adds
                // sin(phase)*modRange*modDepth*(1<<SND_PITCH_DIVISION_BIT=6)>>14 in "1/64 semitone" units,
                // once modDelayTicks have elapsed, phase advancing at modSpeed/512 cycles/tick — i.e.
                // sin(phase)*modRange*modDepth/16384 semitones. (An earlier pass used *60 here, borrowed from
                // a third-party reimplementation that turned out to use the wrong constant for pitch-type
                // modulation — 60 is the REAL source's constant for VOLUME-type modulation, not pitch.)
                // Continuous sin() stands in for the real source's 32-entry lookup table (verified
                // byte-identical) — the same curve, without hardware quantization.
                bool vibratoActive = v.ModType == 0 && v.ModDepth > 0;
                double vibratoDelaySeconds = v.ModDelayTicks * NitroEnvelope.TickSeconds;
                double vibratoHz = v.ModSpeed / 512.0 / NitroEnvelope.TickSeconds;
                double VibratoSemitones(double tSeconds)
                {
                    if (!vibratoActive) return 0;
                    double since = tSeconds - vibratoDelaySeconds;
                    if (since < 0) return 0;
                    double modParam = Math.Sin(2.0 * Math.PI * vibratoHz * since) * 127.0 * v.ModRange * v.ModDepth;
                    return modParam / 16384.0;
                }

                // Sweep Pitch (0xE3): a real, always-active per-note pitch ramp from the raw sweep value
                // (same "1/64 semitone" units as pitch bend) down to 0, linearly over the note's own
                // duration — verified against the real source (`SweepMain`/`seq_updatechnporta` in
                // snd_seq.c): `sweep_length = length` (the note's own duration) unconditionally, NOT gated
                // by portamento as an earlier pass concluded from a third-party reimplementation that (this
                // round confirmed) modelled the portamento gate differently from the real driver.
                double sweepSemitonesTotal = v.SweepPitchRaw / 64.0;
                double SweepSemitones(double tSeconds) =>
                    v.SweepPitchRaw == 0 || v.DurationSeconds <= 0 ? 0.0
                    : sweepSemitonesTotal * Math.Max(0.0, 1.0 - tSeconds / v.DurationSeconds);
                // Velocity/volume/expression/master-volume all read as 0-127 "attenuation level" registers —
                // combined through the same verified squared-dB curve (NitroEnvelope.LevelToGain) rather than
                // a naive linear v/127 multiply, consistent with how the identically-shaped SBNK sustain level
                // is known to work.
                double gain = NitroEnvelope.LevelToGain(v.Velocity) * NitroEnvelope.LevelToGain(v.Volume)
                            * NitroEnvelope.LevelToGain(v.Expression) * NitroEnvelope.LevelToGain(v.MasterVolume);
                double panT = Math.Clamp(v.Pan / 127.0, 0, 1);
                double gL = gain * Math.Sqrt(1 - panT), gR = gain * Math.Sqrt(panT);

                int startSample = (int)(v.StartSeconds * sampleRate);
                int noteSamples = Math.Max(1, (int)(v.DurationSeconds * sampleRate));

                // The real per-instrument attack/decay/sustain/release envelope (SBNK's own SNDInstParam bytes,
                // converted via NitroEnvelope's verified formulas), not a generic declick fade. This matters most
                // for a LOOPING sample held for a long note: without it, the loop played back at constant full
                // volume for the note's whole duration instead of decaying toward its real sustain level, which
                // for a short percussive/zap sample reads as an unnatural sustained drone/warble rather than the
                // real quick-decaying hit — confirmed on Thunder Punch's electric SE (SEQ_SE_DP_W161B, a looping
                // sample held ~0.7s at what was previously flat full volume).
                var envShape = NitroEnvelope.Compute(region.Attack, region.Decay, region.Sustain, region.Release);
                // Attack-phase length: the real curve (see NitroEnvelope.AttackGain) asymptotically approaches
                // full volume and never mathematically reaches it, so pick a practical "close enough" cutover
                // (within 0.1dB, gain >= 0.999) to hand off to the decay phase — solved analytically from the
                // same exponential this curve follows, since scanning tick-by-tick isn't needed for a
                // monotonic exponential.
                double attackRatio = envShape.AttackRate / 256.0;
                double attackTicks = attackRatio <= 0 ? 0 : Math.Log(11.11 / 92544.0) / Math.Log(attackRatio);
                int attackEnd = Math.Min(noteSamples, Math.Max(1, (int)(attackTicks * NitroEnvelope.TickSeconds * sampleRate)));
                double attackEndGain = NitroEnvelope.AttackGain(envShape.AttackRate, attackEnd / (double)sampleRate / NitroEnvelope.TickSeconds);
                int decayEnd = Math.Min(noteSamples, attackEnd + Math.Max(0, (int)(envShape.DecaySeconds * sampleRate)));
                // A handful of extreme raw byte values produce a release lasting minutes (the formula's own
                // reciprocal blows up near raw=0) — capped at 1s, well above any real percussive SE's actual
                // release, so a pathological value can't balloon the render buffer or CPU cost.
                int releaseSamples = Math.Clamp((int)(envShape.ReleaseSeconds * sampleRate), (int)(0.001 * sampleRate), (int)(1.0 * sampleRate));
                // Release starts at key-off (the note's programmed duration) and is allowed to ring past it,
                // same as the real hardware keeps a channel alive through its release tail after note-off.
                int totalNoteSamples = noteSamples + releaseSamples;

                // Decay/release ramp linearly IN DECIBELS (exponential in amplitude) — verified directly
                // against the real source's per-tick recurrence (`env_decay -= rate`, where env_decay is
                // itself a dB-like value; a constant per-tick dB subtraction is exactly a linear-in-dB ramp).
                double DbLerp(double fromGain, double toGain, double t)
                {
                    const double floor = 1e-4;   // ~-80dB — below this both ends round to silence, avoids log(0)
                    if (fromGain <= floor && toGain <= floor) return 0.0;
                    double dbA = 20.0 * Math.Log10(Math.Max(fromGain, floor));
                    double dbB = 20.0 * Math.Log10(Math.Max(toGain, floor));
                    return Math.Pow(10.0, (dbA + (dbB - dbA) * t) / 20.0);
                }

                double EnvelopeAt(int i)
                {
                    // Attack: the REAL per-tick exponential curve (NitroEnvelope.AttackGain), not a linear
                    // ramp — verified against the real source's SND_UpdateExChannelEnvelope ATTACK case.
                    if (i < attackEnd) return NitroEnvelope.AttackGain(envShape.AttackRate, i / (double)sampleRate / NitroEnvelope.TickSeconds);
                    if (i < decayEnd) return decayEnd > attackEnd ? DbLerp(attackEndGain, envShape.SustainLevel, (double)(i - attackEnd) / (decayEnd - attackEnd)) : envShape.SustainLevel;
                    if (i < noteSamples) return envShape.SustainLevel;
                    return releaseSamples > 0 ? DbLerp(envShape.SustainLevel, 0.0, (double)(i - noteSamples) / releaseSamples) : 0.0;
                }

                // Real hardware only updates the envelope/LFO/sweep registers once per ~192Hz tick (~166
                // samples at 32kHz), not every audio sample — a genuine sample-and-hold step function, not a
                // smooth continuous curve. Recomputing these (each involving Math.Pow/Math.Sin/Math.Log10)
                // on every single output sample was both LESS accurate (hardware doesn't interpolate between
                // ticks either) and a severe performance regression: measured worst-case render time hit
                // ~500ms for one real sequence (average ~20ms across the whole corpus, up from sub-1ms before
                // these curves were added) — slow enough that the animation-triggered async preview path
                // could visibly lag behind or seem to never play. Recomputing only when the tick boundary is
                // crossed (~166x fewer transcendental-function calls) fixes both at once.
                double samplesPerTick = sampleRate * NitroEnvelope.TickSeconds;
                int lastTick = -1;
                double heldEnv = 0, heldPitchRatio = basePitchRatio;

                double srcPos = 0;
                for (int i = 0; i < totalNoteSamples; i++)
                {
                    int outIdx = startSample + i;
                    if (outIdx >= totalSamples) break;
                    int i0Check = (int)srcPos;
                    if (i0Check >= wav.Pcm.Length && !wav.Loop) break;

                    int tick = (int)(i / samplesPerTick);
                    if (tick != lastTick)
                    {
                        lastTick = tick;
                        heldEnv = EnvelopeAt(i);
                        double tSeconds = i / (double)sampleRate;
                        double timeVaryingSemitones = (vibratoActive ? VibratoSemitones(tSeconds) : 0)
                                                     + (v.SweepPitchRaw != 0 ? SweepSemitones(tSeconds) : 0);
                        heldPitchRatio = timeVaryingSemitones != 0
                            ? basePitchRatio * Math.Pow(2.0, timeVaryingSemitones / 12.0)
                            : basePitchRatio;
                    }

                    // Percussive one-shots are often pitched several octaves above the recorded sample (a short
                    // "hit" reused for many different note pitches), meaning pitchRatio can be well above 1 —
                    // each output sample then has to stand in for SEVERAL source samples. Simply picking (and
                    // linearly blending between) the two nearest source samples throws the rest away, which
                    // aliases into exactly the harsh, static-like noise this was built to avoid. When stepping
                    // forward by more than ~1 source sample per output sample, average every source sample the
                    // step actually covers (a simple box-filter decimation) instead.
                    double sample = ReadResampled(wav, srcPos, heldPitchRatio);

                    buf[outIdx * 2] += (float)(sample * gL * heldEnv);
                    buf[outIdx * 2 + 1] += (float)(sample * gR * heldEnv);
                    srcPos += heldPitchRatio;
                }
                endSeconds = Math.Max(endSeconds, v.StartSeconds + totalNoteSamples / (double)sampleRate);
            }

            int usedSamples = Math.Min(totalSamples, Math.Max(1, (int)((endSeconds + 0.1) * sampleRate)));

            // Several simultaneous notes can sum past 16-bit range; scale the whole mix down rather than let it
            // hard-clip into distortion (this is a mixdown safety net, not a hardware-accurate limiter).
            float peak = 0;
            for (int i = 0; i < usedSamples * 2; i++) { float a = Math.Abs(buf[i]); if (a > peak) peak = a; }
            float scale = peak > short.MaxValue ? short.MaxValue / peak : 1f;

            var pcm = new short[usedSamples * 2];
            for (int i = 0; i < pcm.Length; i++)
                pcm[i] = (short)Math.Clamp(buf[i] * scale, short.MinValue, short.MaxValue);
            return pcm;
        }

        /// <summary>One source sample via linear interpolation between the two nearest points, wrapping through
        /// the loop region (arbitrarily far past the end — the modulo handles any number of loop cycles) for a
        /// looping wave, or returning 0 past the end of a non-looping one.</summary>
        private static double LinearSample(SwavSample wav, double srcPos)
        {
            int i0 = (int)srcPos;
            if (i0 >= wav.Pcm.Length)
            {
                if (!wav.Loop) return 0;
                int loopLen = wav.Pcm.Length - wav.LoopStartSample;
                if (loopLen <= 0) return 0;
                i0 = wav.LoopStartSample + (i0 - wav.Pcm.Length) % loopLen;
            }
            int i1 = i0 + 1 < wav.Pcm.Length ? i0 + 1 : (wav.Loop ? wav.LoopStartSample : i0);
            double frac = srcPos - Math.Floor(srcPos);
            return wav.Pcm[i0] * (1 - frac) + wav.Pcm[i1] * frac;
        }

        /// <summary>One output sample at <paramref name="srcPos"/>, stepping the source at <paramref name="step"/>
        /// samples per output sample. At step ≤ 1 (same rate or pitched down) plain linear interpolation is
        /// enough; above that (pitched up — very common for one-shot percussive hits reusing a single low sample
        /// across many higher notes) it averages every source sample the step actually spans, a cheap box-filter
        /// decimation that avoids the harsh aliasing a bare 2-tap interpolation produces when it skips samples.</summary>
        private static double ReadResampled(SwavSample wav, double srcPos, double step)
        {
            if (step <= 1.0) return LinearSample(wav, srcPos);
            int taps = Math.Min(64, Math.Max(1, (int)Math.Round(step)));
            double sum = 0;
            double subStep = step / taps;
            for (int k = 0; k < taps; k++) sum += LinearSample(wav, srcPos + k * subStep);
            return sum / taps;
        }
    }
}
