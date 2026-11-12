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
        // The pokeanime file the editor edits: the front number, or the selected back slot's.
        private int CurrentScriptFile()
        {
            if (_scriptTarget <= 0) return _animFrontProg;
            int slot = _scriptTarget - 1;
            return (slot >= 0 && slot < AnimBack.Count) ? AnimBack[slot].Number : -1;
        }

        public string ProgramScriptHeader
        {
            get
            {
                string which = _scriptTarget == 0 ? "Front" : $"Back slot {_scriptTarget - 1}";
                return $"{which} animation {CurrentScriptFile()}: {Plural.Of(ProgramRows.Count, "command")}";
            }
        }
        private bool _scriptDirty;
        public bool ScriptDirty { get => _scriptDirty; private set => Set(ref _scriptDirty, value); }

        private void RefreshProgramScript()
        {
            foreach (ProgramCmdRow r in ProgramRows) r.PropertyChanged -= OnProgramRowChanged;
            ProgramRows.Clear();
            int file = CurrentScriptFile();
            if (IsAvailable && file >= 0)
            {
                EnsureAnimDefsNarc();
                byte[] bytes = _animDefsNarc?.GetRecord(file);
                List<PokemonAnimCommand> cmds = bytes != null ? PokeAnimScript.Parse(bytes) : null;
                if (cmds != null) foreach (PokemonAnimCommand c in cmds) AddProgramRow(c.Op, c.Args);
            }
            ScriptDirty = false;
            OnPropertyChanged(nameof(HasProgramScript)); OnPropertyChanged(nameof(ProgramScriptHeader));
        }

        private void AddProgramRow(PokemonAnimOp op, int[] args)
        {
            ProgramCmdRow row = new ProgramCmdRow { Op = op, ArgsText = string.Join(", ", args) };
            row.PropertyChanged += OnProgramRowChanged;
            ProgramRows.Add(row);
        }
        private void OnProgramRowChanged(object _, PropertyChangedEventArgs __) => ScriptDirty = true;

        public void AddProgramCmd()
        {
            AddProgramRow(PokemonAnimOp.SetStartDelay, new[] { 1 });
            ScriptDirty = true;
            OnPropertyChanged(nameof(HasProgramScript)); OnPropertyChanged(nameof(ProgramScriptHeader));
        }
        public void RemoveProgramCmd(ProgramCmdRow row)
        {
            if (row == null || !ProgramRows.Contains(row)) return;
            row.PropertyChanged -= OnProgramRowChanged;
            ProgramRows.Remove(row);
            ScriptDirty = true;
            OnPropertyChanged(nameof(HasProgramScript)); OnPropertyChanged(nameof(ProgramScriptHeader));
        }
        public void MoveProgramCmd(ProgramCmdRow row, int dir)
        {
            int i = ProgramRows.IndexOf(row), j = i + dir;
            if (i < 0 || j < 0 || j >= ProgramRows.Count) return;
            ProgramRows.Move(i, j);
            ScriptDirty = true;
        }

        // Args are padded or cut to each opcode's fixed count, so the stream stays valid.
        private List<PokemonAnimCommand> BuildCommandsFromRows()
        {
            if (_animDefsNarc == null)
                return new List<PokemonAnimCommand>();

            List<PokemonAnimCommand> cmds = new List<PokemonAnimCommand>();
            foreach (ProgramCmdRow row in ProgramRows)
            {
                int n = PokeAnimScript.ArgsFor(row.Op);
                List<int> parsed = NumberListText.Parse(row.ArgsText);
                int[] args = new int[n];
                for (int i = 0; i < n; i++)
                    args[i] = i < parsed.Count ? parsed[i] : 0;

                cmds.Add(new PokemonAnimCommand(row.Op, args));
            }
            return cmds;
        }

        public void SaveProgramScript()
        {
            int file = CurrentScriptFile();
            if (_animDefsNarc == null || file < 0) return;
            _animDefsNarc.PutRecord(file, PokeAnimScript.Serialize(BuildCommandsFromRows()));
            StopPlayback();
            RefreshProgramScript();   // reflect the canonical (padded) form
        }
    }
}
