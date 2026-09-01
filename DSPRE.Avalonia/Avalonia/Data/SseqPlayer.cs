using System;
using System.Collections.Generic;

namespace DSPRE.Avalonia.Data
{
    /// <summary>
    /// Renders one SSEQ sequence (the Nitro note/control event stream behind both music and short sound
    /// effects) to a flat stereo 16-bit PCM buffer for preview playback. Not a real-time player: the whole
    /// sequence is simulated up front and mixed down once, so no live audio-thread scheduler is needed.
    ///
    /// Models note timing, program change to instrument/wave lookup, pitch (note, transpose, pitch bend),
    /// pan, tempo, track subroutines (call/return) with one bounded loop-back, the real per-instrument
    /// ADSR envelope (see <see cref="NitroEnvelope"/>), velocity/volume/expression/master gain through the
    /// squared-dB attenuation curve, pitch vibrato (modType 0), and Sweep Pitch (0xE3): all against the
    /// real Nintendo ARM7 mixer formulas, not a third-party reimplementation.
    ///
    /// Not modelled: volume/pan modulation (modType 1/2) and portamento (0xC9/0xCE/0xCF). Both are
    /// vanishingly rare across the real HGSS sound-effect corpus, so this is a deliberate, low-risk gap.
    /// </summary>
    public static class SseqPlayer
    {
        private const int Ppqn = 48;   // ticks per quarter note

        private sealed class Voice
        {
            public double StartSeconds;
            public double DurationSeconds;

            /// <summary>
            /// The note was written with no length at all, so nothing ever tells it to stop and it sounds
            /// until its sample runs out.
            /// </summary>
            public bool NoLengthGiven;
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
            public int Track;
        }

        private sealed class Track
        {
            public byte[] Data;
            public int Pos;
            public bool Done;
            public int Program;
            public int Pan = 64;
            // SND_TRACK_DEFAULT_VOLUME (snd_seq.c's InitTrack) is 127 for BOTH volume (0xC1) and
            // volume2/expression (0xD5), not 64.
            public int Volume = 127;
            public int Expression = 127;
            public int MasterVolume = 127;
            public int Transpose;
            // Pitch vibrato defaults, per SND_InitLfoParam (snd_exchannel.c): depth 0 (off), range 1, speed 16,
            // delay 0 ticks, type 0 (pitch).
            public int ModType, ModDepth, ModRange = 1, ModSpeed = 16, ModDelayTicks;
            // Sweep Pitch (0xE3): raw signed value, same "1/64 semitone" units as pitch bend/transpose.
            public int SweepPitchRaw;
            // Raw signed pitch-bend byte (-128..127) and the range it's scaled by (semitones); used constantly
            // in sound effects, not just BGM. Default range of 2 semitones matches General MIDI's conventional
            // default pitch-bend sensitivity; nearly every real track sets its own range explicitly before
            // bending, so the default rarely matters in practice.
            public int PitchBend;
            public int PitchBendRange = 2;
            // NOTEWAIT MODE (0xC7) defaults to off: a note-on event's own duration does NOT advance the track's
            // clock, so the next event fires immediately, until an explicit Rest (0x80) or notewait-mode-on
            // changes that. Almost every real track turns this on near the start and never turns it back off,
            // but the default must still be off for correctness on a track that doesn't set it.
            public bool NoteWait;
            public double TimeSeconds;
            public readonly Stack<int> CallStack = new Stack<int>();
        }

        /// <summary>One note as the sequence wrote it. </summary>
        public sealed class Note
        {
            public double StartSeconds;
            public double DurationSeconds;
            /// <summary>Written with no length, so nothing ever stops it and it runs until its sample
            /// does. Every Pokemon cry is written this way.</summary>
            public bool NoLengthGiven;
            public int Number;        // 0..127, 60 is middle C
            public int Velocity;      // 0..127
            public int Program;       // which instrument of the bank
            public int Pan;           // 0..127, 64 is centre
            public int Volume;        // 0..127
            public int Track;         // which of the sequence's tracks wrote it
        }

        /// <summary>Reads a sequence into its notes without making any sound.</summary>
        public static IReadOnlyList<Note> ReadNotes(SdatArchive sdat, int seqIndex, double maxSeconds = 8.0)
        {
            var voices = Collect(sdat, seqIndex, maxSeconds, out _, out _, out _);
            if (voices == null) return null;
            var notes = new List<Note>(voices.Count);
            foreach (var v in voices)
                notes.Add(new Note
                {
                    StartSeconds = v.StartSeconds, DurationSeconds = v.DurationSeconds,
                    NoLengthGiven = v.NoLengthGiven, Number = v.Note, Velocity = v.Velocity,
                    Program = v.Program, Pan = v.Pan, Volume = v.Volume, Track = v.Track,
                });
            notes.Sort((x, y) => x.StartSeconds != y.StartSeconds
                ? x.StartSeconds.CompareTo(y.StartSeconds)
                : x.Number.CompareTo(y.Number));
            return notes;
        }

