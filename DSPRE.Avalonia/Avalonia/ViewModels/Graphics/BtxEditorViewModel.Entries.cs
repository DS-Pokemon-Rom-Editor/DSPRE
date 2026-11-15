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
        // Diamond, Pearl and Platinum keep a render-properties table; HeartGold and SoulSilver do not.
        private bool _hasRenderTable;
        public bool HasRenderTable => _hasRenderTable;

        public bool IsExpansionApplied => OverworldSpriteTableExpansion.IsApplied;
        public string ExpansionStatusText => HgEngine.HgEngineProject.IsActive
            ? "Saved into the hg-engine checkout. New NPCs go in data/graphics/overworlds/custom."
            : IsExpansionApplied
            ? $"The overworld sprite expansion patch is applied. {OverworldSpriteTableExpansion.UsedCount} of {OverworldSpriteTableExpansion.Capacity} custom slots used."
            : "Adding and deleting entries needs hzla's overworld sprite expansion (PlatPatches). The drawing settings below can still be edited.";

        private bool _isSelectedEntryCustom;
        public bool IsSelectedEntryCustom { get => _isSelectedEntryCustom; private set => Set(ref _isSelectedEntryCustom, value); }

        public bool ShowEntryButtons => HgEngine.HgEngineProject.IsActive || HasRenderTable && IsExpansionApplied;
        public string AddEntryNote => HgEngine.HgEngineProject.IsActive ? "Adds a new NPC to the hg-engine checkout" : "Needs the overworld sprite expansion patch";

        public bool CanAddEntry => HgEngine.HgEngineProject.IsActive
            || IsExpansionApplied && OverworldSpriteTableExpansion.UsedCount < OverworldSpriteTableExpansion.Capacity;
        public bool CanDeleteSelected => IsExpansionApplied && HasSelectedEntry && IsSelectedEntryCustom;

        public string[] DrawTypeOptions { get; } = { "None", "Billboard", "3D model" };
        public string[] ShadowTypeOptions { get; } = { "None", "On" };
        public string[] FootmarkTypeOptions { get; } = { "None", "Normal (2-leg)", "Cycle (bike)" };
        public string[] ReflectTypeOptions { get; } = { "None", "On (billboard reflection)" };

        private bool _hasRenderState;
        public bool HasRenderState { get => _hasRenderState; private set => Set(ref _hasRenderState, value); }

        private bool _loadingRenderState;
        private int _drawTypeIndex, _shadowTypeIndex, _footmarkTypeIndex, _reflectTypeIndex;
        public int DrawTypeIndex { get => _drawTypeIndex; set { if (Set(ref _drawTypeIndex, value)) CommitRenderState(); } }
        public int ShadowTypeIndex { get => _shadowTypeIndex; set { if (Set(ref _shadowTypeIndex, value)) CommitRenderState(); } }
        public int FootmarkTypeIndex { get => _footmarkTypeIndex; set { if (Set(ref _footmarkTypeIndex, value)) CommitRenderState(); } }
        public int ReflectTypeIndex { get => _reflectTypeIndex; set { if (Set(ref _reflectTypeIndex, value)) CommitRenderState(); } }

        private string _rendererInfoText;
        public string RendererInfoText { get => _rendererInfoText; private set => Set(ref _rendererInfoText, value); }
        private string _animationInfoText;
        public string AnimationInfoText { get => _animationInfoText; private set => Set(ref _animationInfoText, value); }

        /// <summary>Adds a custom entry. Without picked art it shares the template's.</summary>
        public string AddEntryWithImage(string appearanceIdText, uint templateMember, uint cloneFrom, string pngPath, string rawBtxPath)
        {
            if (HgEngine.HgEngineProject.IsActive) return AddSourceNpc(templateMember, cloneFrom, pngPath, rawBtxPath);
            if (!TryParseId(appearanceIdText, "Appearance ID", out uint appearanceId, out string error)) return error;

            bool hasImage = rawBtxPath != null || pngPath != null;
            uint mmodelMember = hasImage ? OverworldSpriteTableExpansion.AllocateNewMmodelSlot() : templateMember;

            if (!TryValidateTemplateForCloneSource(templateMember, cloneFrom, out error))
                return error;

            byte[] stagedImage = null;
            if (rawBtxPath != null && !TryBuildRawBtx(templateMember, rawBtxPath, out stagedImage, out error))
                return error;
            if (pngPath != null && !TryBuildPngBtx(templateMember, pngPath, out stagedImage, out error))
                return error;

            // Everything is checked before the table row is added, so a failure leaves no row.
            if (!OverworldSpriteTableExpansion.AddEntry(appearanceId, mmodelMember, cloneFrom, out error))
                return error;

            if (stagedImage != null)
            {
                string newMemberPath = Path.Combine(
                    RomInfo.GameDirs[DirNames.OWSprites].unpackedDir,
                    mmodelMember.ToString("D4"));
                try
                {
                    ArchiveMembers.Put(DirNames.OWSprites, (int)mmodelMember, stagedImage);
                }
                catch (Exception ex)
                {
                    try
                    {
                        ArchiveMembers.Delete(newMemberPath);
                    }
                    catch (Exception removeError) { AppLogger.Warn("BTX editor: removing the new member: " + removeError.Message); }

                    if (!OverworldSpriteTableExpansion.DeleteEntry(appearanceId, out string rollbackError))
                        return $"The new texture couldn't be created ({ex.Message}), and the table entry couldn't be undone: {rollbackError}";
                    return "The new texture couldn't be created, so the table entry was undone: " + ex.Message;
                }
            }

            RomInfo.ReadOWTable();
            LoadEntryList();
            ResetUndo();

            SelectEntry(_owKeys.IndexOf(appearanceId));
            OnPropertyChanged(nameof(ExpansionStatusText));
            OnPropertyChanged(nameof(CanAddEntry));

            return null;
        }

        // hg-engine: a NEW_NPC_ENTRY row from the clone source, and MON_OVERWORLD_GFX_START one higher.
        // On success the project's a/0/8/1 copy shifts too, as the new member sits before every follower.
        private string AddSourceNpc(uint templateMember, uint cloneFrom, string pngPath, string rawBtxPath)
        {
            if (!TryValidateTemplateForCloneSource(templateMember, cloneFrom, out string error)) return error;
            byte[] image = null;
            if (rawBtxPath != null && !TryBuildRawBtx(templateMember, rawBtxPath, out image, out error)) return error;
            if (pngPath != null && !TryBuildPngBtx(templateMember, pngPath, out image, out error)) return error;

            List<HgEngineOverworlds.Member> members = HgEngine.HgEngineOverworlds.Members(out error);
            if (members == null) return error;
            if (templateMember >= members.Count) return "The checkout has no source for that template.";
            if (image != null && OverworldSourceFiles.Check(members[(int)templateMember], image) is string problem) return problem;

            if (!HgEngine.HgEngineOverworlds.TryAddNpc((int)templateMember, out int tag, out int gfx, out HgEngineOverworlds.Member added, out error)) return error;
            // The checkout has the NPC now, so the rest runs even when a step fails.
            string trouble = null;
            if (image != null && OverworldSourceFiles.Write(added, image) is string written)
            {
                trouble = $"NPC {tag} was added but its picture couldn't be saved: {written}";
                image = null;
            }
            int props = RomInfo.OverworldTable.TryGetValue(cloneFrom, out (uint spriteID, ushort properties) clone) ? clone.properties : 0;
            if (props != 0 && !HgEngine.HgEngineOverworlds.TrySetProperties(tag, props, out error))
                trouble ??= $"NPC {tag} was added but its properties couldn't be copied: {error}";

            try
            {
                byte[] picture = image ?? ArchiveMembers.Read(DirNames.OWSprites, (int)templateMember);
                ArchiveMembers.ShiftUpFollowingCheckout(DirNames.OWSprites, gfx);
                ArchiveMembers.Put(DirNames.OWSprites, gfx, picture);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                trouble ??= $"NPC {tag} was added to the checkout, but DSPRE's copy of the textures couldn't follow ({ex.Message}). Build to bring it in line.";
            }

            RomInfo.ReadOWTable();
            LoadEntryList();
            ResetUndo();
            SelectEntry(_owKeys.IndexOf((uint)tag));
            OnPropertyChanged(nameof(CanAddEntry));
            StatusText = $"Added NPC {tag}.";
            return trouble;
        }

        private static bool TryBuildRawBtx(uint templateMember, string rawBtxPath, out byte[] stagedImage, out string error)
        {
            stagedImage = null;
            error = null;
            if (!ArchiveMembers.Has(DirNames.OWSprites, (int)templateMember)) { error = "The template's texture file is missing."; return false; }
            try
            {
                byte[] templateData = ArchiveMembers.Read(DirNames.OWSprites, (int)templateMember);
                if (!Btx0Structure.TryInspect(templateData, out Btx0Structure targetStructure, out string targetError))
                { error = "Template texture slot is unreadable: " + targetError; return false; }

                byte[] sourceData = DSUtils.ReadFromFile(rawBtxPath);
                if (!Btx0Structure.TryInspect(sourceData, out Btx0Structure sourceStructure, out string sourceError))
                { error = "That file isn't a texture DSPRE can read: " + sourceError; return false; }

                if (targetStructure.ProfileDifference(sourceStructure) is string why)
                { error = $"That file can't replace this overworld: {why}."; return false; }

                RawImage source = BTX0.ReadRaw(sourceData, 0);
                if (source == null) { error = "That file isn't a texture DSPRE can write (a 16-colour BTX0)."; return false; }

                stagedImage = sourceData;
                return true;
            }
            catch (Exception ex) when (AppLogger.Noted("BtxEditorViewModel.TryBuildRawBtx", ex))
            {
                error = ex.Message;
                return false;
            }
        }

        private static bool TryBuildPngBtx(uint templateMember, string pngPath, out byte[] stagedImage, out string error)
        {
            stagedImage = null;
            error = null;
            if (!ArchiveMembers.Has(DirNames.OWSprites, (int)templateMember)) { error = "The template's texture file is missing."; return false; }
            try
            {
                byte[] btxData = ArchiveMembers.Read(DirNames.OWSprites, (int)templateMember);
                RawImage import;
                import = ImageConverter.DecodeRawImageFile(pngPath);
                if (import == null) { error = "Image couldn't be decoded."; return false; }
                RawImage current = BTX0.ReadRaw(btxData, 0);
                if (current == null) { error = "Template texture slot is unreadable."; return false; }
                if (import.Width != current.Width || import.Height != current.Height)
                { error = $"The PNG is {import.Width}×{import.Height} but the template is {current.Width}×{current.Height}."; return false; }

                // New art has no palette to keep, so it gets one in the template's normal slot.
                stagedImage = BTX0.WriteNewPalette(btxData, import, 0, out error);
                return stagedImage != null;
            }
            catch (Exception ex) when (AppLogger.Noted("BtxEditorViewModel.TryBuildPngBtx", ex))
            {
                error = ex.Message;
                return false;
            }
        }

        private static bool TryValidateTemplateForCloneSource(uint templateMember, uint cloneFrom, out string error)
        {
            error = null;
            if (!RomInfo.OverworldTable.TryGetValue(cloneFrom, out (uint spriteID, ushort properties) cloneEntry))
            {
                error = "The clone source is no longer present in the overworld table.";
                return false;
            }

            byte[] templateBytes = ArchiveMembers.ReadOrNull(DirNames.OWSprites, (int)templateMember);
            byte[] cloneBytes = ArchiveMembers.ReadOrNull(DirNames.OWSprites, (int)cloneEntry.spriteID);
            if (templateBytes == null || cloneBytes == null)
            {
                error = "The template's or the copied entry's texture file is missing.";
                return false;
            }

            if (!Btx0Structure.TryInspect(templateBytes, out Btx0Structure template, out string templateError))
            {
                error = "The template isn't a texture DSPRE can read: " + templateError;
                return false;
            }
            if (!Btx0Structure.TryInspect(cloneBytes, out Btx0Structure clone, out string cloneError))
            {
                error = "The entry to copy isn't a texture DSPRE can read: " + cloneError;
                return false;
            }
            if (template.ProfileDifference(clone) is string why)
            {
                error = $"The template doesn't match the overworld being copied: {why}. Pick a template the same size and shape.";
                return false;
            }
            return true;
        }

        /// <summary>Deletes the selected entry. Returns null on success, otherwise the error.</summary>
        public string DeleteSelectedEntry()
        {
            if (!HasSelectedEntry) return "No entry selected.";
            uint key = _owKeys[_selectedIndex];
            if (!OverworldSpriteTableExpansion.DeleteEntry(key, out string error)) return error;

            _modifiedFiles.Remove(key);
            _metadataPatches.Remove(key);
            _pendingRender.Remove(key);
            OnPropertyChanged(nameof(HasUnsavedChanges));
            OnPropertyChanged(nameof(ModifiedCount));

            RomInfo.ReadOWTable();
            LoadEntryList();
            ResetUndo();
            SelectEntry(OwEntries.Count > 0 ? 0 : -1);
            OnPropertyChanged(nameof(ExpansionStatusText));
            OnPropertyChanged(nameof(CanAddEntry));
            return null;
        }

        public void OpenAt(uint entry)
        {
            int index = _owKeys.IndexOf(entry);
            if (index >= 0) SelectEntry(index);
        }

        // Always reloads, as Add and Delete reshuffle the list under an index.
        private void SelectEntry(int index)
        {
            _selectedIndex = index;
            OnPropertyChanged(nameof(SelectedIndex));
            OnPropertyChanged(nameof(HasSelectedEntry));
            LoadEntry(index);
        }

        private static bool TryParseId(string text, string label, out uint value, out string error)
        {
            value = 0; error = null;
            text = (text ?? "").Trim();
            bool ok = text.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                ? uint.TryParse(text.Substring(2), System.Globalization.NumberStyles.HexNumber, null, out value)
                : uint.TryParse(text, out value);
            if (!ok) error = $"{label}: \"{text}\" isn't a valid number (decimal or 0x hex).";
            return ok;
        }
    }
}
