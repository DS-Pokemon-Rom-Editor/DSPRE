using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using DSPRE.Avalonia.BattleAnim.Particles;
using DSPRE.Avalonia.Data;
using DSPRE.Avalonia.Gl;
using DSPRE.HgEngine;
using DSPRE.ROMFiles;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using static DSPRE.RomInfo;
using static MKDS_Course_Editor.NSBTP.NSBTP.NSBTP_File;
using IEditorWithUnsavedChanges = global::DSPRE.Editors.IEditorWithUnsavedChanges;

namespace DSPRE.Avalonia.ViewModels.Battle
{
    public partial class BattleDisplayEditorViewModel
    {
        private sealed record DisplayState(int[] Values, int[][] Back, int[][] Pattern, int[][] Front, int[][] BackRuns);
        private ByteStateUndo _undo;
        public bool CanUndo => _undo?.CanUndo == true;
        public bool CanRedo => _undo?.CanRedo == true;
        public void Undo() => _undo?.Undo();
        public void Redo() => _undo?.Redo();
        private void RaiseUndo() { OnPropertyChanged(nameof(CanUndo)); OnPropertyChanged(nameof(CanRedo)); }

        private byte[] TakeState() => DSPRE.Avalonia.UndoJson.Take(new DisplayState(
            new[] { _partyPaletteIndex, _spriteY, _shadowX, _shadowSize, _frontHeightM, _frontHeightF, _backHeightM, _backHeightF,
                    _formFrontH, _formBackH, _animFrontProg, _animFrontWait, _animFrontCryDelay, _animBackCryDelay },
            AnimBack.Select(s => new[] { s.Number, s.Wait }).ToArray(),
            AnimSteps.Select(s => new[] { s.Frame, s.Wait }).ToArray(),
            FrontFrameEntries.Select(f => new[] { f.FrameNo, f.Duration, f.HorizontalShift, f.VerticalShift }).ToArray(),
            BackFrameEntries.Select(f => new[] { f.FrameNo, f.Duration, f.HorizontalShift, f.VerticalShift }).ToArray()));

        private void ApplyState(byte[] state)
        {
            DisplayState s = DSPRE.Avalonia.UndoJson.Read<DisplayState>(state);
            int[] v = s.Values;
            _loading = true;
            try
            {
                if (v[0] >= 0 && v[0] < PartyPalettes.Count) PartyPaletteIndex = v[0];
                SpriteY = v[1]; ShadowX = v[2]; ShadowSize = v[3];
                FrontHeightM = v[4]; FrontHeightF = v[5]; BackHeightM = v[6]; BackHeightF = v[7];
                FormFrontHeight = v[8]; FormBackHeight = v[9];
                AnimFrontProgNum = v[10]; AnimFrontWait = v[11]; AnimFrontCryDelay = v[12]; AnimBackCryDelay = v[13];
                foreach (AnimProgStep st in AnimBack) st.PropertyChanged -= OnAnimStepChanged;
                AnimBack.Clear();
                foreach (int[] b in s.Back) AddBackStep(b[0], b[1]);
                foreach (AnimPatternStep st in AnimSteps) st.PropertyChanged -= OnAnimStepChanged;
                AnimSteps.Clear();
                foreach (int[] p in s.Pattern) AddPatternStep(p[0], p[1]);
                OnPropertyChanged(nameof(CanAddAnimStep));
                if (RecordFamily)
                    LoadFrameEntries(s.Front.Select(f => new SpriteFrameSlot(f[0], f[1], f[2], f[3])), s.BackRuns.Select(f => new SpriteFrameSlot(f[0], f[1], f[2], f[3])));
                else RecomputePatternSlots();
                RaiseLayout();
                RefreshPreview();
            }
            finally { _loading = false; }
            bool dirty = _undo.IsDirty || _pendingIconGraphic != null;
            if (_dirty != dirty) { _dirty = dirty; OnPropertyChanged(nameof(HasUnsavedChanges)); }
        }
        private void SetClean() { if (!_dirty) return; _dirty = false; OnPropertyChanged(nameof(HasUnsavedChanges)); }
    }
}
