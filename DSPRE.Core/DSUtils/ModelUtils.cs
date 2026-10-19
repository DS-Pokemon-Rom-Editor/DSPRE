using System.Diagnostics;
using System.IO;

namespace DSPRE {
    public static class ModelUtils {

        /// <summary>
        /// Converts a model into a sub-folder of <paramref name="folder"/>, or of a folder the user picks when it is
        /// null, and returns that sub-folder, or null when nothing was written. Says nothing on success, so the caller
        /// can confirm it its own way.
        /// </summary>
        public static string ExportModel(string modelName, byte[] modelData, byte[] textureData, bool glb, string folder = null) {
            return Convert(modelName, modelData, textureData, glb, quiet: true, folder);
        }

        private static string Convert(string modelName, byte[] modelData, byte[] textureData, bool glb, bool quiet, string folder) {
            if (!quiet && folder == null) {
                AppMessages.Info("Choose output folder.\nDSPRE will automatically create a sub-folder in it.", "Awaiting user input");
            }

            string chosenDir = folder ?? AppMessages.PickFolder(quiet ? "Choose where the model's folder goes" : "Choose output folder");
            if (string.IsNullOrEmpty(chosenDir)) {
                return null;
            }

            string outDir = Path.Combine(chosenDir, modelName);

            if (Directory.Exists(outDir)) {
                if (Directory.GetFiles(outDir).Length > 0) {
                    if (!AppMessages.Confirm($"Directory \"{outDir}\" already exists and is not empty.\nIts contents will be lost.\n\nDo you want to proceed?", "Directory not empty")) {
                        return null;
                    } else {
                        Directory.Delete(outDir, recursive: true);
                    }
                } else {
                    Directory.Delete(outDir, recursive: true);
                }
            }
            string tempNSBMDPath = outDir + "_temp.nsbmd";

            if (textureData != null && textureData.Length > 0) {
                modelData = NSBUtils.BuildNSBMDwithTextures(modelData, textureData);
            }

            PlainFiles.WriteBytes(tempNSBMDPath, modelData);

            /* Check correct creation of temp NSBMD file*/
            if (!File.Exists(tempNSBMDPath)) {
                AppMessages.Error("Expected NSBMD file could not be found.\nAborting", "Error");
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
                DSUtils.ReportToolUnavailable("apicula");
                return null;
            }
            apicula.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            apicula.StartInfo.CreateNoWindow = true;
            apicula.Start();
            apicula.WaitForExit();

            if (File.Exists(tempNSBMDPath)) {
                File.Delete(tempNSBMDPath);

                if (File.Exists(tempNSBMDPath)) {
                    AppMessages.Warning("Temporary NSBMD file deletion failed.", "Warning");
                }
            } else {
                AppMessages.Warning("Temporary NSBMD file corresponding to this map disappeared.", "Error");
            }

            if (apicula.ExitCode != 0) {
                AppMessages.Error($"NSBMD to {(glb ? "GLB" : "DAE")} conversion failed.", "Apicula error");
                return null;
            }
            if (!quiet) {
                AppMessages.Info("The model was exported and converted.", "Model exported");
            }
            return outDir;
        }
    }
}
