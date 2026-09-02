using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DSPRE;
using DSPRE.Avalonia.ViewModels.Audio;
using DSPRE.Avalonia.Data;
using DSPRE.Avalonia.ViewModels;
using Xunit;
using Xunit.Abstractions;

namespace DSPRE.Tests
{
    /// <summary>A sequence saved as a MIDI holds the same notes the sequence holds.</summary>
    [Collection("rom")]
    public class MidiExportTests
    {
        private readonly ITestOutputHelper _out;
        public MidiExportTests(ITestOutputHelper o) { _out = o; }

        private const string HeartGold = @"C:\Romhacking\ROMs\NDS\HGSS\HeartGold (USA)_DSPRE_contents";
        private const string Platinum =
            @"C:\Romhacking\ROMs\NDS\Plat\Pokemon - Platinum Version (USA) (Rev 1)\Pokemon - Platinum Version (USA) (Rev 1)_DSPRE_contents";

        public static IEnumerable<object[]> Games => new[]
        {
            new object[] { "IPKE", HeartGold, "HeartGold" },
            new object[] { "CPUE", Platinum, "Platinum" },
        };

        // ── a MIDI reader, written here so the writer is checked by something else ──────────────────

        private sealed class MidiNote
        {
            public long Tick;
            public int Number, Velocity, Channel;
        }

        private static (int format, int tracks, int ppqn, List<MidiNote> notes, List<int> programs) ReadMidi(byte[] d)
        {
            int p = 0;
            string Tag() { var s = new string(new[] { (char)d[p], (char)d[p + 1], (char)d[p + 2], (char)d[p + 3] }); p += 4; return s; }
            uint U32() { uint v = (uint)((d[p] << 24) | (d[p + 1] << 16) | (d[p + 2] << 8) | d[p + 3]); p += 4; return v; }
            int U16() { int v = (d[p] << 8) | d[p + 1]; p += 2; return v; }

            Assert.Equal("MThd", Tag());
            Assert.Equal(6u, U32());
            int format = U16(), tracks = U16(), ppqn = U16();

            var notes = new List<MidiNote>();
            var programs = new List<int>();

            for (int t = 0; t < tracks; t++)
            {
                Assert.Equal("MTrk", Tag());
                int len = (int)U32();
                int end = p + len;
                long tick = 0;
                int running = 0;

                while (p < end)
                {
                    int delta = 0;
                    while (true)
                    {
                        byte b = d[p++];
                        delta = (delta << 7) | (b & 0x7F);
                        if ((b & 0x80) == 0) break;
                    }
                    tick += delta;

                    int status = d[p];
                    if (status < 0x80) status = running; else p++;
                    running = status;

                    if (status == 0xFF)
                    {
                        int type = d[p++];
                        int mlen = 0;
                        while (true)
                        {
                            byte b = d[p++];
                            mlen = (mlen << 7) | (b & 0x7F);
                            if ((b & 0x80) == 0) break;
                        }
                        p += mlen;
                        if (type == 0x2F) break;
                        continue;
                    }

                    int kind = status & 0xF0, channel = status & 0x0F;
                    switch (kind)
                    {
                        case 0x90:
                        {
                            int n = d[p++], v = d[p++];
                            if (v > 0) notes.Add(new MidiNote { Tick = tick, Number = n, Velocity = v, Channel = channel });
                            break;
                        }
                        case 0x80: p += 2; break;
                        case 0xC0: programs.Add(d[p++]); break;
                        case 0xD0: p += 1; break;
                        default: p += 2; break;     // note aftertouch, controllers, pitch bend
                    }
                }
                p = end;
            }
            return (format, tracks, ppqn, notes, programs);
        }

        // ── the checks ─────────────────────────────────────────────────────────────────────────────

