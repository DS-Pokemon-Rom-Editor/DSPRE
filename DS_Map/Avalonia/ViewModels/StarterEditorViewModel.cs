using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Controls;
using IEditorWithUnsavedChanges = global::DSPRE.Editors.IEditorWithUnsavedChanges;
using DSPRE.ROMFiles;

namespace DSPRE.Avalonia.ViewModels
{
    public class StarterEditorViewModel : INotifyPropertyChanged, IEditorWithUnsavedChanges, DSPRE.Avalonia.ISupportsUndo
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
        private bool Set<T>(ref T f, T v, [CallerMemberName] string n = null)
        {
            if (Equals(f, v)) return false;
            f = v;
            OnPropertyChanged(n);
            return true;
        }

        // ── IEditorWithUnsavedChanges ───────────────────────────────────────────
        private bool _dirty;
        public bool HasUnsavedChanges => _dirty;
        public string UnsavedChangesDescription => "Starter Pokémon Editor";
        void IEditorWithUnsavedChanges.SaveChanges() => SaveChanges();
        public void DiscardChanges() => _dirty = false;

        // ── Undo / redo (ISupportsUndo) ─────────────────────────────────────────
        // Only the 4 field values are snapshotted; the byte patches (ASM/rival scripts/text) run once, on
        // Save, not per undo step.
        private sealed class Snapshot { public int S1, S2, S3, HeldItem; }
        private readonly DSPRE.Avalonia.UndoHistory<Snapshot> _history = new();
        private System.DateTime _lastCaptureUtc = System.DateTime.MinValue;
        private const int CoalesceMs = 500;

        public bool CanUndo => _history.CanUndo;
        public bool CanRedo => _history.CanRedo;
        public void Undo() { if (_history.CanUndo) ApplyState(_history.Undo()); }
        public void Redo() { if (_history.CanRedo) ApplyState(_history.Redo()); }
        private void RaiseUndoState() { OnPropertyChanged(nameof(CanUndo)); OnPropertyChanged(nameof(CanRedo)); }

        private Snapshot TakeSnapshot() => new Snapshot { S1 = _starter1, S2 = _starter2, S3 = _starter3, HeldItem = _heldItem };

        private void ApplyState(Snapshot snap)
        {
            if (snap == null) return;
            _loading = true;
            _starter1 = snap.S1; OnPropertyChanged(nameof(Starter1));
            _starter2 = snap.S2; OnPropertyChanged(nameof(Starter2));
            _starter3 = snap.S3; OnPropertyChanged(nameof(Starter3));
            _heldItem = snap.HeldItem; OnPropertyChanged(nameof(HeldItem));
            RefreshStarterIcon(1); RefreshStarterIcon(2); RefreshStarterIcon(3); RefreshHeldItemIcon();
            _loading = false;

            _dirty = _history.IsDirty;
            Title = _dirty ? "● Starter Pokémon Editor" : "Starter Pokémon Editor";
            OnPropertyChanged(nameof(HasUnsavedChanges));
            RaiseUndoState();
        }

        private void RecordUndoSnapshot()
        {
            if (_loading) return;
            bool coalesce = (System.DateTime.UtcNow - _lastCaptureUtc).TotalMilliseconds < CoalesceMs;
            _history.Capture(TakeSnapshot(), coalesce);
            _lastCaptureUtc = System.DateTime.UtcNow;
            RaiseUndoState();
        }

        // ── Lists (ComboBox sources) ─────────────────────────────────────────────
        public ObservableCollection<string> PokemonNames { get; } = new();
        public ObservableCollection<string> ItemNames { get; } = new();

        // ── Title ─────────────────────────────────────────────────────────────
        private string _title = "Starter Pokémon Editor";
        public string Title { get => _title; private set => Set(ref _title, value); }

        // ── Starter species / held item ──────────────────────────────────────
        private int _starter1, _starter2, _starter3, _heldItem;
        public int Starter1 { get => _starter1; set { if (Set(ref _starter1, value)) { MarkDirty(); RefreshStarterIcon(1); } } }
        public int Starter2 { get => _starter2; set { if (Set(ref _starter2, value)) { MarkDirty(); RefreshStarterIcon(2); } } }
        public int Starter3 { get => _starter3; set { if (Set(ref _starter3, value)) { MarkDirty(); RefreshStarterIcon(3); } } }
        public int HeldItem { get => _heldItem; set { if (Set(ref _heldItem, value)) { MarkDirty(); RefreshHeldItemIcon(); } } }

        // ── Icons ─────────────────────────────────────────────────────────────
        private readonly PokemonIconCache _pokemonIcons = new();
        private global::Avalonia.Media.IImage _starter1Icon, _starter2Icon, _starter3Icon, _heldItemIcon;
        public global::Avalonia.Media.IImage Starter1Icon { get => _starter1Icon; private set => Set(ref _starter1Icon, value); }
        public global::Avalonia.Media.IImage Starter2Icon { get => _starter2Icon; private set => Set(ref _starter2Icon, value); }
        public global::Avalonia.Media.IImage Starter3Icon { get => _starter3Icon; private set => Set(ref _starter3Icon, value); }
        public global::Avalonia.Media.IImage HeldItemIcon { get => _heldItemIcon; private set => Set(ref _heldItemIcon, value); }

        private void RefreshStarterIcon(int slot)
        {
            var icon = _pokemonIcons.Get(slot == 1 ? _starter1 : slot == 2 ? _starter2 : _starter3);
            if (slot == 1) Starter1Icon = icon;
            else if (slot == 2) Starter2Icon = icon;
            else Starter3Icon = icon;
        }

