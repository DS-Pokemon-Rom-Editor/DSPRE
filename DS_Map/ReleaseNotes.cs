using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace DSPRE {
    /// <summary>
    /// Works out which changelog text a given version would be released with. This mirrors the
    /// "Attach changelog to release notes" step in .github/workflows/update-releases.yaml; change one
    /// and the other has to follow, or the preview stops matching the real release.
    /// </summary>
    public static class ReleaseNotes {
        /// <summary>Version as it is shown to users, with trailing zero parts dropped (2.3.0.0 reads 2.3).</summary>
        public static string DisplayVersion(Version version) {
            int patch = Math.Max(version.Build, 0);
            int revision = Math.Max(version.Revision, 0);

            if (revision != 0) {
                return string.Format("{0}.{1}.{2}.{3}", version.Major, version.Minor, patch, revision);
            }
            if (patch != 0) {
                return string.Format("{0}.{1}.{2}", version.Major, version.Minor, patch);
            }
            return string.Format("{0}.{1}", version.Major, version.Minor);
        }

        public static string MajorMinor(Version version) {
            return string.Format("{0}.{1}", version.Major, version.Minor);
        }

        public static string ChangelogPathFor(string changelogFolder, Version version) {
            return Path.Combine(changelogFolder, "CHANGELOG_" + MajorMinor(version) + "_User.md");
        }

        /// <summary>
        /// The notes this version would be published with: a patch release takes just its own section,
        /// anything else takes the whole file.
        /// </summary>
        public static string Build(string changelogFolder, Version version, out string notesPath, out bool sectionOnly) {
            notesPath = ChangelogPathFor(changelogFolder, version);
            sectionOnly = false;

            if (!File.Exists(notesPath)) {
                return null;
            }

            string text = File.ReadAllText(notesPath);
            int patch = Math.Max(version.Build, 0);
            if (patch == 0) {
                return text;
            }

            string heading = "## " + DisplayVersion(version);
            string section = ExtractSection(text, heading);
            if (section == null) {
                return text;
            }

            sectionOnly = true;
            return section;
        }

        /// <summary>Takes the heading line and everything under it, stopping at the next heading or rule.</summary>
        private static string ExtractSection(string text, string heading) {
            string[] lines = text.Replace("\r\n", "\n").Split('\n');
            List<string> kept = new List<string>();
            bool found = false;

            foreach (string line in lines) {
                if (!found) {
                    if (line == heading) {
                        found = true;
                        kept.Add(line);
                    }
                    continue;
                }

                if (line.StartsWith("---") || line.StartsWith("## ")) {
                    break;
                }
                kept.Add(line);
            }

            if (!found) {
                return null;
            }

            StringBuilder sb = new StringBuilder();
            foreach (string line in kept) {
                sb.AppendLine(line);
            }
            return sb.ToString().TrimEnd() + Environment.NewLine;
        }

        /// <summary>
        /// Finds the repository's Changelogs folder by walking up from the running executable, so the
        /// preview works from a normal debug build without anything being copied next to the exe.
        /// </summary>
        public static string FindChangelogFolder() {
            try {
                DirectoryInfo dir = new DirectoryInfo(System.Windows.Forms.Application.StartupPath);
                for (int depth = 0; dir != null && depth < 8; depth++) {
                    string candidate = Path.Combine(dir.FullName, "Changelogs");
                    if (Directory.Exists(candidate)) {
                        return candidate;
                    }
                    dir = dir.Parent;
                }
            } catch (Exception ex) {
                AppLogger.Warn("Couldn't locate the Changelogs folder: " + ex.Message);
            }
            return null;
        }

        /// <summary>Every version that has a "## " heading in the given changelog file, newest first.</summary>
        public static List<string> HeadingsIn(string changelogPath) {
            List<string> found = new List<string>();
            if (!File.Exists(changelogPath)) {
                return found;
            }
            foreach (string line in File.ReadAllLines(changelogPath)) {
                if (line.StartsWith("## ")) {
                    found.Add(line.Substring(3).Trim());
                }
            }
            return found;
        }
    }
}
