using DSPRE.Editors.Utils;
using DSPRE.Resources;
using DSPRE.ROMFiles;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using static DSPRE.RomInfo;

namespace DSPRE {
    /// <summary>
    /// The sprite data behind the Pokémon Sprite Editor: four poses as raw palette indices plus the
    /// normal and shiny palettes. Everything that reads or writes sprite bytes goes through here, so
    /// the editor form only has to deal with drawing and buttons.
    /// </summary>
    public class PokemonSpriteModel {
        public const int SpriteWidth = 160;
        public const int SpriteHeight = 80;
        public const int FrameWidth = 80;
        public const int PixelCount = SpriteWidth * SpriteHeight;

        // Slot order matches the NARC's own: 0 = Female Back, 1 = Male Back, 2 = Female Front, 3 = Male Front.
        public const int SlotFemaleBack = 0;
        public const int SlotMaleBack = 1;
        public const int SlotFemaleFront = 2;
        public const int SlotMaleFront = 3;

        private const int SpriteEntrySize = 6448;
        private const int PaletteEntrySize = 72;

        public struct FormSpriteData {
            public string Name;
            public int BackSpriteIndex;
            public int FrontSpriteIndex;
            public int NormalPaletteIndex;
            public int ShinyPaletteIndex;

            public FormSpriteData(string name, int backIdx, int frontIdx, int normalPal, int shinyPal) {
                Name = name;
                BackSpriteIndex = backIdx;
                FrontSpriteIndex = frontIdx;
                NormalPaletteIndex = normalPal;
                ShinyPaletteIndex = shinyPal;
            }
        }

        #region State
        private byte[][] rawSprites = new byte[4][];
        private uint[] normalPal;
        private uint[] shinyPal;
        private bool[] normalPalUsed = AllUsed();
        private bool[] shinyPalUsed = AllUsed();

        private int currentId;
        private FormSpriteData[] currentFormData = new FormSpriteData[0];
        private int selectedFormIndex = -1;
        private bool isAlternateForms;
        private bool formSharesBaseData;
        private bool hasAlternateForms;
        private bool missingGenderIsFemale;
        private bool canAddOppositeGenderSprites;
        private readonly bool[] frameAvailable = new bool[8];

        public int CurrentId { get { return currentId; } }
        public bool IsAlternateForms { get { return isAlternateForms; } }
        public bool HasAlternateForms { get { return hasAlternateForms; } }
        public int SelectedFormIndex { get { return selectedFormIndex; } }
        public bool FormSharesBaseData { get { return formSharesBaseData; } }
        public bool CanAddOppositeGenderSprites { get { return canAddOppositeGenderSprites; } }
        public bool MissingGenderIsFemale { get { return missingGenderIsFemale; } }
        public string StatusText { get; private set; }
        public bool Dirty { get; set; }

        public string FormSharesBaseDataText {
            get {
                return "Stats, type, and other Personal Data are shared with the base Pokémon and will be " +
                       "saved there. Only this sprite belongs to the form.";
            }
        }

        public string[] VariantNames {
            get {
                string[] names = new string[currentFormData.Length];
                for (int i = 0; i < currentFormData.Length; i++) {
                    names[i] = currentFormData[i].Name;
                }
                return names;
            }
        }

        public bool HasSlot(int slot) { return rawSprites[slot] != null; }
        public byte[] GetRawSpriteIndices(int slot) { return rawSprites[slot]; }
        public uint[] GetPalette(bool shiny) { return shiny ? shinyPal : normalPal; }
        public bool[] GetPaletteUsed(bool shiny) { return shiny ? shinyPalUsed : normalPalUsed; }
        public bool HasPalette(bool shiny) { return (shiny ? shinyPal : normalPal) != null; }

        /// <summary>Alternate forms store one shared sprite, so the whole-sheet "both genders" layout has no meaning there.</summary>
        public bool CanUseFullSheet { get { return !isAlternateForms; } }

        /// <summary>Female art can't be saved for a form: the NARC has only one sprite slot per pose per form.</summary>
        public bool CanImportFemale { get { return !isAlternateForms; } }

        private static bool[] AllUsed() {
            bool[] a = new bool[16];
            for (int i = 0; i < 16; i++) {
                a[i] = true;
            }
            return a;
        }
        #endregion

        #region Form tables
        private static FormSpriteData[] GetFormDataForCurrentGame() {
            switch (RomInfo.gameFamily) {
                case RomInfo.GameFamilies.DP: return PokemonFormTables.DP;
                case RomInfo.GameFamilies.Plat: return PokemonFormTables.Platinum;
                default: return PokemonFormTables.HGSS;
            }
        }

        /// <summary>Form names read "Deoxys - Attack"; the dash is the only marker of which species an entry belongs to.</summary>
        private static string SpeciesNamePrefix(string formName) {
            int dash = formName.IndexOf(" - ", StringComparison.Ordinal);
            return dash < 0 ? null : formName.Substring(0, dash);
        }

