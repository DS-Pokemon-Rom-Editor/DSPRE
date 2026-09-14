using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using DSPRE.Avalonia.Data;
using DSPRE.Avalonia.ViewModels.Battle;
using DSPRE.Avalonia.ViewModels.Pokemon;
using DSPRE.ROMFiles;
using IEditorWithUnsavedChanges = global::DSPRE.Editors.IEditorWithUnsavedChanges;

namespace DSPRE.Avalonia.ViewModels.Graphics
{
    public sealed class SealChoice
    {
        public BallSeal Seal { get; init; }
        public Bitmap Sticker { get; init; }
        public string Title => $"{Seal.Id}: {Seal.Name}";
    }

    /// <summary>One seal on the capsule, in the lower screen's pixels.</summary>
    public sealed class PlacedSeal
    {
        public int Slot { get; init; }
        public BallSeal Seal { get; init; }
        public Bitmap Sticker { get; init; }
        public int X { get; set; }
        public int Y { get; set; }
    }

    /// <summary>Designs a Ball Capsule, standalone or a trainer capsule, and previews it on a send-out.</summary>
    public sealed class BallCapsuleEditorViewModel : INotifyPropertyChanged, IEditorWithUnsavedChanges
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
        private bool Set<T>(ref T f, T v, [CallerMemberName] string n = null)
        {
            if (EqualityComparer<T>.Default.Equals(f, v)) return false;
            f = v; OnPropertyChanged(n); return true;
        }

        public event Action BoardChanged;

        public ObservableCollection<SealChoice> Seals { get; } = new();
        public ObservableCollection<SealChoice> ShownSeals { get; } = new();
        public ObservableCollection<string> CapsuleNames { get; } = new();
        public ObservableCollection<string> SpeciesNames { get; } = new();

        public Bitmap Board { get; }
        public PokemonSpriteEditorViewModel Sprites { get; }
        public BattleDisplayEditorViewModel Battle { get; }

        private readonly BallCapsule _design = new();
        private BallCapsule[] _trainerCapsules = Array.Empty<BallCapsule>();
        private IReadOnlyList<BallSeal> _seals = Array.Empty<BallSeal>();

        public BallCapsuleEditorViewModel() { if (!Design.IsDesignMode) return; }

        public BallCapsuleEditorViewModel(string[] speciesNames)
        {
            _seals = BallSeals.Read();
            foreach (var seal in _seals.Where(s => s != null))
                Seals.Add(new SealChoice { Seal = seal, Sticker = BallCapsuleGraphics.Sticker(seal) });

            foreach (var choice in Seals) ShownSeals.Add(choice);

            CapsuleNames.Add("Your design");
            if (TrainerCapsules.Available)
            {
                _trainerCapsules = TrainerCapsules.ReadAll();
                for (int i = 0; i < _trainerCapsules.Length; i++)
                    CapsuleNames.Add($"Trainer capsule {i + 1}" + (_trainerCapsules[i].IsEmpty ? "" : $" ({_trainerCapsules[i].Seals.Count(s => s.Seal != 0)} seals)"));
            }

            Board = BallCapsuleGraphics.Board();
            for (int i = 0; i < speciesNames.Length; i++) SpeciesNames.Add($"{i:D3} {speciesNames[i]}");

            Sprites = new PokemonSpriteEditorViewModel(true);
            Battle = new BattleDisplayEditorViewModel(Sprites);
            _capsuleIndex = 0;
            SpeciesIndex = Math.Min(1, SpeciesNames.Count - 1);
            StatusText = _seals.Count == 0 ? "This ROM's seal table was not found." : $"{Seals.Count} seals.";
        }

        public BallCapsule Current => _capsuleIndex > 0 && _capsuleIndex <= _trainerCapsules.Length ? _trainerCapsules[_capsuleIndex - 1] : _design;

        private int _capsuleIndex;
        public int CapsuleIndex
        {
            get => _capsuleIndex;
            set { if (value >= 0 && value < CapsuleNames.Count && Set(ref _capsuleIndex, value)) { Battle?.StopPlayback(); RaiseBoard(); OnPropertyChanged(nameof(UsedByText)); } }
        }

        private Dictionary<int, List<string>> _usedBy = new();
        /// <summary>Which trainers carry each capsule, by the number a party entry stores.</summary>
        public Dictionary<int, List<string>> UsedBy
        {
            get => _usedBy;
            set { _usedBy = value ?? new(); OnPropertyChanged(nameof(UsedByText)); }
        }

        public string UsedByText
        {
            get
            {
                if (_capsuleIndex == 0) return "";
                if (!_usedBy.TryGetValue(_capsuleIndex, out var who) || who.Count == 0) return "No trainer's Pokemon carries this capsule.";
                return "Carried by " + (who.Count <= 6 ? string.Join(", ", who) : $"{string.Join(", ", who.Take(6))} and {who.Count - 6} more");
            }
        }

        private int _speciesIndex = -1;
        public int SpeciesIndex
        {
            get => _speciesIndex;
            set
            {
                if (value < 0 || value >= SpeciesNames.Count || !Set(ref _speciesIndex, value)) return;
                Battle?.StopPlayback();
                Sprites?.LoadMon(value);
                Battle?.LoadMon(value);
            }
        }

