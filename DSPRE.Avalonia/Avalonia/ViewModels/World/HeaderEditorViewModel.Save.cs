using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using global::Avalonia.Collections;
using global::Avalonia.Controls;
using global::Avalonia.Threading;
using global::Avalonia.Media;
using global::Avalonia.Media.Imaging;
using DSPRE.Avalonia;
using DSPRE.Avalonia.Models;
using DSPRE.Editors;
using DSPRE.Resources;
using DSPRE.ROMFiles;
using static DSPRE.RomInfo;
using DSPRE.Avalonia.Data;
using Avalonia.Platform.Storage;

namespace DSPRE.Avalonia.ViewModels.World
{
    public partial class HeaderEditorViewModel
    {
        public void Save()
        {
            if (_header == null) return;
            MapHeader.Save(_header);

            UpdateCurrentInternalName();
            SetClean();
            SaveNotice.Saved(UnsavedChangesDescription);
            _history.MarkSaved();
            RaiseUndoState();
            StatusText = $"Header {_header.ID} saved.";
            AppEvents.RaiseHeaderSaved(this, _header.ID);
        }
    }
}