        [Theory]
        [MemberData(nameof(Games))]
        public void EverySequenceSavesAsAMidiHoldingTheSameNotes(string code, string path, string game)
        {
            if (!Directory.Exists(path)) { _out.WriteLine($"{game}: not unpacked here"); return; }
            new RomInfo(code, path);

            var sdat = SoundArchive.Load();
            if (sdat == null) { _out.WriteLine($"{game}: no sound archive"); return; }

            int looked = 0, withNotes = 0, empty = 0;
            long notesIn = 0, notesOut = 0;
            var wrong = new List<string>();

            for (int i = 0; i < sdat.Sequences.Count; i++)
            {
                IReadOnlyList<SseqPlayer.Note> notes;
                try { notes = SseqPlayer.ReadNotes(sdat, i); } catch { continue; }
                if (notes == null) continue;
                looked++;
                if (notes.Count == 0) { empty++; continue; }
                withNotes++;

                sdat.SeqNames.TryGetValue(i, out string name);
                var midi = MidiFile.FromNotes(notes, name ?? ("Sequence " + i));
                if (midi == null) { wrong.Add($"{i}: nothing came out"); continue; }

                var (format, tracks, ppqn, back, _) = ReadMidi(midi);
                notesIn += notes.Count;
                notesOut += back.Count;

                if (format != 1) wrong.Add($"{i}: wrote a type {format} file");
                if (ppqn != 480) wrong.Add($"{i}: wrote {ppqn} ticks a beat");
                if (back.Count != notes.Count)
                    wrong.Add($"{i}: put {notes.Count} notes in and got {back.Count} back");

                // Compare on the same footing the file uses.
                var wanted = notes
                    .Select(n => (tick: (long)Math.Round(n.StartSeconds * 480 * 120 / 60.0), pitch: n.Number))
                    .OrderBy(x => x.tick).ThenBy(x => x.pitch).ToList();
                var got = back
                    .Select(n => (tick: n.Tick, pitch: n.Number))
                    .OrderBy(x => x.tick).ThenBy(x => x.pitch).ToList();
                if (!wanted.SequenceEqual(got)) wrong.Add($"{i}: the notes came back at different times or pitches");
            }

            _out.WriteLine($"{game}: {looked} sequences read, {withNotes} had notes, {empty} were empty");
            _out.WriteLine($"  {notesIn} notes in, {notesOut} notes back out of the files");
            foreach (var w in wrong.Take(10)) _out.WriteLine("  wrong: " + w);

            Assert.True(withNotes > 100, $"{game}: only {withNotes} sequences had any notes, too few to prove anything");
            Assert.Empty(wrong);
            Assert.Equal(notesIn, notesOut);
        }

        /// <summary>The check above with the notes moved, to show it can fail. A file written from a
        /// changed note list has to be caught, or "the notes match" means nothing.</summary>
        [Fact]
        public void TheNoteCheckCatchesAChangedNote()
        {
            if (!Directory.Exists(HeartGold)) { _out.WriteLine("HeartGold not unpacked here"); return; }
            new RomInfo("IPKE", HeartGold);
            var sdat = SoundArchive.Load();
            if (sdat == null) { _out.WriteLine("no sound archive"); return; }

            IReadOnlyList<SseqPlayer.Note> notes = null;
            for (int i = 0; i < sdat.Sequences.Count && notes == null; i++)
            {
                var n = SseqPlayer.ReadNotes(sdat, i);
                if (n != null && n.Count > 20) notes = n;
            }
            Assert.NotNull(notes);

            var honest = ReadMidi(MidiFile.FromNotes(notes, "honest"));
            Assert.Equal(notes.Count, honest.notes.Count);

            var meddled = notes.Select(n => new SseqPlayer.Note
            {
                StartSeconds = n.StartSeconds, DurationSeconds = n.DurationSeconds,
                NoLengthGiven = n.NoLengthGiven, Number = n.Number, Velocity = n.Velocity,
                Program = n.Program, Pan = n.Pan, Volume = n.Volume, Track = n.Track,
            }).ToList();
            meddled[0].Number = (meddled[0].Number + 5) % 128;

            var after = ReadMidi(MidiFile.FromNotes(meddled, "meddled"));
            var before = honest.notes.OrderBy(n => n.Tick).ThenBy(n => n.Number).Select(n => n.Number).ToList();
            var now = after.notes.OrderBy(n => n.Tick).ThenBy(n => n.Number).Select(n => n.Number).ToList();
            _out.WriteLine($"moved one note of {notes.Count}; the two lists {(before.SequenceEqual(now) ? "still match" : "differ")}");
            Assert.False(before.SequenceEqual(now), "moving a note changed nothing, so this check cannot fail");
        }


