using Avalonia.Controls;
using Avalonia.Media.Imaging;
using DSPRE.Editors;
using DSPRE.LibNDSFormats;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using static DSPRE.RomInfo;
using Avalonia.Threading;
using DSPRE.Avalonia.Data;
using DSPRE.ROMFiles;
using DSPRE.HgEngine;

namespace DSPRE.Avalonia.ViewModels.Graphics
{
    public partial class BtxEditorViewModel
    {
        // Applying a state makes an entry pending only where it differs from the project now.
        private sealed record OwState(Dictionary<uint, byte[]> Files, Dictionary<uint, int[]> Render, Dictionary<uint, byte[]> Profiles);
        private readonly Dictionary<uint, byte[]> _originalFiles = new();
        private readonly Dictionary<uint, int[]> _originalRender = new();
        private readonly Dictionary<uint, OverworldSpriteProfileMetadataPatch> _knownPatches = new();
        private readonly Dictionary<uint, byte[]> _originalProfiles = new();
        private ByteStateUndo _undo;
        public bool CanUndo => _undo?.CanUndo == true;
        public bool CanRedo => _undo?.CanRedo == true;
        public void Undo() => _undo?.Undo();
        public void Redo() => _undo?.Redo();
        private void RaiseUndo() { OnPropertyChanged(nameof(CanUndo)); OnPropertyChanged(nameof(CanRedo)); }

        private static byte[] DiskFile(uint key)
        {
            if (!RomInfo.OverworldTable.TryGetValue(key, out (uint spriteID, ushort properties) entry)) return null;
            return ArchiveMembers.ReadOrNull(DirNames.OWSprites, (int)entry.spriteID);
        }

        private static int[] DiskRender(uint key) => OverworldSpriteTableExpansion.TryReadRenderState(key, out OverworldSpriteTableExpansion.OwRenderState s)
            ? new[] { s.DrawType, s.ShadowType, s.FootmarkType, s.ReflectType } : null;

        private void TouchFile(uint key) { if (!_originalFiles.ContainsKey(key)) _originalFiles[key] = DiskFile(key); }
        private void TouchRender(uint key) { if (!_originalRender.ContainsKey(key)) _originalRender[key] = DiskRender(key); }

        private byte[] TakeState()
        {
            Dictionary<uint, byte[]> files = new Dictionary<uint, byte[]>();
            foreach (uint key in _originalFiles.Keys)
                files[key] = _modifiedFiles.TryGetValue(key, out byte[] mod) ? mod : DiskFile(key);
            Dictionary<uint, int[]> render = new Dictionary<uint, int[]>();
            foreach (uint key in _originalRender.Keys)
                render[key] = _pendingRender.TryGetValue(key, out OverworldSpriteTableExpansion.OwRenderState r)
                    ? new[] { r.DrawType, r.ShadowType, r.FootmarkType, r.ReflectType } : DiskRender(key);
            Dictionary<uint, byte[]> profiles = new Dictionary<uint, byte[]>();
            foreach ((uint key, OverworldSpriteProfileMetadataPatch known) in _knownPatches)
                profiles[key] = _metadataPatches.TryGetValue(key, out OverworldSpriteProfileMetadataPatch pending) ? pending.Target() : known.Current();
            return DSPRE.Avalonia.UndoJson.Take(new OwState(files, render, profiles));
        }

        private void ApplyState(byte[] state)
        {
            OwState s = DSPRE.Avalonia.UndoJson.Read<OwState>(state);
            bool unreadable = false;
            foreach (uint key in _originalFiles.Keys)
            {
                byte[] want = s.Files != null && s.Files.TryGetValue(key, out byte[] f) ? f : _originalFiles[key];
                byte[] disk = DiskFile(key);
                if (want == null || (disk != null && want.AsSpan().SequenceEqual(disk)))
                {
                    _modifiedFiles.Remove(key);
                    _metadataPatches.Remove(key);
                }
                else
                {
                    _modifiedFiles[key] = want;
                    if (!StageProfile(key, s))
                    {
                        _modifiedFiles.Remove(key);
                        unreadable = true;
                    }
                }
            }
            foreach (uint key in _originalRender.Keys)
            {
                int[] want = s.Render != null && s.Render.TryGetValue(key, out int[] r) ? r : _originalRender[key];
                int[] disk = DiskRender(key);
                if (want == null || (disk != null && want.SequenceEqual(disk))) _pendingRender.Remove(key);
                else _pendingRender[key] = new OverworldSpriteTableExpansion.OwRenderState
                    { DrawType = want[0], ShadowType = want[1], FootmarkType = want[2], ReflectType = want[3] };
            }
            OnPropertyChanged(nameof(HasUnsavedChanges));
            OnPropertyChanged(nameof(ModifiedCount));
            LoadEntry(_selectedIndex);
            if (unreadable) StatusText = "Couldn't read that overworld's profile, so its picture wasn't changed.";
        }

        // Stages the profile value that goes with the file. False when it can't be read.
        private bool StageProfile(uint key, OwState s)
        {
            _metadataPatches.Remove(key);
            if (!_knownPatches.TryGetValue(key, out OverworldSpriteProfileMetadataPatch known)) return true;
            byte[] want = s.Profiles != null && s.Profiles.TryGetValue(key, out byte[] p) ? p : _originalProfiles.GetValueOrDefault(key);
            byte[] now = known.Current();
            if (want == null || now == null) return false;
            if (!want.AsSpan().SequenceEqual(now)) _metadataPatches[key] = known.Between(now, want);
            return true;
        }

        private void ResetUndo()
        {
            _originalProfiles.Clear();
            _originalFiles.Clear();
            _originalRender.Clear();
            _knownPatches.Clear();
            _undo = new ByteStateUndo(TakeState, ApplyState, RaiseUndo);
            RaiseUndo();
        }

        private void Edited() => _undo?.Record();
    }
}
