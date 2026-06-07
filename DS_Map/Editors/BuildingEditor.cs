using System;
using System.IO;
using System.Text;
using System.Windows.Forms;
using DSPRE.Editors;
using LibNDSFormats.NSBMD;
using LibNDSFormats.NSBTX;
using Microsoft.WindowsAPICodePack.Dialogs;
using static DSPRE.RomInfo;

namespace DSPRE {
    public partial class BuildingEditor : Form {
        #region Variables
        public static string temp_btxname = "BLDtexture.nsbtx";
        private readonly string folder;
        bool disableHandlers = new bool();
        readonly RomInfo rom;

        NSBMD currentNSBMD;
        byte[] currentModelData;

        readonly NSBMDGlRenderer renderer = new NSBMDGlRenderer();

        public static float ang = 0.0f;
        public static float dist = 12.8f;
        public static float elev = 50.0f;
        public static float tempAng = 0.0f;
        public static float tempDist = 0.0f;
        public static float tempElev = 0.0f;
        public float perspective = 45f;

        /* Bld Rotation vars */
        public bool lRot;
        public bool rRot;
        public bool uRot;
        public bool dRot;
        #endregion

        public BuildingEditor(RomInfo romInfo) {
            InitializeComponent();

            // Register with OpenEditorsRegistry for project switch tracking
            OpenEditorsRegistry.Register(this);

            rom = romInfo;

            buildingOpenGLControl.InitializeContexts();
            buildingOpenGLControl.MakeCurrent();
            buildingOpenGLControl.MouseWheel += new MouseEventHandler(buildingOpenGLControl_MouseWheel);
            // Gl.glEnable(Gl.GL_TEXTURE_2D) stubbed — awaiting OpenTK 4 rewrite

            if (RomInfo.gameFamily == GameFamilies.HGSS) {
                interiorCheckBox.Enabled = true;
            }

            Helpers.DisableHandlers();
            FillListBox(false);
            FillTexturesBox();
            textureComboBox.SelectedIndex = 0;
            Helpers.EnableHandlers();
            buildingEditorBldListBox.SelectedIndex = 0;
        }

        #region Subroutines
        private void CreateEmbeddedTexturesFile(int modelID, bool interior) {
            string readingPath = folder + rom.GetBuildingModelsDirPath(interior) + "\\" + modelID.ToString("D4");

            byte[] txFile = File.ReadAllBytes(readingPath);
            byte[] texData = NSBUtils.GetTexturesFromTexturedNSBMD(txFile);

            if (texData.Length <= 4) {
                AppLogger.Error("No textures found");
                return;
            }
            DSUtils.WriteToFile(Path.GetTempPath() + temp_btxname, texData, fmode: FileMode.Create);
        }
        private void FillListBox(bool interior) {
            int modelCount = Directory.GetFiles(folder + rom.GetBuildingModelsDirPath(interior)).Length;
            for (int currentIndex = 0; currentIndex < modelCount; currentIndex++) {
                string filePath = folder + rom.GetBuildingModelsDirPath(interior) + "\\" + currentIndex.ToString("D4");

                using (DSUtils.EasyReader reader = new DSUtils.EasyReader(filePath, 0x14)) {
                    string nsbmdName = NSBUtils.ReadNSBMDname(reader);
                    buildingEditorBldListBox.Items.Add("[" + currentIndex.ToString("D3") + "] " + nsbmdName);
                }
            }
        }
        private void FillTexturesBox() {
            int texturesCount = Directory.GetFiles(folder + RomInfo.gameDirs[DirNames.buildingTextures].unpackedDir).Length;
            textureComboBox.Items.Add("Embedded textures");

            for (int i = 0; i < texturesCount; i++) {
                textureComboBox.Items.Add("Texture " + i);
            }
        }
        private void LoadModelTextures(int fileID) {
            string path;
            if (fileID > -1) {
                path = folder + RomInfo.gameDirs[RomInfo.DirNames.buildingTextures].unpackedDir + "\\" + fileID.ToString("D4");
            } else {
                path = Path.GetTempPath() + temp_btxname; // Load Embedded textures if the argument passed to this function is -1
            }

            try {
                currentNSBMD.materials = NSBTXLoader.LoadNsbtx(new MemoryStream(File.ReadAllBytes(path)), out currentNSBMD.Textures, out currentNSBMD.Palettes);
                currentNSBMD.MatchTextures();
            } catch { }
        }
        private void RenderModel() {
            MKDS_Course_Editor.NSBTA.NSBTA.NSBTA_File bta = new MKDS_Course_Editor.NSBTA.NSBTA.NSBTA_File();
            MKDS_Course_Editor.NSBTP.NSBTP.NSBTP_File btp = new MKDS_Course_Editor.NSBTP.NSBTP.NSBTP_File();
            MKDS_Course_Editor.NSBCA.NSBCA.NSBCA_File bca = new MKDS_Course_Editor.NSBCA.NSBCA.NSBCA_File();
            int[] aniframeS = new int[0];

            buildingOpenGLControl.Invalidate(); // Invalidate drawing surface
            // SetupRenderer and Gl calls stubbed — awaiting OpenTK 4 rewrite (migration step 33)
            renderer.Model = currentNSBMD.models[0];
            // Gl.glScalef stubbed
            renderer.RenderModel("", bta, aniframeS, aniframeS, aniframeS, aniframeS, aniframeS, bca, false, -1, 0.0f, 0.0f, dist, elev, ang, true, btp, currentNSBMD);
        }
        private void SetupRenderer(float ang, float dist, float elev, float perspective) {
            // TODO (Avalonia migration - step 33): Implement with OpenTK 4.x.
        }
        #endregion

