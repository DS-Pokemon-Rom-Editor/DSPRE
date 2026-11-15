using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using DSPRE.Avalonia.Data;
using DSPRE.Editors;
using DSPRE.ROMFiles;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.ViewModels.Graphics
{
    public sealed partial class CellAnimationEditorViewModel
    {
        /// <summary>Writes the file back. The ROM save packs it.</summary>
        public void SaveChanges()
        {
            if (_file == null) return;
            try
            {
                byte[] animation = Ready(_file.Write(), _animationMarker, out string trouble);
                if (trouble != null) { StatusText = trouble; return; }

                byte[] positions = null;
                if (_layoutTouched && _layout != null && _cells >= 0)
                {
                    positions = Ready(_layout.Write(), _layoutMarker, out trouble);
                    if (trouble != null) { StatusText = trouble; return; }
                }

                // Both are packed before either is written, so a refusal can't half-save.
                Dictionary<int, byte[]> files = new Dictionary<int, byte[]> { [_animation] = animation };
                if (positions != null) files[_cells] = positions;
                _source.Put(files);

                MarkSaved();
                Dirty = false;
                StatusText = "Saved.";
            }
            catch (Exception ex)
            {
                AppLogger.Error("Cell animation save: " + ex.Message);
                StatusText = "That couldn't be saved. " + ex.Message;
            }
        }

        // Packs the file the way it was stored. trouble is set when that can't be done.
        private static byte[] Ready(byte[] plain, byte marker, out string trouble)
        {
            trouble = null;
            if (marker == 0x11)
            {
                trouble = "This file uses a compression DSPRE can't write yet, so nothing was saved.";
                return null;
            }
            if (marker == 0) return plain;

            byte[] packed = GraphicAssets.Squeeze(plain, marker);
            if (packed == null)
                trouble = "This file couldn't be compressed again, so nothing was saved.";
            return packed;
        }

        public void DiscardChanges()
        {
            Stop();
            _layoutTouched = false;
            Load();
            Dirty = false;
            FillSequences();
        }
    }
}
