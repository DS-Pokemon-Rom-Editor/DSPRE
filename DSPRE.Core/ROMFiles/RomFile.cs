﻿using System;
using System.IO;
using static DSPRE.RomInfo;

namespace DSPRE.ROMFiles {
    public abstract class RomFile {
        public abstract byte[] ToByteArray();
        public bool SaveToFile(string path, bool showSuccessMessage = true) {
            
            byte[] romFileToByteArray = ToByteArray();
            if (romFileToByteArray is null) {
                AppLogger.Error(GetType().Name + " couldn't be saved!");
                return false;
            }

            File.WriteAllBytes(path, romFileToByteArray);

            if (showSuccessMessage) {
                AppMessages.Info(GetType().Name + " saved successfully!");
            }

            return true;
        }
        protected internal bool SaveToFileDefaultDir(DirNames dir, int IDtoReplace, bool showSuccessMessage = true) {
            string path = Path.Combine(RomInfo.gameDirs[dir].unpackedDir, IDtoReplace.ToString("D4"));
            return this.SaveToFile(path, showSuccessMessage);
        }
        protected internal void SaveToFileExplorePath(string fileType, string fileExtension, string suggestedFileName, bool showSuccessMessage = true) {
            fileExtension = "*." + fileExtension;

            string chosen = AppMessages.PickSaveFile($"Export {fileType}",
                $"{fileType} ({fileExtension})|{fileExtension}", suggestedFileName);

            if (string.IsNullOrEmpty(chosen)) {
                return;
            }

            this.SaveToFile(chosen, showSuccessMessage);
        }
    }
}