        /// <summary>Runs a sequence's tracks and gathers what they play. Shared by rendering it and by
        /// reading its notes, so there is only ever one reading of a sequence.</summary>
        private static List<Voice> Collect(SdatArchive sdat, int seqIndex, double maxSeconds,
            out List<SbnkInstrument> instruments, out Func<int, List<SwavSample>> wavesForSlot,
            out double bpmOut, int bankOverride = -1)
        {
            instruments = null; wavesForSlot = null; bpmOut = 120.0;
            if (sdat == null || seqIndex < 0 || seqIndex >= sdat.Sequences.Count) return null;
            var seq = sdat.Sequences[seqIndex];
            if (seq == null) return null;
            var seqBytes = sdat.GetFileBytes(seq.FileId);
            if (seqBytes == null || seqBytes.Length < 0x1C) return null;
            int bankNo = bankOverride >= 0 ? bankOverride : seq.BankNo;
            if (bankNo < 0 || bankNo >= sdat.Banks.Count || sdat.Banks[bankNo] == null) return null;
            var bank = sdat.Banks[bankNo];
            instruments = sdat.GetBankInstruments(bankNo);
            if (instruments == null) return null;

            wavesForSlot = slot => slot < 0 || slot >= 4 ? null : sdat.GetWaveArchive(bank.WaveArcNo[slot]);

            var tracks = ParseTrackList(seqBytes);
            double bpm = 120.0;
            var voices = new List<Voice>();
            for (int i = 0; i < tracks.Count; i++)
            {
                int before = voices.Count;
                RunTrack(tracks[i], seqBytes, ref bpm, voices, maxSeconds);
                for (int v = before; v < voices.Count; v++) voices[v].Track = i;
            }
            bpmOut = bpm;
            return voices;
        }

        /// <summary>
        /// Renders sequence <paramref name="seqIndex"/> from <paramref name="sdat"/> to interleaved stereo
        /// 16-bit PCM at <paramref name="sampleRate"/>, or null if the sequence/bank can't be resolved.
        /// </summary>
        /// <param name="bankOverride">
        /// Play the sequence with somebody else's instruments instead of its own. A cry works this way:
        /// there is one short sequence for all of them, and the games hand it the bank belonging to the
        /// Pokemon (snd_play.c:1091 plays SEQ_PV with the bank set to the species number). Leave it at
        /// -1 to use the sequence's own bank.
        /// </param>
        public static short[] Render(SdatArchive sdat, int seqIndex, int sampleRate = 32000, double maxSeconds = 8.0,
                                     int bankOverride = -1)
        {
            var voices = Collect(sdat, seqIndex, maxSeconds, out var instruments,
                                 out var wavesForSlot, out _, bankOverride);
            if (voices == null) return null;

            // Most SEs run under a second; a hardcoded 8-second buffer would allocate a Large Object Heap
            // block several times larger than needed on every preview, and the resulting gen1/gen2
            // collections stall the whole process as audible glitches. Size to what this sequence actually
            // renders; the +1.1s margin covers the mixdown's own release tail (capped at 1s, see Mix) plus
            // a small cushion.
            double neededSeconds = 0;
            foreach (var v in voices) neededSeconds = Math.Max(neededSeconds, v.StartSeconds + v.DurationSeconds);
            double bufferSeconds = Math.Min(maxSeconds, neededSeconds + 1.1);

            return Mix(voices, instruments, wavesForSlot, sampleRate, bufferSeconds);
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
        // event (single-track sequence, track 0 starts right there) or 0xFE marking that a run of 0x93 "Open
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
                        NoLengthGiven = durTicks == 0,
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
                    // next event; the default (and this is what actually differs per-track) is that notes at
                    // the SAME track-time layer as a chord, and only an explicit Rest (0x80) advances the clock.
                    if (t.NoteWait) t.TimeSeconds += durSec;
                    continue;
                }

