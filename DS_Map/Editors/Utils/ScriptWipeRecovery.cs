using DSPRE.ROMFiles;
using NarcAPI;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace DSPRE.Editors.Utils
{
    /// <summary>
    /// Finds and restores script files that DSPRE 2.3.4.3's automatic script repair emptied by mistake.
    /// That repair rebuilt files from an empty .script copy, which always saves as exactly 0x13 0xFD,
    /// a byte pattern no retail DP/Pt/HGSS script file has.
    /// </summary>
    public static class ScriptWipeRecovery
    {
        private const string DismissedMarkerName = ".script_wipe_recovery_dismissed";

        // Vanilla scripts without the 0xFD13 marker: old DSPRE showed them empty, so nobody could have edited them
        // in DSPRE and a clean ROM of the same game restores them exactly.
        private static readonly Dictionary<RomInfo.GameFamilies, int[]> MarkerlessVanillaScripts = new Dictionary<RomInfo.GameFamilies, int[]>
        {
            { RomInfo.GameFamilies.Plat, new[] { 101, 164, 365 } },
            { RomInfo.GameFamilies.DP, new[] { 96, 158, 356 } },
        };

        public class Candidate
        {
            public int FileID;
            public byte[] Replacement;
            public string Source;
        }

        public static bool IsWiped(byte[] data) => data != null && data.Length == 2 && data[0] == 0x13 && data[1] == 0xFD;

        public static bool IsMarkerlessVanillaScript(int fileID) =>
            MarkerlessVanillaScripts.TryGetValue(RomInfo.gameFamily, out int[] ids) && ids.Contains(fileID);

        private static string ScriptsNarcPath => RomInfo.gameDirs[RomInfo.DirNames.scripts].packedDir;
        private static string ScriptsUnpackedDir => RomInfo.gameDirs[RomInfo.DirNames.scripts].unpackedDir;
        private static string DismissedMarkerPath => Path.Combine(RomInfo.workDir, DismissedMarkerName);

        public static void CheckOnProjectOpen(IWin32Window owner)
        {
            try
            {
                if (RomInfo.hasRotomProject) { return; }

                List<int> wiped = FindWipedFiles();
                if (wiped.Count == 0) { return; }

                AppLogger.Warn($"Script files emptied by the 2.3.4.3 script repair bug: {string.Join(", ", wiped)}");

                if (File.Exists(DismissedMarkerPath) && File.ReadAllText(DismissedMarkerPath).Trim() == string.Join(",", wiped))
                {
                    return;
                }

                ShowRecovery(owner, wiped);
            }
            catch (Exception ex)
            {
                AppLogger.Error($"Script wipe recovery check failed: {ex}");
            }
        }

        public static List<int> FindWipedFiles()
        {
            Dictionary<int, byte[]> current = ReadCurrentScripts();
            return current.Where(kv => IsWiped(kv.Value)).Select(kv => kv.Key).OrderBy(i => i).ToList();
        }

        // The unpacked folder is what DSPRE edits and saves; without it the packed NARC is the project's only copy.
        private static Dictionary<int, byte[]> ReadCurrentScripts()
        {
            if (Directory.Exists(ScriptsUnpackedDir) && Directory.GetFiles(ScriptsUnpackedDir).Length > 0)
            {
                var result = new Dictionary<int, byte[]>();
                foreach (string file in Directory.GetFiles(ScriptsUnpackedDir))
                {
                    if (int.TryParse(Path.GetFileName(file), out int id))
                    {
                        result[id] = File.ReadAllBytes(file);
                    }
                }
                return result;
            }
            return ReadNarc(ScriptsNarcPath);
        }

        private static Dictionary<int, byte[]> ReadNarc(string path)
        {
            var result = new Dictionary<int, byte[]>();
            if (!File.Exists(path)) { return result; }

            Narc narc = Narc.Open(path);
            if (narc == null) { return result; }

            for (int i = 0; i < narc.GetElementsLength(); i++)
            {
                result[i] = narc[i].ToArray();
            }
            return result;
        }

        /// <summary>
        /// Adds a replacement for every still-unresolved wiped file that the given copy of the scripts holds intact.
        /// </summary>
        public static int CollectFrom(Dictionary<int, byte[]> source, string sourceName, IEnumerable<int> wiped, Dictionary<int, Candidate> found)
        {
            int added = 0;
            foreach (int id in wiped)
            {
                if (found.ContainsKey(id)) { continue; }
                if (!source.TryGetValue(id, out byte[] data) || IsWiped(data)) { continue; }

                found[id] = new Candidate { FileID = id, Replacement = data, Source = sourceName };
                added++;
            }
            return added;
        }

        public static Dictionary<int, byte[]> ReadProjectNarcIfDistinct()
        {
            // While the unpacked folder exists, the packed NARC still holds the scripts as of the last ROM save.
            bool unpacked = Directory.Exists(ScriptsUnpackedDir) && Directory.GetFiles(ScriptsUnpackedDir).Length > 0;
            return unpacked ? ReadNarc(ScriptsNarcPath) : new Dictionary<int, byte[]>();
        }

        public static string GetProjectRomPath()
        {
            string dir = RomInfo.workDir.TrimEnd('\\', '/');
            if (!dir.EndsWith(RomInfo.folderSuffix)) { return null; }
            string rom = dir.Substring(0, dir.Length - RomInfo.folderSuffix.Length) + ".nds";
            return File.Exists(rom) ? rom : null;
        }

        /// <summary>
        /// Extracts a ROM with dsrom into a temp folder and returns its script files, or null if it can't be used.
        /// </summary>
        public static Dictionary<int, byte[]> ReadScriptsFromRom(string ndsPath, out string problem)
        {
            problem = null;
            string tempDir = Path.Combine(Path.GetTempPath(), "DSPRE_script_recovery_" + Guid.NewGuid().ToString("N"));
            try
            {
                if (!DSUtils.UnpackRomDsRom(ndsPath, tempDir))
                {
                    problem = "dsrom couldn't extract it.";
                    return null;
                }

                var code = YamlUtils.ReadGameCodeFromHeaderYaml(Path.Combine(tempDir, "header.yaml"));
                if (code.HasValue && RomInfo.romID != null && code.Value.gamecode.Length >= 3 && RomInfo.romID.Length >= 3
                    && !code.Value.gamecode.Substring(0, 3).Equals(RomInfo.romID.Substring(0, 3), StringComparison.OrdinalIgnoreCase))
                {
                    problem = $"it is a different game ({code.Value.gamecode}, this project is {RomInfo.romID}).";
                    return null;
                }

                if (!ScriptsNarcPath.StartsWith(RomInfo.workDir, StringComparison.OrdinalIgnoreCase))
                {
                    problem = "DSPRE couldn't work out where its script archive is.";
                    return null;
                }
                string relative = ScriptsNarcPath.Substring(RomInfo.workDir.Length).TrimStart('\\', '/');
                string[] parts = relative.Split(new[] { '\\', '/' }, 2);
                if (parts.Length == 2 && parts[0].Equals("data", StringComparison.OrdinalIgnoreCase))
                {
                    relative = Path.Combine("files", parts[1]);
                }

                var scripts = ReadNarc(Path.Combine(tempDir, relative));
                if (scripts.Count == 0)
                {
                    problem = "its script archive couldn't be found.";
                    return null;
                }
                return scripts;
            }
            finally
            {
                try { Directory.Delete(tempDir, recursive: true); } catch (Exception) { }
            }
        }

        public static void Restore(IEnumerable<Candidate> candidates)
        {
            DSUtils.TryUnpackNarcs(new List<RomInfo.DirNames> { RomInfo.DirNames.scripts });

            foreach (Candidate c in candidates)
            {
                File.WriteAllBytes(Filesystem.GetScriptPath(c.FileID), c.Replacement);

                // The .script copy is empty too; deleting it makes DSPRE re-export from the restored binary.
                string txtPath = ScriptFile.GetFilePaths(c.FileID).txtPath;
                if (File.Exists(txtPath)) { File.Delete(txtPath); }

                AppLogger.Info($"Script file {c.FileID}: restored from {c.Source}.");
            }
            ScriptFile.ClearPlaintextCache();

            try { if (File.Exists(DismissedMarkerPath)) { File.Delete(DismissedMarkerPath); } } catch (Exception) { }
        }

        public static void Dismiss(IEnumerable<int> wiped)
        {
            try { File.WriteAllText(DismissedMarkerPath, string.Join(",", wiped)); } catch (Exception) { }
        }

        public static void ShowRecovery(IWin32Window owner, List<int> wiped)
        {
            var found = new Dictionary<int, Candidate>();

            Helpers.statusLabelMessage("Looking for intact copies of emptied script files...");
            CollectFrom(ReadProjectNarcIfDistinct(), "this project's last saved scripts", wiped, found);

            string romPath = GetProjectRomPath();
            if (romPath != null && found.Count < wiped.Count)
            {
                var romScripts = ReadScriptsFromRom(romPath, out _);
                if (romScripts != null)
                {
                    CollectFrom(romScripts, Path.GetFileName(romPath), wiped, found);
                }
            }
            Helpers.statusLabelMessage();

            using (var form = new ScriptWipeRecoveryForm(wiped, found))
            {
                form.ShowDialog(owner);
            }
        }
    }

    public class ScriptWipeRecoveryForm : Form
    {
        private readonly List<int> wiped;
        private readonly Dictionary<int, ScriptWipeRecovery.Candidate> found;
        private readonly ListView list;
        private readonly Button restoreButton;

        public ScriptWipeRecoveryForm(List<int> wiped, Dictionary<int, ScriptWipeRecovery.Candidate> found)
        {
            this.wiped = wiped;
            this.found = found;

            Text = "Some script files need restoring";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.Sizable;
            MinimizeBox = false;
            MaximizeBox = false;
            ShowInTaskbar = false;
            Font = SystemFonts.MessageBoxFont;
            // Sized in font heights so the dialog fits on scaled displays.
            int em = Font.Height;
            ClientSize = new Size(em * 46, em * 26);
            MinimumSize = new Size(em * 32, em * 20);

            var message = new Label
            {
                AutoSize = true,
                Anchor = AnchorStyles.Left | AnchorStyles.Right,
                Margin = new Padding(0, 0, 0, em / 2),
                Text =
                    "DSPRE 2.3.4.3 shipped an automatic script repair that misjudged some files and replaced them with " +
                    $"empty scripts. {wiped.Count} script file(s) in this project were emptied this way. We apologize for this.\n\n" +
                    "DSPRE can put them back from an intact copy. For files still missing, use " +
                    "\"Search another ROM...\" with an older backup of your project's ROM.",
            };

            list = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                CheckBoxes = true,
                FullRowSelect = true,
                HeaderStyle = ColumnHeaderStyle.Nonclickable,
                ShowItemToolTips = true,
                Margin = new Padding(0),
            };
            list.Columns.Add("Script file", em * 6);
            list.Columns.Add("Intact copy found in", em * 30);
            list.Resize += (s, e) => list.Columns[1].Width = Math.Max(em * 10, list.ClientSize.Width - list.Columns[0].Width);

            var buttons = new FlowLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = true,
                Margin = new Padding(0, em / 2, 0, 0),
            };

            restoreButton = new Button { Text = "Restore checked files", AutoSize = true };
            restoreButton.Click += RestoreButton_Click;

            var laterButton = new Button { Text = "Remind me later", AutoSize = true, DialogResult = DialogResult.Cancel };

            var dismissButton = new Button { Text = "Don't ask again for this project", AutoSize = true };
            dismissButton.Click += (s, e) =>
            {
                ScriptWipeRecovery.Dismiss(wiped);
                DialogResult = DialogResult.Cancel;
            };

            var searchButton = new Button { Text = "Search another ROM...", AutoSize = true };
            searchButton.Click += SearchButton_Click;

            buttons.Controls.Add(restoreButton);
            buttons.Controls.Add(laterButton);
            buttons.Controls.Add(dismissButton);
            buttons.Controls.Add(searchButton);

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Padding = new Padding(em * 3 / 4),
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.Controls.Add(message, 0, 0);
            layout.Controls.Add(list, 0, 1);
            layout.Controls.Add(buttons, 0, 2);
            Controls.Add(layout);

            AcceptButton = restoreButton;
            CancelButton = laterButton;

            RefreshList();
        }

        private void RefreshList()
        {
            list.Items.Clear();
            foreach (int id in wiped)
            {
                string where;
                bool canRestore = found.TryGetValue(id, out var candidate);
                if (canRestore)
                {
                    where = candidate.Source;
                }
                else if (ScriptWipeRecovery.IsMarkerlessVanillaScript(id))
                {
                    where = "Not found yet. This is an original game script DSPRE couldn't edit before, so a clean ROM of the same game restores it exactly.";
                }
                else
                {
                    where = "Not found yet. Search an older backup of this project's ROM.";
                }

                var item = new ListViewItem(new[] { id.ToString("D4"), where })
                {
                    Checked = canRestore,
                    Tag = id,
                    ForeColor = canRestore ? SystemColors.WindowText : SystemColors.GrayText,
                };
                list.Items.Add(item);
            }
            list.ItemCheck -= List_ItemCheck;
            list.ItemCheck += List_ItemCheck;
            restoreButton.Enabled = found.Count > 0;
        }

        private void List_ItemCheck(object sender, ItemCheckEventArgs e)
        {
            if (!found.ContainsKey((int)list.Items[e.Index].Tag))
            {
                e.NewValue = CheckState.Unchecked;
            }
        }

        private void SearchButton_Click(object sender, EventArgs e)
        {
            using (var dialog = new OpenFileDialog { Filter = DSUtils.NDSRomFilter, Title = "Pick an older backup of this ROM, or a clean ROM of the same game" })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) { return; }

                Cursor = Cursors.WaitCursor;
                Dictionary<int, byte[]> scripts;
                string problem;
                try
                {
                    scripts = ScriptWipeRecovery.ReadScriptsFromRom(dialog.FileName, out problem);
                }
                finally
                {
                    Cursor = Cursors.Default;
                }

                if (scripts == null)
                {
                    MessageBox.Show(this, $"DSPRE can't use that ROM: {problem}", "Can't search this ROM", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                int added = ScriptWipeRecovery.CollectFrom(scripts, Path.GetFileName(dialog.FileName), wiped, found);
                RefreshList();

                if (added == 0)
                {
                    MessageBox.Show(this, "That ROM has no intact copy of the files that are still missing.", "Nothing new found",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }

        private void RestoreButton_Click(object sender, EventArgs e)
        {
            var chosen = list.Items.Cast<ListViewItem>()
                .Where(i => i.Checked && found.ContainsKey((int)i.Tag))
                .Select(i => found[(int)i.Tag])
                .ToList();

            if (chosen.Count == 0) { return; }

            try
            {
                ScriptWipeRecovery.Restore(chosen);
            }
            catch (Exception ex)
            {
                AppLogger.Error($"Restoring emptied script files failed: {ex}");
                MessageBox.Show(this, $"Restoring failed: {ex.Message}", "Restore failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            int left = wiped.Count - chosen.Count;
            MessageBox.Show(this,
                $"Restored {chosen.Count} script file(s): {string.Join(", ", chosen.Select(c => c.FileID))}.\n\n" +
                "Save the ROM to keep them." +
                (left > 0 ? $"\n\n{left} file(s) are still empty. DSPRE will ask again the next time you open this project." : ""),
                "Script files restored", MessageBoxButtons.OK, MessageBoxIcon.Information);

            DialogResult = DialogResult.OK;
        }
    }
}
