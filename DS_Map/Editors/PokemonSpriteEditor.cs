using DSPRE.Editors.Utils;
using DSPRE.ROMFiles;
using NarcAPI;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using static DSPRE.RomInfo;

namespace DSPRE.Editors {
    public partial class PokemonSpriteEditor : Form, IEditorWithUnsavedChanges {
        #region Constants and Static Data
        private static readonly string formName = "Sprite Editor";
        
        private static readonly string[] spriteTypeNames = { 
            "Female backsprite", "Male backsprite", "Female frontsprite", "Male frontsprite", "Shiny" 
        };

        private readonly int[] validPalettesHGSS = new int[]
        {
            158, 159, 160, 161, 162, 163, 164, 165, 166, 167,
            168, 169, 170, 171, 172, 173, 174, 175, 176, 177,
            178, 179, 180, 181, 182, 183, 184, 185, 186, 187,
            188, 189, 190, 191, 192, 193, 194, 195, 196, 197,
            198, 199, 200, 201, 202, 203, 204, 205, 206, 207,
            208, 209, 210, 211, 212, 213, 214, 215, 216, 217,
            218, 219, 220, 221, 222, 223, 224, 225, 226, 227,
            228, 229, 230, 231, 232, 233, 234, 235, 236, 237,
            238, 239, 240, 241, 242, 243, 244, 245, 246, 247,
            248, 249, 250, 251, 252, 253, 254, 255, 258, 260
        };

        private readonly int[] validPalettesPt = new int[]
        {
            154, 155, 156, 157, 158, 159, 160, 161, 162, 163,
            164, 165, 166, 167, 168, 169, 170, 171, 172, 173,
            174, 175, 176, 177, 178, 179, 180, 181, 182, 183,
            184, 185, 186, 187, 188, 189, 190, 191, 192, 193,
            194, 195, 196, 197, 198, 199, 200, 201, 202, 203,
            204, 205, 206, 207, 208, 209, 210, 211, 212, 213,
            214, 215, 216, 217, 218, 219, 220, 221, 222, 223,
            224, 225, 226, 227, 228, 229, 230, 231, 232, 233,
            234, 235, 236, 237, 238, 239, 240, 241, 242, 243,
            244, 245, 246, 247, 250, 252
        };

        private readonly int[] validPalettesDP = new int[]
        {
            134, 135, 136, 137, 138, 139, 140, 141, 142, 145,
            146, 147, 148, 149, 150, 151, 152, 153, 154, 155,
            156, 157, 158, 159, 160, 161, 162, 163, 164, 165,
            166, 167, 168, 169, 170, 171, 172, 173, 174, 175,
            176, 177, 178, 179, 180, 181, 182, 183, 184, 185,
            186, 187, 188, 189, 190, 191, 192, 193, 194, 195,
            196, 197, 198, 199, 200, 201, 202, 203, 204, 205,
            206, 207, 210, 212
        };

        // Form tables live in PokemonFormTables now, shared with the sprite model.
        #endregion

        #region Instance Fields
        private readonly string[] pokenames;
        private readonly PokemonEditor parentEditor;

        private NarcReader narcReader;
        private PictureBox[,] displayPictureBoxes;
        private bool[] usedEntries;
        private bool shinyImported;
        private bool[] shinyResolved;
        private SpriteSet currentSprites;
        private int currentLoadedId;
        private bool isLoadingOtherForms = false;
        private bool missingGenderIsFemale;

        // Forms for the species currently shown, and which one of them is on screen.
        private PokemonSpriteModel.FormSpriteData[] currentFormData = new PokemonSpriteModel.FormSpriteData[0];
        private int selectedFormIndex = -1;

        public bool dirty = false;
        #endregion

        #region IEditorWithUnsavedChanges Implementation
        public bool HasUnsavedChanges => dirty;
        public string UnsavedChangesDescription => $"Sprite Editor (Mon {currentLoadedId})";
        void IEditorWithUnsavedChanges.SaveChanges() => SaveChanges_Click(null, null);
        public void DiscardChanges() => SetDirty(false);
        #endregion

        #region Constructor
        public PokemonSpriteEditor(Control parent, PokemonEditor pokeEditor) {
            this.parentEditor = pokeEditor;
            this.pokenames = RomInfo.GetPokemonNamesWithForms();
            
            InitializeComponent();
            
            this.Text = formName;
            this.FormBorderStyle = FormBorderStyle.None;
            this.Size = parent.Size;
            this.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top | AnchorStyles.Bottom;
            
            SetupPictureBoxes();
            InitializePaletteComboBoxes();
            BuildParityUi();
            
            Helpers.DisableHandlers();
            LoadSprites();
            Helpers.EnableHandlers();
            
            SaveBox.SelectedIndex = 0;
        }
        
        private void InitializePaletteComboBoxes() {
            int[] validPalettes = GetValidPalettesForGameFamily();
            foreach (var item in validPalettes) {
                BasePalette.Items.Add(item);
                ShinyPalette.Items.Add(item);
            }
        }
        
        private int[] GetValidPalettesForGameFamily() {
            switch (RomInfo.gameFamily) {
                case RomInfo.GameFamilies.DP:
                    return validPalettesDP;
                case RomInfo.GameFamilies.Plat:
                    return validPalettesPt;
                default:
                    return validPalettesHGSS;
            }
        }
        #endregion

        #region Dirty State Management
        public bool CheckDiscardChanges() {
            if (!dirty) {
                return true;
            }

            DialogResult result = MessageBox.Show(
                "Sprite Editor\nThere are unsaved changes to the current Sprite data.\nDiscard and proceed?", 
                "Sprite Editor - Unsaved changes", 
                MessageBoxButtons.YesNo, 
                MessageBoxIcon.Question);
                
            if (result == DialogResult.Yes) {
                return true;
            }

            IndexBox.SelectedIndex = currentLoadedId;
            return false;
        }

        private void SetDirty(bool status) {
            if (status) {
                dirty = true;
                this.Text = formName + "*";
            } else {
                dirty = false;
                this.Text = formName;
            }
            parentEditor.UpdateTabPageNames();
        }
        #endregion

        #region Event Handlers
        private void IndexBox_SelectedIndexChanged(object sender, EventArgs e) {
            this.Update();
            if (Helpers.HandlersDisabled) {
                return;
            }
            
            if (!isLoadingOtherForms) {
                parentEditor.TrySyncIndices((ComboBox)sender);
            }
            
            Helpers.DisableHandlers();
            if (CheckDiscardChanges()) {
                ChangeLoadedFile(((ComboBox)sender).SelectedIndex);
            }
            Helpers.EnableHandlers();
        }

