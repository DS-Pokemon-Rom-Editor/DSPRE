using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using DSPRE.Avalonia.Data;
using DSPRE.Editors;
using DSPRE.ROMFiles;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.ViewModels.Graphics
{
    public sealed partial class BottomScreenEditorViewModel
    {
        public bool CanUndo => Pending.CanUndo;
        public bool CanRedo => Pending.CanRedo;

        public void Undo() { if (!Pending.CanUndo) return; Pending.Undo(); ReloadAfterImport(); StatusText = ""; }
        public void Redo() { if (!Pending.CanRedo) return; Pending.Redo(); ReloadAfterImport(); StatusText = ""; }

        private void RaiseSteps()
        {
            OnPropertyChanged(nameof(CanUndo));
            OnPropertyChanged(nameof(CanRedo));
            OnPropertyChanged(nameof(HasUnsavedChanges));
        }


        public void Say(string what) => StatusText = what;

        private static uint ToArgb(ushort bgr555)
        {
            (byte r, byte g, byte b) = NitroBgCodec.Colour(bgr555);
            return 0xFF000000u | ((uint)r << 16) | ((uint)g << 8) | b;
        }
    }
}