        private void buildingOpenGLControl_MouseWheel(object sender, MouseEventArgs e) { // Zoom In/Out
            float val = (float)e.Delta / 200;
            
            if ((Control.ModifierKeys & Keys.Shift) == Keys.Shift) {
                dist += val;
            } else {
                dist -= val;
            }

            RenderModel();
        }
        private void buildingEditorListBox_SelectedIndexChanged(object sender, EventArgs e) {
            if (Helpers.HandlersDisabled || buildingEditorBldListBox.SelectedIndex < 0) {
                return;
            }

            string path = folder + rom.GetBuildingModelsDirPath(interiorCheckBox.Checked) + "\\" + buildingEditorBldListBox.SelectedIndex.ToString("D4");

            currentModelData = File.ReadAllBytes(path);
            currentNSBMD = NSBMDLoader.LoadNSBMD(new MemoryStream(currentModelData));

            CreateEmbeddedTexturesFile(buildingEditorBldListBox.SelectedIndex, interiorCheckBox.Checked);
            LoadModelTextures(textureComboBox.SelectedIndex - 1);
            RenderModel();
        }
        private void exportButton_Click(object sender, EventArgs e) {
            SaveFileDialog em = new SaveFileDialog {
                Filter = "NSBMD model (*.nsbmd)|*.nsbmd",
                FileName = buildingEditorBldListBox.SelectedItem.ToString()
            };
            if (em.ShowDialog(this) != DialogResult.OK) {
                return;
            }

            File.Copy(folder + rom.GetBuildingModelsDirPath(interiorCheckBox.Checked) + "\\" + buildingEditorBldListBox.SelectedIndex.ToString("D4"), em.FileName, true);
        }
        private void importButton_Click(object sender, EventArgs e) {
            OpenFileDialog im = new OpenFileDialog {
                Filter = "NSBMD model (*.nsbmd)|*.nsbmd"
            };
            if (im.ShowDialog(this) != DialogResult.OK) {
                return;
            }

            using (DSUtils.EasyReader reader = new DSUtils.EasyReader(im.FileName)) {
                if (reader.ReadUInt32() != NSBMD.NDS_TYPE_BMD0) {
                    MessageBox.Show("Please select an NSBMD file.", "Invalid File", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                } else {
                    int currentIndex = buildingEditorBldListBox.SelectedIndex;

                    File.Copy(im.FileName, folder + rom.GetBuildingModelsDirPath(interiorCheckBox.Checked) + "\\" + currentIndex.ToString("D4"), true);
                    buildingEditorBldListBox.Items[currentIndex] = "[" + currentIndex.ToString("D3") + "] " + NSBUtils.ReadNSBMDname(reader, 0x14);
                    buildingEditorListBox_SelectedIndexChanged(null, null);
                }
            }
        }
        private void interiorCheckBox_CheckedChanged(object sender, EventArgs e) {
            Helpers.DisableHandlers();

            buildingEditorBldListBox.Items.Clear();
            FillListBox(interiorCheckBox.Checked);

            Helpers.EnableHandlers();

            buildingEditorBldListBox.SelectedIndex = 0;
        }
        private void textureComboBox_SelectedIndexChanged(object sender, EventArgs e) {
            if (Helpers.HandlersDisabled) {
                return;
            }
            LoadModelTextures(textureComboBox.SelectedIndex - 1);
            RenderModel();
        }
        private void buildingOpenGLControl_PreviewKeyDown(object sender, PreviewKeyDownEventArgs e) {
            byte multiplier = 2;
            if (e.Modifiers == Keys.Shift) {
                multiplier = 1;
            } else if (e.Modifiers == Keys.Control) {
                multiplier = 4;
            }

            switch (e.KeyCode) {
                case Keys.Right:
                    rRot = true;
                    lRot = false;
                    break;
                case Keys.Left:
                    rRot = false;
                    lRot = true;
                    break;
                case Keys.Up:
                    dRot = false;
                    uRot = true;
                    break;
                case Keys.Down:
                    dRot = true;
                    uRot = false;
                    break;
            }

            if (rRot ^ lRot) {
                if (rRot) {
                    ang += 1 * multiplier;
                } else if (lRot) {
                    ang -= 1 * multiplier;
                }
            }

            if (uRot ^ dRot) {
                if (uRot) {
                    elev -= 1 * multiplier;
                } else if (dRot) {
                    elev += 1 * multiplier;
                }
            }
            RenderModel();
        }

        private void bldExportDAEbutton_Click(object sender, EventArgs e) {
            ModelUtils.ModelToDAE(
                modelName: buildingEditorBldListBox.SelectedItem.ToString().TrimEnd('\0'), 
                modelData: currentModelData, 
                textureData: textureComboBox.SelectedIndex < 1 ? null : File.ReadAllBytes(RomInfo.gameDirs[DirNames.buildingTextures].unpackedDir + "\\" + (textureComboBox.SelectedIndex - 1).ToString("D4"))
            );
        }

        private void buildingOpenGLControl_KeyUp(object sender, KeyEventArgs e) {
            switch (e.KeyCode) {
                case Keys.Right:
                    rRot = false;
                    break;
                case Keys.Left:
                    lRot = false;
                    break;
                case Keys.Up:
                    uRot = false;
                    break;
                case Keys.Down:
                    dRot = false;
                    break;
            }
        }
    }
}