        #region File Loading
        public void ChangeLoadedFile(int toLoad) {
            // Ids past the Pokédex are the extra form entries (Deoxys-Attack and friends); they load as
            // their base species with that form picked.
            int baseId;
            string formDescription;
            bool isPseudo = PokemonSpriteModel.TryResolvePseudoFormId(toLoad, out baseId, out formDescription);
            currentLoadedId = isPseudo ? baseId : toLoad;

            Helpers.DisableHandlers();
            int shown = isPseudo ? toLoad : currentLoadedId;
            if (shown >= 0 && shown < IndexBox.Items.Count) {
                IndexBox.SelectedIndex = shown;
            }
            Helpers.EnableHandlers();

            currentFormData = PokemonSpriteModel.GetAlternateFormsFor(currentLoadedId);
            PopulateFormBox();

            int formToLoad = -1;
            if (isPseudo) {
                formToLoad = PokemonSpriteModel.FindFormByDescription(currentFormData, formDescription);
            } else if (currentFormData.Length > 0) {
                // These species keep their default form in the alternate-forms NARC too, so the main
                // NARC entry is never read by the game. Confirmed against PokeGraArcDataGet.
                formToLoad = 0;
            }

            LoadIntoView(formToLoad);
        }

        private void LoadIntoView(int formIndex) {
            selectedFormIndex = formIndex;
            isLoadingOtherForms = formIndex >= 0;

            currentSprites = new SpriteSet();
            usedEntries = null;
            shinyImported = false;
            shinyResolved = new bool[16];

            OpenNarcForCurrentView();

            if (isLoadingOtherForms) {
                LoadOtherFormSprites(formIndex);
            } else {
                LoadMainSprites(currentLoadedId);
            }

            Helpers.DisableHandlers();
            if (FormBox.Items.Count > 0 && formIndex >= 0 && formIndex < FormBox.Items.Count) {
                FormBox.SelectedIndex = formIndex;
            }
            Helpers.EnableHandlers();

            UpdateFormNoticeVisibility();
            LoadImages();
            OpenPngs.Enabled = true;
            SetDirty(false);
        }

        private void OpenNarcForCurrentView() {
            DirNames dir = isLoadingOtherForms ? DirNames.otherPokemonBattleSprites : DirNames.pokemonBattleSprites;
            narcReader = new NarcReader(RomInfo.gameDirs[dir].packedDir);
        }

        private DirNames CurrentSpriteDir {
            get { return isLoadingOtherForms ? DirNames.otherPokemonBattleSprites : DirNames.pokemonBattleSprites; }
        }

        private void PopulateFormBox() {
            Helpers.DisableHandlers();
            FormBox.Items.Clear();
            foreach (PokemonSpriteModel.FormSpriteData f in currentFormData) {
                FormBox.Items.Add(f.Name);
            }
            FormBox.Visible = currentFormData.Length > 0;
            lblForm.Visible = currentFormData.Length > 0;
            Helpers.EnableHandlers();
        }

        private void UpdateFormNoticeVisibility() {
            bool showsForm = isLoadingOtherForms && selectedFormIndex >= 0 && selectedFormIndex < currentFormData.Length;
            // Only worth saying for a form that piggybacks on the base species' data. Index 0 is the
            // species' own default form, so its stats are the base species' stats by definition, and
            // forms with their own entry in the species list (Deoxys' Attack/Defense/Speed and the
            // like) keep their own stats.
            bool sharesBase = showsForm && selectedFormIndex > 0 &&
                PokemonSpriteModel.ResolveFormPseudoId(currentLoadedId, currentFormData[selectedFormIndex].Name) < 0;
            lblFormSharesBase.Visible = sharesBase;

            // A form has one sprite slot per pose, so there is nowhere to put separate female art.
            if (showsForm) {
                AddOppositeGenderButton.Visible = false;
            }
        }

        private void FormBox_SelectedIndexChanged(object sender, EventArgs e) {
            if (Helpers.HandlersDisabled) {
                return;
            }
            int picked = FormBox.SelectedIndex;
            if (picked < 0 || picked >= currentFormData.Length) {
                return;
            }

            Helpers.DisableHandlers();
            if (CheckDiscardChanges()) {
                LoadIntoView(picked);

                // Keep the rest of the Pokémon Editor on whatever entry this form saves to, so Personal
                // Data and Learnsets don't stay on a different Pokémon than the sprite being shown.
                int pseudoId = PokemonSpriteModel.ResolveFormPseudoId(currentLoadedId, currentFormData[picked].Name);
                int target = pseudoId >= 0 ? pseudoId : currentLoadedId;
                if (target >= 0 && target < IndexBox.Items.Count) {
                    IndexBox.SelectedIndex = target;
                }
                parentEditor.JumpToSpecies(target);
            }
            Helpers.EnableHandlers();
        }
        
        private void LoadMainSprites(int selectedIndex) {
            int baseOffset = selectedIndex * 6;
            bool[] hasRealSprite = new bool[4];

            for (int i = 0; i < 4; i++) {
                hasRealSprite[i] = narcReader.fe[baseOffset + i].Size == 6448;
                if (hasRealSprite[i]) {
                    narcReader.OpenEntry(baseOffset + i);
                    currentSprites.Sprites[i] = MakeImage(narcReader.fs);
                    narcReader.Close();
                }
            }

            if (narcReader.fe[baseOffset + 4].Size == 72) {
                narcReader.OpenEntry(baseOffset + 4);
                currentSprites.Normal = SetPal(narcReader.fs);
                narcReader.Close();
            }

            if (narcReader.fe[baseOffset + 5].Size == 72) {
                narcReader.OpenEntry(baseOffset + 5);
                currentSprites.Shiny = SetPal(narcReader.fs);
                narcReader.Close();
            }

            UpdateOppositeGenderGap(hasRealSprite);
        }

        // Slots: 0=FemaleBack, 1=MaleBack, 2=FemaleFront, 3=MaleFront.
        private void UpdateOppositeGenderGap(bool[] hasRealSprite) {
            bool femaleReal = hasRealSprite[0] && hasRealSprite[2];
            bool femaleMissing = !hasRealSprite[0] && !hasRealSprite[2];
            bool maleReal = hasRealSprite[1] && hasRealSprite[3];
            bool maleMissing = !hasRealSprite[1] && !hasRealSprite[3];

            if (maleReal && femaleMissing) {
                missingGenderIsFemale = true;
                AddOppositeGenderButton.Text = "Add Female Sprites (copy from Male)";
                AddOppositeGenderButton.Visible = true;
            } else if (femaleReal && maleMissing) {
                missingGenderIsFemale = false;
                AddOppositeGenderButton.Text = "Add Male Sprites (copy from Female)";
                AddOppositeGenderButton.Visible = true;
            } else {
                AddOppositeGenderButton.Visible = false;
            }
        }

