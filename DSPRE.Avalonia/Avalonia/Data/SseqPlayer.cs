using System;
using System.Collections.Generic;

namespace DSPRE.Avalonia.Data
{
    /// <summary>
    /// Renders an SSEQ sequence to stereo 16-bit PCM for preview, through <see cref="NitroSoundDriver"/>.
    /// </summary>
    public static class SseqPlayer
    {
        /// <summary>One note as the sequence wrote it.</summary>
        public sealed class Note
        {
            public double StartSeconds;
            public double DurationSeconds;
            /// <summary>Written with no length, so it runs until its sample ends, as every cry is.</summary>
            public bool NoLengthGiven;
            public int Number;        // 0..127, 60 is middle C
            public int Velocity;      // 0..127
            public int Program;       // which instrument of the bank
            public int Pan;           // 0..127, 64 is centre
            public int Volume;        // 0..127
            public int Track;         // which of the sequence's tracks wrote it
        }

        /// <summary>Reads a sequence's notes without sound, once through: loops are not followed.</summary>
        public static IReadOnlyList<Note> ReadNotes(SdatArchive sdat, int seqIndex, double maxSeconds = 8.0)
        {
            var settings = new NitroSoundDriver.Settings { Mix = false, FollowLoops = false, MaxSeconds = maxSeconds };
            var driver = CreateDriver(sdat, seqIndex, settings, -1, -1);
            if (driver == null) return null;
            driver.Run();

            double t = NitroSoundTables.UpdateSeconds;
            var ticks = driver.TickUpdates;
            double TickTime(long tick)
            {
                if (tick < ticks.Count) return ticks[(int)tick] * t;
                // Past the last tick the player ran, carry on at the tempo it ended on.
                double last = ticks.Count > 0 ? ticks[ticks.Count - 1] * t : 0;
                return last + (tick - Math.Max(0, ticks.Count - 1)) * t * 240.0 / Math.Max(1, driver.LastTempo);
            }

            var notes = new List<Note>(driver.Notes.Count);
            foreach (var e in driver.Notes)
            {
                double start = e.Update * t;
                if (start >= maxSeconds) continue;
                notes.Add(new Note
                {
                    StartSeconds = start,
                    DurationSeconds = e.Length > 0 ? Math.Max(0, TickTime((long)e.Tick + e.Length) - start) : 0,
                    NoLengthGiven = e.Length <= 0,
                    Number = e.Key, Velocity = e.Velocity, Program = e.Program,
                    Pan = Math.Clamp(e.Pan, 0, 127), Volume = e.Volume, Track = e.Track,
                });
            }
            notes.Sort((x, y) => x.StartSeconds != y.StartSeconds
                ? x.StartSeconds.CompareTo(y.StartSeconds)
                : x.Number.CompareTo(y.Number));
            return notes;
        }

        /// <summary>
        /// Renders a sequence to interleaved stereo PCM, or null when it or its bank can't be resolved.
        /// A looping sequence plays until <paramref name="maxSeconds"/>.
        /// </summary>
        /// <param name="bankOverride">
        /// Another bank to play with, as cries do: one sequence, the species' bank. -1 uses the sequence's own.
        /// </param>
        /// <param name="waveArcOverride">
        /// Slot 0's wave archive instead of the bank's, for hg-engine's one-archive-per-species cries.
        /// </param>
        public static short[] Render(SdatArchive sdat, int seqIndex, int sampleRate = 32000, double maxSeconds = 8.0,
                                     int bankOverride = -1, int waveArcOverride = -1)
            => Render(sdat, seqIndex, new NitroSoundDriver.Settings { SampleRate = sampleRate, MaxSeconds = maxSeconds },
                      bankOverride, waveArcOverride);

        internal static short[] Render(SdatArchive sdat, int seqIndex, NitroSoundDriver.Settings settings,
                                       int bankOverride = -1, int waveArcOverride = -1)
        {
            if (settings.SampleRate <= 0 || settings.MaxSeconds <= 0) return null;
            return CreateDriver(sdat, seqIndex, settings, bankOverride, waveArcOverride)?.Run();
        }

        private static NitroSoundDriver CreateDriver(SdatArchive sdat, int seqIndex, NitroSoundDriver.Settings settings,
                                                     int bankOverride, int waveArcOverride)
        {
            if (sdat == null || seqIndex < 0 || seqIndex >= sdat.Sequences.Count) return null;
            var seq = sdat.Sequences[seqIndex];
            if (seq == null) return null;
            var seqBytes = sdat.GetFileBytes(seq.FileId);
            if (seqBytes == null || seqBytes.Length < 0x1C) return null;
            int bankNo = bankOverride >= 0 ? bankOverride : seq.BankNo;
            if (bankNo < 0 || bankNo >= sdat.Banks.Count || sdat.Banks[bankNo] == null) return null;
            var bank = sdat.Banks[bankNo];
            var instruments = sdat.GetBankInstruments(bankNo);
            if (instruments == null) return null;

            // hg-engine keeps each cry in its own wave archive and shares one bank.
            Func<int, List<SwavSample>> wavesForSlot = slot =>
                slot < 0 || slot >= 4 ? null
                : waveArcOverride >= 0 ? (slot == 0 ? sdat.GetWaveArchive(waveArcOverride) : null)
                : sdat.GetWaveArchive(bank.WaveArcNo[slot]);

            // The archive's player entry limits which hardware channels the sequence's notes may take.
            int channelMask = seq.PlayerNo >= 0 && seq.PlayerNo < sdat.Players.Count && sdat.Players[seq.PlayerNo] != null
                ? sdat.Players[seq.PlayerNo].AllocChannelMask : 0;

            return new NitroSoundDriver(settings, seqBytes, instruments, wavesForSlot, seq.Volume, seq.ChannelPrio, channelMask);
        }

        /// <summary>Writes interleaved stereo 16-bit PCM to a .wav file.</summary>
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
    }
}
