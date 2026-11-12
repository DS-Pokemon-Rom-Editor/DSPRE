using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using global::Avalonia.Controls;
using global::Avalonia.Platform.Storage;
using DSPRE.Avalonia;
using DSPRE.Avalonia.Gl;
using DSPRE.Editors;
using DSPRE.Resources;
using DSPRE.Models;
using DSPRE.ROMFiles;
using LibNDSFormats.NSBMD;
using LibNDSFormats.NSBTX;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.ViewModels.World
{
    public partial class MapEditorViewModel
    {
        public void Save()
        {
            if (IsHeaderView)
            {
                int saved = 0;
                bool refused = false;
                foreach (HeaderMapCell cell in _headerCells)
                {
                    if (!cell.Dirty) continue;
                    if (MapFile.TooBigForTheGame(cell.Map.MapModelData?.Length ?? 0, cell.Map.Bdhc?.Length ?? 0) is string cellTooBig)
                    {
                        refused = true;
                        StatusText = $"Map {cell.MapIndex} not saved. " + cellTooBig;
                        _ = DialogHelper.ShowErrorAsync($"Map {cell.MapIndex}: {cellTooBig}\n\nIt wasn't saved.", "Map too big");
                        continue;
                    }
                    cell.Map.SaveToFileDefaultDir(cell.MapIndex, showSuccessMessage: false);
                    KeptPlatesFile.Save(cell.MapIndex, cell.Map.KeptPlates);
                    cell.Dirty = false;
                    saved++;
                    AppEvents.RaiseMapSaved(this, cell.MapIndex);
                }
                // Warps moved with a refused map's buildings wait for it, so events and maps stay in step.
                if (refused) return;
                SaveStagedMaps();
                SavePendingEvents();
                bool areaSaved = _areaDirty;
                SaveArea();
                SetClean();
                StatusText = saved > 0 || areaSaved ? $"Saved {Plural.Of(saved, "map")}{(areaSaved ? " and the area" : "")} for header {_headerId}." : "Nothing to save.";
                return;
            }
            if (_map == null || _selectedMapIndex < 0) return;
            // A map too big for the game's buffers blacks out the maps around it.
            if (MapFile.TooBigForTheGame(_map.MapModelData?.Length ?? 0, _map.Bdhc?.Length ?? 0) is string tooBig)
            {
                StatusText = "Not saved. " + tooBig;
                _ = DialogHelper.ShowErrorAsync(tooBig + "\n\nThe map wasn't saved.", "Map too big");
                return;
            }
            _archive.Put(_selectedMapIndex, _map.ToByteArray());
            SaveStagedMaps();
            KeptPlatesFile.Save(_selectedMapIndex, _map.KeptPlates);
            AppEvents.RaiseMapSaved(this, _selectedMapIndex);
            SaveArea();
            SavePendingEvents();
            SetClean();
            SaveNotice.Saved(UnsavedChangesDescription);
            StatusText = $"Saved map {_selectedMapIndex}.";
        }

        /// <summary>Brings in a map file or a model file. True when something came in.</summary>
        public async Task<bool> ImportAsync()
        {
            if (_selectedMapIndex < 0 || _map == null) return false;
            FilePickerFileType bothFiles = new FilePickerFileType("Map or model") { Patterns = new[] { "*.bin", "*.nsbmd" } };
            FilePickerFileType mapFiles = new FilePickerFileType("Map file") { Patterns = new[] { "*.bin" } };
            FilePickerFileType modelFiles = new FilePickerFileType("Map model") { Patterns = new[] { "*.nsbmd" } };
            FilePickerFileType allFiles = new FilePickerFileType("All files") { Patterns = new[] { "*" } };
            string path = await DialogHelper.OpenFileAsync(_owner, "Import map or model", new[] { bothFiles, mapFiles, modelFiles, allFiles });
            if (path == null) return false;
            byte[] data;
            try { data = DSUtils.ReadFromFile(path); }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                await DialogHelper.ShowErrorAsync($"Couldn't import:\n{ex.Message}", "Map editor");
                return false;
            }

            // The file's contents decide, so a renamed file or one with no extension still comes in.
            if (MapFile.ReadImport(data, GameFamily, out MapFile read, out byte[] model, out bool converted) is string whynot)
            {
                await DialogHelper.ShowErrorAsync(whynot, "Map editor");
                return false;
            }
            bool wholeMap = read != null;
            string done = null;
            // Filling the loaded map, not swapping it for a new one, keeps the import on the undo history.
            if (wholeMap)
            {
                _map.TakeContentsOf(read, GameFamily);
                SaveNotice.Imported($"map {_selectedMapIndex}", path);
                if (converted) done = "Converted from the other game's layout.";
            }
            else
            {
                _map.LoadMapModel(model, showMessages: false);
                SaveNotice.Imported($"the model of map {_selectedMapIndex}", path);
            }
            BuildPreview();
            RefreshBuildings();
            MarkDirty();
            OnPropertyChanged(nameof(Collisions));
            OnPropertyChanged(nameof(Types));
            MapLoaded?.Invoke(this, EventArgs.Empty);
            string missing = MapImportCheck.Note(_map, MapTilesetIndex);
            StatusText = string.Join(" ", new[] { done, missing }.Where(t => t != null));
            return true;
        }

        public async Task ExportAsync()
        {
            if (_map == null) return;
            FilePickerFileType filter = new FilePickerFileType("Map file") { Patterns = new[] { "*.bin" } };
            string path = await DialogHelper.SaveFileAsync(_owner, "Export map .bin", new[] { filter }, $"map_{_selectedMapIndex:D4}.bin");
            if (path == null) return;
            try
            {
                PlainFiles.WriteBytes(path, _map.ToByteArray());
                SaveNotice.Exported(path);
            }
            catch (Exception ex) when (AppLogger.Noted("MapEditorViewModel.ExportAsync", ex))
            {
                await DialogHelper.ShowErrorAsync($"Couldn't export:\n{ex.Message}", "Map editor");
            }
        }

        public void AddMapFile()
        {
            try
            {
                int newId = _archive.Add(new MapFile(new MemoryStream(_archive.Read(0)), GameFamily, discardMoveperms: true).ToByteArray());
                _platesToClear.Add(newId);   // a removed map's plates must not land on the new one
                MapNames.Add("Map " + newId);
                SelectedMapIndex = newId;
                OnPropertyChanged(nameof(HasUnsavedChanges));
                StatusText = $"Added map file {newId}.";
            }
            catch (Exception ex) when (AppLogger.Noted("MapEditorViewModel.AddMapFile", ex)) { _ = DialogHelper.ShowErrorAsync($"Couldn't add map file:\n{ex.Message}", "Map editor"); }
        }

        public async Task RemoveLastMapFileAsync()
        {
            if (MapNames.Count == 0) return;
            int last = MapNames.Count - 1;
            if (!await DialogHelper.AskYesNoAsync($"Delete the last map file ({last})?", "Delete map file?")) return;
            try
            {
                // The edits belong to the file being deleted.
                if (_selectedMapIndex == last) { SetClean(); SelectedMapIndex = last - 1; }
                _archive.RemoveLast();
                _platesToClear.Add(last);
                MapNames.RemoveAt(last);
                OnPropertyChanged(nameof(HasUnsavedChanges));
                StatusText = $"Removed map file {last}.";
            }
            catch (Exception ex) when (AppLogger.Noted("MapEditorViewModel.RemoveLastMapFileAsync", ex)) { _ = DialogHelper.ShowErrorAsync($"Couldn't remove map file:\n{ex.Message}", "Map editor"); }
        }

        private byte[] MapTextureData()
        {
            if (_mapTilesetIndex < 0) return null;
            return ArchiveMembers.ReadOrNull(DirNames.MapTextures, _mapTilesetIndex);
        }
        private string ModelName() => $"map_{_selectedMapIndex:D4}";

        public async Task ExportNsbmdAsync()
        {
            if (_map == null) return;
            FilePickerFileType filter = new FilePickerFileType("Model (.nsbmd)") { Patterns = new[] { "*.nsbmd" } };
            string path = await DialogHelper.SaveFileAsync(_owner, "Export map model", new[] { filter }, ModelName() + ".nsbmd");
            if (path == null) return;
            try { PlainFiles.WriteBytes(path, _map.MapModelData); SaveNotice.Exported(path); }
            catch (Exception ex) when (AppLogger.Noted("MapEditorViewModel.ExportNsbmdAsync", ex)) { await DialogHelper.ShowErrorAsync($"Couldn't export:\n{ex.Message}", "Map editor"); }
        }
        /// <summary>Exports the model with the map texture pack built in.</summary>
        public async Task ExportTexturedNsbmdAsync()
        {
            if (_map?.MapModelData == null) return;
            byte[] pack = ArchiveMembers.ReadOrNull(DirNames.MapTextures, MapTilesetIndex);
            if (pack == null) { await DialogHelper.ShowErrorAsync($"Map textures {MapTilesetIndex} couldn't be found.", "Map editor"); return; }
            FilePickerFileType filter = new FilePickerFileType("Model (.nsbmd)") { Patterns = new[] { "*.nsbmd" } };
            string path = await DialogHelper.SaveFileAsync(_owner, "Export textured map model", new[] { filter }, ModelName() + "_textured.nsbmd");
            if (path == null) return;
            try
            {
                PlainFiles.WriteBytes(path, NSBUtils.BuildNSBMDwithTextures(_map.MapModelData, pack));
                SaveNotice.Exported(path);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException)
            {
                await DialogHelper.ShowErrorAsync($"Couldn't export:\n{ex.Message}", "Map editor");
            }
        }

        /// <summary>Shows the saved map file in the file manager; unsaved changes aren't in it.</summary>
        public void ShowMapFile()
        {
            if (_selectedMapIndex < 0) return;
            SystemShell.RevealInFileManager(ArchiveMembers.PathOf(DirNames.Maps, _selectedMapIndex));
            if (HasUnsavedChanges) StatusText = "The file holds the map as last saved.";
        }

        /// <summary>Sets every square of the layer shown, collision or type, to 0.</summary>
        public void ClearPermissionLayer()
        {
            if (_map == null || !IsSingleMap) return;
            byte[,] layer = PermissionShowsType ? _map.Types : _map.Collisions;
            Array.Clear(layer, 0, layer.Length);
            MarkDirty();
            RebuildOverlay();
            StatusText = PermissionShowsType ? "Cleared the type layer." : "Cleared the collision layer.";
        }

        public void ExportDae() => ExportModel(glb: false);
        public void ExportGlb() => ExportModel(glb: true);

        private void ExportModel(bool glb)
        {
            if (_map == null) return;
            try
            {
                string folder = ModelUtils.ExportModel(ModelName(), _map.MapModelData, MapTextureData(), glb);
                if (folder != null) SaveNotice.Exported(folder);
            }
            catch (Exception ex) { AppLogger.Error((glb ? "GLB" : "DAE") + " export: " + ex.Message); }
        }

        public async Task ImportTerrainAsync()
        {
            if (_map == null) return;
            string path = await DialogHelper.OpenFileAsync(_owner, "Import terrain", new[] { new FilePickerFileType("Terrain (.bdhc)") { Patterns = new[] { "*.bdhc", "*.bin", "*.*" } } });
            if (path == null) return;
            // Kept plates belong to the terrain being replaced.
            try { _map.ImportTerrain(DSUtils.ReadFromFile(path)); _map.KeptPlates.Clear(); MarkDirty(); SaveNotice.Imported($"the terrain of map {_selectedMapIndex}", path); }
            catch (Exception ex) when (AppLogger.Noted("MapEditorViewModel.ImportTerrainAsync", ex)) { await DialogHelper.ShowErrorAsync($"Couldn't import:\n{ex.Message}", "Map editor"); }
        }
        public async Task ExportTerrainAsync()
        {
            if (_map == null) return;
            string path = await DialogHelper.SaveFileAsync(_owner, "Export terrain", new[] { new FilePickerFileType("Terrain (.bdhc)") { Patterns = new[] { "*.bdhc" } } }, ModelName() + ".bdhc");
            if (path == null) return;
            try { PlainFiles.WriteBytes(path, _map.Bdhc); SaveNotice.Exported(path); }
            catch (Exception ex) when (AppLogger.Noted("MapEditorViewModel.ExportTerrainAsync", ex)) { await DialogHelper.ShowErrorAsync($"Couldn't export:\n{ex.Message}", "Map editor"); }
        }

        public async Task ImportSoundAsync()
        {
            if (_map == null) return;
            string path = await DialogHelper.OpenFileAsync(_owner, "Import sound plates", new[] { new FilePickerFileType("Sound plates (.bgs)") { Patterns = new[] { "*.bgs", "*.bin", "*.*" } } });
            if (path == null) return;
            try { _map.ImportSoundPlates(DSUtils.ReadFromFile(path)); MarkDirty(); SaveNotice.Imported($"the sound plates of map {_selectedMapIndex}", path); }
            catch (Exception ex) when (AppLogger.Noted("MapEditorViewModel.ImportSoundAsync", ex)) { await DialogHelper.ShowErrorAsync($"Couldn't import:\n{ex.Message}", "Map editor"); }
        }
        public async Task ExportSoundAsync()
        {
            if (_map == null) return;
            string path = await DialogHelper.SaveFileAsync(_owner, "Export sound plates", new[] { new FilePickerFileType("Sound plates (.bgs)") { Patterns = new[] { "*.bgs" } } }, ModelName() + ".bgs");
            if (path == null) return;
            try { PlainFiles.WriteBytes(path, _map.Bgs); SaveNotice.Exported(path); }
            catch (Exception ex) when (AppLogger.Noted("MapEditorViewModel.ExportSoundAsync", ex)) { await DialogHelper.ShowErrorAsync($"Couldn't export:\n{ex.Message}", "Map editor"); }
        }
        public void BlankSound() { if (_map != null) { _map.Bgs = MapFile.BlankBGS; MarkDirty(); StatusText = "Blanked the sound plates."; } }

        public async Task ImportPermissionsAsync()
        {
            if (_map == null) return;
            string path = await DialogHelper.OpenFileAsync(_owner, "Import permissions", new[] { new FilePickerFileType("Permissions") { Patterns = new[] { "*.mp", "*.bin", "*.*" } } });
            if (path == null) return;
            try
            {
                _map.ImportPermissions(DSUtils.ReadFromFile(path));
                MarkDirty();
                OnPropertyChanged(nameof(Collisions)); OnPropertyChanged(nameof(Types));
                RebuildOverlay();
                MapLoaded?.Invoke(this, EventArgs.Empty);
                SaveNotice.Imported($"the permissions of map {_selectedMapIndex}", path);
            }
            catch (Exception ex) when (AppLogger.Noted("MapEditorViewModel.ImportPermissionsAsync", ex)) { await DialogHelper.ShowErrorAsync($"Couldn't import:\n{ex.Message}", "Map editor"); }
        }
        public async Task ExportPermissionsAsync()
        {
            if (_map == null) return;
            string path = await DialogHelper.SaveFileAsync(_owner, "Export permissions", new[] { new FilePickerFileType("Permissions") { Patterns = new[] { "*.mp" } } }, ModelName() + ".mp");
            if (path == null) return;
            try { PlainFiles.WriteBytes(path, _map.CollisionsToByteArray()); SaveNotice.Exported(path); }
            catch (Exception ex) when (AppLogger.Noted("MapEditorViewModel.ExportPermissionsAsync", ex)) { await DialogHelper.ShowErrorAsync($"Couldn't export:\n{ex.Message}", "Map editor"); }
        }

        public async Task ImportBuildingsAsync()
        {
            if (_map == null) return;
            string path = await DialogHelper.OpenFileAsync(_owner, "Import buildings", new[] { new FilePickerFileType("Buildings") { Patterns = new[] { "*.bld", "*.bin", "*.*" } } });
            if (path == null) return;
            try
            {
                _map.ImportBuildings(DSUtils.ReadFromFile(path));
                RefreshBuildings(); RebuildPreview(); MarkDirty();
                SaveNotice.Imported($"the buildings of map {_selectedMapIndex}", path);
            }
            catch (Exception ex) when (AppLogger.Noted("MapEditorViewModel.ImportBuildingsAsync", ex)) { await DialogHelper.ShowErrorAsync($"Couldn't import:\n{ex.Message}", "Map editor"); }
        }
        public async Task ExportBuildingsAsync()
        {
            if (_map == null) return;
            string path = await DialogHelper.SaveFileAsync(_owner, "Export buildings", new[] { new FilePickerFileType("Buildings") { Patterns = new[] { "*.bld" } } }, ModelName() + ".bld");
            if (path == null) return;
            try { PlainFiles.WriteBytes(path, _map.BuildingsToByteArray()); SaveNotice.Exported(path); }
            catch (Exception ex) when (AppLogger.Noted("MapEditorViewModel.ExportBuildingsAsync", ex)) { await DialogHelper.ShowErrorAsync($"Couldn't export:\n{ex.Message}", "Map editor"); }
        }
        public void DuplicateBuilding()
        {
            (MapFile map, int cellX, int cellY, Building building)? resolved = ResolveSelectedBuilding();
            if (resolved == null) return;
            (MapFile map, int _, int _, Building building) = resolved.Value;
            map.Buildings.Add(new Building(building));
            if (IsHeaderView)
            {
                (int ci, int _) = _headerBuildingIndex[_selectedBuildingIndex];
                _headerCells[ci].Dirty = true;
                RefreshBuildings(); MarkDirty();
                SelectedBuildingIndex = _headerBuildingIndex.FindLastIndex(t => t.CellIndex == ci);
                RebuildPreview();
                return;
            }
            RefreshBuildings(); MarkDirty();
            SelectedBuildingIndex = _map.Buildings.Count - 1;
            RebuildPreview();
        }

        /// <summary>Names every tile behaviour any map file uses, comma-separated.</summary>
        public string ScanUsedTypes()
        {
            SortedSet<byte> used = new SortedSet<byte>();
            int count = Filesystem.GetMapCount();
            for (int i = 0; i < count; i++)
            {
                try { used.UnionWith(new MapFile(i, GameFamily, discardMoveperms: false).GetUsedTypes()); }
                catch { /* skip unreadable map */ }
            }
            List<string> parts = new List<string>();
            foreach (byte b in used) parts.Add(TilePermissions.BehaviourLabel(b, GameFamily));
            StatusText = $"{Plural.Of(used.Count, "distinct type")} used across all maps.";
            return string.Join(", ", parts);
        }
    }
}