        private string _sealFilter = "";
        public string SealFilter
        {
            get => _sealFilter;
            set
            {
                if (!Set(ref _sealFilter, value ?? "")) return;
                ShownSeals.Clear();
                string want = _sealFilter.Trim();
                // Exact and leading matches first, so a short name is not buried under longer ones.
                int Rank(SealChoice c) =>
                    c.Seal.Name.Equals(want, StringComparison.OrdinalIgnoreCase) ? 0
                    : c.Seal.Name.StartsWith(want, StringComparison.OrdinalIgnoreCase) ? 1 : 2;
                foreach (var choice in Seals.Where(c => want.Length == 0 || c.Title.IndexOf(want, StringComparison.OrdinalIgnoreCase) >= 0)
                                            .OrderBy(Rank))
                    ShownSeals.Add(choice);
            }
        }

        private string _status = "";
        public string StatusText { get => _status; set => Set(ref _status, value); }

        public IEnumerable<PlacedSeal> Placed =>
            Current.Seals.Select((s, slot) => (s, slot))
                   .Where(p => p.s.Seal > 0 && p.s.Seal < _seals.Count && _seals[p.s.Seal] != null)
                   .Select(p => new PlacedSeal
                   {
                       Slot = p.slot, Seal = _seals[p.s.Seal], X = p.s.X, Y = p.s.Y,
                       Sticker = Seals.FirstOrDefault(c => c.Seal.Id == p.s.Seal)?.Sticker,
                   });

        public int SealsOnBoard => Current.Seals.Count(s => s.Seal != 0);

        private void RaiseBoard()
        {
            OnPropertyChanged(nameof(Current));
            OnPropertyChanged(nameof(SealsOnBoard));
            BoardChanged?.Invoke();
        }

        private void Touched()
        {
            if (_capsuleIndex > 0) HasUnsavedChanges = true;
            RaiseBoard();
        }

        /// <summary>Puts a seal in the first empty slot, at the centre of the board.</summary>
        public void Place(BallSeal seal) => PlaceAt(seal, BallCapsule.BoardCentreX, BallCapsule.BoardCentreY);

        /// <summary>Puts a seal in the first empty slot where it was dropped, kept on the board.</summary>
        public void PlaceAt(BallSeal seal, int x, int y)
        {
            if (seal == null) return;
            int slot = Array.FindIndex(Current.Seals, s => s.Seal == 0);
            if (slot < 0) { StatusText = $"A capsule holds {BallCapsule.Slots} seals."; return; }
            Current.Seals[slot].Seal = seal.Id;
            Current.Seals[slot].X = BallCapsule.BoardCentreX;
            Current.Seals[slot].Y = BallCapsule.BoardCentreY;
            StatusText = $"{seal.Name} placed.";
            Move(slot, x, y);
            Touched();
        }

        /// <summary>Moves a placed seal, keeping it on the board the way the game's editor does.</summary>
        public void Move(int slot, int x, int y)
        {
            if (slot < 0 || slot >= BallCapsule.Slots) return;
            double dx = x - BallCapsule.BoardCentreX, dy = y - BallCapsule.BoardCentreY;
            double d = Math.Sqrt(dx * dx + dy * dy);
            if (d > BallCapsule.BoardRadius)
            {
                x = (int)Math.Round(BallCapsule.BoardCentreX + dx * BallCapsule.BoardRadius / d);
                y = (int)Math.Round(BallCapsule.BoardCentreY + dy * BallCapsule.BoardRadius / d);
            }
            var s = Current.Seals[slot];
            if (s.X == x && s.Y == y) return;
            s.X = x; s.Y = y;
            Touched();
        }

        public void Remove(int slot)
        {
            if (slot < 0 || slot >= BallCapsule.Slots || Current.Seals[slot].Seal == 0) return;
            Current.Seals[slot].Seal = 0;
            Touched();
        }

        public void Clear()
        {
            foreach (var s in Current.Seals) s.Seal = 0;
            Touched();
        }

        /// <summary>Plays the send-out with this capsule on both Pokemon.</summary>
        public void Play()
        {
            if (Battle == null) return;
            // Straight to the throw: the capsule only shows when a trainer's ball opens.
            Battle.SendOutKindIndex = (int)SendOutKind.Trainer;
            Battle.PlayTrainerIntro = false;
            Battle.Capsule = Current;
            Battle.ToggleSendOutPlayback();
        }

        /// <summary>Forgets a seal's particles so an edit to them shows on the next play.</summary>
        public void ParticlesChanged(int entry) => Battle?.ForgetSealParticles(entry);

        // ── saving ─────────────────────────────────────────────────────────────────────────────

        private bool _dirty;
        public bool HasUnsavedChanges { get => _dirty; private set => Set(ref _dirty, value); }
        public string UnsavedChangesDescription => "Ball Capsules (trainer capsules)";

        public void SaveChanges()
        {
            if (!TrainerCapsules.Available || !_dirty) return;
            try
            {
                TrainerCapsules.WriteAll(_trainerCapsules);
                HasUnsavedChanges = false;
                StatusText = "Trainer capsules saved.";
                Data.TrainerCapsuleCatalog.Saved();
            }
            catch (Exception ex) { StatusText = "Could not save: " + ex.Message; }
        }

        public void DiscardChanges()
        {
            if (TrainerCapsules.Available) _trainerCapsules = TrainerCapsules.ReadAll();
            HasUnsavedChanges = false;
            RaiseBoard();
        }
    }
}