        private void AddOppositeGenderButton_Click(object sender, EventArgs e) {
            if (!AddOppositeGenderButton.Visible || isLoadingOtherForms) {
                return;
            }

            string missingGender = missingGenderIsFemale ? "female" : "male";
            string sourceGender = missingGenderIsFemale ? "male" : "female";
            DialogResult confirm = MessageBox.Show(
                $"This Pokémon has no {missingGender} sprites. This will duplicate its {sourceGender} " +
                "back and front sprites into the missing slots, so a gender ratio change won't leave it " +
                "with blank graphics. The duplicates look identical to the existing sprites until you " +
                "import something different over them.\n\nContinue?",
                "Add Opposite Gender Sprites", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (confirm != DialogResult.Yes) {
                return;
            }

            try {
                narcReader.Close();

                DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.pokemonBattleSprites });
                string unpackedDir = RomInfo.gameDirs[DirNames.pokemonBattleSprites].unpackedDir;
                string packedPath = RomInfo.gameDirs[DirNames.pokemonBattleSprites].packedDir;

                int baseOffset = currentLoadedId * 6;
                int srcBack  = baseOffset + (missingGenderIsFemale ? 1 : 0);
                int srcFront = baseOffset + (missingGenderIsFemale ? 3 : 2);
                int dstBack  = baseOffset + (missingGenderIsFemale ? 0 : 1);
                int dstFront = baseOffset + (missingGenderIsFemale ? 2 : 3);

                CopyEntryFile(unpackedDir, srcBack, dstBack);
                CopyEntryFile(unpackedDir, srcFront, dstFront);

                // Re-sync the packed NARC immediately (rather than waiting for the next full ROM save),
                // since every other read/write in this editor goes through the packed file directly.
                Narc.FromFolder(unpackedDir).Save(packedPath);

                // Height wasn't copied before, only sprites, so the new gender rendered at the wrong Y.
                try {
                    DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.pokeHeight });
                    string heightDir = RomInfo.gameDirs[DirNames.pokeHeight].unpackedDir;
                    string heightPackedPath = RomInfo.gameDirs[DirNames.pokeHeight].packedDir;

                    const int FB = 0, MB = 1, FF = 2, MF = 3;
                    int heightBase = currentLoadedId * 4;
                    int srcBackH = heightBase + (missingGenderIsFemale ? MB : FB);
                    int srcFrontH = heightBase + (missingGenderIsFemale ? MF : FF);
                    int dstBackH = heightBase + (missingGenderIsFemale ? FB : MB);
                    int dstFrontH = heightBase + (missingGenderIsFemale ? FF : MF);

                    CopyEntryFile(heightDir, srcBackH, dstBackH);
                    CopyEntryFile(heightDir, srcFrontH, dstFrontH);
                    Narc.FromFolder(heightDir).Save(heightPackedPath);
                } catch (Exception heightEx) {
                    AppLogger.Error($"Failed to copy battle-sprite height data for opposite gender: {heightEx.Message}");
                }

                narcReader = new NarcReader(packedPath);
                ChangeLoadedFile(currentLoadedId);

