using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace DSPRE.Avalonia.Data
{
    /// <summary>
    /// Writes one of the game's instrument banks out as a SoundFont, so the same instruments can be
    /// played in any music program.
    ///
    /// A SoundFont is a RIFF file of three parts: what it is called, the recordings themselves, and
    /// the tables saying which recording covers which notes. Everything written here comes from the
    /// bank and the wave archives it points at.
    /// </summary>
    public static class SoundFontWriter
    {
        public sealed class Result
        {
            public byte[] Bytes;
            /// <summary>Why nothing was made, when nothing was.</summary>
            public string Whynot;

            public int Instruments, Regions, Recordings, TonesMade, LeftOut;
            public List<string> Notes = new();

            public string Summary => Whynot ?? $"{Instruments} instruments, {Regions} note ranges, "
                + $"{Recordings} recordings.";
        }

        /// <param name="name">What to call the bank inside the file.</param>
        public static Result Build(SdatArchive sdat, int bankNo, string name)
        {
            var r = new Result();
            if (sdat == null) return Fail(r, "No sound archive is loaded.");
            if (bankNo < 0 || bankNo >= sdat.Banks.Count || sdat.Banks[bankNo] == null)
                return Fail(r, "This game has no instrument bank with that number.");

            List<SbnkInstrument> instruments;
            try { instruments = sdat.GetBankInstruments(bankNo); }
            catch (Exception ex) { return Fail(r, "That bank could not be read: " + ex.Message); }
            if (instruments == null || instruments.Count == 0)
                return Fail(r, "That bank holds no instruments.");

            var slots = sdat.Banks[bankNo].WaveArcNo;
            var waves = new Dictionary<int, List<SwavSample>>();
            List<SwavSample> WavesIn(int slot)
            {
                if (slot < 0 || slot >= slots.Length) return null;
                int arc = slots[slot];
                if (arc == 0xffff || arc < 0 || arc >= sdat.WaveArcs.Count) return null;
                if (waves.TryGetValue(arc, out var got)) return got;
                try { got = sdat.GetWaveArchive(arc); } catch { got = null; }
                waves[arc] = got;
                return got;
            }

            // One entry per distinct recording, so a sample used by ten note ranges is written once.
            var pool = new List<(string Name, SwavSample Sample)>();
            var known = new Dictionary<string, int>();

            var built = new List<(string Name, List<(SbnkRegion Region, int Recording)> Zones)>();
            int leftOut = 0;

            for (int i = 0; i < instruments.Count; i++)
            {
                // A bank's programs are numbered with gaps in them, so some of these are simply not there.
                if (instruments[i]?.Regions == null) continue;

                var zones = new List<(SbnkRegion, int)>();
                foreach (var region in instruments[i].Regions)
                {
                    if (region == null) { leftOut++; continue; }
                    string key;
                    SwavSample sample;
                    if (region.Psg != PsgKind.None)
                    {
                        // A tone generator has no recording of its own, so write out the sound it makes
                        // as one short looping clip and point at that.
                        sample = PsgWaveform.For(region);
                        key = $"psg{(int)region.Psg}-{region.PsgDuty}";
                    }
                    else
                    {
                        var list = WavesIn(region.WaveArcSlot);
                        sample = list != null && region.WaveIndex >= 0 && region.WaveIndex < list.Count
                            ? list[region.WaveIndex] : null;
                        key = $"w{region.WaveArcSlot}-{region.WaveIndex}";
                    }

                    if (sample?.Pcm == null || sample.Pcm.Length == 0) { leftOut++; continue; }

                    if (!known.TryGetValue(key, out int at))
                    {
                        at = pool.Count;
                        known[key] = at;
                        pool.Add((ShortName(region.Psg != PsgKind.None
                            ? $"tone {(int)region.Psg}-{region.PsgDuty}"
                            : $"{name} {pool.Count}"), sample));
                    }
                    zones.Add((region, at));
                }
                if (zones.Count == 0) continue;
                built.Add(($"{name} {i}", zones));
            }

            if (built.Count == 0)
                return Fail(r, "None of that bank's instruments point at a recording this can read, so "
                             + "there would be nothing in the file.");

            r.Instruments = built.Count;
            r.Regions = built.Sum(b => b.Zones.Count);
            r.Recordings = pool.Count;
            r.TonesMade = known.Keys.Count(k => k.StartsWith("psg", StringComparison.Ordinal));
            r.LeftOut = leftOut;

            if (r.TonesMade > 0)
                r.Notes.Add($"{r.TonesMade} of these are the DS's own tone generators, which carry no "
                          + "recording. Each was written out as one short looping clip of the sound it makes.");
            if (leftOut > 0)
                r.Notes.Add($"{leftOut} note ranges point at a recording that is not there, so they were "
                          + "left out. The rest went in.");
            r.Notes.Add("Note ranges, root notes, loop points and sample rates are carried across exactly. "
                      + "The attack, decay, sustain and release are converted from the way the DS counts "
                      + "them to the way a SoundFont does, so they are close rather than identical.");

            try { r.Bytes = Write(name, built, pool); }
            catch (Exception ex) { return Fail(r, "That SoundFont could not be put together: " + ex.Message); }
            return r;
        }

        private static Result Fail(Result r, string why) { r.Whynot = why; return r; }

        // ── the file ──────────────────────────────────────────────────────────────────────────────

        /// <summary>Every sample is followed by this many silent ones, which the format asks for.</summary>
        private const int Padding = 46;

        /// <summary>
        /// Samples of run-up and run-out kept around each recording, outside the part that plays. A
        /// player working out what sits between two samples reaches a little past the loop in both
        /// directions, and the format asks for room to do it in. Filling that room with the sound that
        /// really comes next in the loop is what keeps a held note from clicking at the seam.
        /// </summary>
        private const int Margin = 8;

        private static byte[] Write(string bankName,
            List<(string Name, List<(SbnkRegion Region, int Recording)> Zones)> instruments,
            List<(string Name, SwavSample Sample)> pool)
        {
            // Where each recording lands in the one long run of samples, with room either side of it.
            var starts = new int[pool.Count];
            var ends = new int[pool.Count];
            int at = 0;
            for (int i = 0; i < pool.Count; i++)
            {
                starts[i] = at + Margin;
                ends[i] = starts[i] + pool[i].Sample.Pcm.Length;
                at = ends[i] + Margin + Padding;
            }

            var samples = new byte[at * 2];
            void Put(int slot, short value)
            {
                samples[slot * 2] = (byte)value;
                samples[slot * 2 + 1] = (byte)(value >> 8);
            }

            for (int i = 0; i < pool.Count; i++)
            {
                var s = pool[i].Sample;
                var pcm = s.Pcm;
                for (int n = 0; n < pcm.Length; n++) Put(starts[i] + n, pcm[n]);

                if (!s.Loop) continue;
                // The run-up is what the loop plays just before coming round again, and the run-out is
                // what it plays just after. Both are real sound from inside the loop, so a player
                // reading past the seam hears the loop continue rather than silence.
                int from = Math.Clamp(s.LoopStartSample, 0, pcm.Length - 1);
                int loopLength = pcm.Length - from;
                for (int n = 0; n < Margin; n++)
                {
                    Put(starts[i] - Margin + n, pcm[pcm.Length - Margin + n < from
                        ? from : pcm.Length - Margin + n]);
                    Put(ends[i] + n, pcm[from + n % loopLength]);
                }
            }

            var info = new MemoryStream();
            WriteChunk(info, "ifil", new byte[] { 2, 0, 1, 0 });                 // SoundFont 2.01
            WriteChunk(info, "isng", Zstring("EMU8000"));
            WriteChunk(info, "INAM", Zstring(bankName));
            WriteChunk(info, "ISFT", Zstring("DSPRE"));

            var sdta = new MemoryStream();
            WriteChunk(sdta, "smpl", samples);

            var pdta = new MemoryStream();
            BuildTables(instruments, pool, starts, ends, pdta);

            var body = new MemoryStream();
            Ascii(body, "sfbk");
            WriteList(body, "INFO", info.ToArray());
            WriteList(body, "sdta", sdta.ToArray());
            WriteList(body, "pdta", pdta.ToArray());

            var file = new MemoryStream();
            Ascii(file, "RIFF");
            U32(file, (int)body.Length);
            body.Position = 0;
            body.CopyTo(file);
            return file.ToArray();
        }

        private static void BuildTables(
            List<(string Name, List<(SbnkRegion Region, int Recording)> Zones)> instruments,
            List<(string Name, SwavSample Sample)> pool, int[] starts, int[] ends, MemoryStream pdta)
        {
            // One preset per instrument, each holding one zone that points at the instrument of the same
            // number. Presets are what a music program lists; instruments are what they are made of.
            var phdr = new MemoryStream();
            var pbag = new MemoryStream();
            var pgen = new MemoryStream();
            for (int i = 0; i < instruments.Count; i++)
            {
                Name20(phdr, instruments[i].Name);
                U16(phdr, i % 128);                 // preset number
                U16(phdr, i / 128);                 // bank, once past 128 presets
                U16(phdr, i);                       // first zone of this preset
                U32(phdr, 0); U32(phdr, 0); U32(phdr, 0);

                U16(pbag, i); U16(pbag, 0);         // this zone's first generator, no modulators
                U16(pgen, 41); U16(pgen, i);        // instrument
            }
            Name20(phdr, "EOP");
            U16(phdr, 0); U16(phdr, 0); U16(phdr, instruments.Count);
            U32(phdr, 0); U32(phdr, 0); U32(phdr, 0);
            U16(pbag, instruments.Count); U16(pbag, 0);
            U16(pgen, 0); U16(pgen, 0);

            var inst = new MemoryStream();
            var ibag = new MemoryStream();
            var igen = new MemoryStream();
            int zone = 0, gen = 0;
            foreach (var (name, zones) in instruments)
            {
                Name20(inst, name);
                U16(inst, zone);
                foreach (var (region, recording) in zones)
                {
                    U16(ibag, gen); U16(ibag, 0);
                    zone++;

                    // The note range has to come first and the recording has to come last; the format
                    // says so, and players that trust it get the rest wrong otherwise.
                    U16(igen, 43); U16(igen, (Clamp7(region.LowKey) & 0xFF)
                                           | ((Clamp7(region.HighKey) & 0xFF) << 8));
                    gen++;

                    var shape = NitroEnvelope.Compute(region.Attack, region.Decay,
                                                      region.Sustain, region.Release);
                    U16(igen, 34); U16(igen, (ushort)(short)Timecents(AttackSeconds(shape.AttackRate)));
                    U16(igen, 36); U16(igen, (ushort)(short)Timecents(shape.DecaySeconds));
                    U16(igen, 37); U16(igen, (ushort)(short)Attenuation(shape.SustainLevel));
                    U16(igen, 38); U16(igen, (ushort)(short)Timecents(shape.ReleaseSeconds));
                    gen += 4;

                    U16(igen, 58); U16(igen, Clamp7(region.BaseNote));      // the note it was recorded at
                    gen++;
                    U16(igen, 54); U16(igen, pool[recording].Sample.Loop ? 1 : 0);
                    gen++;
                    U16(igen, 53); U16(igen, recording);                    // last, as the format asks
                    gen++;
                }
            }
            Name20(inst, "EOI");
            U16(inst, zone);
            U16(ibag, gen); U16(ibag, 0);
            U16(igen, 0); U16(igen, 0);

            var shdr = new MemoryStream();
            for (int i = 0; i < pool.Count; i++)
            {
                var s = pool[i].Sample;
                // A recording that does not loop still has to carry loop points, and nothing reads them.
                // Putting them a little inside it keeps other programs from complaining about numbers
                // they are going to ignore anyway.
                int loopStart = s.Loop ? starts[i] + Math.Clamp(s.LoopStartSample, 0, s.Pcm.Length - 1)
                                       : Math.Min(starts[i] + Margin, ends[i]);
                int loopEnd = s.Loop ? ends[i] : Math.Max(loopStart, ends[i] - Margin);
                Name20(shdr, pool[i].Name);
                U32(shdr, starts[i]);
                U32(shdr, ends[i]);
                U32(shdr, loopStart);
                U32(shdr, loopEnd);
                U32(shdr, s.SampleRate > 0 ? s.SampleRate : 22050);
                shdr.WriteByte(60);                 // the note it plays at, set per note range instead
                shdr.WriteByte(0);                  // no tuning correction
                U16(shdr, 0);                       // nothing linked to it
                U16(shdr, 1);                       // one channel
            }
            Name20(shdr, "EOS");
            U32(shdr, 0); U32(shdr, 0); U32(shdr, 0); U32(shdr, 0); U32(shdr, 0);
            shdr.WriteByte(0); shdr.WriteByte(0); U16(shdr, 0); U16(shdr, 0);

            WriteChunk(pdta, "phdr", phdr.ToArray());
            WriteChunk(pdta, "pbag", pbag.ToArray());
            WriteChunk(pdta, "pmod", new byte[10]);     // no modulators, but the table has to be there
            WriteChunk(pdta, "pgen", pgen.ToArray());
            WriteChunk(pdta, "inst", inst.ToArray());
            WriteChunk(pdta, "ibag", ibag.ToArray());
            WriteChunk(pdta, "imod", new byte[10]);
            WriteChunk(pdta, "igen", igen.ToArray());
            WriteChunk(pdta, "shdr", shdr.ToArray());
        }

        // ── turning the DS's numbers into a SoundFont's ───────────────────────────────────────────

        /// <summary>
        /// How long the attack takes, in seconds. The DS keeps a rate rather than a length, so this runs
        /// the hardware's own per-tick curve until the note is all the way up.
        /// </summary>
        internal static double AttackSeconds(double attackRate)
        {
            for (int tick = 1; tick <= 4000; tick++)
                if (NitroEnvelope.AttackGain(attackRate, tick) >= 0.99)
                    return tick * NitroEnvelope.TickSeconds;
            return 4000 * NitroEnvelope.TickSeconds;
        }

        /// <summary>A length in seconds as the format keeps it: twelve hundred per doubling.</summary>
        internal static int Timecents(double seconds)
        {
            if (seconds <= 0.0005) return -12000;               // as near instant as the format goes
            return (int)Math.Clamp(Math.Round(1200.0 * Math.Log2(seconds)), -12000, 8000);
        }

        /// <summary>A gain of 0 to 1 as the format keeps it: tenths of a decibel of quietening.</summary>
        internal static int Attenuation(double gain)
        {
            if (gain >= 1.0) return 0;
            if (gain <= 0.0) return 1440;                       // as quiet as the format goes
            return (int)Math.Clamp(Math.Round(-200.0 * Math.Log10(gain)), 0, 1440);
        }

        private static int Clamp7(int v) => Math.Clamp(v, 0, 127);

        // ── RIFF plumbing ─────────────────────────────────────────────────────────────────────────

        private static void Ascii(Stream s, string four)
        { foreach (char c in four) s.WriteByte((byte)c); }

        private static void U16(Stream s, int v) { s.WriteByte((byte)v); s.WriteByte((byte)(v >> 8)); }
        private static void U32(Stream s, int v)
        {
            s.WriteByte((byte)v); s.WriteByte((byte)(v >> 8));
            s.WriteByte((byte)(v >> 16)); s.WriteByte((byte)(v >> 24));
        }

        private static void WriteChunk(Stream s, string tag, byte[] data)
        {
            Ascii(s, tag);
            U32(s, data.Length);
            s.Write(data, 0, data.Length);
            if ((data.Length & 1) != 0) s.WriteByte(0);         // chunks sit on even boundaries
        }

        private static void WriteList(Stream s, string tag, byte[] data)
        {
            Ascii(s, "LIST");
            U32(s, data.Length + 4);
            Ascii(s, tag);
            s.Write(data, 0, data.Length);
            if ((data.Length & 1) != 0) s.WriteByte(0);
        }

        private static byte[] Zstring(string text)
        {
            var b = Encoding.ASCII.GetBytes(text ?? "");
            var o = new byte[b.Length + (b.Length % 2 == 0 ? 2 : 1)];
            Array.Copy(b, o, b.Length);
            return o;
        }

        /// <summary>Names in these tables are exactly twenty bytes, cut short if need be.</summary>
        private static void Name20(Stream s, string text)
        {
            var b = Encoding.ASCII.GetBytes(ShortName(text));
            for (int i = 0; i < 20; i++) s.WriteByte(i < b.Length ? b[i] : (byte)0);
        }

        private static string ShortName(string text)
        {
            text = (text ?? "").Trim();
            var clean = new string(text.Where(c => c >= 32 && c < 127).ToArray());
            if (clean.Length == 0) clean = "sound";
            return clean.Length > 19 ? clean.Substring(0, 19) : clean;
        }
    }
}
