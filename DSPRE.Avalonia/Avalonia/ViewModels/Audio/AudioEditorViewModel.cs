using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using DSPRE.Avalonia.Data;
using DSPRE.Editors;

namespace DSPRE.Avalonia.ViewModels.Audio
{
    /// <summary>One thing you can listen to, whichever part of the ROM it came from.</summary>
    public sealed class AudioItem : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>Where to find it: a sequence number, or a species number for a cry.</summary>
        public int Number;

        /// <summary>Whether this is a cry, which is the one kind made from a sample of its own.</summary>
        public bool IsCry;

        /// <summary>Whether this is one of the ROM's other sounds: an instrument a tune plays, or the
        /// noise a sound effect is made of. Like a cry, it is a sample, so it can be replaced.</summary>
        public bool IsSample;

        /// <summary>For a sample, which set it lives in and where in that set.</summary>
        public int WaveArc = -1, SampleIndex = -1;

        public string Name = "";
        public string Detail = "";

        /// <summary>For a cry, the Pokemon number its bank is named after, which is not always the bank's
        /// own number: HeartGold's bank 474 is BANK_PV504, Rotom's Wash form.</summary>
        public int NamedNumber;

        private bool _isPending;
        /// <summary>Imported and not saved yet.</summary>
        public bool IsPending
        {
            get => _isPending;
            set
            {
                if (_isPending == value) return;
                _isPending = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsPending)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Label)));
            }
        }

        public string Label => $"{Number,5}  {Name}" + (_isPending ? "  (not saved)" : "");
    }

    /// <summary>Everything the ROM can play, in one place.</summary>
    public class AudioEditorViewModel : INotifyPropertyChanged, IEditorWithUnsavedChanges
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
        private bool Set<T>(ref T field, T value, [CallerMemberName] string n = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value; OnPropertyChanged(n); return true;
        }

        public ObservableCollection<AudioItem> Cries { get; } = new();
        public ObservableCollection<AudioItem> Music { get; } = new();
        public ObservableCollection<AudioItem> Fanfares { get; } = new();
        public ObservableCollection<AudioItem> Effects { get; } = new();
        public ObservableCollection<AudioItem> Sounds { get; } = new();

        private readonly List<AudioItem> _allCries = new();
        private readonly List<AudioItem> _allMusic = new();
        private readonly List<AudioItem> _allFanfares = new();
        private readonly List<AudioItem> _allEffects = new();
        private readonly List<AudioItem> _allSounds = new();

        public AudioEditorViewModel() : this(null) { }

        private enum Player { None, Music, Fanfare, Effect }

        /// <summary>
        /// Which tab a sequence belongs on, from the player the game hands it to rather than from its name.
        /// </summary>
        private static Player PlayerOf(SdatArchive sdat, int seq, string name)
        {
            int player = -1;
            if (seq >= 0 && seq < sdat.Sequences.Count && sdat.Sequences[seq] != null)
                player = sdat.Sequences[seq].PlayerNo;

            switch (player)
            {
                case 1: case 7: return Player.Music;
                case 2: return Player.Fanfare;
                case 3: case 4: case 5: case 6: return Player.Effect;
                // The cry player. What is left on it is the machinery around a cry rather than music, so
                // it goes with the rest of the noises.
                case 0: return Player.Effect;
            }

            if (name.StartsWith("SEQ_ME_", StringComparison.Ordinal)) return Player.Fanfare;
            if (name.StartsWith("SEQ_SE_", StringComparison.Ordinal)) return Player.Effect;
            if (name.StartsWith("SEQ_PV", StringComparison.Ordinal)) return Player.Effect;
            if (name.StartsWith("SEQ_", StringComparison.Ordinal)) return Player.Music;
            return Player.None;
        }

        public AudioEditorViewModel(IReadOnlyList<string> pokemonNames)
        {
            var sdat = SoundArchive.Load();
            if (sdat == null) { Status = "This ROM has no sound archive."; return; }

            foreach (var kv in sdat.SeqNames.OrderBy(k => k.Key))
            {
                string name = kv.Value ?? "";
                var item = new AudioItem { Number = kv.Key, Name = name };

                // The cry sequence is what the Cries tab stands for, so it is not listed twice.
                if (name == SoundArchive.CrySequenceName) continue;

                switch (PlayerOf(sdat, kv.Key, name))
                {
                    case Player.Fanfare: _allFanfares.Add(item); break;
                    case Player.Effect: _allEffects.Add(item); break;
                    case Player.Music: _allMusic.Add(item); break;
                    default: break;
                }
            }

            // A cry belongs to a Pokemon, not to a sequence, so it is listed by the number the game plays
            // it with: the bank on a vanilla ROM, the wave archive on an hg-engine one.
            foreach (int cry in SoundArchive.CryNumbers())
            {
                string label = pokemonNames != null && cry > 0 && cry < pokemonNames.Count
                               && !string.IsNullOrWhiteSpace(pokemonNames[cry])
                    ? pokemonNames[cry]
                    : "PV" + cry.ToString("D3");

                _allCries.Add(new AudioItem { Number = cry, Name = label, IsCry = true, NamedNumber = cry });
            }

            // The samples that are not cries. These are what the tunes and the sound effects are actually
            // made of, and nothing in DSPRE could reach them before.
            foreach (var set in SoundArchive.SampleArchives())
            {
                string shown = set.Name.StartsWith("WAVE_ARC_", StringComparison.Ordinal)
                    ? set.Name.Substring("WAVE_ARC_".Length) : set.Name;
                for (int i = 0; i < set.Count; i++)
                    _allSounds.Add(new AudioItem
                    {
                        Number = set.Arc,
                        Name = set.Count == 1 ? shown : shown + "  " + i,
                        IsSample = true, WaveArc = set.Arc, SampleIndex = i,
                        Detail = set.Name,
                    });
            }

            ApplyFilter();
            Status = $"{_allCries.Count} cries, {_allMusic.Count} tunes, {_allFanfares.Count} fanfares, "
                   + $"{_allEffects.Count} sound effects, {_allSounds.Count} sounds they are made of.";
        }

        // ── narrowing the lists down ────────────────────────────────────────────────

        private string _search = "";
        /// <summary>Types into all four lists at once, since somebody rarely knows which tab a name is on.</summary>
        public string Search
        {
            get => _search;
            set { if (Set(ref _search, value)) ApplyFilter(); }
        }

        private void ApplyFilter()
        {
            void Fill(ObservableCollection<AudioItem> into, List<AudioItem> from)
            {
                into.Clear();
                foreach (var i in from)
                    if (string.IsNullOrWhiteSpace(_search)
                     || i.Name.IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0
                     || i.Number.ToString().Contains(_search))
                        into.Add(i);
            }
            Fill(Cries, _allCries);
            Fill(Music, _allMusic);
            Fill(Fanfares, _allFanfares);
            Fill(Effects, _allEffects);
            Fill(Sounds, _allSounds);
            OnPropertyChanged(nameof(FoundSummary));
        }

        public string FoundSummary => string.IsNullOrWhiteSpace(_search)
            ? ""
            : $"{Cries.Count + Music.Count + Fanfares.Count + Effects.Count + Sounds.Count} "
              + $"match “{_search}”";

        // ── what is picked ──────────────────────────────────────────────────────────

        // Each tab remembers what is picked in it. They cannot share one, because picking in one list
        // makes the other three drop their own choice, and that would immediately wipe the new one.
        private AudioItem _selectedCry, _selectedMusic, _selectedFanfare, _selectedEffect,
                          _selectedSound;

        public AudioItem SelectedCry
        {
            get => _selectedCry;
            set { if (Set(ref _selectedCry, value)) SelectionChanged(); }
        }
        public AudioItem SelectedMusic
        {
            get => _selectedMusic;
            set { if (Set(ref _selectedMusic, value)) SelectionChanged(); }
        }
        public AudioItem SelectedFanfare
        {
            get => _selectedFanfare;
            set { if (Set(ref _selectedFanfare, value)) SelectionChanged(); }
        }
        public AudioItem SelectedEffect
        {
            get => _selectedEffect;
            set { if (Set(ref _selectedEffect, value)) SelectionChanged(); }
        }
        public AudioItem SelectedSound
        {
            get => _selectedSound;
            set { if (Set(ref _selectedSound, value)) SelectionChanged(); }
        }

        private int _tab;
        /// <summary>Which tab is open, which is what decides whose choice the buttons act on.</summary>
        public int SelectedTab
        {
            get => _tab;
            set { if (Set(ref _tab, value)) SelectionChanged(); }
        }

        /// <summary>Opens on one Pokemon's cry, for arriving here from the Pokemon editor. </summary>
        public void ShowCryFor(int species)
        {
            Search = "";
            var row = Cries.FirstOrDefault(c => c.NamedNumber == species)
                   ?? Cries.FirstOrDefault(c => c.Number == species);
            if (row == null) return;
            SelectedTab = 0;
            SelectedCry = row;
        }

        private void SelectionChanged()
        {
            OnPropertyChanged(nameof(Selected));
            OnPropertyChanged(nameof(CanPlay));
            OnPropertyChanged(nameof(CanImport));
            OnPropertyChanged(nameof(CanSaveMidi));
            OnPropertyChanged(nameof(CanSaveSoundFont));
            OnPropertyChanged(nameof(SaveSoundFontHelp));
            OnPropertyChanged(nameof(SaveMidiHelp));
            OnPropertyChanged(nameof(SelectedDescription));
        }

        /// <summary>Whatever is picked on the tab that is open.</summary>
        public AudioItem Selected => _tab switch
        {
            0 => _selectedCry,
            1 => _selectedMusic,
            2 => _selectedFanfare,
            3 => _selectedEffect,
            _ => _selectedSound,
        };

        public bool CanPlay => Selected != null;

        /// <summary>A sample can be replaced. A sequence cannot, because it is notes rather than sound.</summary>
        public bool CanImport => Selected != null && (Selected.IsCry || Selected.IsSample);

        public string SelectedDescription
        {
            get
            {
                var it = Selected;
                if (it == null) return "Pick something to hear it.";
                if (it.IsCry && it.IsPending) return it.Name + ". The new cry is not saved yet.";
                if (it.IsSample && it.IsPending) return $"{it.Detail}, sound {it.SampleIndex}. The new sound is not saved yet.";
                if (it.IsCry) return it.Name + ". A cry is a sample of its own, so it can be replaced.";
                if (it.IsSample)
                    return $"{it.Detail}, sound {it.SampleIndex}. One of the sounds the game is built from, so it "
                         + "can be replaced. More than one tune or effect may use it.";
                return it.Name + ". This is written-out notes played on the game's own instruments, so it "
                     + "can be saved as sound but a WAV cannot be put in its place. The sounds it plays "
                     + "are on the Sounds tab, and those can be replaced.";
            }
        }

        /// <summary>Why the Import button is on for a sample and off for a sequence.</summary>
        public string ImportHelp =>
            (Selected != null && Selected.IsSample
                ? "Put a WAV in over this sound.\n\n"
                : "Put a WAV in as this Pokémon's cry.\n\n")
            + SoundArchive.HowItWorks + "\n\n"
            + CryFiles.AcceptedFormat + "\n\n"
            + "It is squeezed down the way the games squeeze their own sounds, so it takes about the same "
            + "room. That costs a little detail, so put a sound in once from your own source rather than "
            + "exporting and importing the same one repeatedly.\n\n"
            + "Cries and the sounds on the Sounds tab can be replaced this way. The music, fanfares and "
            + "sound effects cannot: they are written-out notes rather than sound, and what they play is "
            + "on the Sounds tab.";

        private string _status = "";
        public string Status { get => _status; set => Set(ref _status, value); }

        // ── imports waiting for Save ────────────────────────────────────────────────

        // On hg-engine an imported cry is a checkout file, so it is held here until the user saves.
        private readonly Dictionary<AudioItem, SoundArchive.PendingCry> _pendingCries = new();

        // Everything else replaces a sample in the ROM's sound archive, held in the order it was imported.
        private sealed class HeldSample
        {
            public AudioItem Item;
            public SoundArchive.PendingSample Sample;
        }
        private readonly List<HeldSample> _pendingSamples = new();

        // Rendering runs off the UI thread, so the held list and the archive built from it share a lock.
        private readonly object _heldLock = new();
        private SdatArchive _withHeld;
        private int _heldVersion;

        public bool HasUnsavedChanges
        {
            get { lock (_heldLock) return _pendingCries.Count + _pendingSamples.Count > 0; }
        }

        public string UnsavedChangesDescription
        {
            get
            {
                int cries, sounds;
                lock (_heldLock)
                {
                    cries = _pendingCries.Count + _pendingSamples.Count(h => h.Item.IsCry);
                    sounds = _pendingSamples.Count(h => !h.Item.IsCry);
                }
                string c = cries == 1 ? "1 imported cry" : $"{cries} imported cries";
                string s = sounds == 1 ? "1 imported sound" : $"{sounds} imported sounds";
                return cries > 0 && sounds > 0 ? c + " and " + s : sounds > 0 ? s : c;
            }
        }

        /// <summary>Holds a cry until Save, replacing one already held for the same Pokemon.</summary>
        public void StageCry(AudioItem item, SoundArchive.PendingCry cry)
        {
            if (item == null || cry == null) return;
            lock (_heldLock) _pendingCries[item] = cry;
            item.IsPending = true;
            PendingChanged();
            Status = $"Ready to save to {cry.RelPath}.";
        }

        /// <summary>Holds a replacement sample until Save, replacing one already held for the same slot.</summary>
        public void StageSample(AudioItem item, SoundArchive.PendingSample sample)
        {
            if (item == null || sample?.Sample == null) return;
            lock (_heldLock)
            {
                var same = _pendingSamples.FirstOrDefault(h => h.Sample.WaveArc == sample.WaveArc && h.Sample.Index == sample.Index);
                if (same == null) _pendingSamples.Add(new HeldSample { Item = item, Sample = sample });
                else
                {
                    // Two rows can reach one slot, and only the latest import will be written there.
                    if (!ReferenceEquals(same.Item, item)) same.Item.IsPending = false;
                    same.Item = item;
                    same.Sample = sample;
                }
                _withHeld = null; _heldVersion++;
            }
            item.IsPending = true;
            PendingChanged();
            Status = "Imported. Save writes it to the ROM.";
        }

        /// <summary>The sound archive as it will be once saved, which is what is played until then.</summary>
        private SdatArchive Archive()
        {
            List<SoundArchive.PendingSample> held;
            int version;
            lock (_heldLock)
            {
                if (_pendingSamples.Count == 0) return SoundArchive.Load();
                if (_withHeld != null) return _withHeld;
                held = _pendingSamples.Select(h => h.Sample).ToList();
                version = _heldVersion;
            }

            // Built outside the lock: encoding takes long enough that the UI thread must not wait on it.
            var built = SoundArchive.WithSamples(held, out string problem);
            if (built == null)
            {
                AppLogger.Error("Held sounds could not be previewed: " + problem);
                return SoundArchive.Load();
            }
            lock (_heldLock) if (version == _heldVersion) _withHeld = built;
            return built;
        }

        /// <summary>Writes everything held: the sound archive in one rewrite, then any checkout cries. Null on
        /// success, otherwise why not, with whatever was not written still held.</summary>
        public string Save()
        {
            if (!HasUnsavedChanges) return null;

            List<HeldSample> samples;
            lock (_heldLock) samples = _pendingSamples.ToList();
            if (samples.Count > 0)
            {
                if (!SoundArchive.WriteSamples(samples.Select(h => h.Sample).ToList(), out string problem))
                {
                    PendingChanged();
                    return problem;
                }
                lock (_heldLock) { _pendingSamples.Clear(); _withHeld = null; _heldVersion++; }
                foreach (var h in samples) h.Item.IsPending = false;
            }

            bool wroteCheckout = false;
            List<KeyValuePair<AudioItem, SoundArchive.PendingCry>> cries;
            lock (_heldLock) cries = _pendingCries.ToList();
            foreach (var kv in cries)
            {
                if (!SoundArchive.WriteCheckoutCry(kv.Value, out string problem))
                {
                    PendingChanged();
                    return problem;
                }
                lock (_heldLock) _pendingCries.Remove(kv.Key);
                kv.Key.IsPending = false;
                wroteCheckout = true;
            }
            PendingChanged();
            Status = wroteCheckout ? "Saved. Compile the ROM to hear the new cries in game." : "Saved.";
            return null;
        }

        public void SaveChanges() => Save();

        public Task<bool> SaveChangesAsync()
        {
            string problem = Save();
            return problem != null
                ? Task.FromException<bool>(new InvalidOperationException(problem))
                : Task.FromResult(!HasUnsavedChanges);
        }

        public void DiscardChanges()
        {
            List<AudioItem> items;
            lock (_heldLock)
            {
                items = _pendingCries.Keys.Concat(_pendingSamples.Select(h => h.Item)).ToList();
                _pendingCries.Clear();
                _pendingSamples.Clear();
                _withHeld = null; _heldVersion++;
            }
            foreach (var item in items) item.IsPending = false;
            PendingChanged();
        }

        private void PendingChanged()
        {
            OnPropertyChanged(nameof(HasUnsavedChanges));
            OnPropertyChanged(nameof(SelectedDescription));
        }

        // ── doing something with it ─────────────────────────────────────────────────

        /// <summary>The sound of whatever is picked, ready to play or save.</summary>
        public short[] RenderSelected(int playAt = 32000)
        {
            var item = Selected;
            if (item == null) return null;

            // A checkout cry only becomes part of the archive when the ROM is compiled, so the WAV itself
            // is what can be heard before then.
            if (item.IsCry && HeldCheckoutWav(item) is byte[] wav)
            {
                var pcm = CryFiles.ReadWav(wav, out int rate, out _);
                return pcm == null ? null : Stereo(Resample(pcm, rate, playAt));
            }

            var sdat = Archive();
            if (item.IsCry) return SoundArchive.RenderCry(sdat, item.Number);

            // A sound has no sequence to play it, so what is heard is the sample itself.
            if (item.IsSample)
            {
                var s = SoundArchive.Sample(sdat, item.WaveArc, item.SampleIndex);
                if (s?.Pcm == null || s.Pcm.Length == 0) return null;
                return Stereo(Resample(s.Pcm, s.SampleRate, playAt));
            }

            return sdat == null ? null : SseqPlayer.Render(sdat, item.Number, playAt, PreviewSeconds);
        }

        private byte[] HeldCheckoutWav(AudioItem item)
        {
            lock (_heldLock) return _pendingCries.TryGetValue(item, out var cry) ? cry.Wav : null;
        }

        /// <summary>Writes the picked cry or sound out as a WAV, as it will be once saved. False when there is
        /// nothing there to write.</summary>
        public bool ExportSelectedSample(string path)
        {
            var item = Selected;
            if (item == null || !(item.IsCry || item.IsSample)) return false;

            if (item.IsCry && HeldCheckoutWav(item) is byte[] wav)
            {
                System.IO.File.WriteAllBytes(path, wav);
                return true;
            }

            var sdat = Archive();
            var sample = item.IsCry
                ? SoundArchive.CrySample(sdat, item.Number)
                : SoundArchive.Sample(sdat, item.WaveArc, item.SampleIndex);
            if (sample?.Pcm == null || sample.Pcm.Length == 0) return false;
            System.IO.File.WriteAllBytes(path, CryFiles.WriteWav(sample.Pcm, sample.SampleRate));
            return true;
        }

        /// <summary>
        /// How much of a tune to render for a listen. Long enough to get past an introduction and reach the
        /// part somebody would recognise.
        /// </summary>
        public const double PreviewSeconds = 45.0;

        /// <summary>The same sound in both ears, which is the shape everything here plays, draws and saves.</summary>
        private static short[] Stereo(short[] mono)
        {
            if (mono == null) return null;
            var both = new short[mono.Length * 2];
            for (int i = 0; i < mono.Length; i++) both[i * 2] = both[i * 2 + 1] = mono[i];
            return both;
        }

        /// <summary>Stretches a sample to the rate the player runs at, so it sounds at its own pitch.</summary>
        private static short[] Resample(short[] pcm, int from, int to)
        {
            if (from <= 0 || to <= 0 || from == to) return pcm;
            long length = (long)pcm.Length * to / from;
            if (length <= 0) return pcm;
            var outp = new short[length];
            for (int i = 0; i < outp.Length; i++)
            {
                double at = (double)i * from / to;
                int a = (int)at;
                int b = a + 1 < pcm.Length ? a + 1 : a;
                double t = at - a;
                outp[i] = (short)(pcm[a] + (pcm[b] - pcm[a]) * t);
            }
            return outp;
        }

        /// <summary>The notes of whatever is picked, for drawing and for saving as a MIDI. A cry is a
        /// sample rather than written-out notes, so there are none for one.</summary>
        public System.Collections.Generic.IReadOnlyList<SseqPlayer.Note> ReadSelectedNotes()
        {
            var item = Selected;
            // A sample's number is which set it lives in, not a sequence, so it must not be read as one.
            if (item == null || item.IsCry || item.IsSample) return null;
            var sdat = SoundArchive.Load();
            if (sdat == null) return null;
            try { return SseqPlayer.ReadNotes(sdat, item.Number); } catch { return null; }
        }

        /// <summary>How far into a sequence a saved MIDI reads.</summary>
        internal const double WholeTuneSeconds = 3600.0;

        public bool CanSaveMidi => Selected != null && !Selected.IsCry && !Selected.IsSample;

        public string SaveMidiHelp => Selected == null
            ? "Pick a piece of music, a fanfare or a sound effect first."
            : Selected.IsSample
                ? "This is one of the sounds the game plays rather than written-out notes, so there is "
                  + "nothing to put in a MIDI. Save it as a WAV instead."
            : Selected.IsCry
                ? "A cry is a recorded sample rather than written-out notes, so there is nothing to put in "
                  + "a MIDI. Save it as a WAV instead."
                : "Save the notes as a MIDI other music programs open. The notes and their timing are "
                  + "exact. The instruments are not: the game plays these on samples kept in the ROM, "
                  + "which a MIDI cannot carry, so it names an instrument number and the program you open "
                  + "it in picks its own sound for that number. Save the SoundFont beside it and load "
                  + "both, and it sounds like the game.";

        // ── the instruments, as a SoundFont ─────────────────────────────────────────

        /// <summary>Which of the ROM's instrument banks whatever is picked is played on.</summary>
        public int BankOfSelection
        {
            get
            {
                var item = Selected;
                if (item == null) return -1;
                if (item.IsCry) return item.Number;             // a cry is listed by its own bank
                if (item.IsSample) return -1;                   // one recording, not a set of instruments
                var sdat = SoundArchive.Load();
                if (sdat == null || item.Number < 0 || item.Number >= sdat.Sequences.Count) return -1;
                return sdat.Sequences[item.Number]?.BankNo ?? -1;
            }
        }

        public bool CanSaveSoundFont => BankOfSelection >= 0;

        public string SaveSoundFontHelp
        {
            get
            {
                var item = Selected;
                if (item == null) return "Pick a cry, a piece of music, a fanfare or a sound effect first.";
                if (item.IsSample)
                    return "This is a single recording, so there is no bank to save. Save it as a WAV.";
                if (BankOfSelection < 0)
                    return "This does not say which bank of instruments it plays on, so there is nothing "
                         + "to save.";
                var sdat = SoundArchive.Load();
                string name = sdat != null && sdat.BankNames.TryGetValue(BankOfSelection, out var n)
                              && !string.IsNullOrWhiteSpace(n) ? n : "bank " + BankOfSelection;
                return $"Saves {name}, the instruments this plays on, as a SoundFont. Open it with the MIDI "
                     + "to hear the game's own sounds.";
            }
        }

        /// <summary>Turns the bank whatever is picked plays on into a SoundFont, or says why it cannot.</summary>
        public byte[] BuildSoundFont(out string whynot, out string note)
        {
            whynot = null; note = null;
            int bank = BankOfSelection;
            if (bank < 0) { whynot = SaveSoundFontHelp; return null; }

            var sdat = Archive();
            string name = sdat != null && sdat.BankNames.TryGetValue(bank, out var n)
                          && !string.IsNullOrWhiteSpace(n) ? n : "Bank " + bank;
            var made = SoundFontWriter.Build(sdat, bank, name);
            if (made.Whynot != null) { whynot = made.Whynot; return null; }
            note = made.Summary + " " + string.Join(" ", made.Notes);
            return made.Bytes;
        }

        /// <summary>A sensible name for the file, from what the game calls the bank.</summary>
        public string SuggestedSoundFontName()
        {
            int bank = BankOfSelection;
            var sdat = SoundArchive.Load();
            string name = sdat != null && sdat.BankNames.TryGetValue(bank, out var n)
                          && !string.IsNullOrWhiteSpace(n) ? n : "bank" + bank;
            return new string(name.Where(c => char.IsLetterOrDigit(c) || c == '_').ToArray()) + ".sf2";
        }

        /// <summary>Turns what is picked into a MIDI, or says why it cannot.</summary>
        public byte[] BuildMidi(out string whynot)
        {
            whynot = null;
            var item = Selected;
            if (item == null) { whynot = "Pick something first."; return null; }
            if (item.IsCry) { whynot = SaveMidiHelp; return null; }

            // The preview only renders the first few seconds, which is all anyone wants to hear before
            // deciding. A file is different: it should hold the whole tune, so this reads much further.
            var sdat = SoundArchive.Load();
            if (sdat == null) { whynot = "This game's sound archive could not be read."; return null; }
            System.Collections.Generic.IReadOnlyList<SseqPlayer.Note> notes;
            try { notes = SseqPlayer.ReadNotes(sdat, item.Number, WholeTuneSeconds); }
            catch { notes = null; }
            if (notes == null) { whynot = "This sequence could not be read."; return null; }
            if (notes.Count == 0) { whynot = "This sequence plays no notes, so a MIDI of it would be empty."; return null; }
            var midi = MidiFile.FromNotes(notes, item.Name);
            if (midi == null) { whynot = "This sequence could not be turned into a MIDI."; return null; }
            return midi;
        }

        public string SuggestedMidiName()
        {
            string wav = SuggestedFileName();
            return wav.EndsWith(".wav", System.StringComparison.OrdinalIgnoreCase)
                ? wav.Substring(0, wav.Length - 4) + ".mid" : wav + ".mid";
        }

        /// <summary>A sensible file name for saving what is picked.</summary>
        public string SuggestedFileName()
        {
            var item = Selected;
            if (item == null) return "sound.wav";
            string name = item.Name;
            int space = name.IndexOf(' ');
            if (item.IsCry && space > 0) name = name.Substring(space + 1).Trim();
            foreach (char c in System.IO.Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
            return item.IsCry ? $"cry_{item.Number:D3}_{name}.wav" : $"{name}.wav";
        }
    }
}