                MessageBox.Show($"Added {missingGender} sprites (duplicated from the existing {sourceGender} sprites).\n" +
                    "Use Load Sprite Set to give them their own look.", "Add Opposite Gender Sprites");
            } catch (Exception ex) {
                MessageBox.Show($"Failed to add {missingGender} sprites: {ex.Message}", "Add Opposite Gender Sprites",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                narcReader = new NarcReader(RomInfo.gameDirs[DirNames.pokemonBattleSprites].packedDir);
            }
        }

        private static void CopyEntryFile(string unpackedDir, int srcIdx, int dstIdx) {
            File.Copy(Path.Combine(unpackedDir, srcIdx.ToString("D4")),
                      Path.Combine(unpackedDir, dstIdx.ToString("D4")), true);
        }

        // Keeps a stale unpacked folder from overwriting this edit on the next Save ROM.
        private void SyncUnpackedEntryIfPresent(DirNames dir, int entryIndex) {
            if (!RomInfo.gameDirs.ContainsKey(dir)) return;
            string unpackedDir = RomInfo.gameDirs[dir].unpackedDir;
            string entryPath = Path.Combine(unpackedDir, entryIndex.ToString("D4"));
            if (!File.Exists(entryPath)) return;

            narcReader.OpenEntry(entryIndex);
            byte[] data = new byte[narcReader.fe[entryIndex].Size];
            narcReader.fs.Read(data, 0, data.Length);
            narcReader.Close();
            File.WriteAllBytes(entryPath, data);
        }
        
        // Showing the one shared form sprite under both genders is misleading for a species that only has
        // one: Deoxys and Unown are genderless, Wormadam is female-only. Blank the gender that isn't real.
        private void ApplyFormGenderGap() {
            byte ratio = PokemonSpriteModel.ReadGenderRatio(currentLoadedId);
            if (ratio == SpeciesFile.GENDER_RATIO_FEMALE) {
                currentSprites.Sprites[1] = null;
                currentSprites.Sprites[3] = null;
            } else if (ratio == SpeciesFile.GENDER_RATIO_MALE || ratio == SpeciesFile.GENDER_RATIO_GENDERLESS) {
                currentSprites.Sprites[0] = null;
                currentSprites.Sprites[2] = null;
            }
        }

        private void LoadOtherFormSprites(int selectedIndex) {
            if (currentFormData == null || selectedIndex >= currentFormData.Length) {
                MessageBox.Show($"Invalid form index: {selectedIndex}", "Error");
                return;
            }
            
            PokemonSpriteModel.FormSpriteData formData = currentFormData[selectedIndex];
            
            // A form has one back and one front sprite shared by both genders, so the same image goes in
            // both slots and the gender the species doesn't actually have is blanked out below.
            if (formData.BackSpriteIndex >= 0 && formData.BackSpriteIndex < narcReader.fe.Length
                && narcReader.fe[formData.BackSpriteIndex].Size == 6448) {
                narcReader.OpenEntry(formData.BackSpriteIndex);
                Bitmap backSprite = MakeImage(narcReader.fs);
                narcReader.Close();

                currentSprites.Sprites[0] = backSprite;
                currentSprites.Sprites[1] = backSprite;
            }

            if (formData.FrontSpriteIndex >= 0 && formData.FrontSpriteIndex < narcReader.fe.Length
                && narcReader.fe[formData.FrontSpriteIndex].Size == 6448) {
                narcReader.OpenEntry(formData.FrontSpriteIndex);
                Bitmap frontSprite = MakeImage(narcReader.fs);
                narcReader.Close();

                currentSprites.Sprites[2] = frontSprite;
                currentSprites.Sprites[3] = frontSprite;
            }

            ApplyFormGenderGap();

            // Load normal palette
            if (narcReader.fe[formData.NormalPaletteIndex].Size == 72) {
                narcReader.OpenEntry(formData.NormalPaletteIndex);
                currentSprites.Normal = SetPal(narcReader.fs);
                narcReader.Close();
            }
            
            // Load shiny palette
            if (narcReader.fe[formData.ShinyPaletteIndex].Size == 72) {
                narcReader.OpenEntry(formData.ShinyPaletteIndex);
                currentSprites.Shiny = SetPal(narcReader.fs);
                narcReader.Close();
            }
        }
        #endregion

        private void BasePalette_SelectedIndexChanged(object sender, EventArgs e) {
            if (Helpers.HandlersDisabled) {
                return;
            }
            
            if (narcReader.fe[(int)BasePalette.SelectedItem].Size == 72) {
                narcReader.OpenEntry((int)BasePalette.SelectedItem);
                currentSprites.Normal = SetPal(narcReader.fs);
                narcReader.Close();
            }
            LoadImages();
            SetDirty(true);
        }

        private void ShinyPalette_SelectedIndexChanged(object sender, EventArgs e) {
            if (Helpers.HandlersDisabled) {
                return;
            }
            
            if (narcReader.fe[(int)ShinyPalette.SelectedItem].Size == 72) {
                narcReader.OpenEntry((int)ShinyPalette.SelectedItem);
                currentSprites.Shiny = SetPal(narcReader.fs);
                narcReader.Close();
            }
            LoadImages();
            SetDirty(true);
        }

        #region UI Setup
        private void SetupPictureBoxes() {
            displayPictureBoxes = new PictureBox[2, 4];

            femaleBackNormalPic.Name = "0";
            displayPictureBoxes[0, 0] = femaleBackNormalPic;

            maleBackNormalPic.Name = "1";
            displayPictureBoxes[1, 0] = maleBackNormalPic;

            femaleFrontNormalPic.Name = "2";
            displayPictureBoxes[0, 1] = femaleFrontNormalPic;

            maleFrontNormalPic.Name = "3";
            displayPictureBoxes[1, 1] = maleFrontNormalPic;

            femaleBackShinyPic.Name = "4";
            displayPictureBoxes[0, 2] = femaleBackShinyPic;

            maleBackShinyPic.Name = "5";
            displayPictureBoxes[1, 2] = maleBackShinyPic;

            femaleFrontShinyPic.Name = "6";
            displayPictureBoxes[0, 3] = femaleFrontShinyPic;

            maleFrontShinyPic.Name = "7";
            displayPictureBoxes[1, 3] = maleFrontShinyPic;
        }
        #endregion

        #region Image Display
        private void LoadImages() {
            StopAnimation();

            if (currentSprites.Normal != null && currentSprites.Shiny == null) {
                currentSprites.Shiny = currentSprites.Normal;
            }

            // A freshly loaded Pokémon starts on frame 1 everywhere; RenderCells moves any pose whose
            // first frame is blank onto the one that isn't.
            for (int cell = 0; cell < CellCount; cell++) {
                cellFrame[cell] = 0;
            }

            RenderCells();
        }
        #endregion

        #region Image Validation
        private Bitmap CheckSize(Bitmap image, string filename, string name, int spritenumber = 2) {
            IndexedBitmapHandler handler = new IndexedBitmapHandler();
            
            if (image.PixelFormat != PixelFormat.Format8bppIndexed) {
                DialogResult result = MessageBox.Show(
                    $"{filename} is not 8bpp Indexed! Attempt conversion?", 
                    "Incompatible image format", 
                    MessageBoxButtons.YesNo);
                    
                if (result != DialogResult.Yes) {
                    return null;
                }
                
                image = handler.Convert(image, PixelFormat.Format8bppIndexed);
                if (image == null || image.PixelFormat != PixelFormat.Format8bppIndexed || image.Palette == null) {
                    MessageBox.Show("Conversion failed.", "Failed");
                    return null;
                }
            }
            
            if (!IsValidSpriteSize(image)) {
                image = TryResizeSprite(image, handler, filename);
                if (image == null) {
                    return null;
                }
            }
            
            // Adjust sprite dimensions to standard size
            if (image.Width == 64) {
                image = handler.Resize(image, 48, 8, 0, 0);
            }
            if (image.Height == 64) {
                image = handler.Resize(image, 0, 0, 0, 16);
            }
            if (image.Width == 80) {
                image = handler.Resize(image, 40, 0, 0, 0);
            }
            
            if (image.Palette.Entries.Length > 16) {
                MessageBox.Show($"{filename} has too many colors. Must have 16 or less.", "Too many colors");
                return null;
            }
            
            return image;
        }
        
        private bool IsValidSpriteSize(Bitmap image) {
            bool validHeight = (image.Height == 64 || image.Height == 80);
            bool validWidth = (image.Width == 64 || image.Width == 80 || image.Width == 160);
            return validHeight && validWidth;
        }
        
        private Bitmap TryResizeSprite(Bitmap image, IndexedBitmapHandler handler, string filename) {
            int imagescale = 0;
            
            if ((image.Width / 64 == image.Height / 64) && (image.Width % 64 == 0) && (image.Height % 64 == 0)) {
                imagescale = image.Width / 64;
            }
            if ((image.Width / 80 == image.Height / 80) && (image.Width % 80 == 0) && (image.Height % 80 == 0)) {
                imagescale = image.Width / 80;
            }
            if ((image.Width / 160 == image.Height / 80) && (image.Width % 160 == 0) && (image.Height % 80 == 0)) {
                imagescale = image.Width / 160;
            }
            
            if (imagescale > 1) {
                DialogResult result = MessageBox.Show(
                    $"{filename} is too large. Attempt to shrink?", 
                    "Image too large", 
                    MessageBoxButtons.YesNo);
                    
                if (result != DialogResult.Yes) {
                    return null;
                }
                return handler.Resize(image, 0, 0, imagescale, imagescale);
            }
            
            MessageBox.Show($"{filename} is wrong size. Must be 64x64, 80x80 or 160x80.", "Wrong size");
            return null;
        }
        #endregion

        private void OpenPngs_Click(object sender, EventArgs e) {
            if (!OpenPngs.Enabled) {
                return;
            }

            PictureBox source = sender as PictureBox;
            if (source == null) {
                return;
            }
            int index = Convert.ToInt32(source.Name);

            using (OpenFileDialog openFileDialog = new OpenFileDialog()) {
                openFileDialog.Title = "Choose an image for " + SlotCaption(index);
                openFileDialog.CheckPathExists = true;
                openFileDialog.Filter = "Supported formats: *.bmp, *.gif, *.png | *.bmp; *.gif; *.png";

                if (openFileDialog.ShowDialog() != DialogResult.OK) {
                    return;
                }

                if (ImportImageIntoSlot(openFileDialog.FileName, index)) {
                    RenderCells();
                    SetDirty(true);
                }
            }
        }

        /// <summary>Human name for one of the eight display cells; 4-7 are the shiny-palette views.</summary>
        public static string SlotCaption(int index) {
            string[] names = { "Female Back", "Male Back", "Female Front", "Male Front" };
            return names[index % 4] + (index > 3 ? " (shiny palette)" : "");
        }

        /// <summary>
        /// Loads one image into a pose. Cells 4-7 take the shiny palette from the image instead of
        /// replacing the artwork, since shiny is the same pixels under a second palette.
        /// </summary>
        public bool ImportImageIntoSlot(string path, int index) {
            Bitmap image;
            try {
                image = new Bitmap(path);
            } catch (Exception ex) {
                MessageBox.Show("Couldn't read " + Path.GetFileName(path) + ": " + ex.Message,
                    "Import failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }

            IndexedBitmapHandler handler = new IndexedBitmapHandler();

            if (index > 3) {
                image = CheckSize(image, path, "Shiny");
                if (image == null) {
                    return false;
                }

                // Sprites[] holds one pixel-index bitmap per pose that both the Normal and Shiny views
                // render, so a shiny colour has to land at the slot number that pose's own pixels
                // already use for it, matched positionally rather than taken from this file's own
                // palette order.
                Bitmap parent = currentSprites.Sprites[index % 4];
                bool[] resolved = null;
                ColorPalette candidate = parent == null ? null : handler.AlternatePalette(parent, image, out resolved);
                if (candidate == null) {
                    currentSprites.Shiny = PadPaletteTo16(image.Palette);
                    shinyResolved = PadBoolTo16(handler.IsUsed(image));
                } else {
                    currentSprites.Shiny = handler.MergeByIndex(currentSprites.Shiny, shinyResolved, candidate, resolved);
                    bool[] padResolved = PadBoolTo16(resolved);
                    for (int i = 0; i < 16; i++) {
                        shinyResolved[i] = shinyResolved[i] || padResolved[i];
                    }
                }
                shinyImported = true;
                return true;
            }

            image = CheckSize(image, path, spriteTypeNames[index], index);
            if (image == null) {
                return false;
            }

            bool match = handler.PaletteEquals(currentSprites.Normal, image);
            if (!match) {
                if (usedEntries == null) {
                    usedEntries = handler.IsUsed(image);
                } else {
                    Bitmap matched = handler.PaletteMatch(currentSprites.Normal, image, usedEntries);
                    if (matched == null) {
                        // The shiny sprites are the same artwork under a second palette, so
                        // renumbering the colours here changes how they look too.
                        DialogResult replace = MessageBox.Show(
                            "This image's colours don't fit alongside the existing palette (16 colours max combined)." +
                            Environment.NewLine + Environment.NewLine +
                            "Continuing replaces the whole palette with this image's own colours, which also changes " +
                            "how the shiny sprites look, because they are drawn from this same artwork." +
                            Environment.NewLine + Environment.NewLine + "Continue anyway?",
                            "This will change the shiny sprites too", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                        if (replace != DialogResult.Yes) {
                            return false;
                        }
                        usedEntries = handler.IsUsed(image);
                    } else {
                        image = matched;
                        usedEntries = handler.IsUsed(image, usedEntries);
                    }
                }
                currentSprites.Normal = PadPaletteTo16(image.Palette);
            }
            currentSprites.Sprites[index] = image;
            return true;
        }

        private void SaveChanges_Click(object sender, EventArgs e) {
            if (!OpenPngs.Enabled) {
                return;
            }
            int selectedIndex = IndexBox.SelectedIndex;
            if (selectedIndex < 0) {
                MessageBox.Show("No valid Pokémon selected. Fix it before saving.", "Invalid Selection", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // If editing main sprites, files are organized as groups of 6 entries per pokemon
            if (!isLoadingOtherForms) {
                int baseOffset = currentLoadedId * 6;

                for (int i = 0; i < 4; i++) {
                    if (currentSprites.Sprites[i] == null) continue;
                    if (narcReader.fe[baseOffset + i].Size == 6448) {
                        narcReader.OpenEntry(baseOffset + i);
                        SaveBin(narcReader.fs, currentSprites.Sprites[i]);
                        narcReader.Close();
                        SyncUnpackedEntryIfPresent(DirNames.pokemonBattleSprites, baseOffset + i);
                    }
                }

                if (narcReader.fe[baseOffset + 4].Size == 72 && currentSprites.Normal != null) {
                    narcReader.OpenEntry(baseOffset + 4);
                    SavePal(narcReader.fs, currentSprites.Normal);
                    narcReader.Close();
                    SyncUnpackedEntryIfPresent(DirNames.pokemonBattleSprites, baseOffset + 4);
                }

                if (narcReader.fe[baseOffset + 5].Size == 72 && currentSprites.Shiny != null) {
                    narcReader.OpenEntry(baseOffset + 5);
                    SavePal(narcReader.fs, currentSprites.Shiny);
                    narcReader.Close();
                    SyncUnpackedEntryIfPresent(DirNames.pokemonBattleSprites, baseOffset + 5);
                }
            }
            else {
                // Other-forms NARC uses arbitrary indices defined in currentFormData
                if (selectedFormIndex < 0 || selectedFormIndex >= currentFormData.Length) {
                    MessageBox.Show("Invalid form data selected. Save aborted.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                PokemonSpriteModel.FormSpriteData form = currentFormData[selectedFormIndex];

                // Whichever gender this species actually has holds the form's art; the other slot is blank.
                Bitmap formBack = currentSprites.Sprites[1] ?? currentSprites.Sprites[0];
                Bitmap formFront = currentSprites.Sprites[3] ?? currentSprites.Sprites[2];

                if (form.BackSpriteIndex >= 0 && form.BackSpriteIndex < narcReader.fe.Length && formBack != null) {
                    if (narcReader.fe[form.BackSpriteIndex].Size == 6448) {
                        narcReader.OpenEntry(form.BackSpriteIndex);
                        SaveBin(narcReader.fs, formBack);
                        narcReader.Close();
                        SyncUnpackedEntryIfPresent(DirNames.otherPokemonBattleSprites, form.BackSpriteIndex);
                    }
                }

                if (form.FrontSpriteIndex >= 0 && form.FrontSpriteIndex < narcReader.fe.Length && formFront != null) {
                    if (narcReader.fe[form.FrontSpriteIndex].Size == 6448) {
                        narcReader.OpenEntry(form.FrontSpriteIndex);
                        SaveBin(narcReader.fs, formFront);
                        narcReader.Close();
                        SyncUnpackedEntryIfPresent(DirNames.otherPokemonBattleSprites, form.FrontSpriteIndex);
                    }
                }

                if (form.NormalPaletteIndex >= 0 && form.NormalPaletteIndex < narcReader.fe.Length && currentSprites.Normal != null) {
                    if (narcReader.fe[form.NormalPaletteIndex].Size == 72) {
                        narcReader.OpenEntry(form.NormalPaletteIndex);
                        SavePal(narcReader.fs, currentSprites.Normal);
                        narcReader.Close();
                        SyncUnpackedEntryIfPresent(DirNames.otherPokemonBattleSprites, form.NormalPaletteIndex);
                    }
                }

                if (form.ShinyPaletteIndex >= 0 && form.ShinyPaletteIndex < narcReader.fe.Length && currentSprites.Shiny != null) {
                    if (narcReader.fe[form.ShinyPaletteIndex].Size == 72) {
                        narcReader.OpenEntry(form.ShinyPaletteIndex);
                        SavePal(narcReader.fs, currentSprites.Shiny);
                        narcReader.Close();
                        SyncUnpackedEntryIfPresent(DirNames.otherPokemonBattleSprites, form.ShinyPaletteIndex);
                    }
                }
            }

            SetDirty(false);
        }

        // Credit to loadingNOW and SCV for the original PokeDsPic and PokeDsPicPlatinum, 
        // without which this would never have happened. In addition to G4SpriteEditor
        
        private void btnSaveAs_Click(object sender, EventArgs e) {
            using (SaveFileDialog saveFileDialog = new SaveFileDialog()) {
                saveFileDialog.Title = "Save Image Set";
                saveFileDialog.CheckPathExists = true;
                saveFileDialog.Filter = "*.png|*.png";
                
                if (saveFileDialog.ShowDialog() != DialogResult.OK) {
                    return;
                }
                
                string baseFileName = saveFileDialog.FileName.Replace(".png", "");
                bool shinySaved = false;
                
                // Save front sprites (priority for shiny)
                if (currentSprites.Sprites[2] != null) {
                    if (currentSprites.Shiny != null) {
                        currentSprites.Sprites[2].Palette = currentSprites.Shiny;
                        SavePNG(currentSprites.Sprites[2], baseFileName + "Shiny.png");
                        shinySaved = true;
                    }
                    currentSprites.Sprites[2].Palette = currentSprites.Normal;
                    SavePNG(currentSprites.Sprites[2], baseFileName + "FFront.png");
                }
                
                if (currentSprites.Sprites[3] != null) {
                    if (currentSprites.Shiny != null && !shinySaved) {
                        currentSprites.Sprites[3].Palette = currentSprites.Shiny;
                        SavePNG(currentSprites.Sprites[3], baseFileName + "Shiny.png");
                        shinySaved = true;
                    }
                    currentSprites.Sprites[3].Palette = currentSprites.Normal;
                    SavePNG(currentSprites.Sprites[3], baseFileName + "MFront.png");
                }
                
                // Save back sprites
                if (currentSprites.Sprites[0] != null) {
                    if (currentSprites.Shiny != null && !shinySaved) {
                        currentSprites.Sprites[0].Palette = currentSprites.Shiny;
                        SavePNG(currentSprites.Sprites[0], baseFileName + "Shiny.png");
                        shinySaved = true;
                    }
                    currentSprites.Sprites[0].Palette = currentSprites.Normal;
                    SavePNG(currentSprites.Sprites[0], baseFileName + "FBack.png");
                }
                
                if (currentSprites.Sprites[1] != null) {
                    if (currentSprites.Shiny != null && !shinySaved) {
                        currentSprites.Sprites[1].Palette = currentSprites.Shiny;
                        SavePNG(currentSprites.Sprites[1], baseFileName + "Shiny.png");
                    }
                    currentSprites.Sprites[1].Palette = currentSprites.Normal;
                    SavePNG(currentSprites.Sprites[1], baseFileName + "MBack.png");
                }
            }
        }

        private void SaveSingle_Click(object sender, EventArgs e) {
            int index = SaveBox.SelectedIndex;
            
            if (currentSprites.Sprites[index % 4] == null) {
                MessageBox.Show("Image is empty.");
                return;
            }
            
            using (SaveFileDialog saveFileDialog = new SaveFileDialog()) {
                saveFileDialog.Title = "Save As PNG";
                saveFileDialog.OverwritePrompt = true;
                saveFileDialog.CheckPathExists = true;
                saveFileDialog.Filter = "*.png|*.png";
                
                if (saveFileDialog.ShowDialog() != DialogResult.OK) {
                    return;
                }
                
                Bitmap image = currentSprites.Sprites[index % 4];
                image.Palette = index > 3 ? currentSprites.Shiny : currentSprites.Normal;
                SavePNG(image, saveFileDialog.FileName);
            }
        }

        private void btnOpenOther_Click(object sender, EventArgs e) {
            // Superseded by the Form dropdown, which is always visible for species that have forms.
        }

        private void btnLoadSheet_Click(object sender, EventArgs e) {
            if (!OpenPngs.Enabled) {
                return;
            }
            
            OpenPngs.Enabled = false;
            
            using (OpenFileDialog openFileDialog = new OpenFileDialog()) {
                openFileDialog.Title = "Select a sprite sheet";
                openFileDialog.CheckPathExists = true;
                openFileDialog.Filter = "Supported formats: *.bmp, *.gif, *.png | *.bmp; *.gif; *.png";
                
                if (openFileDialog.ShowDialog() != DialogResult.OK) {
                    OpenPngs.Enabled = true;
                    return;
                }
                
                Bitmap image = new Bitmap(openFileDialog.FileName);
                
                if (image.Width != 256 || image.Height != 64) {
                    MessageBox.Show("The sprite sheet should be 256x64.");
                    OpenPngs.Enabled = true;
                    return;
                }
                
                IndexedBitmapHandler handler = new IndexedBitmapHandler();
                image = handler.Convert(image, PixelFormat.Format8bppIndexed);
                image.Palette = StandardizeColors(image);
                
                Bitmap[] tiles = handler.Split(image, 64, 64);
                SpriteSet sprites = new SpriteSet();
                
                bool[] used = handler.IsUsed(tiles[0]);
                used = handler.IsUsed(tiles[2], used);
                
                // Process front sprite
                Bitmap temp = handler.ShrinkPalette(tiles[0], used);
                sprites.Normal = temp.Palette;
                temp = handler.Resize(temp, 8, 8, 8, 8);
                temp = handler.Concat(temp, temp);
                sprites.Sprites[2] = temp;
                sprites.Sprites[3] = temp;
                
                // Process back sprite
                temp = handler.ShrinkPalette(tiles[2], used);
                temp = handler.Resize(temp, 8, 8, 8, 8);
                if (RomInfo.gameFamily == RomInfo.GameFamilies.DP) {
                    temp = handler.Resize(temp, 0, 0, 0, 80);
                } else {
                    temp = handler.Concat(temp, temp);
                }
                sprites.Sprites[0] = temp;
                sprites.Sprites[1] = temp;
                
                // Process shiny palette
                temp = handler.ShrinkPalette(tiles[1], used);
                temp = handler.Resize(temp, 8, 8, 8, 8);
                temp = handler.Concat(temp, temp);
                ColorPalette shinyCandidate = handler.AlternatePalette(sprites.Sprites[2], temp, out bool[] shinyCandidateResolved);
                sprites.Shiny = PadPaletteTo16(shinyCandidate);
                shinyResolved = PadBoolTo16(shinyCandidateResolved);

                currentSprites = sprites;
                shinyImported = true;
            }
            
            OpenPngs.Enabled = true;
            LoadImages();
            SetDirty(true);
        }

        private void MakeShiny_Click(object sender, EventArgs e) {
            if (!OpenPngs.Enabled) {
                return;
            }
            
            OpenPngs.Enabled = false;
            
            using (OpenFileDialog openFileDialog = new OpenFileDialog()) {
                openFileDialog.Title = "Choose the base image";
                openFileDialog.CheckPathExists = true;
                openFileDialog.Filter = "Supported formats: *.bmp, *.gif, *.png | *.bmp; *.gif; *.png";
                
                if (openFileDialog.ShowDialog() != DialogResult.OK) {
                    OpenPngs.Enabled = true;
                    return;
                }
                
                string baseFilename = openFileDialog.FileName;
                
                openFileDialog.Title = "Choose the shiny image";
                if (openFileDialog.ShowDialog() != DialogResult.OK) {
                    OpenPngs.Enabled = true;
                    return;
                }
                
                Bitmap baseImage = new Bitmap(baseFilename);
                Bitmap shinyImage = new Bitmap(openFileDialog.FileName);
                IndexedBitmapHandler handler = new IndexedBitmapHandler();
                
                ColorPalette candidate = handler.AlternatePalette(baseImage, shinyImage, out bool[] resolved);
                if (candidate == null) {
                    MessageBox.Show("Failed!", "Failed");
                } else {
                    currentSprites.Shiny = handler.MergeByIndex(currentSprites.Shiny, shinyResolved, candidate, resolved);
                    bool[] padResolved = PadBoolTo16(resolved);
                    for (int i = 0; i < 16; i++) {
                        shinyResolved[i] = shinyResolved[i] || padResolved[i];
                    }
                    shinyImported = true;
                }
            }
            
            OpenPngs.Enabled = true;
            LoadImages();
            SetDirty(true);
        }
        #endregion

        #region Utility Methods
        private ColorPalette StandardizeColors(Bitmap image) {
            ColorPalette pal = image.Palette;
            bool hasOffColors = false;
            
            for (int i = 0; i < pal.Entries.Length; i++) {
                if ((pal.Entries[i].R % 8 != 0) || (pal.Entries[i].G % 8 != 0) || (pal.Entries[i].B % 8 != 0)) {
                    hasOffColors = true;
                    break;
                }
            }
            
            if (hasOffColors) {
                for (int i = 0; i < pal.Entries.Length; i++) {
                    byte r = (byte)(pal.Entries[i].R - (pal.Entries[i].R % 8));
                    byte g = (byte)(pal.Entries[i].G - (pal.Entries[i].G % 8));
                    byte b = (byte)(pal.Entries[i].B - (pal.Entries[i].B % 8));
                    pal.Entries[i] = Color.FromArgb(r, g, b);
                }
            }
            
            return pal;
        }

        private void SavePNG(Bitmap image, string filename) {
            IndexedBitmapHandler handler = new IndexedBitmapHandler();
            byte[] array = handler.GetArray(image);
            Bitmap temp = handler.MakeImage(image.Width, image.Height, array, image.PixelFormat);
            ColorPalette cleaned = handler.CleanPalette(image);
            temp.Palette = cleaned;
            temp.Save(filename, ImageFormat.Png);
        }
        #endregion

        #region Binary Operations
        private static Bitmap MakeImage(FileStream fs) {
            fs.Seek(48L, SeekOrigin.Current);
            BinaryReader binaryReader = new BinaryReader(fs);
            
            ushort[] array = new ushort[3200];
            for (int i = 0; i < 3200; i++) {
                array[i] = binaryReader.ReadUInt16();
            }
            
            uint num = array[0];
            if (RomInfo.gameFamily != RomInfo.GameFamilies.DP) {
                for (int j = 0; j < 3200; j++) {
                    unchecked {
                        array[j] = (ushort)(array[j] ^ (ushort)(num & 0xFFFF));
                        num *= 1103515245;
                        num += 24691;
                    }
                }
            } else {
                num = array[3199];
                for (int j = 3199; j >= 0; j--) {
                    unchecked {
                        array[j] = (ushort)(array[j] ^ (ushort)(num & 0xFFFF));
                        num *= 1103515245;
                        num += 24691;
                    }
                }
            }
            
            Bitmap resultBitmap = new Bitmap(160, 80, PixelFormat.Format8bppIndexed);
            Rectangle rect = new Rectangle(0, 0, 160, 80);
            
            byte[] pixelArray = new byte[12800];
            for (int k = 0; k < 3200; k++) {
                pixelArray[k * 4] = (byte)(array[k] & 0xF);
                pixelArray[k * 4 + 1] = (byte)((array[k] >> 4) & 0xF);
                pixelArray[k * 4 + 2] = (byte)((array[k] >> 8) & 0xF);
                pixelArray[k * 4 + 3] = (byte)((array[k] >> 12) & 0xF);
            }
            
            BitmapData bitmapData = resultBitmap.LockBits(rect, ImageLockMode.WriteOnly, resultBitmap.PixelFormat);
            IntPtr scan = bitmapData.Scan0;
            Marshal.Copy(pixelArray, 0, scan, 12800);
            resultBitmap.UnlockBits(bitmapData);
            
            Bitmap tempBitmap = new Bitmap(1, 1, PixelFormat.Format4bppIndexed);
            ColorPalette palette = tempBitmap.Palette;
            for (int l = 0; l < 16; l++) {
                palette.Entries[l] = Color.FromArgb(l << 4, l << 4, l << 4);
            }
            resultBitmap.Palette = palette;
            
            if (resultBitmap == null) {
                MessageBox.Show("MakeImage Failed");
                return null;
            }
            
            return resultBitmap;
        }

        private static ColorPalette SetPal(FileStream fs) {
            fs.Seek(40L, SeekOrigin.Current);
            
            ushort[] array = new ushort[16];
            BinaryReader binaryReader = new BinaryReader(fs);
            for (int i = 0; i < 16; i++) {
                array[i] = binaryReader.ReadUInt16();
            }
            
            Bitmap bitmap = new Bitmap(1, 1, PixelFormat.Format4bppIndexed);
            ColorPalette palette = bitmap.Palette;
            
            for (int j = 0; j < 16; j++) {
                palette.Entries[j] = Color.FromArgb(
                    (array[j] & 0x1F) << 3, 
                    ((array[j] >> 5) & 0x1F) << 3, 
                    ((array[j] >> 10) & 0x1F) << 3);
            }
            
            return palette;
        }

        // Battle Display support: static, id-keyed methods (not instance methods off currentSprites),
        // since the Battle Display tab has its own species selector and needs its own sprite data.
        // Slot order: 0=FemaleBack, 1=MaleBack, 2=FemaleFront, 3=MaleFront.

        /// <summary>Decodes a species' 4 battle-sprite slots + normal palette. Null entries mean that
        /// slot has no data (e.g. the missing gender on a mono-gender species).</summary>
        public static Bitmap[] LoadBattleSpritesFor(int id) {
            var sprites = new Bitmap[4];
            try {
                var narc = new NarcReader(RomInfo.gameDirs[DirNames.pokemonBattleSprites].packedDir);
                int baseOffset = id * 6;

                ColorPalette normalPal = null;
                if (baseOffset + 4 < narc.fe.Length && narc.fe[baseOffset + 4].Size == 72) {
                    narc.OpenEntry(baseOffset + 4);
                    normalPal = SetPal(narc.fs);
                    narc.Close();
                }
                if (normalPal == null) return sprites;

                for (int i = 0; i < 4; i++) {
                    if (baseOffset + i < narc.fe.Length && narc.fe[baseOffset + i].Size == 6448) {
                        narc.OpenEntry(baseOffset + i);
                        sprites[i] = MakeImage(narc.fs);
                        narc.Close();
                        sprites[i].Palette = normalPal;
                    }
                }
            } catch { }
            return sprites;
        }

        /// <summary>Number of 80px-wide frames in a decoded battle-sprite slot (1 if null/single-width).</summary>
        public static int GetBattleFrameCount(Bitmap slot) => slot == null ? 1 : Math.Max(1, slot.Width / 80);

        /// <summary>Crops the given frame (0-based, 80px wide) out of a decoded battle-sprite slot
        /// (see <see cref="LoadBattleSpritesFor"/>), with palette index 0 made transparent. Returns null
        /// if the slot has no data.</summary>
        public static Bitmap CropBattleFrame(Bitmap slot, int frame) {
            if (slot == null) return null;

            int frameCount = Math.Max(1, slot.Width / 80);
            if (frame < 0 || frame >= frameCount) frame = 0;
            int x0 = frame * 80;

            Color key = slot.Palette.Entries[0];
            var outBmp = new Bitmap(80, slot.Height, PixelFormat.Format32bppArgb);
            for (int y = 0; y < slot.Height; y++) {
                for (int x = 0; x < 80; x++) {
                    Color c = slot.GetPixel(x0 + x, y);
                    outBmp.SetPixel(x, y, (c.R == key.R && c.G == key.G && c.B == key.B) ? Color.Transparent : c);
                }
            }
            return outBmp;
        }

        private void LoadSprites() {
            narcReader = new NarcReader(RomInfo.gameDirs[DirNames.pokemonBattleSprites].packedDir);

            IndexBox.Items.Clear();
            for (int i = 0; i < pokenames.Length; i++) {
                IndexBox.Items.Add($"{i:D3} {pokenames[i]}");
            }

            // Load first entry (index 1 to skip "None/Egg" at 0)
            ChangeLoadedFile(1);
        }

        private void SaveBin(FileStream fs, Bitmap source) {
            BinaryWriter binaryWriter = new BinaryWriter(fs);
            Rectangle rect = new Rectangle(0, 0, 160, 80);
            
            BitmapData bitmapData = source.LockBits(rect, ImageLockMode.ReadOnly, source.PixelFormat);
            IntPtr scan = bitmapData.Scan0;
            byte[] array = new byte[12800];
            Marshal.Copy(scan, array, 0, 12800);
            source.UnlockBits(bitmapData);
            
            ushort[] array2 = new ushort[3200];
            for (int i = 0; i < 3200; i++) {
                array2[i] = (ushort)((array[i * 4] & 0xF) | 
                                     ((array[i * 4 + 1] & 0xF) << 4) | 
                                     ((array[i * 4 + 2] & 0xF) << 8) | 
                                     ((array[i * 4 + 3] & 0xF) << 12));
            }
            
            uint num = 0u;
            if (RomInfo.gameFamily != RomInfo.GameFamilies.DP) {
                for (int j = 0; j < 3200; j++) {
                    unchecked {
                        array2[j] = (ushort)(array2[j] ^ (ushort)(num & 0xFFFF));
                        num *= 1103515245;
                        num += 24691;
                    }
                }
            } else {
                num = 31315u;
                for (int k = 3199; k >= 0; k--) {
                    num += array2[k];
                }
                for (int k = 3199; k >= 0; k--) {
                    unchecked {
                        array2[k] = (ushort)(array2[k] ^ (ushort)(num & 0xFFFF));
                        num *= 1103515245;
                        num += 24691;
                    }
                }
            }
            
            byte[] header = new byte[48] {
                82, 71, 67, 78, 255, 254, 0, 1, 48, 25, 0, 0, 16, 0, 1, 0,
                82, 65, 72, 67, 32, 25, 0, 0, 10, 0, 20, 0, 3, 0, 0, 0,
                0, 0, 0, 0, 1, 0, 0, 0, 0, 25, 0, 0, 24, 0, 0, 0
            };
            
            for (int k = 0; k < 48; k++) {
                binaryWriter.Write(header[k]);
            }
            
            for (int l = 0; l < 3200; l++) {
                binaryWriter.Write(array2[l]);
            }
        }

        // LoadImages() applies this palette to every direction/gender slot, so it must always be 16 entries.
        private ColorPalette PadPaletteTo16(ColorPalette pal) {
            if (pal.Entries.Length >= 16) {
                return pal;
            }
            using (Bitmap temp = new Bitmap(1, 1, PixelFormat.Format8bppIndexed)) {
                ColorPalette padded = temp.Palette;
                for (int i = 0; i < 16; i++) {
                    padded.Entries[i] = i < pal.Entries.Length ? pal.Entries[i] : Color.Black;
                }
                return padded;
            }
        }

        private bool[] PadBoolTo16(bool[] used) {
            bool[] padded = new bool[16];
            for (int i = 0; i < 16; i++) {
                padded[i] = i < used.Length && used[i];
            }
            return padded;
        }

        private void SavePal(FileStream fs, ColorPalette palette) {
            byte[] buffer = new byte[40] {
                82, 76, 67, 78, 255, 254, 0, 1, 72, 0, 0, 0, 16, 0, 1, 0,
                84, 84, 76, 80, 56, 0, 0, 0, 4, 0, 10, 0, 0, 0, 0, 0,
                32, 0, 0, 0, 16, 0, 0, 0
            };
            
            BinaryWriter binaryWriter = new BinaryWriter(fs);
            binaryWriter.Write(buffer, 0, 40);
            
            ushort[] array = new ushort[16];
            for (int i = 0; i < 16; i++) {
                // A palette shorter than 16 entries would index out of range past this point.
                Color c = i < palette.Entries.Length ? palette.Entries[i] : Color.Black;
                array[i] = (ushort)(((c.R >> 3) & 0x1F) |
                                    (((c.G >> 3) & 0x1F) << 5) |
                                    (((c.B >> 3) & 0x1F) << 10));
            }
            
            for (int j = 0; j < 16; j++) {
                binaryWriter.Write(array[j]);
            }
        }
        #endregion
    }
}