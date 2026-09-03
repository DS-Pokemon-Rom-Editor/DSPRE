using System;
using System.Linq;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Controls;
using IEditorWithUnsavedChanges = global::DSPRE.Editors.IEditorWithUnsavedChanges;
using DSPRE.ROMFiles;

namespace DSPRE.Avalonia.ViewModels.Pokemon
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

        // Diamond and Pearl set the held item and the level inside a script command, and DSPRE cannot
        // read those scripts reliably yet, so it says where they are instead of offering a control that
        // would write to the wrong place. Platinum keeps the same layout but does parse.
        private static bool IsDiamondOrPearl => RomInfo.gameFamily == RomInfo.GameFamilies.DP;

        // Platinum keeps the level and the held item in the script that hands the starter over, so
        // both are editable there once that command has been found.
        private StarterRotomSource.Match _command;

        private int _starterLevel = 5;
        public int StarterLevel
        {
            get => _starterLevel;
            set { if (Set(ref _starterLevel, value)) { MarkDirty(); } }
        }

        public bool IsLevelSupported => _command != null;

        /// <summary>Where the starter is handed over, so it is clear what the editor is about to change.</summary>
        public string CommandLocation => _command == null ? null : _command.Where;
        public bool HasCommandLocation => _command != null;

        /// <summary>HGSS keeps its starters in the ARM9, so there is no script command to point at.</summary>
        public bool CanManageCommand => StarterRotomSource.IsAvailable && IsHeldItemSupported;

        /// <summary>HGSS starters never carry a held item, and DP keep theirs out of reach.</summary>
        public bool IsHeldItemSupported =>
            RomInfo.gameFamily != RomInfo.GameFamilies.HGSS && !IsDiamondOrPearl;

        /// <summary>Shown instead of the held item and level fields on Diamond and Pearl.</summary>
        public bool HasScriptNote => IsDiamondOrPearl;

        public string ScriptNote
        {
            get
            {
                if (!IsDiamondOrPearl) return null;
                int file = RomInfo.starterHeldItemScriptFileID;
                int number = DSPRE.ROMFiles.StarterPokemonData.GetStarterScriptNumber();
                string where = number > 0
                    ? $"script file {file}, script {number}"
                    : $"script file {file}";
                return "The starter's held item and level are set by the GivePokemon command in "
                     + where + ". Edit them there; this editor only changes the species.";
            }
        }

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
            OnPropertyChanged(nameof(HasScriptNote));
            OnPropertyChanged(nameof(ScriptNote));
            RefreshStarterIcon(1); RefreshStarterIcon(2); RefreshStarterIcon(3); RefreshHeldItemIcon();

            LocateStarterCommand();

            _dirty = false;
            Title = "Starter Pokémon Editor";
            OnPropertyChanged(nameof(HasUnsavedChanges));

            _history.Reset(TakeSnapshot());
            _lastCaptureUtc = System.DateTime.MinValue;
            RaiseUndoState();
        }

        /// <summary>
        /// Finds the command that hands the starter over. Two cheap reads of one file cover everything
        /// but a starter somebody has moved: whatever was chosen for this project last time, then the
        /// place an untouched game keeps it. Only when neither holds up is the whole game read, and only
        /// then can the editor have anything new to say about which command is the right one. Changing
        /// the held item or the level leaves both of those pointing where they did, so the ordinary use
        /// of this editor never reaches the scan. Diamond and Pearl are left alone: their scripts are
        /// handled elsewhere.
        /// </summary>
        private void LocateStarterCommand()
        {
            _command = null;
            SpeciesEditable = true;
            CommandsHaveChanged = false;

            if (!IsDiamondOrPearl && StarterRotomSource.IsAvailable)
            {
                string remembered = RememberedChoice();
                _command = StarterRotomSource.FindByKey(remembered)
                        ?? StarterRotomSource.FindVanilla();

                if (_command == null)
                {
                    // The starter is not where it should be, so now it is worth reading the whole game.
                    var all = StarterRotomSource.FindAll();
                    _command = all.FirstOrDefault(c => c.NamedAsStarter);

                    // Say once that the give commands are not what they were, so a romhack that has
                    // added its own gets a chance to point the editor at the right one.
                    string now = Fingerprint(all);
                    CommandsHaveChanged = KnownFingerprint() != null && KnownFingerprint() != now;
                    RememberFingerprint(now);
                }

                if (_command != null) StarterLevel = _command.Level;
            }
            OnPropertyChanged(nameof(IsLevelSupported));
            OnPropertyChanged(nameof(CommandLocation));
            OnPropertyChanged(nameof(HasCommandLocation));
            OnPropertyChanged(nameof(CanManageCommand));
        }

        private bool _commandsHaveChanged;
        /// <summary>Set when the project has gained or lost a give command since the choice was made.</summary>
        public bool CommandsHaveChanged { get => _commandsHaveChanged; private set => Set(ref _commandsHaveChanged, value); }

        private static string Fingerprint(System.Collections.Generic.IEnumerable<StarterRotomSource.Match> all) =>
            string.Join(",", all.Select(c => c.Key).OrderBy(k => k, System.StringComparer.Ordinal));

        private static string KnownFingerprint()
        {
            var map = SettingsManager.Settings?.starterCommandFingerprint;
            return map != null && map.TryGetValue(ProjectKey(), out string f) ? f : null;
        }

        private static void RememberFingerprint(string fingerprint)
        {
            if (SettingsManager.Settings == null || KnownFingerprint() == fingerprint) return;
            SettingsManager.Settings.starterCommandFingerprint ??= new System.Collections.Generic.Dictionary<string, string>();
            SettingsManager.Settings.starterCommandFingerprint[ProjectKey()] = fingerprint;
            SettingsManager.Save();
        }

        private static string ProjectKey() => RomInfo.workDir ?? "";

        private static string RememberedChoice()
        {
            var map = SettingsManager.Settings?.starterCommandChoice;
            return map != null && map.TryGetValue(ProjectKey(), out string key) ? key : null;
        }

        /// <summary>The dialog's starting state: the candidates, with the current one picked out.</summary>
        public StarterCommandDialogViewModel NewCommandChoice() => new StarterCommandDialogViewModel(_command);

        /// <summary>
        /// Takes what the dialog came back with. A script that picks the species itself is allowed,
        /// but the species dropdowns are switched off: changing them there would do nothing.
        /// </summary>
        public void ApplyCommandChoice(StarterCommandDialogViewModel dialog)
        {
            if (dialog?.Chosen == null) return;
            SpeciesEditable = !dialog.SpeciesIsOutOfOurHands;
            ChooseCommand(dialog.Chosen);
        }

        private bool _speciesEditable = true;
        public bool SpeciesEditable { get => _speciesEditable; private set => Set(ref _speciesEditable, value); }

        /// <summary>Remembers which command the user said is the starter, for this project.</summary>
        public void ChooseCommand(StarterRotomSource.Match chosen)
        {
            if (chosen == null || SettingsManager.Settings == null) return;
            _command = chosen;
            StarterLevel = chosen.Level;
            SettingsManager.Settings.starterCommandChoice ??= new System.Collections.Generic.Dictionary<string, string>();
            SettingsManager.Settings.starterCommandChoice[ProjectKey()] = chosen.Key;
            CommandsHaveChanged = false;
            SettingsManager.Save();
            OnPropertyChanged(nameof(IsLevelSupported));
            OnPropertyChanged(nameof(CommandLocation));
            OnPropertyChanged(nameof(HasCommandLocation));
        }

        private void OnNamesChanged(object sender, System.EventArgs e)
        {
            DSPRE.Avalonia.Data.ListSync.Apply(PokemonNames, RomInfo.GetPokemonNames());
            DSPRE.Avalonia.Data.ListSync.Apply(ItemNames, RomInfo.GetItemNames());
        }

        /// <summary>Unsubscribes from app-wide events; call when the editor window closes.</summary>
        public void Detach() => AppEvents.NamesChanged -= OnNamesChanged;

        // ── Busy state (the rotom work after Save, see FinishSavingAsync) ──
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

            // With the sources in front of us, the held item and the level go through the script the
            // same way the Script Editor saves one: change the line, compile the project. Writing the
            // bytes instead would be undone the next time that script is compiled.
            bool throughTheScript = _command != null;
            if (IsHeldItemSupported && !throughTheScript)
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

            // The species bytes above are already on disk. What is left shells out to rotom, and
            // awaiting it here would freeze the app: SaveChanges runs straight off a Click handler.
            if ((touchedScripts.Count > 0 && RomInfo.hasRotomProject) || throughTheScript)
                _ = FinishSavingAsync(touchedScripts, throughTheScript);
        }

        /// <summary>
        /// The two rotom steps, one after the other and never at the same time: one turns binaries back
        /// into text, the other turns text into binaries, and running both at once would have them
        /// overwrite each other. Both shell out to rotom, so this stays off the UI thread.
        /// </summary>
        private async System.Threading.Tasks.Task FinishSavingAsync(
            System.Collections.Generic.List<int> touchedScripts, bool throughTheScript)
        {
            IsBusy = true;
            try
            {
                if (throughTheScript)
                {
                    BusyText = "Saving the script…";
                    await SaveThroughTheScriptAsync();
                }
                if (touchedScripts.Count > 0 && RomInfo.hasRotomProject)
                {
                    BusyText = "Reparsing scripts…";
                    try { await StarterPokemonData.RefreshRotomSourcesAsync(touchedScripts); }
                    catch (Exception ex) { AppLogger.Warn("StarterEditor: .rotom refresh failed: " + ex.Message); }
                }
            }
            finally
            {
                IsBusy = false;
                BusyText = null;
            }
        }

        /// <summary>
        /// Rewrites the give command's level and held item and compiles, which is what the Script
        /// Editor's own Save does. Reports what went wrong rather than leaving it silent.
        /// </summary>
        private async System.Threading.Tasks.Task SaveThroughTheScriptAsync()
        {
            var command = _command;
            int item = HeldItem;
            int level = StarterLevel;
            string itemName = item > 0 && item < ItemNames.Count
                ? StarterRotomSource.ItemToken(ItemNames[item]) : "ITEM_NONE";

            try
            {
                string failure = await StarterRotomSource.SaveAsync(command, level, itemName, item);
                if (failure != null) AppMessages.Error(failure, "Starter Pokémon Editor");
            }
            catch (Exception ex)
            {
                AppMessages.Error("The script could not be saved: " + ex.Message, "Starter Pokémon Editor");
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
