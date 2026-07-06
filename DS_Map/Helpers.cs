using DSPRE.Resources;
using DSPRE.ROMFiles;
using Ekona.Images;
using Images;
using LibGit2Sharp;
using LibNDSFormats.NSBMD;
using LibNDSFormats.NSBTX;
using Microsoft.WindowsAPICodePack.Dialogs;
using ScintillaNET;
using ScintillaNET.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Windows.Forms;
using Velopack;
using Velopack.Sources;
using static DSPRE.RomInfo;

namespace DSPRE
{
    public static class Helpers
    {
        static MainProgram MainProgram;

        public static RomInfo romInfo;
        public static bool hideBuildings = new bool();

        public static NSBMDGlRenderer mapRenderer;

        public static ToolStripProgressBar toolStripProgressBar { get { return MainProgram.toolStripProgressBar; } }

        public static void Initialize(MainProgram mainProgram)
        {
            MainProgram = mainProgram;
            mapRenderer = new NSBMDGlRenderer();
        }

        public static void CheckForUpdates(bool silent = true)
        {
            AppLogger.Info("Checking for updates...");

            try
            {
                var mgr = new UpdateManager(new GithubSource("https://github.com/DS-Pokemon-Rom-Editor/DSPRE", "", prerelease: false));
                var newVersion = mgr.CheckForUpdates();

                if (newVersion == null)
                {
                    AppLogger.Info("No updates available.");
                    if (!silent)
                        MessageBox.Show("No update is available.", "Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                // Get current version (e.g., "1.14.2.0")
                string currentVersion = GetDSPREVersion();

                // Convert Velopack version back to .NET version for display
                string velopackVersion = newVersion.TargetFullRelease.Version.ToString();
                string displayVersion = ConvertVelopackToDotNetVersion(velopackVersion);

                // Determine update type
                string updateType = GetUpdateType(currentVersion, displayVersion);

                // Fetch changelog for this release
                string changelog = FetchChangelogForTag($"v{displayVersion}");

                // Build update message
                string updateMessage = $"A new DSPRE version is available!\n\n" +
                                      $"Current: {currentVersion}\n" +
                                      $"Available: {displayVersion}\n" +
                                      $"Update Type: {updateType}\n\n";

                if (!string.IsNullOrEmpty(changelog))
                {
                    updateMessage += $"Changelog:\n{changelog}\n\n";
                }

                updateMessage += "Do you want to install it now?";

                DialogResult update = MessageBox.Show(updateMessage,
                    "New Update Available",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Information);

                if (update == DialogResult.Yes)
                {
                    AppLogger.Info($"New version available: {displayVersion} (Velopack: {velopackVersion}, Current: {currentVersion})");
                    mgr.DownloadUpdates(newVersion);

                    AppLogger.Info($"Installing update {displayVersion} and restarting app...");
                    mgr.ApplyUpdatesAndRestart(newVersion);
                }
                else
                {
                    AppLogger.Info("User declined to update the application.");
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error($"Error checking for updates: {ex.Message}");
                if (!silent)
                {
                    MessageBox.Show($"Error checking for updates: {ex.Message}",
                        "Error",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                }
            }
        }

        /// <summary>
        /// Convert Velopack version back to .NET AssemblyVersion format
        /// </summary>
        private static string ConvertVelopackToDotNetVersion(string velopackVersion)
        {
            try
            {
                // Handle prerelease format (1.14.3-rev1)
                if (velopackVersion.Contains("-rev"))
                {
                    var parts = velopackVersion.Split('-');
                    string versionPart = parts[0]; // "1.14.3"
                    string revisionPart = parts[1]; // "rev1"

                    // Extract revision number
                    int revision = int.Parse(revisionPart.Replace("rev", ""));

                    // Parse version
                    var versionParts = versionPart.Split('.');
                    int major = int.Parse(versionParts[0]);
                    int minor = int.Parse(versionParts[1]);
                    int patch = int.Parse(versionParts[2]);

                    // Convert back: patch was incremented for Velopack, so decrement it
                    int originalPatch = patch - 1;

                    return $"{major}.{minor}.{originalPatch}.{revision}";
                }
                else
                {
                    // Regular version (1.14.3) add .0 revision
                    var versionParts = velopackVersion.Split('.');
                    if (versionParts.Length == 3)
                    {
                        return $"{velopackVersion}.0";
                    }
                    return velopackVersion;
                }
            }
            catch (Exception ex)
            {
                AppLogger.Warn($"Failed to convert Velopack version '{velopackVersion}': {ex.Message}");
                return velopackVersion; // Return as-is if conversion fails
            }
        }

        /// <summary>
        /// Determine the type of update (Major, Minor, Build, or Revision)
        /// </summary>
        private static string GetUpdateType(string currentVersion, string newVersion)
        {
            try
            {
                var current = ParseVersion(currentVersion);
                var update = ParseVersion(newVersion);

                if (current == null || update == null)
                    return "Update";

                if (update.Major > current.Major)
                    return "Major Update";
                else if (update.Minor > current.Minor)
                    return "Minor Update";
                else if (update.Build > current.Build)
                    return "Build Update";
                else if (update.Revision > current.Revision)
                    return "Revision Update";
                else
                    return "Update";
            }
            catch
            {
                return "Update";
            }
        }

        /// <summary>
        /// Parse version string into components
        /// </summary>
        private static VersionParts ParseVersion(string version)
        {
            try
            {
                var parts = version.Split('.');
                return new VersionParts
                {
                    Major = parts.Length > 0 ? int.Parse(parts[0]) : 0,
                    Minor = parts.Length > 1 ? int.Parse(parts[1]) : 0,
                    Build = parts.Length > 2 ? int.Parse(parts[2]) : 0,
                    Revision = parts.Length > 3 ? int.Parse(parts[3]) : 0
                };
            }
            catch
            {
                return null;
            }
        }

        private class VersionParts
        {
            public int Major { get; set; }
            public int Minor { get; set; }
            public int Build { get; set; }
            public int Revision { get; set; }
        }

        /// <summary>
        /// Fetch changelog from GitHub for a specific tag
        /// </summary>
        private static string FetchChangelogForTag(string tag)
        {
            try
            {
                const string owner = "DS-Pokemon-Rom-Editor";
                const string repo = "DSPRE";

                using (HttpClient client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Add("User-Agent", "DSPRE");
                    client.DefaultRequestHeaders.Add("Accept", "application/vnd.github.v3+json");

                    string url = $"https://api.github.com/repos/{owner}/{repo}/releases/tags/{tag}";

                    var response = client.GetAsync(url).Result;

                    if (response.IsSuccessStatusCode)
                    {
                        string json = response.Content.ReadAsStringAsync().Result;

                        int bodyStart = json.IndexOf("\"body\":\"") + 8;
                        if (bodyStart > 8)
                        {
                            int bodyEnd = json.IndexOf("\",\"", bodyStart);
                            if (bodyEnd > bodyStart)
                            {
                                string body = json.Substring(bodyStart, bodyEnd - bodyStart);
                                // Unescape JSON
                                return System.Text.RegularExpressions.Regex.Unescape(body);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                AppLogger.Warn($"Failed to fetch changelog: {ex.Message}");
            }

            return "Changelog not available.";
        }

        // Moved to the core ScriptDatabaseSetup class; kept as forwarders for existing UI call sites.
        public static void CheckForDatabaseUpdates(bool silent = true) => ScriptDatabaseSetup.CheckForDatabaseUpdates(silent);

        public static void InitializeScriptDatabase(string romFileName, GameFamilies gameFamily, GameVersions gameVersion)
            => ScriptDatabaseSetup.InitializeScriptDatabase(romFileName, gameFamily, gameVersion);

        static bool disableHandlersOld;
        static bool disableHandlers;

        public static bool HandlersDisabled { get { return disableHandlers == true; } }
        public static bool HandlersEnabled { get { return disableHandlers == false; } }

        public static void BackUpDisableHandler()
        {
            disableHandlersOld = disableHandlers;
        }

        public static void RestoreDisableHandler()
        {
            disableHandlers = disableHandlersOld;
        }

        public static void DisableHandlers()
        {
            disableHandlers = true;
        }

        public static void EnableHandlers()
        {
            disableHandlers = false;
        }

        public static string GetDSPREVersion() => AppInfo.GetDSPREVersion();

        public static void statusLabelMessage(string msg = "Ready")
        {
            ToolStripStatusLabel statusLabel = MainProgram.statusLabel;
            statusLabel.Text = msg;
            statusLabel.Font = new Font(statusLabel.Font, FontStyle.Regular);
            statusLabel.ForeColor = Color.Black;
            statusLabel.Invalidate();
        }

        public static void statusLabelError(string errorMsg, bool severe = true)
        {
            ToolStripStatusLabel statusLabel = MainProgram.statusLabel;
            statusLabel.Text = errorMsg;
            statusLabel.Font = new Font(statusLabel.Font, FontStyle.Bold);
            statusLabel.ForeColor = severe ? Color.Red : Color.DarkOrange;
            statusLabel.Invalidate();
        }

        // Moved to the core SystemShell class (cross-platform); forwarders for existing call sites.
        public static void ExplorerSelect(string path) => SystemShell.RevealInFileManager(path);
        public static void OpenFileWithDefaultApp(string path) => SystemShell.OpenWithDefaultApp(path);

        // Moved to the core TrainerNames class; forwarder for existing call sites.
        public static string[] GetTrainerNames() => TrainerNames.GetAll();

        public static void MW_LoadModelTextures(NSBMD model, string textureFolder, int fileID)
        {
            if (fileID < 0)
            {
                return;
            }

            string texturePath = Filesystem.GetPath(textureFolder, fileID);
            
            try
            {
                model.materials = NSBTXLoader.LoadNsbtx(new MemoryStream(System.IO.File.ReadAllBytes(texturePath)), out model.Textures, out model.Palettes);
                model.MatchTextures();
            }
            catch (Exception ex)
            {
                AppLogger.Error($"Failed to load model textures: {ex.Message}");
            }
        }

        public static void MW_LoadModelTextures(MapFile mapFile, int fileID)
        {
            MW_LoadModelTextures(mapFile.mapModel, Filesystem.mapTextures, fileID);
        }

        public static void MW_LoadModelTextures(Building building, int fileID)
        {
            MW_LoadModelTextures(building.NSBMDFile, Filesystem.buildingTextures, fileID);
        }

        public static void SetupRenderer(float ang, float dist, float elev, float perspective, int width, int height)
        {
            // TODO (Avalonia migration - step 33): Implement with OpenTK 4.x.
        }

        public static void RenderMap(ref NSBMDGlRenderer mapRenderer, ref NSBMDGlRenderer buildingsRenderer, ref MapFile mapFile, float ang, float dist, float elev, float perspective, int width, int height, bool mapTexturesON = true, bool buildingTexturesON = true)
        {
            #region Useless variables that the rendering API still needs
            MKDS_Course_Editor.NSBTA.NSBTA.NSBTA_File ani = new MKDS_Course_Editor.NSBTA.NSBTA.NSBTA_File();
            MKDS_Course_Editor.NSBTP.NSBTP.NSBTP_File tp = new MKDS_Course_Editor.NSBTP.NSBTP.NSBTP_File();
            MKDS_Course_Editor.NSBCA.NSBCA.NSBCA_File ca = new MKDS_Course_Editor.NSBCA.NSBCA.NSBCA_File();
            int[] aniframeS = new int[0];
            #endregion

            /* Invalidate drawing surfaces */
            EditorPanels.mapEditor.mapOpenGlControl.Invalidate();
            EditorPanels.eventEditor.eventOpenGlControl.Invalidate();

            // TODO (Avalonia migration - step 33): Restore SetupRenderer + Gl calls with OpenTK 4.x.
            mapRenderer.Model = mapFile.mapModel.models[0];
            mapRenderer.RenderModel("", ani, aniframeS, aniframeS, aniframeS, aniframeS, aniframeS, ca, false, -1, 0.0f, 0.0f, dist, elev, ang, true, tp, mapFile.mapModel); // Render map model

            if (!hideBuildings)
            {
                for (int i = 0; i < mapFile.buildings.Count; i++)
                {
                    NSBMD file = mapFile.buildings[i].NSBMDFile;
                    if (file is null)
                    {
                        AppLogger.Warn("Null building can't be rendered");
                    }
                    else
                    {
                        buildingsRenderer.Model = file.models[0];
                        ScaleTranslateRotateBuilding(mapFile.buildings[i]);
                        buildingsRenderer.RenderModel("", ani, aniframeS, aniframeS, aniframeS, aniframeS, aniframeS, ca, false, -1, 0.0f, 0.0f, dist, elev, ang, true, tp, file);
                    }
                }
            }
        }

        public static Bitmap GrabMapScreenshot(int width, int height)
        {
            // TODO (Avalonia migration - step 33): Read pixels from OpenTK 4.x framebuffer.
            return new Bitmap(width, height);
        }

        private static void ScaleTranslateRotateBuilding(Building building)
        {
            // TODO (Avalonia migration - step 33): Apply matrix transforms via OpenTK 4.x.
        }

        public static Image GetPokePic(int species, int w, int h, PaletteBase paletteBase, ImageBase imageBase, SpriteBase spriteBase)
        {
            bool fiveDigits = false; // some extreme future proofing
            try
            {
                string path = Filesystem.GetMonIconPath(0);
                paletteBase = new NCLR(path, 0, Path.GetFileName(path));
            }
            catch (FileNotFoundException)
            {
                string path = Filesystem.GetMonIconPath(0, "D5");
                paletteBase = new NCLR(path, 0, Path.GetFileName(path));
                fiveDigits = true;
            }

            // read arm9 table to grab pal ID
            int paletteId = 0;
            byte[] iconPalTableBuf;

            switch (RomInfo.gameFamily)
            {
                case RomInfo.GameFamilies.DP:
                    iconPalTableBuf = ARM9.ReadBytes(0x6B838, 4);
                    break;
                case RomInfo.GameFamilies.Plat:
                    iconPalTableBuf = ARM9.ReadBytes(0x79F80, 4);
                    break;
                case RomInfo.GameFamilies.HGSS:
                default:
                    iconPalTableBuf = ARM9.ReadBytes(0x74408, 4);
                    break;
            }

            int iconPalTableAddress = (iconPalTableBuf[3] & 0xFF) << 24 | (iconPalTableBuf[2] & 0xFF) << 16 | (iconPalTableBuf[1] & 0xFF) << 8 | (iconPalTableBuf[0] & 0xFF) /* << 0 */;
            string iconTablePath;

            int iconPalTableOffsetFromFileStart;
            if (iconPalTableAddress >= RomInfo.synthOverlayLoadAddress)
            {
                // if the pointer shows the table was moved to the synthetic overlay
                iconPalTableOffsetFromFileStart = iconPalTableAddress - (int)RomInfo.synthOverlayLoadAddress;
                iconTablePath = Filesystem.expArmPath;
            }
            else
            {
                iconPalTableOffsetFromFileStart = iconPalTableAddress - 0x02000000;
                iconTablePath = RomInfo.arm9Path;
            }

            using (DSUtils.EasyReader idReader = new DSUtils.EasyReader(iconTablePath, iconPalTableOffsetFromFileStart + species))
            {
                paletteId = idReader.ReadByte();
            }

            if (paletteId != 0)
            {
                paletteBase.Palette[0] = paletteBase.Palette[paletteId]; // update pal 0 to be the new pal
            }

            // grab tiles
            int spriteFileID = species + 7;
            if (fiveDigits)
            {
                string path = Filesystem.GetMonIconPath(spriteFileID, "D5");
                imageBase = new NCGR(path, spriteFileID, Path.GetFileName(path));
            }
            else
            {
                string path = Filesystem.GetMonIconPath(spriteFileID);
                imageBase = new NCGR(path, spriteFileID, Path.GetFileName(path));
            }

            // grab sprite
            const int ncerFileId = 2;
            if (fiveDigits)
            {
                string path = Filesystem.GetMonIconPath(ncerFileId, "D5");
                spriteBase = new NCER(path, ncerFileId, Path.GetFileName(path));
            }
            else
            {
                string path = Filesystem.GetMonIconPath(ncerFileId);
                spriteBase = new NCER(path, ncerFileId, Path.GetFileName(path));
            }

            // copy this from the trainer
            int bank0OAMcount = spriteBase.Banks[0].oams.Length;
            int[] OAMenabled = new int[bank0OAMcount];
            for (int i = 0; i < OAMenabled.Length; i++)
            {
                OAMenabled[i] = i;
            }

            // finally compose image
            try
            {
                return spriteBase.Get_Image(imageBase, paletteBase, 0, w, h, false, false, false, true, true, -1, OAMenabled);
            }
            catch (FormatException)
            {
                return Properties.Resources.IconPokeball;
            }
            // default:
            //partyPokemonPictureBoxList[partyPos].Image = cb.SelectedIndex > 0 ? (Image)Properties.PokePics.ResourceManager.GetObject(FixPokenameString(PokeDatabase.System.pokeNames[(ushort)cb.SelectedIndex])) : global::DSPRE.Properties.Resources.IconPokeball;
        }

        public static void GenerateKeystrokes(string keys, Scintilla textArea)
        {
            //Example
            //GenerateKeystrokes("+{TAB}");
            HotKeyManager.Enable = false;
            textArea.Focus();
            SendKeys.Send(keys);
            HotKeyManager.Enable = true;
        }

        public static void PictureBoxDisable(object sender, PaintEventArgs e)
        {
            if (sender is PictureBox pict && pict.Image != null && (!pict.Enabled))
            {
                using (Bitmap img = new Bitmap(pict.Image, pict.ClientSize))
                {
                    ControlPaint.DrawImageDisabled(e.Graphics, img, 0, 0, pict.BackColor);
                }
            }
        }

        /// <summary>Classic Levenshtein edit distance between two strings (fuzzy search / matching).</summary>
        // Moved to CoreExtensions (core, cross-platform); forwarder kept for existing UI call sites.
        public static int Levenshtein(string s1, string s2) => CoreExtensions.Levenshtein(s1, s2);

        // Moved to the core HeaderLists class; forwarders for existing call sites.
        public static List<string> getHeaderListBoxNames() => HeaderLists.GetHeaderListBoxNames();
        public static List<string> getInternalNames() => HeaderLists.GetInternalNames();

        public static int CalculateTimeDifferenceInSeconds(int startHour, int startMinute, int startSecond, int endHour, int endMinute, int endSecond)
        {
            // Convert start time and end time to seconds since midnight
            int startTimeInSeconds = (startHour * 3600) + (startMinute * 60) + startSecond;
            int endTimeInSeconds = (endHour * 3600) + (endMinute * 60) + endSecond;

            // Calculate difference
            int timeDifference = endTimeInSeconds - startTimeInSeconds;

            // If time difference is negative (end time is past midnight), adjust
            if (timeDifference < 0)
            {
                timeDifference += 24 * 3600; // Add 24 hours in seconds
            }

            return timeDifference;
        }

        public static String formatTime(int time)
        {
            string stringTime = time.ToString();
            if (time < 10)
            {
                stringTime = "0" + stringTime;
            }

            return stringTime;
        }

        [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Auto)]
        extern static bool DestroyIcon(IntPtr handle);


        public static void PopOutEditorHandler<T>(T control, string title, Image icon, Action<T> onClose = null)
               where T : Control
        {
            if (control == null) return;

            if (EditorPanels.PopoutRegistry.TryGetHost(control, out var existingHost))
            {
                if (existingHost.WindowState == FormWindowState.Minimized) existingHost.WindowState = FormWindowState.Normal;
                existingHost.Activate();
                return;
            }

            var originalParent = control.Parent;
            var originalIndex = originalParent?.Controls.IndexOf(control) ?? -1;
            var originalDock = control.Dock;

            originalParent?.Controls.Remove(control);

            Icon managedIcon = null;
            if (icon != null)
            {
                using (var bmp = new Bitmap(icon))
                {
                    IntPtr hIcon = bmp.GetHicon();
                    try
                    {
                        using (var tmp = Icon.FromHandle(hIcon))
                        {
                            managedIcon = (Icon)tmp.Clone();
                        }
                    }
                    finally
                    {
                        DestroyIcon(hIcon);
                    }
                }
            }

            var form = new Form
            {
                Text = title,
                StartPosition = FormStartPosition.CenterScreen,
                FormBorderStyle = FormBorderStyle.FixedSingle,
                MaximizeBox = false,
                ClientSize = control.Size,
                ShowIcon = managedIcon != null,
                Icon = managedIcon
            };


            control.Dock = DockStyle.Fill;
            form.Controls.Add(control);

            EditorPanels.PopoutRegistry.Add(control, form);

            form.FormClosing += (s, e) =>
            {

                form.Controls.Remove(control);

                if (originalParent != null && !originalParent.IsDisposed)
                {
                    originalParent.Controls.Add(control);
                    if (originalIndex >= 0 && originalIndex < originalParent.Controls.Count)
                        originalParent.Controls.SetChildIndex(control, originalIndex);

                    control.Dock = originalDock;
                }

                managedIcon?.Dispose();

                onClose?.Invoke(control);
            };

            form.Show();
        }

        public static void PopOutEditor(Control control, string editorName, Label label, Button button, Image icon)
        {
            if (control == null)
            {
                MessageBox.Show("The editor control is not initialized.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            label.Visible = true; // Show Editor popped-out label
            button.Enabled = false; // Disable popout button

            Helpers.PopOutEditorHandler(control, editorName, icon, onClose =>
            {
                label.Visible = false; // Hide Editor popped-out label
                button.Enabled = true; // Enable popout button
            });
        }

        public static void ExclusiveCBInvert(CheckBox cb)
        {
            if (Helpers.HandlersDisabled)
            {
                return;
            }

            Helpers.DisableHandlers();

            if (cb.Checked)
            {
                cb.Checked = !cb.Checked;
            }

            Helpers.EnableHandlers();
        }

        public static void ContentBasedBatchRename(MainProgram parent, DirectoryInfo d = null)
        {
            (DirectoryInfo d, FileInfo[] files) dirData = OpenNonEmptyDir(d, title: "Content-Based Batch Rename Tool");
            d = dirData.d;
            FileInfo[] files = dirData.files;

            if (d == null || files == null)
            {
                return;
            }

            DialogResult dr = MessageBox.Show("About to rename " + files.Length + " file" + (files.Length > 1 ? "s" : "") +
                " from the input folder (taken in ascending order), according to their content.\n" +
                "If a destination file already exists, DSPRE will append a number to its name.\n\n" +
                "Do you want to proceed?", "Confirm operation", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

            if (dr.Equals(DialogResult.Yes))
            {
                List<string> enumerationFile = new List<string> {
                    "#============================================================================",
                    "# File enumeration definition for folder " + "\"" + d.Name + "\"",
                    "#============================================================================"
                };
                int initialLength = enumerationFile.Count;

                const byte toRead = 16;
                foreach (FileInfo f in files)
                {

                    string fileNameOnly = Path.GetFileNameWithoutExtension(f.FullName);
                    string dirNameOnly = Path.GetDirectoryName(f.FullName);

                    string destName = "";
                    byte[] b = DSUtils.ReadFromFile(f.FullName, 0, toRead);

                    if (b == null || b.Length < toRead)
                    {
                        continue;
                    }

                    string magic = "";

                    if (b[0] == 'B' && b[3] == '0')
                    { //B**0
                        ushort nameOffset;

                        destName = dirNameOnly + "\\"; //Full filename can be changed
                        nameOffset = (ushort)(52 + (4 * (BitConverter.ToUInt16(b, 0xE) - 1)));

                        if (b[1] == 'T' && b[2] == 'X')
                        { //BTX0
#if false
                            nameOffset += 0xEC;
#else
                            destName = fileNameOnly;
#endif
                        }

                        string nameRead = Encoding.UTF8.GetString(DSUtils.ReadFromFile(f.FullName, nameOffset, 16)).TrimEnd(new char[] { (char)0 });

                        if (nameRead.Length <= 0 || nameRead.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
                        {
                            destName = fileNameOnly; //Filename can't be changed, only extension
                        }
                        else
                        {
                            destName += nameRead;
                        }

                        destName += ".ns";
                        for (int i = 0; i < 3; i++)
                        {
                            magic += Char.ToLower((char)b[i]);
                        }
                    }
                    else
                    {
                        destName = fileNameOnly + ".";
                        byte offset = 0;

                        if (b[5] == 'R' && b[8] == 'N')
                        {
                            offset = 5;
                        }

                        for (int i = 0; i < 4; i++)
                        {
                            magic += Char.ToLower((char)b[offset + i]);
                        }
                    }

                    if (string.IsNullOrWhiteSpace(magic) || !magic.All(char.IsLetterOrDigit))
                    {
                        continue;
                    }

                    destName += magic;

                    if (string.IsNullOrWhiteSpace(destName))
                    {
                        continue;
                    }

                    destName = MakeUniqueName(destName, fileNameOnly = null, dirNameOnly);
                    System.IO.File.Move(f.FullName, Path.Combine(Path.GetDirectoryName(f.FullName), Path.GetFileName(destName)));

                    enumerationFile.Add(Path.GetFileName(destName));
                }

                if (enumerationFile.Count > initialLength)
                {
                    MessageBox.Show("Files inside folder \"" + d.FullName + "\" have been renamed according to their contents.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);

                    DialogResult response = MessageBox.Show("Do you want to save a file enumeration list?", "Waiting for user", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                    if (response.Equals(DialogResult.Yes))
                    {
                        MessageBox.Show("Choose where to save the output list file.", "Name your list file", MessageBoxButtons.OK, MessageBoxIcon.Information);

                        SaveFileDialog sf = new SaveFileDialog
                        {
                            Filter = "List File (*.txt; *.list)|*.txt;*.list",
                            FileName = d.Name + ".list"
                        };
                        if (sf.ShowDialog(parent) != DialogResult.OK)
                        {
                            return;
                        }

                        System.IO.File.WriteAllLines(sf.FileName, enumerationFile);
                        MessageBox.Show("List file saved.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
                else
                {
                    MessageBox.Show("No file content could be recognized.", "Operation terminated", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }

        public static (DirectoryInfo, FileInfo[]) OpenNonEmptyDir(DirectoryInfo d = null, string title = "Waiting for user")
        {
            /*==================================================================*/
            if (d == null)
            {
                MessageBox.Show("Choose a source folder.", title, MessageBoxButtons.OK, MessageBoxIcon.Information);
                CommonOpenFileDialog sourceDirDialog = new CommonOpenFileDialog
                {
                    IsFolderPicker = true,
                    Multiselect = false
                };

                if (sourceDirDialog.ShowDialog() != CommonFileDialogResult.Ok)
                {
                    return (null, null);
                }

                d = new DirectoryInfo(sourceDirDialog.FileName);
            }

            FileInfo[] tempfiles = d.GetFiles();
            FileInfo[] files = tempfiles.OrderBy(n => System.Text.RegularExpressions.Regex.Replace(n.Name, @"\d+", e => e.Value.PadLeft(tempfiles.Length.ToString().Length, '0'))).ToArray();

            if (files.Length <= 0)
            {
                MessageBox.Show("Folder " + "\"" + d.FullName + "\"" + " is empty.\nCan't proceed.", "Invalid folder", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return (null, null);
            }
            ;

            return (d, files);
        }

        public static string MakeUniqueName(string fileName, string fileNameOnly = null, string dirNameOnly = null, string extension = null)
        {
            if (fileNameOnly == null)
            {
                fileNameOnly = Path.GetFileNameWithoutExtension(fileName);
            }
            if (dirNameOnly == null)
            {
                dirNameOnly = Path.GetDirectoryName(fileName);
            }
            if (extension == null)
            {
                extension = Path.GetExtension(fileName);
            }

            int append = 1;

            while (System.IO.File.Exists(Path.Combine(dirNameOnly, fileName)))
            {
                string tmp = fileNameOnly + "(" + (append++) + ")";
                fileName = Path.Combine(dirNameOnly, tmp + extension);
            }
            return fileName;
        }

        // Moved to the core TrainerUsageReport class; forwarder for existing call sites.
        public static void ExportTrainerUsageToCSV(Dictionary<string, Dictionary<string, int>> trainerUsage, string csvFilePath)
            => TrainerUsageReport.WriteCsv(trainerUsage, csvFilePath);

    }
}
