using System.Diagnostics;
using System.IO;

namespace DSPRE {
    public static class ModelUtils {

        public static void ModelToDAE(string modelName, byte[] modelData, byte[] textureData) {
            AppMessages.Info("Choose output folder.\nDSPRE will automatically create a sub-folder in it.", "Awaiting user input");

            string chosenDir = AppMessages.PickFolder("Choose output folder");
            if (string.IsNullOrEmpty(chosenDir)) {
                return;
            }

            string outDir = Path.Combine(chosenDir, modelName);

            if (Directory.Exists(outDir)) {
                if (Directory.GetFiles(outDir).Length > 0) {
                    if (!AppMessages.Confirm($"Directory \"{outDir}\" already exists and is not empty.\nIts contents will be lost.\n\nDo you want to proceed?", "Directory not empty")) {
                        return;
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

            File.WriteAllBytes(tempNSBMDPath, modelData);

            /* Check correct creation of temp NSBMD file*/
            if (!File.Exists(tempNSBMDPath)) {
                AppMessages.Info("Expected NSBMD file could not be found.\nAborting", "Error");
                return;
            }

            Process apicula = new Process();
            apicula.StartInfo.Arguments = $" convert \"{tempNSBMDPath}\" --output \"{outDir}\"";
            if (!DSUtils.ConfigureToolStartInfo(apicula.StartInfo, "apicula"))
            {
                apicula.Dispose();
                File.Delete(tempNSBMDPath);
                DSUtils.ReportToolUnavailable("apicula");
                return;
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

            if (apicula.ExitCode == 0) {
                AppMessages.Info("NSBMD was exported and converted successfully!", "Operation successful");
            } else {
                AppMessages.Error("NSBMD to DAE conversion failed.", "Apicula error");
            }
        }

        public static void ModelToGLB(string modelName, byte[] modelData, byte[] textureData) {
            AppMessages.Info("Choose output folder.\nDSPRE will automatically create a sub-folder in it.", "Awaiting user input");

            string chosenDir = AppMessages.PickFolder("Choose output folder");
            if (string.IsNullOrEmpty(chosenDir)) {
                return;
            }

            string outDir = Path.Combine(chosenDir, modelName);

            if (Directory.Exists(outDir)) {
                if (Directory.GetFiles(outDir).Length > 0) {
                    if (!AppMessages.Confirm($"Directory \"{outDir}\" already exists and is not empty.\nIts contents will be lost.\n\nDo you want to proceed?", "Directory not empty")) {
                        return;
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

            File.WriteAllBytes(tempNSBMDPath, modelData);

            /* Check correct creation of temp NSBMD file*/
            if (!File.Exists(tempNSBMDPath)) {
                AppMessages.Info("NSBMD file corresponding to this map could not be found.\nAborting", "Error");
                return;
            }

            Process apicula = new Process();
            apicula.StartInfo.Arguments = $" convert \"{tempNSBMDPath}\" -f glb --output \"{outDir}\"";
            if (!DSUtils.ConfigureToolStartInfo(apicula.StartInfo, "apicula"))
            {
                apicula.Dispose();
                File.Delete(tempNSBMDPath);
                DSUtils.ReportToolUnavailable("apicula");
                return;
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

            if (apicula.ExitCode == 0) {
                AppMessages.Info("NSBMD was exported and converted successfully!", "Operation successful");
            } else {
                AppMessages.Error("NSBMD to GLB conversion failed.", "Apicula error");
            }
        }
    }
}
