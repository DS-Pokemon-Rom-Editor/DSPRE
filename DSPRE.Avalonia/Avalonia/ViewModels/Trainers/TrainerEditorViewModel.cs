using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using global::Avalonia.Controls;
using global::Avalonia.Media.Imaging;
using global::Avalonia.Platform.Storage;
using DSPRE.Avalonia;
using DSPRE.Editors;
using DSPRE.HgEngine;
using DSPRE.ROMFiles;
using static DSPRE.RomInfo;
using Avalonia.Threading;

namespace DSPRE.Avalonia.ViewModels.Trainers
{
    /// <summary>
    /// Edits a trainer's properties and party, with the class sprite preview. The gender selector is
    /// editable whenever the game supports it, whatever the species.
    /// </summary>
    public partial class TrainerEditorViewModel : INotifyPropertyChanged, IEditorWithUnsavedChanges, DSPRE.Avalonia.ISupportsUndo, IDetachable
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
        private bool Set<T>(ref T f, T v, [CallerMemberName] string n = null)
        { if (EqualityComparer<T>.Default.Equals(f, v)) return false; f = v; OnPropertyChanged(n); return true; }

        public string HgEngineBanner => DSPRE.HgEngine.HgEngineProject.BannerText;
        public bool ShowHgEngineBanner => HgEngineBanner != null;

        private Window _owner;
        private bool _suppress;
        private TrainerFile _trainer;
        private int _loadedTrainerId = -1;
        // What the list row shows, so a save that changes either refreshes it.
        private string _savedName;
        private byte _savedClass;

        private string[] _abilityNames = Array.Empty<string>();
        private (int abi1, int abi2)[] _abilities = Array.Empty<(int, int)>();
        private bool _abilityEditable;
        private bool _genderEditable;
        private bool _shinyPatch;
        private bool _customPokeballsPatch;
        private bool _ballEnabled;
        private bool _formVisible;

        public ObservableCollection<string> TrainerNames { get; } = new ObservableCollection<string>();

        /// <summary>The trainer list as shown, filtered by its search box.</summary>
        public Data.FilteredNames TrainerList => _trainerList ??= new Data.FilteredNames(TrainerNames,
            () => SelectedTrainerIndex, v => SelectedTrainerIndex = v, this, nameof(SelectedTrainerIndex));
        private Data.FilteredNames _trainerList;
        public ObservableCollection<string> TrainerClassItems { get; } = new ObservableCollection<string>();
        public ObservableCollection<string> PokemonNames { get; } = new ObservableCollection<string>();
        public ObservableCollection<string> MoveNames { get; } = new ObservableCollection<string>();
        public ObservableCollection<string> ItemNames { get; } = new ObservableCollection<string>();
        public ObservableCollection<TrainerPartyMonViewModel> Party { get; } = new ObservableCollection<TrainerPartyMonViewModel>();
        public ObservableCollection<AiFlagViewModel> AiFlags { get; } = new ObservableCollection<AiFlagViewModel>();

        /// <summary>The trainer's battle messages, read-only here.</summary>
        public sealed record BattleLine(string When, string Text);
        public ObservableCollection<BattleLine> BattleLines { get; } = new ObservableCollection<BattleLine>();
        public bool HasBattleLines => BattleLines.Count > 0;