        private static bool FormNameMatchesDescription(string formName, string description) {
            int dash = formName.IndexOf(" - ", StringComparison.Ordinal);
            return dash >= 0 && string.Equals(formName.Substring(dash + 3), description, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Ids past the real Pokédex are the pl_personal_extra formes; resolves one back to its base species and form name.</summary>
        public static bool TryResolvePseudoFormId(int id, out int baseId, out string description) {
            baseId = -1;
            description = null;
            int extraIndex = id - RomInfo.GetPokemonNames().Length;
            PokeDatabase.PersonalData.PersonalExtraFiles[] extras = PokeDatabase.PersonalData.personalExtraFiles;
            if (extraIndex < 0 || extraIndex >= extras.Length) {
                return false;
            }
            baseId = extras[extraIndex].monId;
            description = extras[extraIndex].description;
            return true;
        }

        /// <summary>Reverse of TryResolvePseudoFormId. Most forms have no entry of their own, so -1 is a normal answer.</summary>
        public static int ResolveFormPseudoId(int baseId, string formName) {
            PokeDatabase.PersonalData.PersonalExtraFiles[] extras = PokeDatabase.PersonalData.personalExtraFiles;
            for (int i = 0; i < extras.Length; i++) {
                if (extras[i].monId == baseId && FormNameMatchesDescription(formName, extras[i].description)) {
                    return RomInfo.GetPokemonNames().Length + i;
                }
            }
            return -1;
        }

        public static FormSpriteData[] GetAlternateFormsFor(int speciesId) {
            if (speciesId <= 0) {
                return new FormSpriteData[0];
            }
            string[] names = RomInfo.GetPokemonNames();
            if (speciesId >= names.Length) {
                return new FormSpriteData[0];
            }

            List<FormSpriteData> matches = new List<FormSpriteData>();
            foreach (FormSpriteData f in GetFormDataForCurrentGame()) {
                string prefix = SpeciesNamePrefix(f.Name);
                if (prefix != null && string.Equals(prefix, names[speciesId], StringComparison.OrdinalIgnoreCase)) {
                    matches.Add(f);
                }
            }
            return matches.ToArray();
        }

        /// <summary>Finds the form whose name ends in the given description, or -1.</summary>
        public static int FindFormByDescription(FormSpriteData[] forms, string description) {
            for (int i = 0; i < forms.Length; i++) {
                if (FormNameMatchesDescription(forms[i].Name, description)) {
                    return i;
                }
            }
            return -1;
        }

        /// <summary>The current form's back/front indices, which double as its height_o.narc record indices.</summary>
        public bool TryGetCurrentFormHeightIndices(out int backIndex, out int frontIndex) {
            backIndex = frontIndex = -1;
            if (selectedFormIndex < 0 || selectedFormIndex >= currentFormData.Length) {
                return false;
            }
            backIndex = currentFormData[selectedFormIndex].BackSpriteIndex;
            frontIndex = currentFormData[selectedFormIndex].FrontSpriteIndex;
            return backIndex >= 0 && frontIndex >= 0;
        }
        #endregion

        #region Loading
        /// <summary>
        /// Loads a species. Ids past the Pokédex resolve to their forme, and the 13 species whose default
        /// sprite lives in the alternate-forms NARC open on that form instead of the unused main entry.
        /// </summary>
        public void LoadMon(int id) {
            Clear();
            StatusText = "";
            isAlternateForms = false;
            formSharesBaseData = false;

            int baseId;
            string description;
            bool isPseudo = TryResolvePseudoFormId(id, out baseId, out description);
            currentId = isPseudo ? baseId : id;

            currentFormData = GetAlternateFormsFor(currentId);
            hasAlternateForms = currentFormData.Length > 0;
            selectedFormIndex = -1;

            if (isPseudo) {
                int formIdx = -1;
                for (int i = 0; i < currentFormData.Length; i++) {
                    if (FormNameMatchesDescription(currentFormData[i].Name, description)) {
                        formIdx = i;
                        break;
                    }
                }
                if (formIdx >= 0) {
                    SelectForm(formIdx);
                } else {
                    StatusText = "This form doesn't have its own sprite data.";
                }
                return;
            }

            // Species with form-table entries also keep their default form there, not in the main NARC.
            // Confirmed against the real GameFreak source (PokeGraArcDataGet in poke_tool.c).
            if (currentFormData.Length > 0) {
                SelectForm(0);
                return;
            }

            if (id <= 0) {
                StatusText = "No Pokémon selected.";
                return;
            }

            LoadBaseSprites(id);
        }

        private void LoadBaseSprites(int id) {
            try {
                string packedPath = RomInfo.gameDirs[DirNames.pokemonBattleSprites].packedDir;
                if (!File.Exists(packedPath)) {
                    StatusText = "Battle sprites NARC not found. Make sure the ROM is loaded.";
                    return;
                }

                NarcReader narc = new NarcReader(packedPath);
                try {
                    int baseOffset = id * 6;
                    byte[][] loaded = new byte[4][];
                    bool[] hasRealSprite = new bool[4];
                    for (int i = 0; i < 4; i++) {
                        int idx = baseOffset + i;
                        hasRealSprite[i] = idx < narc.fe.Length && narc.fe[idx].Size == SpriteEntrySize;
                        if (hasRealSprite[i]) {
                            narc.OpenEntry(idx);
                            loaded[i] = ReadSpriteEntry(narc.fs);
                            narc.Close();
                        }
                    }
                    UpdateOppositeGenderGap(hasRealSprite);

                    uint[] loadedNormal = null, loadedShiny = null;
                    int palIdx = baseOffset + 4, shinyIdx = baseOffset + 5;
                    if (palIdx < narc.fe.Length && narc.fe[palIdx].Size == PaletteEntrySize) {
                        narc.OpenEntry(palIdx);
                        loadedNormal = ReadPaletteEntry(narc.fs);
                        narc.Close();
                    }
                    if (shinyIdx < narc.fe.Length && narc.fe[shinyIdx].Size == PaletteEntrySize) {
                        narc.OpenEntry(shinyIdx);
                        loadedShiny = ReadPaletteEntry(narc.fs);
                        narc.Close();
                    }

                    if (loadedNormal == null) {
                        StatusText = "Could not load palette for this Pokémon.";
                        return;
                    }

                    rawSprites = loaded;
                    normalPal = loadedNormal;
                    shinyPal = loadedShiny ?? loadedNormal;
                    normalPalUsed = AllUsed();
                    shinyPalUsed = AllUsed();
                    UpdateFrameAvailability();
                    Dirty = false;
                } finally {
                    narc.Close();
                }
            } catch (Exception ex) {
                StatusText = "Error loading sprites: " + ex.Message;
            }
        }

        /// <summary>Picks an alternate form and loads it. Returns the id the main species list should move to.</summary>
        public int SelectForm(int formIndex) {
            if (formIndex < 0 || formIndex >= currentFormData.Length) {
                return currentId;
            }

            isAlternateForms = true;
            selectedFormIndex = formIndex;
            LoadAlternateForm(formIndex);

            int pseudoId = ResolveFormPseudoId(currentId, currentFormData[formIndex].Name);
            formSharesBaseData = pseudoId < 0;
            return pseudoId >= 0 ? pseudoId : currentId;
        }

        private void LoadAlternateForm(int formIndex) {
            Clear();
            StatusText = "";

            try {
                string packedPath = RomInfo.gameDirs[DirNames.otherPokemonBattleSprites].packedDir;
                if (!File.Exists(packedPath)) {
                    StatusText = "Alternate forms NARC not found. Make sure the ROM is loaded.";
                    return;
                }

                NarcReader narc = new NarcReader(packedPath);
                try {
                    FormSpriteData form = currentFormData[formIndex];
                    byte[][] loaded = new byte[4][];

                    if (IsSpriteEntry(narc, form.BackSpriteIndex)) {
                        narc.OpenEntry(form.BackSpriteIndex);
                        byte[] back = ReadSpriteEntry(narc.fs);
                        narc.Close();
                        loaded[SlotFemaleBack] = back;
                        loaded[SlotMaleBack] = back;
                    }
                    if (IsSpriteEntry(narc, form.FrontSpriteIndex)) {
                        narc.OpenEntry(form.FrontSpriteIndex);
                        byte[] front = ReadSpriteEntry(narc.fs);
                        narc.Close();
                        loaded[SlotFemaleFront] = front;
                        loaded[SlotMaleFront] = front;
                    }

                    uint[] loadedNormal = null, loadedShiny = null;
                    if (IsPaletteEntry(narc, form.NormalPaletteIndex)) {
                        narc.OpenEntry(form.NormalPaletteIndex);
                        loadedNormal = ReadPaletteEntry(narc.fs);
                        narc.Close();
                    }
                    if (IsPaletteEntry(narc, form.ShinyPaletteIndex)) {
                        narc.OpenEntry(form.ShinyPaletteIndex);
                        loadedShiny = ReadPaletteEntry(narc.fs);
                        narc.Close();
                    }

                    if (loadedNormal == null) {
                        StatusText = "Could not load palette for this alternate form.";
                        return;
                    }

                    ApplyFormGenderGap(loaded, currentId);

                    rawSprites = loaded;
                    normalPal = loadedNormal;
                    shinyPal = loadedShiny ?? loadedNormal;
                    normalPalUsed = AllUsed();
                    shinyPalUsed = AllUsed();
                    canAddOppositeGenderSprites = false;
                    UpdateFrameAvailability();
                    Dirty = false;
                } finally {
                    narc.Close();
                }
            } catch (Exception ex) {
                StatusText = "Error loading alternate form: " + ex.Message;
            }
        }

        // A form stores one shared sprite, so a species that only has one real gender should show that
        // gap here too instead of a mirrored copy that can never be saved.
        public static void ApplyFormGenderGap(byte[][] sprites, int baseSpeciesId) {
            byte ratio = ReadGenderRatio(baseSpeciesId);
            if (ratio == SpeciesFile.GENDER_RATIO_FEMALE) {
                sprites[SlotMaleBack] = null;
                sprites[SlotMaleFront] = null;
            } else if (ratio == SpeciesFile.GENDER_RATIO_MALE || ratio == SpeciesFile.GENDER_RATIO_GENDERLESS) {
                sprites[SlotFemaleBack] = null;
                sprites[SlotFemaleFront] = null;
            }
        }

        public static byte ReadGenderRatio(int speciesId) {
            try {
                string path = Path.Combine(RomInfo.gameDirs[DirNames.personalPokeData].unpackedDir, speciesId.ToString("D4"));
                if (!File.Exists(path)) {
                    return 127;
                }
                using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read)) {
                    return new SpeciesFile(fs).GenderRatioMaleToFemale;
                }
            } catch {
                return 127;
            }
        }

