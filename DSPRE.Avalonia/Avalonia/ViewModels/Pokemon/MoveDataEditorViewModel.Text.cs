using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Avalonia.Controls;
using DSPRE.HgEngine;
using DSPRE.ROMFiles;
using DSPRE.Resources;
using IEditorWithUnsavedChanges = global::DSPRE.Editors.IEditorWithUnsavedChanges;
using static DSPRE.MoveData;
using static DSPRE.RomInfo;
using DSPRE.Avalonia.Data;
using DSPRE.Avalonia.Views.Shell;
using DSPRE.Csv;

namespace DSPRE.Avalonia.ViewModels.Pokemon
{
    public partial class MoveDataEditorViewModel
    {
        private EditableTextBank _descBank, _bagBank;
        // hg-engine keeps bag descriptions in one archive per item generation.
        private readonly Dictionary<int, EditableTextBank> _bagBanks = new();
        private int[] _machineMoves = Array.Empty<int>();
        // Held with real line breaks. The archives store them as \n.
        private string _savedDescription = "", _bagText = "", _savedBagText = "";
        private int _bagItem = -1;

        public bool DescriptionReadOnly => _descBank?.ReadOnlyReason != null || _pendingMove != null;
        public string DescriptionReadOnlyReason => _pendingMove != null ? null : _descBank?.ReadOnlyReason;
        public bool HasBag => _bagItem >= 0;
        public string BagLabel { get; private set; } = "";
        public bool BagReadOnly => _bagBank?.ReadOnlyReason != null;
        public string BagText
        {
            get => _bagText;
            set
            {
                value = (value ?? "").Replace("\r\n", "\n");
                if (Set(ref _bagText, value)) { RaiseTextChecks(); if (!_loading) SetDirty(); }
            }
        }

        private static string Shown(string stored) => (stored ?? "").Replace("\\n", "\n");
        private static string Stored(string shown) => (shown ?? "").Replace("\n", "\\n");

        private void LoadTexts()
        {
            bool was = _loading;
            _loading = true;
            _savedDescription = _pendingMove == null && _descBank != null && _currentId < _descBank.Messages.Count
                ? Shown(_descBank.Messages[_currentId]) : "";
            Description = _savedDescription;
            int machine = Array.IndexOf(_machineMoves, _currentId);
            _bagItem = -1;
            if (machine >= 0 && _pendingMove == null && DSPRE.Editors.TmItemDescriptions.TryLocate(TMEditor.MachineItemId(machine), out int bank, out int line))
            {
                if (!_bagBanks.TryGetValue(bank, out _bagBank)) _bagBanks[bank] = _bagBank = new EditableTextBank(bank);
                if (line < _bagBank.Messages.Count) _bagItem = line;
            }
            BagLabel = machine >= 0 ? TMEditor.MachineLabelFromIndex(machine) : "";
            _savedBagText = _bagItem >= 0 ? Shown(_bagBank.Messages[_bagItem]) : "";
            BagText = _savedBagText;
            _loading = was;
            foreach (string n in new[] { nameof(DescriptionReadOnly), nameof(DescriptionReadOnlyReason), nameof(HasBag), nameof(BagLabel), nameof(BagReadOnly) })
                OnPropertyChanged(n);
            if (IsBagTab && !HasBag) PreviewTab = 0;
            RefreshPreview();
        }

        // Writes the description and bag text when they changed. Returns an error, or null.
        private string SaveTexts()
        {
            if (_descBank != null && _description != _savedDescription && _currentId < _descBank.Messages.Count)
            {
                _descBank.Messages[_currentId] = Stored(_description);
                if (_descBank.Save(this) is string error) return error;
                _savedDescription = _description;
            }
            if (_bagItem >= 0 && _bagText != _savedBagText)
            {
                _bagBank.Messages[_bagItem] = Stored(_bagText);
                if (_bagBank.Save(this) is string error) return error;
                _savedBagText = _bagText;
            }
            return null;
        }

        // The game prints these as stored, without wrapping, so text past the box is cut off.
        public const int DescriptionWidth = 120, DescriptionLines = 5, BagLines = 3;
        private FieldFont _systemFont;
        private bool _systemFontTried;
        public FieldFont SystemFont
        {
            get
            {
                if (_systemFontTried) return _systemFont;
                _systemFontTried = true;
                try { _systemFont = FieldFontCharacters.Ready ? FieldFont.LoadSystemFont() : null; }
                catch (Exception ex) { AppLogger.Warn("Move data editor: system font not read: " + ex.Message); }
                return _systemFont;
            }
        }
        public string DescriptionWarning => Overflow(_description, DescriptionWidth, DescriptionLines);
        public string BagWarning => Overflow(_bagText, MoveTextScreens.BagTextWidth, BagLines);
        public bool HasDescriptionWarning => DescriptionWarning != null;
        public bool HasBagWarning => BagWarning != null;

        private string Overflow(string text, int width, int maxLines)
        {
            List<string> lines = (text ?? "").Split('\n').ToList();
            if (lines.Count > 1 && lines[^1].Length == 0) lines.RemoveAt(lines.Count - 1);
            if (lines.Count > maxLines) return $"Only {maxLines} lines show";
            if (SystemFont == null) return null;
            for (int i = 0; i < lines.Count; i++)
            {
                int over = SystemFont.Measure(lines[i]) - width;
                if (over > 0) return $"Line {i + 1} runs {over} px past the box";
            }
            return null;
        }

        private void RaiseTextChecks()
        {
            foreach (string n in new[] { nameof(DescriptionWarning), nameof(HasDescriptionWarning), nameof(BagWarning), nameof(HasBagWarning) })
                OnPropertyChanged(n);
            RefreshPreview();
        }
    }
}
