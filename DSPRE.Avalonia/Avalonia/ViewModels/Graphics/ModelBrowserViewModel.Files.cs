using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using global::Avalonia;
using global::Avalonia.Media.Imaging;
using global::Avalonia.Platform;
using DSPRE.Avalonia.Data;
using DSPRE.Avalonia.Gl;
using DSPRE.ROMFiles;
using LibNDSFormats.NSBMD;

namespace DSPRE.Avalonia.ViewModels.Graphics
{
    public sealed partial class ModelBrowserViewModel
    {
        public string SuggestedFileName(string extension)
        {
            if (_selected == null) return "model" + extension;
            string name = _selected.Archive.Title.ToLowerInvariant().Replace(' ', '_');
            foreach (char c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
            return $"{name}_{ShowingIndex:D4}{extension}";
        }

        public bool CanPutFileIn => _selected != null && _cannotImport == null;

        public string PutFileInHelp => _selected == null
            ? "Pick something first."
            : _cannotImport
              ?? ("Replace this with a finished model, texture or animation file, or an OBJ mesh, which is "
                  + "turned into a model on import. Anything already in the game's own format "
                  + "has to be the same kind as what is here now.\n\n" + ModelAssets.CanConvertAMesh);

        private string _cannotImport = "Pick something first.";

        /// <summary>Imports a file over the entry showing. Returns a reason when it could not.</summary>
        public string PutFileIn(string path) => PutFileIn(path, out _);

        public string PutFileIn(string path, out string note)
        {
            note = null;
            if (_selected == null) return "Pick something first.";
            if (_cannotImport != null) return _cannotImport;
            ModelAssets.Archive archive = _selected.Archive;
            int index = ShowingIndex;
            string said = null;
            string err = string.Equals(Path.GetExtension(path), ".obj", StringComparison.OrdinalIgnoreCase)
                ? _pending.Import(() => ModelAssets.ImportMesh(archive, index, path, out said))
                : _pending.Import(() => ModelAssets.ImportRaw(archive, index, path));
            note = said;
            return err;
        }

        public string SaveFileAsItIs(string path)
        {
            if (_selected == null) return "Pick something first.";
            return ModelAssets.SaveRaw(_selected.Archive, ShowingIndex, path);
        }

        /// <summary>Saves the model as a file other 3D programs open, with its pictures.</summary>
        public string SaveAsThreeD(string path, bool glb, out string written)
        {
            written = null;
            if (_selected == null) return "Pick something first.";
            if (!CanSaveModel) return _options?.SaveNote ?? "This isn't a model.";

            ScriptNarc narc = new ScriptNarc(_selected.Archive.Dir);
            byte[] model = narc.Get(ShowingIndex);
            int textureSet = _textureChoice >= 0 && _textureChoice < _textureSetEntries.Count
                ? _textureSetEntries[_textureChoice] : -1;
            byte[] textures = ModelAssets.TexturesFor(_selected.Archive, ShowingIndex, textureSet);

            try
            {
                // The picked file name becomes the folder apicula writes into, beside the picked place.
                written = DSPRE.ModelUtils.ExportModel(Path.GetFileNameWithoutExtension(path), model, textures, glb,
                    Path.GetDirectoryName(path));
                return null;
            }
            catch (Exception ex)
            {
                AppLogger.Error("ModelBrowser.SaveAsThreeD failed: " + ex.Message);
                return "This model couldn't be turned into a 3D file. The whole file can still be saved "
                     + "as it is.";
            }
        }
    }
}
