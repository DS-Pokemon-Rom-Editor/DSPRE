using System;
using System.Collections.Generic;
using System.IO;
using DSPRE.HgEngine;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.Data
{
    /// <summary>Finds and reads the ROM's own sound archive. </summary>
    public static class SoundArchive
    {
        private static SdatArchive _cached;
        private static string _cachedFor;

        /// <summary>The loaded ROM's sound archive, or null when it cannot be read.</summary>
        public static SdatArchive Load()
        {
            string path = PathFor();
            if (path == null) return null;
            if (_cached != null && _cachedFor == path) return _cached;

            try
            {
                var sdat = SdatArchive.Parse(File.ReadAllBytes(path));
                if (sdat == null || sdat.Sequences.Count == 0) return null;
                // A different ROM keeps its cry sequence at its own index, so that has to be found again.
                _cached = sdat; _cachedFor = path; _crySequence = null;
                return sdat;
            }
            catch (Exception ex) { AppLogger.Error("Sound archive could not be read: " + ex.Message); return null; }
        }

        /// <summary>Forgets what was read, for when a different ROM is opened.</summary>
        public static void Reset() { _cached = null; _cachedFor = null; _crySequence = null; }

        /// <summary>The one short sequence every cry is played from. </summary>
        public const string CrySequenceName = "SEQ_PV";

        private static int? _crySequence;

        /// <summary>Which sequence the cries are played from, or -1 when this ROM has no such sequence.</summary>
        public static int CrySequence(SdatArchive sdat)
        {
            if (_crySequence != null) return _crySequence.Value;
            int found = -1;
            if (sdat?.SeqNames != null)
                foreach (var kv in sdat.SeqNames)
                    if (string.Equals(kv.Value, CrySequenceName, StringComparison.Ordinal)) { found = kv.Key; break; }
            _crySequence = found;
            return found;
        }

        /// <summary>
        /// How a cry is put together, for telling somebody what they are about to change.
        /// </summary>
        public const string HowItWorks =
            "Every cry plays the same short sequence; what makes them different is the sample it is given, "
            + "one per Pokemon. Taking a cry out gives you that sample on its own, as ordinary sound.";

        /// <summary>The prefix the sound archive puts on the name of every bank that holds a cry.</summary>
        public const string CryBankPrefix = "BANK_PV";

        /// <summary>
        /// The cries this ROM actually has, as the bank numbers the game plays them with, in order.
        /// </summary>
        public static List<int> CryBanks()
        {
            var list = new List<int>();
            var sdat = Load();
            if (sdat == null) return list;
            foreach (var kv in sdat.BankNames)
                if (kv.Value != null && kv.Value.StartsWith(CryBankPrefix, StringComparison.Ordinal)
                    && kv.Key > 0 && kv.Key < sdat.Banks.Count && sdat.Banks[kv.Key] != null)
                    list.Add(kv.Key);
            list.Sort();
            return list;
        }

        /// <summary>The prefix on the name of every wave archive that holds one species' cry.</summary>
        public const string CryWaveArcPrefix = "WAVE_ARC_PV";

        /// <summary>
        /// hg-engine moves cries off the per-species banks and onto one wave archive per species, keeping
        /// only BANK_PV001/002. See scripts/rebuild_json.py in a checkout, which deletes every other
        /// BANK_PV entry, and narcs.mk, which builds WAVE_ARC_PV&lt;n&gt; from sound/cries/&lt;n&gt;.wav.
        /// </summary>
        private static bool CriesLiveInWaveArchives => HgEngineProject.IsActive;

        /// <summary>
        /// The cry number the game would play for a species. hg-engine's GrabCryNumSpeciesForm returns the
        /// species itself (its mega branch cancels out, both bases being SPECIES_MAX_MON_NUM + 1), except
        /// for the slots between the old last species and its first new one, which have no cry of their own.
        /// </summary>
        private const int LimboFirst = 494, LimboLast = 543, LimboFallback = 1;

        public static int CryNumberFor(int species) =>
            species >= LimboFirst && species <= LimboLast ? LimboFallback : species;

        /// <summary>The wave archive holding a cry number, found by name because the archives are appended
        /// past the vanilla count and their index stops matching their number after the first 493.</summary>
        private static int CryWaveArcByName(SdatArchive sdat, int cryNumber)
        {
            if (sdat?.WaveArcNames == null || cryNumber <= 0) return -1;
            string want = CryWaveArcPrefix + cryNumber.ToString("D3");
            foreach (var kv in sdat.WaveArcNames)
                if (string.Equals(kv.Value, want, StringComparison.Ordinal)
                    && kv.Key >= 0 && kv.Key < sdat.WaveArcs.Count && sdat.WaveArcs[kv.Key] != null)
                    return kv.Key;
            return -1;
        }

        /// <summary>The bank a cry is played with. hg-engine keeps one; vanilla has one per species.</summary>
        private static int CryBankFor(SdatArchive sdat, int species)
        {
            if (!CriesLiveInWaveArchives) return species;
            foreach (var kv in sdat.BankNames)
                if (kv.Value != null && kv.Value.StartsWith(CryBankPrefix, StringComparison.Ordinal)
                    && kv.Key > 0 && kv.Key < sdat.Banks.Count && sdat.Banks[kv.Key] != null)
                    return kv.Key;
            return -1;
        }

        /// <summary>Every cry this ROM has, as the numbers the game plays them with, in order.</summary>
        public static List<int> CryNumbers()
        {
            var list = new List<int>();
            var sdat = Load();
            if (sdat == null) return list;

            if (!CriesLiveInWaveArchives)
            {
                foreach (int bank in CryBanks()) list.Add(bank);
                return list;
            }

            foreach (var kv in sdat.WaveArcNames)
            {
                if (kv.Value == null || !kv.Value.StartsWith(CryWaveArcPrefix, StringComparison.Ordinal)) continue;
                if (kv.Key < 0 || kv.Key >= sdat.WaveArcs.Count || sdat.WaveArcs[kv.Key] == null) continue;
                string digits = kv.Value.Substring(CryWaveArcPrefix.Length);
                if (int.TryParse(digits, out int n) && n > 0) list.Add(n);
            }
            list.Sort();
            return list;
        }

        /// <summary>Which wave archive holds a species' cry, or -1 when it has none.</summary>
        public static int CryWaveArchive(int species)
        {
            var sdat = Load();
            if (sdat == null || species <= 0) return -1;

            if (CriesLiveInWaveArchives) return CryWaveArcByName(sdat, CryNumberFor(species));

            if (species >= sdat.Banks.Count) return -1;
            var bank = sdat.Banks[species];
            if (bank == null) return -1;
            foreach (int w in bank.WaveArcNo)
                if (w != 0xffff && w >= 0 && w < sdat.WaveArcs.Count) return w;
            return -1;
        }

        /// <summary>The sample a species' cry is made from, as it sits in the ROM. </summary>
        public static SwavSample CrySample(int species)
        {
            var sdat = Load();
            int arc = CryWaveArchive(species);
            if (sdat == null || arc < 0) return null;
            var waves = sdat.GetWaveArchive(arc);
            return waves != null && waves.Count > 0 ? waves[0] : null;
        }

        /// <summary>Writes a species' cry out as a WAV. False when this ROM has no cry for it.</summary>
        public static bool ExportCry(int species, string path)
        {
            var sample = CrySample(species);
            if (sample == null || sample.Pcm == null || sample.Pcm.Length == 0) return false;
            File.WriteAllBytes(path, CryFiles.WriteWav(sample.Pcm, sample.SampleRate));
            return true;
        }

        /// <summary>
        /// Puts a WAV in as a species' cry, writing the sound archive back to the ROM folder.
        /// </summary>
        public static bool ImportCry(int species, string path, out string problem)
            => ImportCry(species, path, out problem, out _);

        /// <param name="note">Set when the cry went somewhere other than the ROM, and the user needs to
        /// know what still has to happen for it to be heard in game.</param>
        public static bool ImportCry(int species, string path, out string problem, out string note)
        {
            problem = null;
            note = null;

            // hg-engine builds the whole sound archive from sound/cries, so the cry belongs there; writing
            // it into the ROM's copy would last until the next compile and no longer.
            if (CriesLiveInWaveArchives) return ImportCryToCheckout(species, path, out problem, out note);

            var sdat = Load();
            string sdatPath = PathFor();
            if (sdat == null || sdatPath == null) { problem = "This ROM has no sound archive to write to."; return false; }
            if (RefusedByHgEngine(sdatPath, out problem)) return false;

            int arc = CryWaveArchive(species);
            if (arc < 0) { problem = "This ROM has no cry for that Pokemon to replace."; return false; }
            var arcInfo = sdat.WaveArcs[arc];
            if (arcInfo == null) { problem = "This ROM has no cry for that Pokemon to replace."; return false; }

            byte[] file;
            try { file = File.ReadAllBytes(path); }
            catch (Exception ex) { problem = "That file could not be read: " + ex.Message; return false; }

            var pcm = CryFiles.ReadWav(file, out int rate, out problem);
            if (pcm == null) return false;
            if (pcm.Length == 0) { problem = "That WAV has no sound in it."; return false; }

            // Keep whatever else was in the archive; a cry archive holds one wave, but do not assume it.
            var waves = sdat.GetWaveArchive(arc) ?? new System.Collections.Generic.List<SwavSample>();
            var replaced = new System.Collections.Generic.List<SwavSample>(waves);
            var fresh = new SwavSample { SampleRate = rate, Loop = false, LoopStartSample = 0, Pcm = pcm };
            if (replaced.Count == 0) replaced.Add(fresh); else replaced[0] = fresh;

            byte[] rebuilt = CryFiles.BuildArchive(replaced);
            byte[] whole = sdat.ReplaceFile(arcInfo.FileId, rebuilt);
            if (whole == null) { problem = "The sound archive could not be rewritten."; return false; }

            try { File.WriteAllBytes(sdatPath, whole); }
            catch (Exception ex) { problem = "The sound archive could not be saved: " + ex.Message; return false; }

            Reset();          // read it again next time, so what plays is what is now on disk
            return true;
        }

        /// <summary>Puts a cry into the linked checkout's sound/cries, which is what its build reads.</summary>
        private static bool ImportCryToCheckout(int species, string path, out string problem, out string note)
        {
            problem = null;
            note = null;

            int cry = CryNumberFor(species);
            if (cry != species)
            {
                problem = "That slot has no cry of its own in hg-engine; it falls back to another one.";
                return false;
            }

            byte[] file;
            try { file = File.ReadAllBytes(path); }
            catch (Exception ex) { problem = "That file could not be read: " + ex.Message; return false; }

            // Parsed only to refuse a file its build would choke on; what gets written is the WAV itself,
            // since the build converts it with its own tool and settings.
            var pcm = CryFiles.ReadWav(file, out _, out problem);
            if (pcm == null) return false;
            if (pcm.Length == 0) { problem = "That WAV has no sound in it."; return false; }

            string rel = Path.Combine("sound", "cries", cry.ToString("D3") + ".wav");
            string full = Path.Combine(HgEngineProject.RepoPathUnc, rel);
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(full));
                File.WriteAllBytes(full, file);
            }
            catch (Exception ex) { problem = "That cry could not be saved to the checkout: " + ex.Message; return false; }

            note = "Saved to " + rel.Replace(Path.DirectorySeparatorChar, '/')
                 + ". Compile the ROM to hear it in game; the copy in the ROM is still the old one until then.";
            return true;
        }

        /// <summary>
        /// A Pokemon's cry, as sound ready to play, or null when this ROM has nothing for that species.
        /// </summary>
        public static short[] RenderCry(int species, int sampleRate = 32000)
        {
            var sdat = Load();
            if (sdat == null || species <= 0) return null;

            int seq = CrySequence(sdat);
            if (seq < 0) return null;

            int bank = CryBankFor(sdat, species);
            if (bank < 0 || bank >= sdat.Banks.Count || sdat.Banks[bank] == null) return null;

            // hg-engine plays every cry through the one surviving bank, with the species' own wave archive
            // supplying the sound; vanilla's bank already carries it.
            int waveArc = CriesLiveInWaveArchives ? CryWaveArchive(species) : -1;
            if (CriesLiveInWaveArchives && waveArc < 0) return null;

            // A cry is one short sample, so there is no reason to render a long tail for it.
            return SseqPlayer.Render(sdat, seq, sampleRate, 3.0, bank, waveArc);
        }


        // ── the samples that are not cries ─────────────────────────────────────────────────────────

        /// <summary>Every wave archive that is not a cry bank's, with how many sounds it holds.</summary>
        public static List<(int Arc, string Name, int Count)> SampleArchives()
        {
            var found = new List<(int, string, int)>();
            var sdat = Load();
            if (sdat == null) return found;

            // Which archives are already listed as cries, so they are not offered a second time as
            // ordinary samples. On hg-engine that is one archive per cry rather than one per cry bank.
            var cryArcs = new HashSet<int>();
            if (CriesLiveInWaveArchives)
            {
                foreach (int cry in CryNumbers())
                {
                    int w = CryWaveArcByName(sdat, cry);
                    if (w >= 0) cryArcs.Add(w);
                }
            }
            else
            {
                foreach (int b in CryBanks())
                {
                    if (b < 0 || b >= sdat.Banks.Count || sdat.Banks[b] == null) continue;
                    foreach (int w in sdat.Banks[b].WaveArcNo)
                        if (w != 0xffff && w >= 0) cryArcs.Add(w);
                }
            }

            for (int i = 0; i < sdat.WaveArcs.Count; i++)
            {
                if (sdat.WaveArcs[i] == null || cryArcs.Contains(i)) continue;
                int n;
                try { n = sdat.GetWaveArchive(i)?.Count ?? 0; } catch { continue; }
                if (n == 0) continue;
                string name = sdat.WaveArcNames.TryGetValue(i, out var nm) && !string.IsNullOrWhiteSpace(nm)
                    ? nm : "Wave archive " + i;
                found.Add((i, name, n));
            }
            return found;
        }

        /// <summary>One sample as it sits in the ROM, or null when there is no such sample.</summary>
        public static SwavSample Sample(int waveArc, int index)
        {
            var sdat = Load();
            if (sdat == null || waveArc < 0 || waveArc >= sdat.WaveArcs.Count) return null;
            List<SwavSample> waves;
            try { waves = sdat.GetWaveArchive(waveArc); } catch { return null; }
            if (waves == null || index < 0 || index >= waves.Count) return null;
            return waves[index];
        }

        /// <summary>Writes one sample out as a WAV. False when there is nothing there to write.</summary>
        public static bool ExportSample(int waveArc, int index, string path)
        {
            var sample = Sample(waveArc, index);
            if (sample?.Pcm == null || sample.Pcm.Length == 0) return false;
            File.WriteAllBytes(path, CryFiles.WriteWav(sample.Pcm, sample.SampleRate));
            return true;
        }

        /// <summary>Puts a WAV in over one sample, keeping the rest of its archive as it was. </summary>
        public static bool ImportSample(int waveArc, int index, string path, out string problem)
        {
            problem = null;
            var sdat = Load();
            string sdatPath = PathFor();
            if (sdat == null || sdatPath == null) { problem = "This ROM has no sound archive to write to."; return false; }
            if (RefusedByHgEngine(sdatPath, out problem)) return false;
            if (waveArc < 0 || waveArc >= sdat.WaveArcs.Count || sdat.WaveArcs[waveArc] == null)
            { problem = "There is no such set of sounds in this ROM."; return false; }

            List<SwavSample> waves;
            try { waves = sdat.GetWaveArchive(waveArc); } catch { waves = null; }
            if (waves == null || index < 0 || index >= waves.Count)
            { problem = "There is no such sound in that set to replace."; return false; }

            byte[] file;
            try { file = File.ReadAllBytes(path); }
            catch (Exception ex) { problem = "That file could not be read: " + ex.Message; return false; }

            var pcm = CryFiles.ReadWav(file, out int rate, out problem);
            if (pcm == null) return false;
            if (pcm.Length == 0) { problem = "That WAV has no sound in it."; return false; }

            // Keep whatever looping the sample had. An instrument that loops and is replaced by one that
            // does not stops sounding when the note is still being held.
            var old = waves[index];
            var replaced = new List<SwavSample>(waves);
            replaced[index] = new SwavSample
            {
                SampleRate = rate,
                Loop = old.Loop,
                LoopStartSample = old.Loop && old.LoopStartSample < pcm.Length ? old.LoopStartSample : 0,
                Pcm = pcm,
                // Back in the form the slot was kept in. Writing a whole sample where the game expects a
                // squeezed one, or the other way round, is read as noise.
                Encoding = old.Encoding,
            };

            byte[] rebuilt = CryFiles.BuildArchive(replaced);
            byte[] whole = sdat.ReplaceFile(sdat.WaveArcs[waveArc].FileId, rebuilt);
            if (whole == null) { problem = "The sound archive could not be rewritten."; return false; }

            try { File.WriteAllBytes(sdatPath, whole); }
            catch (Exception ex) { problem = "The sound archive could not be saved: " + ex.Message; return false; }

            Reset();
            return true;
        }

        /// <summary>hg-engine rebuilds the sound archive from its own cries, so an edit here would not last.</summary>
        private static bool RefusedByHgEngine(string sdatPath, out string problem)
        {
            problem = null;
            string archive = DSPRE.HgEngine.HgEngineOwnedFiles.ArchiveOfPath(sdatPath);
            var rule = DSPRE.HgEngine.HgEngineOwnedFiles.RuleForArchive(archive);
            if (rule == null || !rule.ReplacesWholeArchive) return false;

            problem = $"hg-engine builds the {rule.Label} from {rule.SourceDirRelPath} on every build, "
                    + "so anything changed here is overwritten the next time you compile.";
            return true;
        }

        private static string PathFor()
        {
            try
            {
                if (string.IsNullOrEmpty(workDir)) return null;
                string name = gameFamily switch
                {
                    GameFamilies.HGSS => "gs_sound_data.sdat",
                    GameFamilies.Plat => "pl_sound_data.sdat",
                    _ => "sound_data.sdat",
                };
                // The ROM's own filesystem sits one level in, named for the layout: ds-rom uses files,
                // a DSPRE ndstool project data, and an hg-engine checkout's own tree root.
                foreach (string root in new[] { "files", "data", "root" })
                {
                    string path = Path.Combine(workDir, root, "data", "sound", name);
                    if (File.Exists(path)) return path;
                }
                return null;
            }
            catch { return null; }
        }
    }
}