        private void UpdateOppositeGenderGap(bool[] hasRealSprite) {
            bool femaleReal = hasRealSprite[SlotFemaleBack] && hasRealSprite[SlotFemaleFront];
            bool femaleMissing = !hasRealSprite[SlotFemaleBack] && !hasRealSprite[SlotFemaleFront];
            bool maleReal = hasRealSprite[SlotMaleBack] && hasRealSprite[SlotMaleFront];
            bool maleMissing = !hasRealSprite[SlotMaleBack] && !hasRealSprite[SlotMaleFront];

            canAddOppositeGenderSprites = (maleReal && femaleMissing) || (femaleReal && maleMissing);
            missingGenderIsFemale = maleReal && femaleMissing;
        }

        private static bool IsSpriteEntry(NarcReader narc, int idx) {
            return idx >= 0 && idx < narc.fe.Length && narc.fe[idx].Size == SpriteEntrySize;
        }

        private static bool IsPaletteEntry(NarcReader narc, int idx) {
            return idx >= 0 && idx < narc.fe.Length && narc.fe[idx].Size == PaletteEntrySize;
        }

        private void Clear() {
            rawSprites = new byte[4][];
            normalPal = null;
            shinyPal = null;
            normalPalUsed = AllUsed();
            shinyPalUsed = AllUsed();
            canAddOppositeGenderSprites = false;
        }
        #endregion

