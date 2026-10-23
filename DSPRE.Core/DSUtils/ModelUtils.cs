using System.Diagnostics;
using System.IO;

namespace DSPRE
{
    public static class ModelUtils
    {

        /// <summary>
        /// Converts a model into a sub-folder of <paramref name="folder"/>, or of a folder the user picks when it is
        /// null, and returns that sub-folder, or null when nothing was written. Says nothing on success, so the caller
        /// can confirm it its own way.
        /// </summary>
        /// <param name="report">False for a batch that reports its failures once at the end; they are only logged here.</param>
        public static string ExportModel(string modelName, byte[] modelData, byte[] textureData, bool glb, string folder = null, bool report = true)
        {
            return Convert(modelName, modelData, textureData, glb, quiet: true, folder, report);
        }

        private static string Convert(string modelName, byte[] modelData, byte[] textureData, bool glb, bool quiet, string folder, bool report = true)
        {
            void Fail(string message, string title)
            {
                AppLogger.Error(message);
                if (report) AppMessages.Error(message, title);
            }

            if (!quiet && folder == null)
            {
                AppMessages.Info("Choose a folder. DSPRE makes a sub-folder in it for the model.", "Export model");
            }

            string chosenDir = folder ?? AppMessages.PickFolder(quiet ? "Choose where the model's folder goes" : "Choose output folder");
            if (string.IsNullOrEmpty(chosenDir))
            {
                return null;
            }

            string outDir = Path.Combine(chosenDir, modelName);

            if (Directory.Exists(outDir))
            {
                if (Directory.GetFiles(outDir).Length > 0)
                {
                    if (!AppMessages.Confirm($"The folder \"{outDir}\" already has files in it. They will be replaced.\n\nReplace them?", "Folder not empty"))
                    {
                        return null;
                    }
                    else
                    {
                        Directory.Delete(outDir, recursive: true);
                    }
                }
                else
                {
                    Directory.Delete(outDir, recursive: true);
                }
            }
            string tempNSBMDPath = outDir + "_temp.nsbmd";

            if (textureData != null && textureData.Length > 0)
            {
                modelData = NSBUtils.BuildNSBMDwithTextures(modelData, textureData);
            }

            PlainFiles.WriteBytes(tempNSBMDPath, modelData);

            /* Check correct creation of temp NSBMD file*/
            if (!File.Exists(tempNSBMDPath))
            {
                Fail("The NSBMD file wasn't found.", "Error");
                return null;
            }

            Process apicula = new Process();
            apicula.StartInfo.Arguments = glb
                ? $" convert \"{tempNSBMDPath}\" -f glb --output \"{outDir}\""
                : $" convert \"{tempNSBMDPath}\" --output \"{outDir}\"";
            if (!DSUtils.ConfigureToolStartInfo(apicula.StartInfo, "apicula"))
            {
                apicula.Dispose();
                File.Delete(tempNSBMDPath);
                Fail(DSUtils.ToolAvailabilityError("apicula"), "Tool unavailable");
                return null;
            }
            apicula.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            apicula.StartInfo.CreateNoWindow = true;
            apicula.Start();
            apicula.WaitForExit();

            if (File.Exists(tempNSBMDPath))
            {
                File.Delete(tempNSBMDPath);

                if (File.Exists(tempNSBMDPath))
                {
                    AppMessages.Warning("The temporary NSBMD file couldn't be deleted.", "Warning");
                }
            }
            else
            {
                AppMessages.Warning("The temporary NSBMD file for this map is missing.", "Warning");
            }

            if (apicula.ExitCode != 0)
            {
                Fail($"The NSBMD couldn't be converted to {(glb ? "GLB" : "DAE")}.", "Apicula error");
                return null;
            }
            if (!quiet)
            {
                AppMessages.Info("The model was exported and converted.", "Model exported");
            }
            return outDir;
        }
    }
}