                switch (op)
                {
                    case 0x80: t.TimeSeconds += TicksToSeconds(ReadVarLen(d, ref t.Pos), bpm); break;
                    case 0x81: t.Program = ReadVarLen(d, ref t.Pos); break;
                    case 0x93: t.Pos += 4; break;                       // Open Track (mid-stream, rare, skip its args)
                    case 0x94:                                          // Jump
                    {
                        int target = 0x1C + Read24(d, t.Pos); t.Pos += 3;
                        // A forward jump is normal control flow and is followed as usual. A backward jump
                        // is the sequence's own loop mechanism (BGM loops forever until the game stops the
                        // channel); a static preview render has no such stop signal, so looping it would
                        // just play an audible doubled/echoed copy of content meant to sustain indefinitely.
                        // Play it through once instead.
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
                    case 0xC2: t.MasterVolume = d[t.Pos++]; break;   // unused in SE data, kept for completeness/BGM
                    case 0xC3: t.Transpose = (sbyte)d[t.Pos++]; break;
                    case 0xC4: t.PitchBend = (sbyte)d[t.Pos++]; break;             // pervasive in SE data, not a rare feature
                    case 0xC5: t.PitchBendRange = d[t.Pos++]; break;
                    case 0xC6: t.Pos += 1; break;                                  // Priority (voice-stealing only, not audible)
                    case 0xC7: t.NoteWait = d[t.Pos++] != 0; break;                // see Track.NoteWait doc
                    case 0xC8: t.Pos += 1; break;                                  // Tie (0 real occurrences)
                    // Portamento (0xC9 key / 0xCE on-off / 0xCF time): unused by sound effects in this game, so
                    // not modelled. Left structurally parsed (byte counts still correct) so track parsing
                    // doesn't desync.
                    case 0xC9: t.Pos += 1; break;
                    // Modulation LFO: modType 0 (pitch vibrato) is by far the dominant case in real SE data and
                    // is implemented in Mix's per-sample loop via the real per-tick formula. modType 1/2
                    // (volume/pan) are vanishingly rare and NOT modelled, see the class doc comment.
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
                    // Sweep Pitch: a real, always-active per-note pitch ramp (see class doc comment); it does
                    // not require portamento to be active. Raw signed value, applied in Mix.
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
                // A square or noise region has no recording to look up; its sound is made on the spot.
                var wav = PsgWaveform.For(region);
                if (wav == null)
                {
                    var waves = wavesForSlot(region.WaveArcSlot);
                    if (waves == null || region.WaveIndex >= waves.Count || waves[region.WaveIndex] == null) continue;
                    wav = waves[region.WaveIndex];
                }
                if (wav.Pcm == null || wav.Pcm.Length == 0) continue;

                // Pitch bend (0xC4/0xC5) folds straight into the same semitone-to-ratio math as the note's own
                // transposition; it's pervasive in real SE data, not a rare BGM-only feature worth skipping.
                double semitones = v.Note - region.BaseNote + v.PitchBendSemitones;
                double basePitchRatio = Math.Pow(2.0, semitones / 12.0) * wav.SampleRate / sampleRate;

                // Pitch vibrato (modType 0 only; see class doc for why modType 1/2 aren't modelled). Matches
                // the real source (`LfoMain`/`SND_GetLfoValue` in snd_exchannel.c): each tick adds
                // sin(phase)*modRange*modDepth/16384 semitones once modDelayTicks have elapsed, phase
                // advancing at modSpeed/512 cycles/tick. Continuous sin() stands in for the real source's
                // 32-entry lookup table.
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
                // (same "1/64 semitone" units as pitch bend) down to 0, linearly over the note's own duration.
                // Per the real source (`SweepMain`/`seq_updatechnporta` in snd_seq.c), `sweep_length = length`
                // (the note's own duration) unconditionally, NOT gated by portamento.
                double sweepSemitonesTotal = v.SweepPitchRaw / 64.0;
                double SweepSemitones(double tSeconds) =>
                    v.SweepPitchRaw == 0 || v.DurationSeconds <= 0 ? 0.0
                    : sweepSemitonesTotal * Math.Max(0.0, 1.0 - tSeconds / v.DurationSeconds);
                // Velocity/volume/expression/master-volume all read as 0-127 "attenuation level" registers,
                // combined through the same squared-dB curve (NitroEnvelope.LevelToGain) rather than a naive
                // linear v/127 multiply, matching how SBNK sustain level uses the identically-shaped curve.
                double gain = NitroEnvelope.LevelToGain(v.Velocity) * NitroEnvelope.LevelToGain(v.Volume)
                            * NitroEnvelope.LevelToGain(v.Expression) * NitroEnvelope.LevelToGain(v.MasterVolume);
                double panT = Math.Clamp(v.Pan / 127.0, 0, 1);
                double gL = gain * Math.Sqrt(1 - panT), gR = gain * Math.Sqrt(panT);

                int startSample = (int)(v.StartSeconds * sampleRate);
                int noteSamples = Math.Max(1, (int)(v.DurationSeconds * sampleRate));

                // A note with no length of its own is never told to stop, so it sounds for as long as its
                // sample lasts. Without this a cry renders as a single sample of silence.
                if (v.NoLengthGiven && wav.Pcm != null && wav.Pcm.Length > 0 && !wav.Loop)
                {
                    // How long the sample runs once it is played at this note's pitch.
                    int wholeSample = (int)(wav.Pcm.Length / Math.Max(1e-6, basePitchRatio));
                    noteSamples = Math.Max(noteSamples, Math.Max(1, wholeSample));
                }

                // The real per-instrument attack/decay/sustain/release envelope (SBNK's SNDInstParam bytes via
                // NitroEnvelope), not a generic declick fade. Matters most for a looping sample held on a long
                // note: without it the loop plays at constant full volume for the note's whole duration instead
                // of decaying toward its real sustain level, reading as an unnatural drone rather than a
                // quick-decaying hit.
                var envShape = NitroEnvelope.Compute(region.Attack, region.Decay, region.Sustain, region.Release);
                // Attack-phase length: the real curve (NitroEnvelope.AttackGain) asymptotically approaches full
                // volume and never mathematically reaches it, so pick a practical cutover (within 0.1dB, gain
                // >= 0.999) to hand off to the decay phase, solved analytically since the curve is monotonic.
                double attackRatio = envShape.AttackRate / 256.0;
                double attackTicks = attackRatio <= 0 ? 0 : Math.Log(11.11 / 92544.0) / Math.Log(attackRatio);
                int attackEnd = Math.Min(noteSamples, Math.Max(1, (int)(attackTicks * NitroEnvelope.TickSeconds * sampleRate)));
                double attackEndGain = NitroEnvelope.AttackGain(envShape.AttackRate, attackEnd / (double)sampleRate / NitroEnvelope.TickSeconds);
                int decayEnd = Math.Min(noteSamples, attackEnd + Math.Max(0, (int)(envShape.DecaySeconds * sampleRate)));
                // A handful of extreme raw byte values produce a release lasting minutes (the formula's own
                // reciprocal blows up near raw=0), capped at 1s, well above any real percussive SE's actual
                // release, so a pathological value can't balloon the render buffer or CPU cost.
                int releaseSamples = Math.Clamp((int)(envShape.ReleaseSeconds * sampleRate), (int)(0.001 * sampleRate), (int)(1.0 * sampleRate));
                // Release starts at key-off (the note's programmed duration) and is allowed to ring past it,
                // same as the real hardware keeps a channel alive through its release tail after note-off.
                int totalNoteSamples = noteSamples + releaseSamples;

                // Decay/release ramps linearly IN DECIBELS (exponential in amplitude): the hardware's per-tick
                // recurrence is `env_decay -= rate`, a constant per-tick dB subtraction, which is exactly a
                // linear-in-dB ramp.
                double DbLerp(double fromGain, double toGain, double t)
                {
                    const double floor = 1e-4;   // ~-80dB: below this both ends round to silence, avoids log(0)
                    if (fromGain <= floor && toGain <= floor) return 0.0;
                    double dbA = 20.0 * Math.Log10(Math.Max(fromGain, floor));
                    double dbB = 20.0 * Math.Log10(Math.Max(toGain, floor));
                    return Math.Pow(10.0, (dbA + (dbB - dbA) * t) / 20.0);
                }

                double EnvelopeAt(int i)
                {
                    // Attack uses the hardware's real per-tick exponential curve (NitroEnvelope.AttackGain),
                    // not a linear ramp.
                    if (i < attackEnd) return NitroEnvelope.AttackGain(envShape.AttackRate, i / (double)sampleRate / NitroEnvelope.TickSeconds);
                    if (i < decayEnd) return decayEnd > attackEnd ? DbLerp(attackEndGain, envShape.SustainLevel, (double)(i - attackEnd) / (decayEnd - attackEnd)) : envShape.SustainLevel;
                    if (i < noteSamples) return envShape.SustainLevel;
                    return releaseSamples > 0 ? DbLerp(envShape.SustainLevel, 0.0, (double)(i - noteSamples) / releaseSamples) : 0.0;
                }

                // Real hardware updates the envelope/LFO/sweep registers once per ~192Hz tick (~166 samples
                // at 32kHz), not every audio sample: a sample-and-hold step function, not a smooth curve.
                // Recomputing these (Math.Pow/Math.Sin/Math.Log10 each) on every output sample is both less
                // accurate and far slower than needed, so only recompute when the tick boundary is crossed.
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
                    // "hit" reused for many different note pitches), so pitchRatio can be well above 1: each
                    // output sample then has to stand in for SEVERAL source samples. Simply picking (and
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
        /// the loop region (arbitrarily far past the end, since the modulo handles any number of loop cycles)
        /// for a looping wave, or returning 0 past the end of a non-looping one.</summary>
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
        /// enough; above that (pitched up, very common for one-shot percussive hits reusing a single low sample
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
