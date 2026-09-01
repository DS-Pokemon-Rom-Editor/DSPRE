using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DSPRE.Avalonia.Data
{
    /// <summary>
    /// Writes a sequence out as a MIDI file, so the game's music can be opened in an ordinary music
    /// program.
    ///
    /// What comes out is the notes, not the sound. The games play their notes on their own instruments,
    /// which are samples living in the ROM, and a MIDI file cannot carry those. So the file says which
    /// instrument number each track asked for and the receiving program picks whatever it has under that
    /// number, which will not sound the same. The notes, their timing, how hard they are hit and where
    /// they sit left to right are all exact.
    ///
    /// Timing is written at a fixed hundred and twenty beats a minute and every note is placed by the
    /// second it actually starts at. That keeps the timing right even for a sequence that changes speed
    /// part way through, at the cost of the file not carrying those speed changes as speed changes.
    /// </summary>
    public static class MidiFile
    {
        private const int TicksPerQuarter = 480;
        private const int Bpm = 120;
        private const double TicksPerSecond = TicksPerQuarter * Bpm / 60.0;

        /// <summary>How long a note with no written length is given in the file. Nothing in the sequence
        /// ever stops these, so a MIDI has to choose something; a cry is one of them.</summary>
        private const double OpenEndedSeconds = 2.0;

        public static byte[] FromNotes(IReadOnlyList<SseqPlayer.Note> notes, string name)
        {
            if (notes == null) return null;

            // One MIDI track per track of the sequence, plus a first track holding the name and the speed,
            // which is what a Type 1 file is.
            var byTrack = notes.GroupBy(n => n.Track).OrderBy(g => g.Key).ToList();
            var chunks = new List<byte[]> { HeaderTrack(name) };
            foreach (var g in byTrack) chunks.Add(OneTrack(g.ToList(), g.Key));

            using var ms = new MemoryStream();
            WriteAscii(ms, "MThd");
            WriteU32(ms, 6);
            WriteU16(ms, 1);                        // Type 1: several tracks played together
            WriteU16(ms, (ushort)chunks.Count);
            WriteU16(ms, TicksPerQuarter);
            foreach (var c in chunks) ms.Write(c, 0, c.Length);
            return ms.ToArray();
        }

        private static byte[] HeaderTrack(string name)
        {
            using var ms = new MemoryStream();
            WriteVarInt(ms, 0);
            ms.WriteByte(0xFF); ms.WriteByte(0x03);          // track name
            var bytes = System.Text.Encoding.ASCII.GetBytes(Tidy(name));
            WriteVarInt(ms, bytes.Length);
            ms.Write(bytes, 0, bytes.Length);

            WriteVarInt(ms, 0);
            ms.WriteByte(0xFF); ms.WriteByte(0x51); ms.WriteByte(0x03);   // speed
            int usPerQuarter = 60_000_000 / Bpm;
            ms.WriteByte((byte)(usPerQuarter >> 16));
            ms.WriteByte((byte)(usPerQuarter >> 8));
            ms.WriteByte((byte)usPerQuarter);

            WriteVarInt(ms, 0);
            ms.WriteByte(0xFF); ms.WriteByte(0x2F); ms.WriteByte(0x00);   // end
            return Chunk(ms.ToArray());
        }

        private static byte[] OneTrack(List<SseqPlayer.Note> notes, int trackNumber)
        {
            // Channel 9 is the drum channel everywhere, and one of these tracks landing on it would be
            // played as percussion by accident, so it is stepped over.
            int channel = trackNumber % 15;
            if (channel >= 9) channel++;

            var events = new List<(long tick, int order, byte[] bytes)>();
            int lastProgram = -1, lastPan = -1, lastVolume = -1;

            foreach (var n in notes.OrderBy(n => n.StartSeconds))
            {
                long on = (long)Math.Round(n.StartSeconds * TicksPerSecond);
                double length = n.NoLengthGiven || n.DurationSeconds <= 0
                    ? OpenEndedSeconds : n.DurationSeconds;
                long off = Math.Max(on + 1, (long)Math.Round((n.StartSeconds + length) * TicksPerSecond));

                if (n.Program != lastProgram && n.Program >= 0 && n.Program < 128)
                {
                    events.Add((on, 0, new[] { (byte)(0xC0 | channel), (byte)n.Program }));
                    lastProgram = n.Program;
                }
                if (n.Volume != lastVolume && n.Volume >= 0 && n.Volume < 128)
                {
                    events.Add((on, 1, new[] { (byte)(0xB0 | channel), (byte)7, (byte)n.Volume }));
                    lastVolume = n.Volume;
                }
                if (n.Pan != lastPan && n.Pan >= 0 && n.Pan < 128)
                {
                    events.Add((on, 1, new[] { (byte)(0xB0 | channel), (byte)10, (byte)n.Pan }));
                    lastPan = n.Pan;
                }

                byte note = (byte)Math.Clamp(n.Number, 0, 127);
                byte velocity = (byte)Math.Clamp(n.Velocity, 1, 127);
                events.Add((on, 2, new[] { (byte)(0x90 | channel), note, velocity }));
                events.Add((off, 3, new[] { (byte)(0x80 | channel), note, (byte)0 }));
            }

            // A note off at the same tick as the next note on has to come first, or the new note is cut
            // off by the old one's release. The order field does that.
            events.Sort((a, b) => a.tick != b.tick ? a.tick.CompareTo(b.tick) : a.order.CompareTo(b.order));

            using var ms = new MemoryStream();
            WriteVarInt(ms, 0);
            ms.WriteByte(0xFF); ms.WriteByte(0x03);
            var title = System.Text.Encoding.ASCII.GetBytes("Track " + (trackNumber + 1));
            WriteVarInt(ms, title.Length);
            ms.Write(title, 0, title.Length);

            long at = 0;
            foreach (var e in events)
            {
                WriteVarInt(ms, (int)(e.tick - at));
                at = e.tick;
                ms.Write(e.bytes, 0, e.bytes.Length);
            }

            WriteVarInt(ms, 0);
            ms.WriteByte(0xFF); ms.WriteByte(0x2F); ms.WriteByte(0x00);
            return Chunk(ms.ToArray());
        }

        private static string Tidy(string s)
        {
            if (string.IsNullOrEmpty(s)) return "Sequence";
            var clean = new string(s.Where(c => c >= 32 && c < 127).ToArray());
            return clean.Length == 0 ? "Sequence" : clean;
        }

        private static byte[] Chunk(byte[] body)
        {
            using var ms = new MemoryStream();
            WriteAscii(ms, "MTrk");
            WriteU32(ms, (uint)body.Length);
            ms.Write(body, 0, body.Length);
            return ms.ToArray();
        }

        private static void WriteAscii(Stream s, string four)
        {
            foreach (char c in four) s.WriteByte((byte)c);
        }

        private static void WriteU32(Stream s, uint v)
        {
            s.WriteByte((byte)(v >> 24)); s.WriteByte((byte)(v >> 16));
            s.WriteByte((byte)(v >> 8)); s.WriteByte((byte)v);
        }

        private static void WriteU16(Stream s, ushort v)
        {
            s.WriteByte((byte)(v >> 8)); s.WriteByte((byte)v);
        }

        /// <summary>MIDI writes its gaps seven bits at a time, high bit set on every byte but the last.</summary>
        private static void WriteVarInt(Stream s, int value)
        {
            if (value < 0) value = 0;
            var stack = new Stack<byte>();
            stack.Push((byte)(value & 0x7F));
            value >>= 7;
            while (value > 0) { stack.Push((byte)((value & 0x7F) | 0x80)); value >>= 7; }
            while (stack.Count > 0) s.WriteByte(stack.Pop());
        }
    }
}
