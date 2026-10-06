using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using DSPRE.HgEngine;
using IEditorWithUnsavedChanges = global::DSPRE.Editors.IEditorWithUnsavedChanges;

namespace DSPRE.Avalonia.ViewModels.Tools
{
    /// <summary>One setting from include/config.h or armips/include/config.s.</summary>
    public sealed class HgEngineSettingRow : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        internal readonly HgEngineConfig.Setting Setting;
        private readonly System.Action _changed;

        internal HgEngineSettingRow(HgEngineConfig.Setting setting, System.Action changed) { Setting = setting; _changed = changed; }

        public string Name => Setting.Name;
        public string Description => Setting.Description;
        public bool HasDescription => Setting.Description.Length > 0;
        public bool CanDisable => Setting.CanDisable;
        public bool HasValue => Setting.Value != null;

        public bool Enabled
        {
            get => Setting.Enabled;
            set { if (Setting.Enabled == value) return; Setting.Enabled = value; Raise(); _changed(); }
        }

        public string Value
        {
            get => Setting.Value;
            set { if (Setting.Value == value || value == null) return; Setting.Value = value; Raise(); _changed(); }
        }

        internal void Refresh() { Raise(nameof(Enabled)); Raise(nameof(Value)); }
        private void Raise([CallerMemberName] string n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    }

    /// <summary>
    /// hg-engine's build settings in one list: config.h's defines and config.s's assembler values, each with the
    /// comment its file gives it. A setting both files hold shows once and is changed in both. Takes effect on the
    /// next compile.
    /// </summary>
    public class HgEngineSettingsViewModel : INotifyPropertyChanged, IEditorWithUnsavedChanges, ISupportsUndo
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void Raise([CallerMemberName] string n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

        private List<HgEngineConfig.Setting> _settings = new();
        private List<HgEngineSettingRow> _all = new();
        public ObservableCollection<HgEngineSettingRow> CodeSettings { get; } = new();
        public ObservableCollection<HgEngineSettingRow> AssemblerSettings { get; } = new();

        private string _filter = "";
        public string Filter { get => _filter; set { if (_filter == (value ?? "")) return; _filter = value ?? ""; Raise(); Show(); } }

        private string _status = "";
        public string StatusText { get => _status; set { _status = value; Raise(); } }

        private byte[] _saved;
        private ByteStateUndo _undo;
        public bool CanUndo => _undo?.CanUndo == true;
        public bool CanRedo => _undo?.CanRedo == true;
        public void Undo() => _undo?.Undo();
        public void Redo() => _undo?.Redo();

        private byte[] TakeState() => ByteStateUndo.Pack(w =>
        {
            foreach (HgEngineConfig.Setting s in _settings) { w.Write(s.Enabled); w.Write(s.Value != null); w.Write(s.Value ?? ""); }
        });

        private void ApplyState(byte[] state)
        {
            ByteStateUndo.Unpack(state, r =>
            {
                foreach (HgEngineConfig.Setting s in _settings) { s.Enabled = r.ReadBoolean(); bool has = r.ReadBoolean(); string v = r.ReadString(); s.Value = has ? v : null; }
            });
            foreach (HgEngineSettingRow row in _all) row.Refresh();
            Changed();
        }

        public bool HasUnsavedChanges => _saved != null && !TakeState().AsSpan().SequenceEqual(_saved);
        public string UnsavedChangesDescription => "hg-engine settings";

        public HgEngineSettingsViewModel() { }

        public HgEngineSettingsViewModel(bool load)
        {
            if (load) Load();
        }

        private void Load()
        {
            if (!HgEngineConfig.TryRead(out _settings, out string error)) { StatusText = error; _settings = new(); }
            _all = _settings.Where(s => !s.FollowsHeader).Select(s => new HgEngineSettingRow(s, Changed)).ToList();
            _saved = TakeState();
            _undo = new ByteStateUndo(TakeState, ApplyState, () => { Raise(nameof(CanUndo)); Raise(nameof(CanRedo)); });
            Raise(nameof(CanUndo)); Raise(nameof(CanRedo));
            Raise(nameof(HasUnsavedChanges));
            Show();
        }

        private void Show()
        {
            CodeSettings.Clear();
            AssemblerSettings.Clear();
            foreach (HgEngineSettingRow row in _all.Where(r => _filter.Length == 0 || SearchMatch.Contains(r.Name + " " + r.Description, _filter)))
                (row.Setting.File == HgEngineConfig.HeaderRelPath ? CodeSettings : AssemblerSettings).Add(row);
        }

        private void Changed()
        {
            Raise(nameof(HasUnsavedChanges));
            _undo?.Record();
        }

        public void SaveChanges() => _ = SaveChangesAsync();

        public async Task<bool> SaveChangesAsync()
        {
            if (!HasUnsavedChanges) return true;
            (bool saved, string error) = await HgEngineSave.RunAsync(() => HgEngineConfig.TryWrite(_settings, out string e) ? null : e);
            if (!saved)
            {
                if (error != null) await DialogHelper.ShowError("The settings were not saved:\n" + error, "hg-engine Settings");
                return false;
            }
            Load();
            SaveNotice.Saved(UnsavedChangesDescription);
            StatusText = "Saved. Compile the ROM to apply them.";
            return true;
        }

        public void DiscardChanges() => Load();
    }
}