        /// <summary>
        /// A saved MIDI holds the whole tune rather than the few seconds the preview plays.
        /// </summary>
        [Theory]
        [MemberData(nameof(Games))]
        public void ASavedMidiGoesPastThePreviewLength(string code, string path, string game)
        {
            if (!Directory.Exists(path)) { _out.WriteLine($"{game}: not unpacked here"); return; }
            new RomInfo(code, path);
            var sdat = SoundArchive.Load();
            if (sdat == null) { _out.WriteLine($"{game}: no sound archive"); return; }

            int longer = 0, looked = 0;
            double longest = 0;
            for (int i = 0; i < sdat.Sequences.Count; i++)
            {
                var preview = SseqPlayer.ReadNotes(sdat, i, 8.0);
                if (preview == null || preview.Count == 0) continue;
                var whole = SseqPlayer.ReadNotes(sdat, i, AudioEditorViewModel.WholeTuneSeconds);
                if (whole == null) continue;
                looked++;
                if (whole.Count > preview.Count) longer++;
                foreach (var n in whole) longest = Math.Max(longest, n.StartSeconds + n.DurationSeconds);
            }

            // Nothing should be cut off by the reading limit either. A sequence stops on its own at the
            // first backward jump, so one pass is all there is; reading twice as far has to find no more.
            int truncated = 0;
            for (int i = 0; i < sdat.Sequences.Count; i++)
            {
                var whole = SseqPlayer.ReadNotes(sdat, i, AudioEditorViewModel.WholeTuneSeconds);
                if (whole == null || whole.Count == 0) continue;
                var further = SseqPlayer.ReadNotes(sdat, i, AudioEditorViewModel.WholeTuneSeconds * 2);
                if (further != null && further.Count > whole.Count) truncated++;
            }

            _out.WriteLine($"{game}: {looked} sequences, {longer} hold more than the preview shows; "
                         + $"the longest runs {longest:F1} seconds; {truncated} were still cut off at "
                         + "the reading limit");
            Assert.Equal(0, truncated);
            Assert.True(looked > 100, $"{game}: only {looked} sequences were read");
            Assert.True(longer > 50,
                $"{game}: only {longer} sequences got longer when read further, so the preview length is "
                + "probably still being used for the file");
            Assert.True(longest > 20, $"{game}: the longest sequence found was only {longest:F1} seconds");
        }

        /// <summary>Nothing lands on the drum channel, which would be played as percussion by mistake.</summary>
        [Fact]
        public void NoTrackIsWrittenOnTheDrumChannel()
        {
            if (!Directory.Exists(HeartGold)) { _out.WriteLine("HeartGold not unpacked here"); return; }
            new RomInfo("IPKE", HeartGold);
            var sdat = SoundArchive.Load();
            if (sdat == null) { _out.WriteLine("no sound archive"); return; }

            int looked = 0;
            for (int i = 0; i < sdat.Sequences.Count; i++)
            {
                var notes = SseqPlayer.ReadNotes(sdat, i);
                if (notes == null || notes.Count == 0) continue;
                looked++;
                var (_, _, _, back, _) = ReadMidi(MidiFile.FromNotes(notes, "s" + i));
                Assert.DoesNotContain(back, n => n.Channel == 9);
            }
            _out.WriteLine($"{looked} sequences checked, none used the drum channel");
            Assert.True(looked > 100, $"only {looked} sequences were checked");
        }
    }
}
