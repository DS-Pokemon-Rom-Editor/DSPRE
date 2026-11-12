using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using DSPRE.Avalonia.BattleAnim;
using DSPRE.HgEngine;
using DSPRE.ROMFiles;
using global::Avalonia.Media;
using global::Avalonia.Media.Imaging;
using global::Avalonia.Threading;
using DSPRE.Avalonia.Data;
using static DSPRE.RomInfo;
using Avalonia.Platform;
using System.Runtime.InteropServices;

namespace DSPRE.Avalonia.ViewModels.Battle
{
    /// <summary>Battle scripts (waza_seq, be_seq, sub_seq) and move animations, Pt and HGSS.</summary>
    public sealed partial class BattleScriptEditorViewModel : INotifyPropertyChanged, DSPRE.Editors.IEditorWithUnsavedChanges, ISupportsUndo
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
        private bool Set<T>(ref T field, T value, [CallerMemberName] string n = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value; OnPropertyChanged(n); return true;
        }

        public enum Archive { MoveScripts = 0, EffectScripts = 1, Subroutines = 2, MoveAnimation = 3, SubAnimation = 4 }

        private readonly WazaSeqVersion _version;
        private string[] _moveNames;
        private readonly ScriptNarc[] _narcs = new ScriptNarc[5];

        public bool IsAvailable { get; }
        public string UnavailableText => "Not in this game";

        public BattleScriptEditorViewModel()
        {
            IsAvailable = GameFamily == GameFamilies.Plat || GameFamily == GameFamilies.HGSS;
            _version = GameFamily switch
            {
                GameFamilies.DP => WazaSeqVersion.DP,
                GameFamilies.Plat => WazaSeqVersion.Plat,
                _ => WazaSeqVersion.HGSS,
            };
            _moveNames = SafeMoveNames();

            _narcs[(int)Archive.MoveScripts]  = new ScriptNarc(DirNames.WazaSeq);
            _narcs[(int)Archive.EffectScripts] = new ScriptNarc(DirNames.BeSeq);
            _narcs[(int)Archive.Subroutines]  = new ScriptNarc(DirNames.SubSeq);
            _narcs[(int)Archive.MoveAnimation] = new ScriptNarc(DirNames.WazaEffectScripts);
            _narcs[(int)Archive.SubAnimation] = new ScriptNarc(DirNames.WazaEffectSub);

            _dirs[(int)Archive.MoveScripts] = DirNames.WazaSeq;
            _dirs[(int)Archive.EffectScripts] = DirNames.BeSeq;
            _dirs[(int)Archive.Subroutines] = DirNames.SubSeq;
            _dirs[(int)Archive.MoveAnimation] = DirNames.WazaEffectScripts;
            _dirs[(int)Archive.SubAnimation] = DirNames.WazaEffectSub;

            if (IsAvailable) SelectArchive(0);
        }