        #region Frames
        /// <summary>True when the given pose really has that frame drawn. Some ROM sprites only ever had one.</summary>
        public bool HasFrame(int slot, int frame) {
            return frameAvailable[slot * 2 + frame];
        }

        public bool ShowFrameToggle(int slot) {
            return HasFrame(slot, 0) && HasFrame(slot, 1);
        }

        public int FirstRealFrame(int slot) {
            return HasFrame(slot, 0) ? 0 : 1;
        }

        private void UpdateFrameAvailability() {
            for (int slot = 0; slot < 4; slot++) {
                byte[] indices = rawSprites[slot];
                bool hasFrame1 = indices != null && !IsFrameBlank(indices, 0);
                bool hasFrame2 = indices != null && !IsFrameBlank(indices, 1);
                if (!hasFrame1 && !hasFrame2) {
                    hasFrame1 = true; // nothing loaded: don't hide both
                }
                frameAvailable[slot * 2] = hasFrame1;
                frameAvailable[slot * 2 + 1] = hasFrame2;
            }
        }

        private static bool IsFrameBlank(byte[] indices, int frame) {
            int x0 = frame * FrameWidth;
            for (int y = 0; y < SpriteHeight; y++) {
                for (int x = 0; x < FrameWidth; x++) {
                    if (indices[y * SpriteWidth + x0 + x] != 0) {
                        return false;
                    }
                }
            }
            return true;
        }
        #endregion

        #region Rendering
        /// <summary>Draws one pose as an 8bpp indexed bitmap. Pass frame -1 for the whole two-frame sheet.</summary>
        public Bitmap Render(int slot, bool shiny, int frame) {
            byte[] indices = rawSprites[slot];
            uint[] pal = shiny ? shinyPal : normalPal;
            if (indices == null || pal == null) {
                return null;
            }

            int width = frame < 0 ? SpriteWidth : FrameWidth;
            int x0 = frame < 0 ? 0 : frame * FrameWidth;

            Bitmap bmp = new Bitmap(width, SpriteHeight, PixelFormat.Format8bppIndexed);
            ColorPalette cp = bmp.Palette;
            for (int i = 0; i < 16; i++) {
                cp.Entries[i] = Color.FromArgb(unchecked((int)(0xFF000000u | (pal[i] & 0x00FFFFFFu))));
            }
            bmp.Palette = cp;

            BitmapData data = bmp.LockBits(new Rectangle(0, 0, width, SpriteHeight),
                ImageLockMode.WriteOnly, PixelFormat.Format8bppIndexed);
            try {
                byte[] row = new byte[data.Stride];
                for (int y = 0; y < SpriteHeight; y++) {
                    Array.Clear(row, 0, row.Length);
                    for (int x = 0; x < width; x++) {
                        row[x] = indices[y * SpriteWidth + x0 + x];
                    }
                    System.Runtime.InteropServices.Marshal.Copy(row, 0,
                        IntPtr.Add(data.Scan0, y * data.Stride), data.Stride);
                }
            } finally {
                bmp.UnlockBits(data);
            }
            return bmp;
        }
        #endregion

        #region Sheets
        private static int[] GenderSlots(bool female) {
            return female ? new[] { SlotFemaleBack, SlotFemaleFront } : new[] { SlotMaleBack, SlotMaleFront };
        }

        /// <summary>Lays the given poses out left to right as one wide index buffer.</summary>
        public byte[] BuildSheetIndices(int[] slots) {
            int width = SpriteWidth * slots.Length;
            byte[] sheet = new byte[width * SpriteHeight];
            for (int s = 0; s < slots.Length; s++) {
                byte[] src = rawSprites[slots[s]];
                if (src == null) {
                    continue;
                }
                for (int y = 0; y < SpriteHeight; y++) {
                    Array.Copy(src, y * SpriteWidth, sheet, y * width + s * SpriteWidth, SpriteWidth);
                }
            }
            return sheet;
        }

        public void WriteSheetIndices(byte[] sheet, int[] slots) {
            int width = SpriteWidth * slots.Length;
            for (int s = 0; s < slots.Length; s++) {
                if (rawSprites[slots[s]] == null) {
                    continue;
                }
                byte[] dst = new byte[PixelCount];
                for (int y = 0; y < SpriteHeight; y++) {
                    Array.Copy(sheet, y * width + s * SpriteWidth, dst, y * SpriteWidth, SpriteWidth);
                }
                rawSprites[slots[s]] = dst;
            }
        }