        private void RefreshHeldItemIcon()
        {
            if (!IsHeldItemSupported || _heldItem <= 0) { HeldItemIcon = null; return; }
            try
            {
                var raw = DSUtils.GetItemPicRaw(_heldItem, 32, 32);
                HeldItemIcon = raw != null ? DSPRE.Avalonia.ImageConverter.ToAvaloniaBitmap(raw) : null;
            }
            catch { HeldItemIcon = null; }
        }

        /// <summary>HGSS starters never carry a held item.</summary>
        public bool IsHeldItemSupported => RomInfo.gameFamily != RomInfo.GameFamilies.HGSS;

        // ── Loading flag (prevents handlers from firing during load) ────────────
        private bool _loading;

        // ── Constructors ──────────────────────────────────────────────────────
        public StarterEditorViewModel()
        {
            _loading = true;

            if (Design.IsDesignMode)
            {
                for (int i = 0; i < 10; i++) PokemonNames.Add($"Pokémon {i}");
                for (int i = 0; i < 10; i++) ItemNames.Add($"Item {i}");
                Starter1 = 1; Starter2 = 2; Starter3 = 3; HeldItem = 0;
                _loading = false;
                return;
            }

            DSUtils.TryUnpackNarcs(new System.Collections.Generic.List<RomInfo.DirNames> { RomInfo.DirNames.monIcons, RomInfo.DirNames.itemIcons });
            RomInfo.SetMonIconsPalTableAddress();

            foreach (var n in RomInfo.GetPokemonNames()) PokemonNames.Add(n);
            foreach (var n in RomInfo.GetItemNames()) ItemNames.Add(n);
            ReloadFromRom();

            AppEvents.NamesChanged -= OnNamesChanged;
            AppEvents.NamesChanged += OnNamesChanged;

            _loading = false;
        }

        private void ReloadFromRom()
        {
            int[] starters = StarterPokemonData.GetStarters();
            _starter1 = starters[0];
            _starter2 = starters[1];
            _starter3 = starters[2];
            _heldItem = IsHeldItemSupported ? StarterPokemonData.GetHeldItem() : 0;
            OnPropertyChanged(nameof(Starter1));
            OnPropertyChanged(nameof(Starter2));
            OnPropertyChanged(nameof(Starter3));
            OnPropertyChanged(nameof(HeldItem));
            OnPropertyChanged(nameof(IsHeldItemSupported));
            RefreshStarterIcon(1); RefreshStarterIcon(2); RefreshStarterIcon(3); RefreshHeldItemIcon();

            _dirty = false;
            Title = "Starter Pokémon Editor";
            OnPropertyChanged(nameof(HasUnsavedChanges));

            _history.Reset(TakeSnapshot());
            _lastCaptureUtc = System.DateTime.MinValue;
            RaiseUndoState();
        }

        private void OnNamesChanged(object sender, System.EventArgs e)
        {
            DSPRE.Avalonia.Data.ListSync.Apply(PokemonNames, RomInfo.GetPokemonNames());
            DSPRE.Avalonia.Data.ListSync.Apply(ItemNames, RomInfo.GetItemNames());
        }

        /// <summary>Unsubscribes from app-wide events; call when the editor window closes.</summary>
        public void Detach() => AppEvents.NamesChanged -= OnNamesChanged;

        // ── Busy state (background .rotom resync after Save, see SaveChanges) ──
        private bool _isBusy;
        public bool IsBusy { get => _isBusy; private set => Set(ref _isBusy, value); }
        private string _busyText;
        public string BusyText { get => _busyText; private set => Set(ref _busyText, value); }

        // ── Commands ──────────────────────────────────────────────────────────
        public void SaveChanges()
        {
            var newStarters = new[] { Starter1, Starter2, Starter3 };
            bool ok = StarterPokemonData.ApplyStarters(newStarters, out var touchedScripts);
            if (!ok)
            {
                AppMessages.Error(
                    "Couldn't safely locate the starter species table on this ROM (it may already be modified " +
                    "by another tool); nothing was changed.",
                    "Starter Pokémon Editor");
                return;
            }

            if (IsHeldItemSupported)
            {
                StarterPokemonData.SetHeldItem(HeldItem);
                if (RomInfo.starterHeldItemScriptFileID >= 0) touchedScripts.Add(RomInfo.starterHeldItemScriptFileID);
            }

            _dirty = false;
            Title = "Starter Pokémon Editor";
            OnPropertyChanged(nameof(HasUnsavedChanges));
            AppLogger.Debug($"StarterEditor: Saved starters [{Starter1}, {Starter2}, {Starter3}].");
            _history.MarkSaved();
            RaiseUndoState();

            // The species/rival-script bytes above are already on disk; this only keeps the Script
            // Editor's .rotom text in sync (see RefreshRotomSourcesAsync's remarks) and must run in the
            // background: it shells out to rotom.exe per touched file, and awaiting it synchronously here
            // would freeze the whole app (SaveChanges is called directly from a UI Click handler).
            if (touchedScripts.Count > 0 && RomInfo.hasRotomProject)
                _ = RefreshRotomInBackgroundAsync(touchedScripts);
        }

        private async System.Threading.Tasks.Task RefreshRotomInBackgroundAsync(System.Collections.Generic.List<int> fileIds)
        {
            IsBusy = true;
            BusyText = "Reparsing scripts…";
            try
            {
                await StarterPokemonData.RefreshRotomSourcesAsync(fileIds);
            }
            catch (Exception ex)
            {
                AppLogger.Warn("StarterEditor: background .rotom refresh failed: " + ex.Message);
            }
            finally
            {
                IsBusy = false;
                BusyText = null;
            }
        }

        private void MarkDirty()
        {
            if (_loading) return;
            RecordUndoSnapshot();
            _dirty = true;
            Title = "● Starter Pokémon Editor";
            OnPropertyChanged(nameof(HasUnsavedChanges));
        }
    }
}
