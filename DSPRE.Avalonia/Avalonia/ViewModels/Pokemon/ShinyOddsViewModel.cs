using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using DSPRE.Editors;
using DSPRE.ROMFiles;

namespace DSPRE.Avalonia.ViewModels.Pokemon
{
    /// <summary>The game's shiny check, and the odds set by Eclipse's shiny odds patch.</summary>
    public class ShinyOddsViewModel : INotifyPropertyChanged, IEditorWithUnsavedChanges, ISupportsUndo, IDetachable
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void Raise([CallerMemberName] string n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

        private int? _checkValues;
        private bool _patchSupported, _installed;
        private int? _odds, _saved;

        private ByteStateUndo _undo;
        private void StartUndo() => _undo = new ByteStateUndo(() => BitConverter.GetBytes(_odds ?? 0), b => { _odds = BitConverter.ToInt32(b, 0); RaiseAll(); },
            () => { Raise(nameof(CanUndo)); Raise(nameof(CanRedo)); });
        public bool CanUndo => _undo?.CanUndo == true;
        public bool CanRedo => _undo?.CanRedo == true;
        public void Undo() => _undo?.Undo();
        public void Redo() => _undo?.Redo();

        public ShinyOddsViewModel() { }

        public ShinyOddsViewModel(bool load)
        {
            if (!load) return;
            Load();
            AppEvents.RomPatchStateChanged += OnPatchStateChanged;
        }

        public void Detach() => AppEvents.RomPatchStateChanged -= OnPatchStateChanged;

        private void OnPatchStateChanged(object sender, EventArgs e)
        {
            if (HasUnsavedChanges) return;
            Load();
        }

        private void Load()
        {
            _checkValues = ShinyCheck.Values();
            _installed = ShinyOddsPatch.SupportsCurrentRom && ShinyOddsPatch.IsApplied;
            // The patch needs the retail check, so it's hidden on ROMs with a rate patch.
            _patchSupported = _installed || ShinyOddsPatch.SupportsCurrentRom && _checkValues == ShinyCheck.Retail;
            _odds = _saved = _installed ? ShinyOddsPatch.ReadOdds() : null;
            StartUndo();
            RaiseAll();
        }

        public string CheckText => _checkValues switch
        {
            null => "Changed by a patch DSPRE can't read",
            0 => "No shiny Pokémon",
            ShinyCheck.Retail => "1 in 8192",
            int v => $"About 1 in {Math.Round(65536.0 / v)}",
        };

        public string CheckTip => _checkValues == ShinyCheck.Retail ? "Used for every Pokémon" : "Changed by a rate patch";

        public bool ShowsPatch => _patchSupported;
        public bool NeedsPatch => _patchSupported && !_installed;
        public bool CanEditOdds => _odds != null;
        public bool OddsUnreadable => _installed && _odds == null;

        public decimal MinOdds => ShinyOddsPatch.MinOdds;
        public decimal MaxOdds => ShinyOddsPatch.MaxOdds;

        public decimal Odds
        {
            get => _odds ?? ShinyOddsPatch.DefaultOdds;
            set
            {
                if (_odds == null) return;
                int odds = (int)Math.Clamp(value, ShinyOddsPatch.MinOdds, ShinyOddsPatch.MaxOdds);
                if (odds == _odds) return;
                _odds = odds;
                RaiseAll();
                _undo?.Record();
            }
        }

        public bool HasUnsavedChanges => _odds != _saved;
        public string UnsavedChangesDescription => "Shiny odds";

        private void RaiseAll()
        {
            foreach (string n in new[] { nameof(CheckText), nameof(CheckTip), nameof(ShowsPatch), nameof(NeedsPatch), nameof(CanEditOdds),
                                         nameof(OddsUnreadable), nameof(Odds), nameof(HasUnsavedChanges) })
                Raise(n);
        }

        public void SaveChanges() => DialogHelper.Watch(SaveChangesAsync());

        public async Task<bool> SaveChangesAsync()
        {
            if (_odds is not int odds || !HasUnsavedChanges) return true;
            try { ShinyOddsPatch.WriteOdds(odds); }
            catch (Exception e) when (e is IOException || e is InvalidOperationException || e is UnauthorizedAccessException)
            {
                await DialogHelper.ShowErrorAsync("The shiny odds weren't saved:\n" + e.Message, "Shiny odds");
                return false;
            }
            _saved = _odds;
            Raise(nameof(HasUnsavedChanges));
            SaveNotice.Saved(UnsavedChangesDescription);
            return true;
        }

        public void DiscardChanges()
        {
            _odds = _saved;
            StartUndo();
            RaiseAll();
        }
    }
}