        public byte[] BuildGenderSheetIndices(bool female) { return BuildSheetIndices(GenderSlots(female)); }
        public void WriteGenderSheetIndices(bool female, byte[] sheet) { WriteSheetIndices(sheet, GenderSlots(female)); }
        public byte[] BuildFullSheetIndices() { return BuildSheetIndices(new[] { 0, 1, 2, 3 }); }
        public void WriteFullSheetIndices(byte[] sheet) { WriteSheetIndices(sheet, new[] { 0, 1, 2, 3 }); }

        /// <summary>Draws an arbitrary index buffer with the given palette, for sheet export.</summary>
        public static Bitmap RenderIndices(byte[] indices, uint[] pal, int width, int height) {
            Bitmap bmp = new Bitmap(width, height, PixelFormat.Format8bppIndexed);
            ColorPalette cp = bmp.Palette;
            for (int i = 0; i < 16; i++) {
                cp.Entries[i] = Color.FromArgb(unchecked((int)(0xFF000000u | (pal[i] & 0x00FFFFFFu))));
            }
            bmp.Palette = cp;

            BitmapData data = bmp.LockBits(new Rectangle(0, 0, width, height),
                ImageLockMode.WriteOnly, PixelFormat.Format8bppIndexed);
            try {
                byte[] row = new byte[data.Stride];
                for (int y = 0; y < height; y++) {
                    Array.Clear(row, 0, row.Length);
                    Array.Copy(indices, y * width, row, 0, width);
                    System.Runtime.InteropServices.Marshal.Copy(row, 0,
                        IntPtr.Add(data.Scan0, y * data.Stride), data.Stride);
                }
            } finally {
                bmp.UnlockBits(data);
            }
            return bmp;
        }
        #endregion

        #region Editing
        public void SetSpriteIndices(int slot, byte[] indices) {
            rawSprites[slot] = indices;
            UpdateFrameAvailability();
            Dirty = true;
        }

        public void SetPalette(bool shiny, uint[] pal, bool[] used) {
            if (shiny) {
                shinyPal = pal;
                shinyPalUsed = used;
            } else {
                normalPal = pal;
                normalPalUsed = used;
            }
            Dirty = true;
        }

        public void SetPaletteColor(bool shiny, int index, uint argb) {
            uint[] pal = shiny ? shinyPal : normalPal;
            if (pal == null || index < 0 || index >= pal.Length) {
                return;
            }
            pal[index] = argb;
            Dirty = true;
        }

        /// <summary>Copies the gender that has real art onto the one that doesn't, so both can be edited.</summary>
        public void AddOppositeGenderSprites() {
            int srcBack = missingGenderIsFemale ? SlotMaleBack : SlotFemaleBack;
            int srcFront = missingGenderIsFemale ? SlotMaleFront : SlotFemaleFront;
            int dstBack = missingGenderIsFemale ? SlotFemaleBack : SlotMaleBack;
            int dstFront = missingGenderIsFemale ? SlotFemaleFront : SlotMaleFront;

            if (rawSprites[srcBack] != null) {
                rawSprites[dstBack] = (byte[])rawSprites[srcBack].Clone();
            }
            if (rawSprites[srcFront] != null) {
                rawSprites[dstFront] = (byte[])rawSprites[srcFront].Clone();
            }
            canAddOppositeGenderSprites = false;
            UpdateFrameAvailability();
            Dirty = true;
        }
        #endregion

        #region Saving
        public bool Save() {
            if (!Dirty || currentId <= 0) {
                return false;
            }
            return isAlternateForms ? SaveAlternateForm() : SaveBaseSprites();
        }

        private bool SaveBaseSprites() {
            string packedPath = RomInfo.gameDirs[DirNames.pokemonBattleSprites].packedDir;
            if (!File.Exists(packedPath)) {
                StatusText = "Battle sprites NARC not found. Make sure the ROM is loaded.";
                return false;
            }

            NarcReader narc = new NarcReader(packedPath);
            try {
                int baseOffset = currentId * 6;
                for (int i = 0; i < 4; i++) {
                    if (rawSprites[i] == null) {
                        continue;
                    }
                    int idx = baseOffset + i;
                    if (!IsSpriteEntry(narc, idx)) {
                        continue;
                    }
                    narc.OpenEntry(idx);
                    WriteSpriteEntry(narc.fs, rawSprites[i]);
                    narc.Close();
                    SyncUnpackedEntry(DirNames.pokemonBattleSprites, idx);
                }
                if (normalPal != null && IsPaletteEntry(narc, baseOffset + 4)) {
                    narc.OpenEntry(baseOffset + 4);
                    WritePaletteEntry(narc.fs, normalPal);
                    narc.Close();
                    SyncUnpackedEntry(DirNames.pokemonBattleSprites, baseOffset + 4);
                }
                if (shinyPal != null && IsPaletteEntry(narc, baseOffset + 5)) {
                    narc.OpenEntry(baseOffset + 5);
                    WritePaletteEntry(narc.fs, shinyPal);
                    narc.Close();
                    SyncUnpackedEntry(DirNames.pokemonBattleSprites, baseOffset + 5);
                }
            } finally {
                narc.Close();
            }

            Dirty = false;
            StatusText = "Saved.";
            return true;
        }