        private void LoadBattleLines(int trainer)
        {
            if (IsHgeActive) { LoadHgeBattleLines(trainer); return; }
            BattleLines.Clear();
            try
            {
                DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.TrainerTextTable, DirNames.TrainerTextOffset });
                TextArchive text = new TextArchive(TrainerMessageTextNumber);
                foreach (TrainerMessageTable.Entry e in TrainerMessageTable.Read().Where(e => e.TrainerId == trainer).OrderBy(e => e.TriggerId))
                    BattleLines.Add(new BattleLine(BattleMessageEditorViewModel.TriggerDescription(e.TriggerId),
                        e.MessageId >= 0 && e.MessageId < text.Messages.Count ? Shown(text.Messages[e.MessageId]) : ""));
            }
            catch (Exception ex) when (ex is IOException || ex is InvalidDataException || ex is ArgumentException)
            {
                AppLogger.Warn($"Battle messages of trainer {trainer}: {ex.Message}");
            }
            OnPropertyChanged(nameof(HasBattleLines));
        }

        // Line and page breaks are stored as escapes; here the text reads on as one paragraph.
        private static string Shown(string message) => string.Join(" ", message.Split(new[] { "\\n", "\\r", "\\f", "\n" }, StringSplitOptions.RemoveEmptyEntries).Select(p => p.Trim()));

        public TrainerEditorViewModel()
        {
            if (!Design.IsDesignMode) return;
            TrainerNames.Add("DESIGN TRAINER");
            for (int i = 0; i < AiFlagLabels.Length; i++) AiFlags.Add(new AiFlagViewModel(AiFlagLabels[i]) { Tip = AiFlagTip(i) });
        }

        public TrainerEditorViewModel(bool _) { }

        private void OnNamesChanged(object sender, System.EventArgs e)
        {
            ListSync.Apply(PokemonNames, GetPokemonNames());
            ListSync.Apply(MoveNames,    GetAttackNames());
            ListSync.Apply(ItemNames,    GetItemNames());
            if (_customPokeballsPatch) ShowCapsuleNames();
            string[] classNames = GetTrainerClassNames();
            // Replacing the selected row can clear the list's selection, as in RefreshTrainerListEntry.
            int keep = _selectedTrainerIndex;
            bool wasSuppressed = _suppress;
            _suppress = true;
            try
            {
                ListSync.Apply(TrainerNames, TrainerListEntries(classNames));
                if (keep >= 0 && keep < TrainerNames.Count)
                {
                    _selectedTrainerIndex = keep;
                    OnPropertyChanged(nameof(SelectedTrainerIndex));
                }
            }
            finally { _suppress = wasSuppressed; }

            List<string> formatted = new System.Collections.Generic.List<string>(classNames.Length);
            for (int i = 0; i < classNames.Length; i++) formatted.Add($"[{i:D3}] {classNames[i]}");
            ListSync.Apply(TrainerClassItems, formatted);
        }
        /// <summary>Unsubscribes from app-wide events; call when the editor window closes.</summary>
        public void Detach()
        {
            AppEvents.NamesChanged -= OnNamesChanged;
            AppEvents.RomPatchStateChanged -= OnRomPatchStateChanged;
            AppEvents.ClassIntrosSaved -= OnClassIntrosSaved;
            _spriteSaved?.Dispose();
            _propertiesSaved?.Dispose();
            Data.TrainerCapsuleCatalog.Changed -= ShowCapsuleNames;
            TextArchive.Saved -= OnTextSaved;
            StopPlaying();
        }

        private void OnClassIntrosSaved(object sender, EventArgs e) => Classes?.RefreshIntroSummary();

        public bool OffersShinyPatch => !_shinyPatch && GameFamily == GameFamilies.HGSS && !HgEngineProject.IsActive && TrainerShinyPatch.SupportsCurrentRom;
        public bool OffersBallPatch => !_customPokeballsPatch && !HgEngineProject.IsActive && TrainerCustomPokeballsPatch.SupportsCurrentRom;

        // Reconfigure controls without reloading party data or discarding unsaved edits.
        private void OnRomPatchStateChanged(object sender, EventArgs e)
        {
            bool shiny = GameFamily == GameFamilies.HGSS && TrainerShinyPatch.DetectCurrentProject();
            if (shiny != _shinyPatch)
            {
                _shinyPatch = shiny;
                foreach (TrainerPartyMonViewModel mon in Party) mon.ShinyEnabled = shiny;
                OnPropertyChanged(nameof(OffersShinyPatch));
            }
            Classes?.RefreshPatchState();
            bool customPokeballs = TrainerCustomPokeballsPatch.DetectCurrentProject();
            if (customPokeballs != _customPokeballsPatch)
            {
                _customPokeballsPatch = customPokeballs;
                if (customPokeballs) DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.ItemIcons });
                ShowCapsuleNames();
                OnPropertyChanged(nameof(OffersBallPatch));
            }
        }

        // A new list instance after a save makes every picker refresh its rows.
        private void ShowCapsuleNames()
        {
            IList<string> names = _customPokeballsPatch ? Array.Empty<string>() : Data.TrainerCapsuleCatalog.Names();
            foreach (TrainerPartyMonViewModel mon in Party) mon.ConfigureCapsulePicker(_customPokeballsPatch, names);
        }
    }

    public class AiFlagViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public event EventHandler Changed;
        public string Label { get; }
        /// <summary>The real bit value this flag represents when dynamically resolved from an hg-engine
        /// header (see <see cref="DSPRE.HgEngine.HgEngineTrainerFieldSchema"/>); -1 for the static,
        /// vanilla-derived flag lists that don't carry a resolvable value.</summary>
        public int Value { get; }
        private bool _checked;
        public bool Checked
        {
            get => _checked;
            set { if (_checked != value) { _checked = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Checked))); Changed?.Invoke(this, EventArgs.Empty); } }
        }
        /// <summary>What the flag makes the AI favour, or null for flags with no note.</summary>
        public string Tip { get; init; }
        public AiFlagViewModel(string label, int value = -1) { Label = label; Value = value; }
    }

    public class TrainerItemSlotViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public event EventHandler Changed;
        public ObservableCollection<string> ItemNames { get; }
        private int _itemIndex = -1;
        public int ItemIndex
        {
            get => _itemIndex;
            set
            {
                // A ComboBox reports -1 while its list refills; only a load may clear the item.
                if (value < 0)
                {
                    global::Avalonia.Threading.Dispatcher.UIThread.Post(() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ItemIndex))));
                    return;
                }
                if (_itemIndex != value) { _itemIndex = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ItemIndex))); Changed?.Invoke(this, EventArgs.Empty); }
            }
        }

        /// <summary>Sets the item as read, where -1 means one with no row.</summary>
        public void Load(int index)
        {
            if (_itemIndex == index) return;
            _itemIndex = index;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ItemIndex)));
        }

        public TrainerItemSlotViewModel(ObservableCollection<string> itemNames) { ItemNames = itemNames; }
    }
}
