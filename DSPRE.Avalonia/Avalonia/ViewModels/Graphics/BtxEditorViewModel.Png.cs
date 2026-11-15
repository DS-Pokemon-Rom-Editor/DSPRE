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
        /// <summary>Imports a PNG, or with apply false only reports why not. Null on success.</summary>
        public string ImportPng(string filePath, bool apply = true)
        {
            if (_btxData == null || _selectedIndex < 0) return "No entry selected.";
            try
            {
                RawImage import;
                import = ImageConverter.DecodeRawImageFile(filePath);
                if (import == null) return "Image couldn't be decoded.";
                RawImage current = BTX0.ReadRaw(_btxData, CurrentPaletteIndex);
                if (current == null) return "This entry's texture file isn't a readable image (it may be a 3D model, not a flat texture).";
                if (import.Width != current.Width || import.Height != current.Height)
                    return $"The PNG is {import.Width}×{import.Height} but the texture is {current.Width}×{current.Height}.";

                // Both palettes share the pixels, so the imported one keeps its slot order.
                byte[] newData = BTX0.WriteKeepingPalette(_btxData, import, CurrentPaletteIndex, out string writeError);
                if (newData == null) return writeError;
                if (!apply || newData.AsSpan().SequenceEqual(_btxData)) return null;
                _btxData = newData;

                uint key = _owKeys[_selectedIndex];
                TouchFile(key);
                _modifiedFiles[key] = newData;

                RefreshImage();
                OnPropertyChanged(nameof(HasUnsavedChanges));
                OnPropertyChanged(nameof(ModifiedCount));
                Edited();
                return null;
            }
            catch (Exception ex) when (AppLogger.Noted("BtxEditorViewModel.ImportPng", ex))
            {
                return ex.Message;
            }
        }

        /// <summary>True when the PNG needs a full profile. error is set when none fits.</summary>
        public bool TryGetCompatibleProfiles(
            string filePath,
            out List<OverworldGraphicsProfileOption> profiles,
            out string error)
        {
            profiles = new List<OverworldGraphicsProfileOption>();
            error = null;
            if (_btxData == null || !HasSelectedEntry)
            {
                error = "No entry selected.";
                return false;
            }

            try
            {
                RawImage import;
                import = ImageConverter.DecodeRawImageFile(filePath);
                if (import == null)
                {
                    error = "Image couldn't be decoded.";
                    return false;
                }

                RawImage current = BTX0.ReadRaw(_btxData, 0);
                if (current == null)
                {
                    error = "This entry's texture isn't a 16-colour BTX0 DSPRE can read.";
                    return false;
                }
                if (import.Width == current.Width && import.Height == current.Height)
                    return false;

                uint colorCount = CountColors(import);
                uint targetKey = _owKeys[_selectedIndex];
                Dictionary<uint, Btx0Structure> structures = new Dictionary<uint, Btx0Structure>();
                Dictionary<uint, int> sharedCounts = RomInfo.OverworldTable.Values
                    .GroupBy(v => v.spriteID)
                    .ToDictionary(g => g.Key, g => g.Count());
                foreach (KeyValuePair<uint, (uint spriteID, ushort properties)> entry in RomInfo.OverworldTable)
                {
                    if (entry.Key == targetKey || entry.Value.spriteID == 0x3D3D) continue;
                    if (!structures.TryGetValue(entry.Value.spriteID, out Btx0Structure structure))
                    {
                        byte[] member = ArchiveMembers.ReadOrNull(DirNames.OWSprites, (int)entry.Value.spriteID);
                        if (member == null || !Btx0Structure.TryInspect(member, out structure, out _))
                            continue;
                        structures.Add(entry.Value.spriteID, structure);
                    }
                    if (
                        structure.SheetWidth != import.Width || structure.SheetHeight != import.Height ||
                        structure.Palettes.Count == 0 || structure.Palettes[0].ColorCapacity < colorCount)
                        continue;

                    int sharedBy = sharedCounts[entry.Value.spriteID];
                    profiles.Add(new OverworldGraphicsProfileOption
                    {
                        AppearanceId = entry.Key,
                        SpriteMember = entry.Value.spriteID,
                        Label = $"{OverworldLabels.Of(entry.Key)} · slot {entry.Value.spriteID} · " +
                            $"{structure.Textures.Count} entries / {structure.UniqueTextureBlockCount} stored frames" +
                            (sharedBy > 1 ? $" · art shared by {sharedBy} appearances" : ""),
                    });
                }

                profiles = profiles
                    .OrderBy(p => p.SpriteMember)
                    .ThenBy(p => p.AppearanceId)
                    .ToList();
                if (profiles.Count == 0)
                    error = $"No existing overworld profile in this ROM accepts a {import.Width}×{import.Height} PNG with {colorCount} colours.";
                return true;
            }
            catch (Exception ex) when (AppLogger.Noted("BtxEditorViewModel.TryGetCompatibleProfiles", ex))
            {
                error = ex.Message;
                return false;
            }
        }

        public string ImportPngUsingProfile(string filePath, uint sourceAppearanceId)
        {
            if (_btxData == null || !HasSelectedEntry) return "No entry selected.";
            if (!RomInfo.OverworldTable.TryGetValue(sourceAppearanceId, out (uint spriteID, ushort properties) sourceEntry))
                return "The selected profile is no longer present in the overworld table.";

            uint targetAppearanceId = _owKeys[_selectedIndex];
            // Only this appearance's animation row takes the profile. Another sharing the texture would break.
            uint targetMember = RomInfo.OverworldTable[targetAppearanceId].spriteID;
            int sharers = RomInfo.OverworldTable.Count(kv => kv.Value.spriteID == targetMember);
            if (sharers > 1)
                return $"Texture slot {targetMember} is shared by {sharers} appearances, and a new profile would break the others. Give this appearance its own texture slot first.";
            try
            {
                RawImage import;
                import = ImageConverter.DecodeRawImageFile(filePath);
                if (import == null) return "Image couldn't be decoded.";

                byte[] sourceData = ArchiveMembers.ReadOrNull(DirNames.OWSprites, (int)sourceEntry.spriteID);
                if (sourceData == null) return "The selected profile's BTX file wasn't found.";

                if (!Btx0Structure.TryInspect(sourceData, out Btx0Structure structure, out string structureError))
                    return "DSPRE can't read the selected profile: " + structureError;
                if (structure.SheetWidth != import.Width || structure.SheetHeight != import.Height)
                    return $"The selected profile expects {structure.SheetWidth}×{structure.SheetHeight}, but the PNG is {import.Width}×{import.Height}.";
                uint colors = CountColors(import);
                if (structure.Palettes.Count == 0 || colors > structure.Palettes[0].ColorCapacity)
                    return $"The selected profile can't hold the PNG's {colors} colours.";

                if (!OverworldSpriteProfileMetadata.TryCreatePatch(
                    targetAppearanceId, sourceAppearanceId, out OverworldSpriteProfileMetadataPatch metadataPatch, out string metadataError))
                    return metadataError;

                RawImage profileImage = BTX0.ReadRaw(sourceData, 0);
                if (profileImage == null || profileImage.Width != import.Width || profileImage.Height != import.Height)
                    return "DSPRE can only write 16-colour textures, and this profile isn't one.";

                // The profile's own art is replaced, so its palette is rebuilt from the picture.
                byte[] newData = BTX0.WriteNewPalette(sourceData, import, 0, out string writeError);
                if (newData == null) return writeError;
                _btxData = newData;
                TouchFile(targetAppearanceId);
                _modifiedFiles[targetAppearanceId] = newData;
                if (!_originalProfiles.ContainsKey(targetAppearanceId)) _originalProfiles[targetAppearanceId] = metadataPatch.Current();
                _metadataPatches[targetAppearanceId] = metadataPatch;
                _knownPatches[targetAppearanceId] = metadataPatch;

                RefreshImage();
                StatusText = $"Imported a {import.Width}×{import.Height} picture using {OverworldLabels.Of(sourceAppearanceId)}'s profile.";
                OnPropertyChanged(nameof(HasUnsavedChanges));
                OnPropertyChanged(nameof(ModifiedCount));
                Edited();
                return null;
            }
            catch (Exception ex) when (AppLogger.Noted("BtxEditorViewModel.ImportPngUsingProfile", ex))
            {
                return ex.Message;
            }
        }

        public string GetSelectedMemberUsageWarning()
        {
            if (!HasSelectedEntry) return null;
            uint targetKey = _owKeys[_selectedIndex];
            uint member = RomInfo.OverworldTable[targetKey].spriteID;
            uint[] users = RomInfo.OverworldTable
                .Where(kv => kv.Value.spriteID == member)
                .Select(kv => kv.Key)
                .ToArray();
            if (users.Length <= 1) return null;
            return $"Texture slot {member} is shared by {users.Length} appearances. Saving this import changes their art too. Import anyway?";
        }

        public bool ExportPng(string filePath)
        {
            if (_btxData == null) return false;
            try
            {
                RawImage raw = BTX0.ReadRaw(_btxData, CurrentPaletteIndex);
                if (raw == null) return false;
                ImageConverter.ToAvaloniaBitmap(raw).Save(filePath, PngBitmapEncoderOptions.Default);
                return true;
            }
            catch (Exception ex) { AppLogger.WarnOnce("BtxEditorViewModel.ExportPng", ex); return false; }
        }
    }
}