        // Male Back/Front are the only poses persisted: the NARC has one sprite slot per pose per form,
        // which is also why importing female art for a form is disabled.
        private bool SaveAlternateForm() {
            if (selectedFormIndex < 0 || selectedFormIndex >= currentFormData.Length) {
                return false;
            }

            string packedPath = RomInfo.gameDirs[DirNames.otherPokemonBattleSprites].packedDir;
            if (!File.Exists(packedPath)) {
                StatusText = "Alternate forms NARC not found. Make sure the ROM is loaded.";
                return false;
            }

            FormSpriteData form = currentFormData[selectedFormIndex];
            NarcReader narc = new NarcReader(packedPath);
            try {
                WriteFormSprite(narc, form.BackSpriteIndex, rawSprites[SlotMaleBack] ?? rawSprites[SlotFemaleBack]);
                WriteFormSprite(narc, form.FrontSpriteIndex, rawSprites[SlotMaleFront] ?? rawSprites[SlotFemaleFront]);
                WriteFormPalette(narc, form.NormalPaletteIndex, normalPal);
                WriteFormPalette(narc, form.ShinyPaletteIndex, shinyPal);
            } finally {
                narc.Close();
            }

            Dirty = false;
            StatusText = "Saved.";
            return true;
        }

        private static void WriteFormSprite(NarcReader narc, int idx, byte[] indices) {
            if (indices == null || !IsSpriteEntry(narc, idx)) {
                return;
            }
            narc.OpenEntry(idx);
            WriteSpriteEntry(narc.fs, indices);
            narc.Close();
            SyncUnpackedEntry(DirNames.otherPokemonBattleSprites, idx);
        }

        private static void WriteFormPalette(NarcReader narc, int idx, uint[] palette) {
            if (palette == null || !IsPaletteEntry(narc, idx)) {
                return;
            }
            narc.OpenEntry(idx);
            WritePaletteEntry(narc.fs, palette);
            narc.Close();
            SyncUnpackedEntry(DirNames.otherPokemonBattleSprites, idx);
        }

        // Editors that read from the unpacked folder would otherwise keep showing the old bytes.
        private static void SyncUnpackedEntry(DirNames dir, int entryIndex) {
            try {
                string unpackedDir = RomInfo.gameDirs[dir].unpackedDir;
                string packedPath = RomInfo.gameDirs[dir].packedDir;
                if (!Directory.Exists(unpackedDir) || !File.Exists(packedPath)) {
                    return;
                }

                string target = Path.Combine(unpackedDir, entryIndex.ToString("D4"));
                if (!File.Exists(target)) {
                    return;
                }

                NarcReader narc = new NarcReader(packedPath);
                try {
                    if (entryIndex >= narc.fe.Length) {
                        return;
                    }
                    narc.OpenEntry(entryIndex);
                    byte[] data = new byte[narc.fe[entryIndex].Size];
                    narc.fs.Read(data, 0, data.Length);
                    narc.Close();
                    File.WriteAllBytes(target, data);
                } finally {
                    narc.Close();
                }
            } catch {
                // A stale unpacked copy is a display nuisance, never a reason to fail the save.
            }
        }
        #endregion

        #region NARC entry codecs
        private static byte[] ReadSpriteEntry(FileStream fs) {
            fs.Seek(48L, SeekOrigin.Current);
            BinaryReader reader = new BinaryReader(fs);

            ushort[] arr = new ushort[3200];
            for (int i = 0; i < 3200; i++) {
                arr[i] = reader.ReadUInt16();
            }

            uint num;
            if (RomInfo.gameFamily != RomInfo.GameFamilies.DP) {
                num = arr[0];
                for (int j = 0; j < 3200; j++) {
                    unchecked {
                        arr[j] = (ushort)(arr[j] ^ (ushort)(num & 0xFFFF));
                        num *= 1103515245;
                        num += 24691;
                    }
                }
            } else {
                num = arr[3199];
                for (int j = 3199; j >= 0; j--) {
                    unchecked {
                        arr[j] = (ushort)(arr[j] ^ (ushort)(num & 0xFFFF));
                        num *= 1103515245;
                        num += 24691;
                    }
                }
            }

            byte[] pixels = new byte[PixelCount];
            for (int k = 0; k < 3200; k++) {
                pixels[k * 4] = (byte)(arr[k] & 0xF);
                pixels[k * 4 + 1] = (byte)((arr[k] >> 4) & 0xF);
                pixels[k * 4 + 2] = (byte)((arr[k] >> 8) & 0xF);
                pixels[k * 4 + 3] = (byte)((arr[k] >> 12) & 0xF);
            }
            return pixels;
        }

        private static uint[] ReadPaletteEntry(FileStream fs) {
            fs.Seek(40L, SeekOrigin.Current);
            BinaryReader reader = new BinaryReader(fs);
            uint[] pal = new uint[16];
            for (int j = 0; j < 16; j++) {
                ushort v = reader.ReadUInt16();
                uint r = (uint)((v & 0x1F) << 3);
                uint g = (uint)(((v >> 5) & 0x1F) << 3);
                uint b = (uint)(((v >> 10) & 0x1F) << 3);
                pal[j] = 0xFF000000u | (r << 16) | (g << 8) | b;
            }
            return pal;
        }

