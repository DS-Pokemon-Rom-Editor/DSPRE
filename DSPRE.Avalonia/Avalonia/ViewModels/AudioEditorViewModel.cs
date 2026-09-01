using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using DSPRE.Avalonia.Data;

namespace DSPRE.Avalonia.ViewModels
{
    /// <summary>
    /// One thing you can listen to, whichever part of the ROM it came from.
    /// </summary>
    public sealed class AudioItem
    {
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

        public string Label => $"{Number,5}  {Name}";
    }

    /// <summary>
    /// Everything the ROM can play, in one place.
    ///
    /// The sound archive keeps its sequences under names that say what each one is for, and those names
    /// are what the tabs are built from rather than anything invented here. In HeartGold there are 319
    /// beginning SEQ_GS_ (the music: a map's own tune, such as SEQ_GS_T_WAKABA for New Bark), 33
    /// beginning SEQ_ME_ (the short fanfares for levelling up, catching something and so on), and 1006
    /// beginning SEQ_SE_ (everything else that makes a noise). Cries are counted separately because they
    /// are not one sequence each: every cry plays the same short sequence with a different Pokemon's
    /// sample handed to it.
    ///
    /// That difference decides what can be done with each. A cry is a sample, so it can be taken out and
    /// a new one put in. The rest are written-out notes that share the game's instruments, so they can be
    /// listened to and saved as sound, but a WAV cannot be put back in their place.
    /// </summary>
    public class AudioEditorViewModel : INotifyPropertyChanged
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
        ///
        /// The archive gives every sequence a player number, and both games number them the same way:
        /// snd_system.c and the two .sadl files agree on PLAYER_PV 0, PLAYER_FIELD 1, PLAYER_ME 2,
        /// PLAYER_SE_1 to _4 3 to 6 and PLAYER_BGM 7. Splitting on names instead worked in HeartGold and
        /// failed in Platinum, which names none of its fanfares SEQ_ME_ and so showed none at all. The
        /// player numbers agree with the names exactly where the names work: HeartGold has 33 sequences on
        /// the fanfare player and 33 named SEQ_ME_, and 1006 on the four effect players and 1006 named
        /// SEQ_SE_. Platinum has 20 fanfares that were never listed.
        ///
        /// The names are still used for anything the archive has no record for, which is nothing in either
        /// game but costs little to keep.
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

            // A cry belongs to a Pokemon, not to a sequence, so it is listed by the bank the game plays it
            // from. Whose it is comes from the bank's own name: the first 493 are named for their own
            // number, and the rest, the alternate forms, are named for the form they belong to instead
            // (bank 494 is BANK_PV516_SKY, Shaymin's Sky form). Either way the number in the name is the
            // one to look the Pokemon up by.
            foreach (int bank in SoundArchive.CryBanks())
            {
                string bankName = sdat.BankNames.TryGetValue(bank, out var bn) ? bn : "";
                string shortName = bankName.StartsWith("BANK_", StringComparison.Ordinal)
                    ? bankName.Substring("BANK_".Length) : bankName;

                string digits = new string(shortName.SkipWhile(c => !char.IsDigit(c)).TakeWhile(char.IsDigit).ToArray());
                int.TryParse(digits, out int named);
                string label = shortName;
                if (named > 0 && pokemonNames != null
                    && named > 0 && named < pokemonNames.Count && !string.IsNullOrWhiteSpace(pokemonNames[named]))
                    label = pokemonNames[named];

                _allCries.Add(new AudioItem { Number = bank, Name = label, IsCry = true, NamedNumber = named });
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

        /// <summary>
        /// Opens on one Pokemon's cry, for arriving here from the Pokemon editor. A base species is played
        /// from the bank of its own number; a form is played from a later bank that carries the form's
        /// number in its name instead, so both are looked for.
        /// </summary>
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
                if (it.IsCry) return it.Name + ". A cry is a sample of its own, so it can be replaced.";
                if (it.IsSample)
                    return $"{it.Detail}, sound {it.SampleIndex}. This is one of the sounds the game is "
                         + "built from, so it can be replaced. Whatever plays it will play the new one, "
                         + "and more than one tune or effect may be using it.";
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

        // ── doing something with it ─────────────────────────────────────────────────

        /// <summary>The sound of whatever is picked, ready to play or save.</summary>
        public short[] RenderSelected(int playAt = 32000)
        {
            var item = Selected;
            if (item == null) return null;
            if (item.IsCry) return SoundArchive.RenderCry(item.Number);

            // A sound has no sequence to play it, so what is heard is the sample itself. It is recorded at
            // whatever rate suited it, and the player runs at one fixed rate, so it is stretched to match
            // or it would come out at the wrong pitch.
            if (item.IsSample)
            {
                var s = SoundArchive.Sample(item.WaveArc, item.SampleIndex);
                if (s?.Pcm == null || s.Pcm.Length == 0) return null;
                return Resample(s.Pcm, s.SampleRate, playAt);
            }

            var sdat = SoundArchive.Load();
            return sdat == null ? null : SseqPlayer.Render(sdat, item.Number);
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

        /// <summary>How far into a sequence a saved MIDI reads.
        ///
        /// A sequence stops itself at the first backward jump, so this is only a guard against a file
        /// that never ends rather than a real limit. Ten minutes was not enough: two Platinum sequences
        /// run longer than that in a single pass and were being cut off.</summary>
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
                  + "it in picks its own sound for that number.";

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
