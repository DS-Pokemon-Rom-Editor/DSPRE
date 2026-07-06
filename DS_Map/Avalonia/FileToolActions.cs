using Avalonia.Controls;
using Avalonia.Platform.Storage;
using System.IO;
using System.Threading.Tasks;
using NarcAPI;

namespace DSPRE.Avalonia
{
    /// <summary>
    /// Avalonia ports of the standalone WinForms file utilities (NARC pack/unpack, NSBMD texture
    /// add/remove/extract). Pure picker-orchestration over the core <c>Narc</c>/<c>NSBUtils</c> APIs;
    /// no ROM needs to be loaded.
    /// </summary>
    public static class FileToolActions
    {
        private static readonly FilePickerFileType NarcFilter =
            new FilePickerFileType("NARC File") { Patterns = new[] { "*.narc" } };
        private static readonly FilePickerFileType NsbmdFilter =
            new FilePickerFileType("NSBMD File") { Patterns = new[] { "*.nsbmd" } };
        private static readonly FilePickerFileType NsbtxFilter =
            new FilePickerFileType("NSBTX File") { Patterns = new[] { "*.nsbtx" } };

        // ── NARC Utility ───────────────────────────────────────────────────────────

        public static async Task UnpackNarcToFolder(Window owner)
        {
            string narcPath = await DialogHelper.OpenFile(owner, "Select a NARC to unpack", new[] { NarcFilter, DialogHelper.AllFilter });
            if (string.IsNullOrEmpty(narcPath)) return;

            Narc userFile = Narc.Open(narcPath);
            if (userFile is null)
            {
                await DialogHelper.ShowError("The file you selected is not a valid NARC.", "Cannot proceed");
                return;
            }

            string destParent = await DialogHelper.OpenFolder(owner, "Choose where to save the NARC content (a subfolder is created)");
            if (string.IsNullOrEmpty(destParent)) return;

            string finalExtractedPath = Path.Combine(destParent, Path.GetFileNameWithoutExtension(narcPath));
            userFile.ExtractToFolder(finalExtractedPath);
            await DialogHelper.ShowInfo("The contents of " + narcPath + " have been extracted to:\n" + finalExtractedPath, "NARC Extracted");
        }

        public static async Task PackFolderToNarc(Window owner)
        {
            string folder = await DialogHelper.OpenFolder(owner, "Select the folder to pack into a NARC");
            if (string.IsNullOrEmpty(folder)) return;

            string dest = await DialogHelper.SaveFile(owner, "Save NARC as", new[] { NarcFilter }, Path.GetFileName(folder) + ".narc");
            if (string.IsNullOrEmpty(dest)) return;

            Narc.FromFolder(folder).Save(dest);
            await DialogHelper.ShowInfo("The contents of folder \"" + folder + "\" have been packed to:\n" + dest, "NARC Created");
        }

        // ── NSBMD Utility ──────────────────────────────────────────────────────────

        public static async Task SaveTexturesFromNsbmd(Window owner)
        {
            string modelPath = await DialogHelper.OpenFile(owner, "Select a textured NSBMD", new[] { NsbmdFilter, DialogHelper.AllFilter });
            if (string.IsNullOrEmpty(modelPath)) return;

            byte[] modelFile = DSUtils.ReadFromFile(modelPath);
            if (NSBUtils.CheckNSBMDHeader(modelFile) == NSBUtils.NSBMD_DOESNTHAVE_TEXTURE)
            {
                await DialogHelper.ShowInfo("This NSBMD file is untextured.", "No textures to extract");
                return;
            }

            string dest = await DialogHelper.SaveFile(owner, "Save textures as NSBTX", new[] { NsbtxFilter },
                Path.GetFileNameWithoutExtension(modelPath) + ".nsbtx");
            if (string.IsNullOrEmpty(dest)) return;

            DSUtils.WriteToFile(dest, NSBUtils.GetTexturesFromTexturedNSBMD(modelFile));
            await DialogHelper.ShowInfo("The textures of " + modelPath + " have been extracted and saved.", "Textures saved");
        }

        public static async Task RemoveTexturesFromNsbmd(Window owner)
        {
            string modelPath = await DialogHelper.OpenFile(owner, "Select a textured NSBMD", new[] { NsbmdFilter, DialogHelper.AllFilter });
            if (string.IsNullOrEmpty(modelPath)) return;

            byte[] modelFile = DSUtils.ReadFromFile(modelPath);
            if (NSBUtils.CheckNSBMDHeader(modelFile) == NSBUtils.NSBMD_DOESNTHAVE_TEXTURE)
            {
                await DialogHelper.ShowInfo("This NSBMD file is already untextured.", "No textures to remove");
                return;
            }

            string extramsg = "";
            if (await DialogHelper.AskYesNo("Would you like to save the removed textures to a file?", "Save textures?"))
            {
                string texDest = await DialogHelper.SaveFile(owner, "Save textures as NSBTX", new[] { NsbtxFilter },
                    Path.GetFileNameWithoutExtension(modelPath) + ".nsbtx");
                if (!string.IsNullOrEmpty(texDest))
                {
                    DSUtils.WriteToFile(texDest, NSBUtils.GetTexturesFromTexturedNSBMD(modelFile));
                    extramsg = " exported and";
                }
            }

            string dest = await DialogHelper.SaveFile(owner, "Save untextured NSBMD as", new[] { NsbmdFilter },
                Path.GetFileNameWithoutExtension(modelPath) + "_untextured.nsbmd");
            if (string.IsNullOrEmpty(dest)) return;

            DSUtils.WriteToFile(dest, NSBUtils.GetModelWithoutTextures(modelFile));
            await DialogHelper.ShowInfo("Textures correctly" + extramsg + " removed!", "Success!");
        }

        public static async Task AddTexturesToNsbmd(Window owner)
        {
            string modelPath = await DialogHelper.OpenFile(owner, "Select the NSBMD model", new[] { NsbmdFilter, DialogHelper.AllFilter });
            if (string.IsNullOrEmpty(modelPath)) return;

            byte[] modelFile = File.ReadAllBytes(modelPath);
            if (NSBUtils.CheckNSBMDHeader(modelFile) == NSBUtils.NSBMD_HAS_TEXTURE)
            {
                if (!await DialogHelper.AskYesNo("This NSBMD file is already textured.\nDo you want to overwrite its textures?", "Textures found"))
                {
                    return;
                }
            }

            string nsbtxPath = await DialogHelper.OpenFile(owner, "Select the new NSBTX texture file", new[] { NsbtxFilter, DialogHelper.AllFilter });
            if (string.IsNullOrEmpty(nsbtxPath)) return;

            byte[] textureFile = File.ReadAllBytes(nsbtxPath);

            string baseName = Path.GetFileNameWithoutExtension(modelPath);
            if (baseName.EndsWith("_untextured"))
            {
                baseName = baseName.Substring(0, baseName.Length - "_untextured".Length);
            }

            string dest = await DialogHelper.SaveFile(owner, "Save textured NSBMD as", new[] { NsbmdFilter }, baseName + "_textured.nsbmd");
            if (string.IsNullOrEmpty(dest)) return;

            DSUtils.WriteToFile(dest, NSBUtils.BuildNSBMDwithTextures(modelFile, textureFile), fmode: FileMode.Create);
            await DialogHelper.ShowInfo("Textures correctly written to NSBMD file.", "Success!");
        }
    }
}
