using DSPRE.Avalonia.Data;
using DSPRE.Avalonia.Gl;
using LibNDSFormats.NSBMD;
using DSPRE.Editors;
using DSPRE.Resources;
using DSPRE.ROMFiles;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using static DSPRE.RomInfo;
using System.IO;

namespace DSPRE.Avalonia.ViewModels.World
{
    public partial class DistortionWorldViewModel
    {
        public void SaveChanges()
        {
            if (!HasUnsavedChanges) return;

            try
            {
                CommitShownFloor();
                foreach (FloorRow row in Floors.Where(f => _editedMembers.Contains(f.Floor.DataMember)))
                {
                    _archive.Put(row.Floor.DataMember, row.Data.ToByteArray());
                }

                foreach (int id in _editedGrids)
                {
                    if (!_gridCache.TryGetValue(id, out ushort[] grid)) continue;
                    _attributes.Put(id, TornWorldSurfaces.GridToBytes(grid));
                }

                foreach (KeyValuePair<int, MapFile> kv in _shapeEdited)
                {
                    if (!_shapeChanged.Contains(kv.Key)) continue;
                    kv.Value.SaveToFileDefaultDir(kv.Key, showSuccessMessage: false);
                    AppEvents.RaiseMapSaved(this, kv.Key);
                }

                _editedMembers.Clear();
                _editedGrids.Clear();
                _shapeChanged.Clear();
                Raise(nameof(HasUnsavedChanges));
                ResetSteps();
                StatusText = "Saved.";
            }
            catch (Exception ex)
            {
                AppLogger.Error("DistortionWorld.SaveChanges: " + ex.Message);
                AppMessages.Error("That couldn't be saved. " + ex.Message, "Distortion World");
            }
        }

        private void CommitShownFloor()
        {
            if (_shownFloorIndex < 0 || _shownFloorIndex >= Floors.Count) return;

            TornWorldFile data = Floors[_shownFloorIndex].Data;
            data.Platforms.Clear(); data.Platforms.AddRange(Platforms);
            data.JumpPoints.Clear(); data.JumpPoints.AddRange(JumpPoints);
            data.CameraRegions.Clear(); data.CameraRegions.AddRange(CameraRegions);
            data.GhostProps.Clear(); data.GhostProps.AddRange(GhostProps);
            data.GhostTriggers.Clear(); data.GhostTriggers.AddRange(GhostTriggers);
        }

        public Task<bool> SaveChangesAsync()
        {
            SaveChanges();
            return Task.FromResult(!HasUnsavedChanges);
        }

        public void DiscardChanges()
        {
            if (!HasUnsavedChanges) return;
            _editedMembers.Clear();
            _editedGrids.Clear();

            _shapeChanged.Clear();
            _shapeEdited.Clear();
            MapModel.Open(null, 0, GameFamily, null);
            _gridCache.Clear();
            _shownGrids.Clear();
            _shownFloorIndex = -1;
            Floors.Clear();
            Platforms.Clear(); JumpPoints.Clear(); CameraRegions.Clear();
            GhostProps.Clear(); GhostTriggers.Clear();
            _selectedFloorIndex = -1;
            Load();
            Raise(nameof(HasUnsavedChanges));
        }
    }
}