        private static string[] SafeMoveNames()
        {
            try { return GetAttackNames(); } catch (Exception ex) { AppLogger.WarnOnce("BattleScriptEditorViewModel.SafeMoveNames", ex); return Array.Empty<string>(); }
        }
    }

    /// <summary>One command card. Summary is its tooltip. Args holds the canonical values.</summary>
    public sealed class ScriptCmdRow : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void Raise(string n) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

        /// <summary>Words the reader could not decode, shown and saved as they are.</summary>
        public bool Raw { get; init; }

        internal System.Func<int, string> OpNameOf;
        internal bool Script;
        internal System.Func<int, System.Collections.Generic.IReadOnlyList<int>, int> ArgCountOf;
        internal System.Action<ScriptCmdRow> OnEdited;
        internal System.Func<int, string> PreviewSound;   // returns an error message, or null on success
        internal System.Func<int, string> SoundNameOf;

        public System.Collections.Generic.List<int> Args { get; } = new();
        public ObservableCollection<ParamVM> Params { get; } = new();

        private int _opId;
        public int OpId
        {
            get => _opId;
            set
            {
                // A card's command list sends -1 while it has no items, and reads the real value once it has.
                if (value < 0) { Dispatcher.UIThread.Post(() => Raise(nameof(OpId))); return; }
                if (_opId == value) return;
                _opId = value; Raise(nameof(OpId)); Raise(nameof(OpName)); Raise(nameof(OpDisplay)); Raise(nameof(ValuesSummary)); PadArgs(); Rebuild(); OnEdited?.Invoke(this);
            }
        }
        // Pads the args to the opcode's fixed count. Extra ones stay, for variable-length opcodes.
        private void PadArgs() { int need = ArgCountOf?.Invoke(_opId, Args) ?? 0; while (Args.Count < need) Args.Add(0); }
        public string OpName => OpNameOf?.Invoke(_opId) ?? ("op" + _opId);
        public string OpDisplay => BattleAnimSchema.OpcodeDisplay(OpName, Script);
        public string OpDoc => BattleAnimSchema.OpcodeDoc(OpName, Script);
        public bool HasDoc => !string.IsNullOrEmpty(OpDoc);
        // Sets the opcode during load without raising an edit. The caller rebuilds.
        internal void _opIdSilent(int id) { _opId = id; Raise(nameof(OpId)); Raise(nameof(OpName)); Raise(nameof(OpDisplay)); Raise(nameof(ValuesSummary)); Raise(nameof(OpDoc)); Raise(nameof(HasDoc)); }

        private bool _expanded;
        public bool IsExpanded { get => _expanded; set { if (_expanded != value) { _expanded = value; Raise(nameof(IsExpanded)); } } }

        // Tooltip line: "OPCODE  name=val  name=val …"
        public string Summary
        {
            get
            {
                if (Args.Count == 0) return OpDisplay;
                StringBuilder sb = new System.Text.StringBuilder(OpDisplay).Append("  ");
                for (int i = 0; i < Args.Count; i++)
                {
                    if (i > 0) sb.Append("  ");
                    sb.Append(BattleAnimSchema.ParamLabel(OpName, i, Args, Script)).Append('=').Append(Args[i]);
                }
                return sb.ToString();
            }
        }

        /// <summary>The values alone, for the line under a collapsed card's title.</summary>
        public string ValuesSummary
        {
            get
            {
                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < Args.Count; i++)
                {
                    if (i > 0) sb.Append("  ");
                    sb.Append(BattleAnimSchema.ParamLabel(OpName, i, Args, Script)).Append('=').Append(Args[i]);
                }
                return sb.ToString();
            }
        }
        public bool HasValues => Args.Count > 0;

        // Raw args, comma or space separated, for adding or removing them on variable-length opcodes.
        public string RawArgs
        {
            get => string.Join(", ", Args);
            set
            {
                Args.Clear();
                foreach (string t in (value ?? "").Split(new[] { ',', ' ', '\t' }, System.StringSplitOptions.RemoveEmptyEntries))
                {
                    string s = t.Trim();
                    int v = s.StartsWith("0x", System.StringComparison.OrdinalIgnoreCase)
                        ? System.Convert.ToInt32(s.Substring(2), 16)
                        : (int.TryParse(s, out int n) ? n : 0);
                    Args.Add(v);
                }
                Rebuild(); OnEdited?.Invoke(this);
            }
        }

        // Builds the typed param editors from the current args and schema.
        internal void Rebuild()
        {
            Params.Clear();
            for (int i = 0; i < Args.Count; i++)
                Params.Add(new ParamVM(BattleAnimSchema.ParamLabel(OpName, i, Args, Script), Args[i], i, this,
                                       BattleAnimSchema.EnumFor(OpName, i)));
            Raise(nameof(Summary)); Raise(nameof(ValuesSummary)); Raise(nameof(HasValues)); Raise(nameof(RawArgs)); Raise(nameof(HasParams));
        }
        public bool HasParams => Args.Count > 0;

        internal ParamVM ParamAt(int index)
        {
            foreach (ParamVM p in Params) if (p.Index == index) return p;
            return null;
        }

        /// <summary>Sets a value as its editor would, so the card shows it.</summary>
        internal void SetArg(int index, int v)
        {
            ParamVM p = ParamAt(index);
            if (p != null) p.Value = v; else SetParam(index, v);
        }

        internal void SetParam(int index, int v)
        {
            if (index < 0 || index >= Args.Count || Args[index] == v) return;
            Args[index] = v;
            // A message command's tag decides how many values follow it.
            if (Script && !Raw && ArgCountOf != null)
            {
                int need = ArgCountOf(_opId, Args);
                if (need != Args.Count)
                {
                    while (Args.Count < need) Args.Add(0);
                    if (Args.Count > need) Args.RemoveRange(need, Args.Count - need);
                    Rebuild();
                }
            }
            // A new routine number names its values differently.
            if (!Script && index == 0 && OpName == "CallFunc")
                foreach (ParamVM p in Params) p.Name = BattleAnimSchema.ParamLabel(OpName, p.Index, Args);
            Raise(nameof(Summary)); Raise(nameof(ValuesSummary)); Raise(nameof(HasValues)); Raise(nameof(RawArgs));
            OnEdited?.Invoke(this);
        }
    }

    /// <summary>One argument editor in a command card. Writes straight into the row's args.</summary>
    public sealed class ParamVM : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private readonly ScriptCmdRow _row;
        private readonly int _index;
        internal int Index => _index;
        private string _name;
        public string Name { get => _name; set { if (_name != value) { _name = value; Raise(nameof(Name)); } } }

        private readonly System.Collections.Generic.List<int> _enumValues = new();
        public ObservableCollection<string> EnumItems { get; } = new();
        public bool IsEnum { get; }
        public bool IsInt => !IsEnum;

        private int _value;
        public int Value
        {
            get => _value;
            set { if (_value != value) { _value = value; Raise(nameof(Value)); Raise(nameof(ValueDec)); Raise(nameof(SelectedEnumIndex)); Raise(nameof(SoundName)); _row.SetParam(_index, value); } }
        }
        // NumericUpDown.Value is decimal?
        public decimal ValueDec { get => _value; set { Value = (int)value; } }

        // A Sound argument gets a name lookup and a preview button beside its number.
        public bool IsSound => Name == "Sound";
        public string SoundName => IsSound ? _row.SoundNameOf?.Invoke(_value) : null;
        /// <summary>Plays this sound. Returns an error to show, or null on success.</summary>
        public string PreviewSound() => IsSound ? _row.PreviewSound?.Invoke(_value) : null;

        public int SelectedEnumIndex
        {
            get => _enumValues.IndexOf(_value);
            set { if (value >= 0 && value < _enumValues.Count) Value = _enumValues[value]; }
        }

        private void Raise(string n) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

        public ParamVM(string name, int value, int index, ScriptCmdRow row, BattleAnimSchema.EnumOption[] options)
        {
            Name = name; _value = value; _index = index; _row = row;
            if (options != null)
            {
                IsEnum = true;
                foreach (BattleAnimSchema.EnumOption o in options) { EnumItems.Add($"{o.Label}  ({o.Value})"); _enumValues.Add(o.Value); }
                if (!_enumValues.Contains(value)) { EnumItems.Add($"(raw {value})"); _enumValues.Add(value); }   // keep an out-of-table value selectable
            }
        }
    }
}