        private static void WriteSpriteEntry(FileStream fs, byte[] indices) {
            ushort[] packed = new ushort[3200];
            for (int i = 0; i < 3200; i++) {
                packed[i] = (ushort)((indices[i * 4] & 0xF) | ((indices[i * 4 + 1] & 0xF) << 4) |
                                     ((indices[i * 4 + 2] & 0xF) << 8) | ((indices[i * 4 + 3] & 0xF) << 12));
            }

            // The reader takes its seed straight back from this position, so it has to literally be the
            // seed, not the seed XORed with a pixel, or everything after it decodes wrong.
            if (RomInfo.gameFamily != RomInfo.GameFamilies.DP) {
                uint num = 0u;
                packed[0] = (ushort)(num & 0xFFFF);
                num = num * 1103515245 + 24691;
                for (int j = 1; j < 3200; j++) {
                    unchecked {
                        packed[j] = (ushort)(packed[j] ^ (ushort)(num & 0xFFFF));
                        num = num * 1103515245 + 24691;
                    }
                }
            } else {
                uint seed = 31315u;
                for (int k = 3199; k >= 0; k--) {
                    seed += packed[k];
                }
                uint num = seed;
                packed[3199] = (ushort)(num & 0xFFFF);
                num = num * 1103515245 + 24691;
                for (int k = 3198; k >= 0; k--) {
                    unchecked {
                        packed[k] = (ushort)(packed[k] ^ (ushort)(num & 0xFFFF));
                        num = num * 1103515245 + 24691;
                    }
                }
            }

            byte[] header = {
                82, 71, 67, 78, 255, 254, 0, 1, 48, 25, 0, 0, 16, 0, 1, 0,
                82, 65, 72, 67, 32, 25, 0, 0, 10, 0, 20, 0, 3, 0, 0, 0,
                0, 0, 0, 0, 1, 0, 0, 0, 0, 25, 0, 0, 24, 0, 0, 0
            };
            BinaryWriter bw = new BinaryWriter(fs);
            bw.Write(header, 0, 48);
            for (int l = 0; l < 3200; l++) {
                bw.Write(packed[l]);
            }
        }

        private static void WritePaletteEntry(FileStream fs, uint[] palette) {
            byte[] header = {
                82, 76, 67, 78, 255, 254, 0, 1, 72, 0, 0, 0, 16, 0, 1, 0,
                84, 84, 76, 80, 56, 0, 0, 0, 4, 0, 10, 0, 0, 0, 0, 0,
                32, 0, 0, 0, 16, 0, 0, 0
            };
            BinaryWriter bw = new BinaryWriter(fs);
            bw.Write(header, 0, 40);
            for (int i = 0; i < 16; i++) {
                byte r = (byte)(palette[i] >> 16), g = (byte)(palette[i] >> 8), b = (byte)palette[i];
                ushort v = (ushort)(((r >> 3) & 0x1F) | (((g >> 3) & 0x1F) << 5) | (((b >> 3) & 0x1F) << 10));
                bw.Write(v);
            }
        }
        #endregion

        #region Palette matching
        /// <summary>Prefers a PNG's own index order when it really has one, rather than re-deriving it by scanning colours.</summary>
        public static bool TryReadIndexedOrQuantize(byte[] fileBytes, RawImage decoded, out byte[] indices, out uint[] palette, out int usedCount) {
            byte[] realIndices;
            uint[] realPalette;
            int w, h;
            if (fileBytes != null && IndexedPng.TryRead(fileBytes, out realIndices, out realPalette, out w, out h) &&
                w == decoded.Width && h == decoded.Height && realPalette.Length <= 16) {
                indices = realIndices;
                usedCount = realPalette.Length;
                palette = Pad16(realPalette);
                return true;
            }
            return TryReadImageColors(decoded, out indices, out palette, out usedCount);
        }

        private static uint[] Pad16(uint[] palette) {
            uint[] padded = new uint[16];
            for (int i = 0; i < 16; i++) {
                padded[i] = i < palette.Length ? palette[i] : 0xFF000000u;
            }
            return padded;
        }

        /// <summary>Builds a 0-15 index per pixel plus the colours used, in first-seen order. Fails past 16 colours.</summary>
        public static bool TryReadImageColors(RawImage img, out byte[] indices, out uint[] palette, out int usedCount) {
            int n = img.Width * img.Height;
            indices = new byte[n];
            palette = new uint[16];
            usedCount = 0;

            Dictionary<uint, byte> seen = new Dictionary<uint, byte>();
            for (int p = 0; p < n; p++) {
                int o = p * 4;
                uint c = 0xFF000000u | ((uint)img.Bgra[o + 2] << 16) | ((uint)img.Bgra[o + 1] << 8) | img.Bgra[o];
                byte idx;
                if (!seen.TryGetValue(c, out idx)) {
                    if (seen.Count >= 16) {
                        indices = null;
                        palette = null;
                        usedCount = 0;
                        return false;
                    }
                    idx = (byte)seen.Count;
                    seen[c] = idx;
                    palette[idx] = c;
                }
                indices[p] = idx;
            }
            usedCount = seen.Count;
            for (int i = usedCount; i < 16; i++) {
                palette[i] = 0xFF000000u;
            }
            return true;
        }

