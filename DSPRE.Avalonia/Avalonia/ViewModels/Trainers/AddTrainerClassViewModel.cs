using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DSPRE.Avalonia.ViewModels.Trainers
{
    /// <summary>Backing model for the "Add Trainer Class…" dialog. Pure input collection,
    /// <see cref="TrainerClassesViewModel.AddTrainerClass"/> does the actual write once the dialog
    /// closes with <see cref="Confirmed"/>. Platinum-only (see IsExpansionSupported gating on the
    /// button that opens this), so there's no HGSS "night music" variant to collect here.</summary>
    public class AddTrainerClassViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
        private bool Set<T>(ref T f, T v, [CallerMemberName] string n = null)
        { if (EqualityComparer<T>.Default.Equals(f, v)) return false; f = v; OnPropertyChanged(n); return true; }

        /// <summary>Adding to an hg-engine checkout rather than a Platinum ROM.</summary>
        public bool ForHgEngine { get; init; }

        public string Note => ForHgEngine
            ? "Adds the class to the hg-engine source: its constant, names, sprite files, gender and prize money. Compile the ROM to use it."
            : "Writes the class into the room the \"Trainer class tables\" patch made for its gender and prize money.";

        /// <summary>Whether the eye-contact music table has room for the class; without its patch the option is off.</summary>
        public bool CanAddMusic { get; init; } = true;
        public bool MusicNeedsPatch => !CanAddMusic;

        private string _className = "";
        public string ClassName { get => _className; set => Set(ref _className, value); }

        private string _nameWithArticle = "";
        public string NameWithArticle { get => _nameWithArticle; set => Set(ref _nameWithArticle, value); }

        private int _genderIndex;
        public int GenderIndex { get => _genderIndex; set => Set(ref _genderIndex, value); }

        private int _prizeMultiplier = 1;
        public int PrizeMultiplier { get => _prizeMultiplier; set => Set(ref _prizeMultiplier, value); }

        private bool _addMusic;
        public bool AddMusic { get => _addMusic; set => Set(ref _addMusic, value); }

        private decimal _musicMain;
        public decimal MusicMain { get => _musicMain; set => Set(ref _musicMain, value); }

        public ObservableCollection<string> SpriteChoices { get; } = new();

        private int _spriteChoiceIndex;
        public int SpriteChoiceIndex { get => _spriteChoiceIndex; set => Set(ref _spriteChoiceIndex, value); }

        private bool _keepsSlotSprite;

        /// <summary>Lists the classes whose sprite the new class can start with. When the archive already
        /// holds a sprite in the new class's slot, keeping it comes first and is chosen.</summary>
        public void SetSpriteChoices(IEnumerable<string> classNames, int selectedClass, int newClassId, bool slotHasSprite)
        {
            _keepsSlotSprite = slotHasSprite;
            SpriteChoices.Clear();
            if (slotHasSprite) SpriteChoices.Add($"[{newClassId:D3}] Sprite already in this slot");
            foreach (string name in classNames) SpriteChoices.Add(name);
            SpriteChoiceIndex = slotHasSprite ? 0 : Math.Max(0, selectedClass);
        }

        /// <summary>The class to copy the sprite from, or -1 to keep the one already in the slot.</summary>
        public int SpriteFrom => _keepsSlotSprite ? SpriteChoiceIndex - 1 : SpriteChoiceIndex;

        private string _statusText = "";
        public string StatusText { get => _statusText; set => Set(ref _statusText, value); }

        public bool Confirmed { get; private set; }
        public void Confirm() => Confirmed = true;
    }
}