        public static bool PaletteEqualsUpTo(uint[] existing, uint[] candidate, int count) {
            for (int i = 0; i < count; i++) {
                if (existing[i] != candidate[i]) {
                    return false;
                }
            }
            return true;
        }

        public static bool[] MakeUsedMask(int usedCount) {
            bool[] a = new bool[16];
            for (int i = 0; i < usedCount && i < 16; i++) {
                a[i] = true;
            }
            return a;
        }

        /// <summary>One old index showing two different colours means the artwork's shape changed, not just its colours.</summary>
        public static bool TryDeriveRecolorPalette(byte[] oldIndices, RawImage newImg, out uint[] palette, out bool[] used) {
            palette = new uint[16];
            used = new bool[16];
            if (oldIndices == null || newImg == null || oldIndices.Length != newImg.Width * newImg.Height) {
                return false;
            }
            for (int p = 0; p < oldIndices.Length; p++) {
                int idx = oldIndices[p] & 0xF;
                int o = p * 4;
                uint c = 0xFF000000u | ((uint)newImg.Bgra[o + 2] << 16) | ((uint)newImg.Bgra[o + 1] << 8) | newImg.Bgra[o];
                if (!used[idx]) {
                    palette[idx] = c;
                    used[idx] = true;
                } else if (palette[idx] != c) {
                    return false;
                }
            }
            return true;
        }

        /// <summary>Colours with no exact match in the saved palette need a free slot to be added.</summary>
        public static int CountUnmatchedColors(uint[] newPalette, int usedCount, uint[] existingPalette) {
            int unmatched = 0;
            for (int i = 0; i < usedCount; i++) {
                bool found = false;
                for (int j = 0; j < 16; j++) {
                    if (existingPalette[j] == newPalette[i]) {
                        found = true;
                        break;
                    }
                }
                if (!found) {
                    unmatched++;
                }
            }
            return unmatched;
        }

        public static int CountFreeSlots(bool[] used) {
            int free = 0;
            foreach (bool u in used) {
                if (!u) {
                    free++;
                }
            }
            return free;
        }

        /// <summary>Matches colours by value; an unmatched one takes a free slot before ever reusing a used one.</summary>
        public static byte[] RemapToExistingPalette(byte[] newIndices, uint[] newPalette, int usedCount,
                uint[] existingPalette, bool[] existingUsed, out uint[] mergedPalette, out bool[] mergedUsed) {
            byte[] indexMap = new byte[16];
            bool[] claimed = new bool[16];

            for (int i = 0; i < usedCount; i++) {
                int found = -1;
                for (int j = 0; j < 16; j++) {
                    if (!claimed[j] && existingPalette[j] == newPalette[i]) {
                        found = j;
                        break;
                    }
                }
                indexMap[i] = found >= 0 ? (byte)found : (byte)255;
                if (found >= 0) {
                    claimed[found] = true;
                }
            }

            mergedPalette = (uint[])existingPalette.Clone();
            mergedUsed = (bool[])(existingUsed ?? AllUsed()).Clone();
            for (int i = 0; i < usedCount; i++) {
                if (indexMap[i] != 255) {
                    continue;
                }
                int freeSlot = -1;
                for (int j = 0; j < 16; j++) {
                    if (!claimed[j] && !mergedUsed[j]) {
                        freeSlot = j;
                        break;
                    }
                }
                if (freeSlot < 0) {
                    for (int j = 0; j < 16; j++) {
                        if (!claimed[j]) {
                            freeSlot = j;
                            break;
                        }
                    }
                }
                if (freeSlot < 0) {
                    freeSlot = 0;
                }
                claimed[freeSlot] = true;
                indexMap[i] = (byte)freeSlot;
                mergedPalette[freeSlot] = newPalette[i];
                mergedUsed[freeSlot] = true;
            }

            byte[] outIdx = new byte[newIndices.Length];
            for (int p = 0; p < newIndices.Length; p++) {
                outIdx[p] = indexMap[newIndices[p]];
            }
            return outIdx;
        }

        /// <summary>Reads the other palette off a reference image, one colour per index.</summary>
        public static uint[] DeriveAlternatePalette(byte[] parentIndices, byte[] childIndices, uint[] childPalette, out bool[] used) {
            used = null;
            if (parentIndices == null || childIndices == null || parentIndices.Length != childIndices.Length) {
                return null;
            }
            uint[] result = new uint[16];
            bool[] found = new bool[16];
            for (int p = 0; p < parentIndices.Length; p++) {
                int i = parentIndices[p] & 0xF;
                if (!found[i]) {
                    result[i] = childPalette[childIndices[p]];
                    found[i] = true;
                }
            }
            for (int i = 0; i < 16; i++) {
                if (!found[i]) {
                    result[i] = 0xFF000000u;
                }
            }
            used = found;
            return result;
        }

        /// <summary>Copies only the slots the reference actually resolved, so importing one pose can't wipe what another already filled in.</summary>
        public void MergePalette(bool shiny, uint[] derived, bool[] derivedUsed) {
            uint[] pal = shiny ? shinyPal : normalPal;
            bool[] palUsed = shiny ? shinyPalUsed : normalPalUsed;
            if (pal == null || derived == null || derivedUsed == null) {
                return;
            }
            for (int i = 0; i < 16; i++) {
                if (derivedUsed[i]) {
                    pal[i] = derived[i];
                    palUsed[i] = true;
                }
            }
            Dirty = true;
        }
        #endregion
    }
}
