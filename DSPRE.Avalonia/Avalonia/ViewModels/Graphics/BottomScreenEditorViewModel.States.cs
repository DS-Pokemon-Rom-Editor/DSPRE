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
        private bool _scriptRunning;
        public bool ScriptRunning { get => _scriptRunning; set { if (Set(ref _scriptRunning, value)) Draw(); } }

        private bool _menuOpen;
        public bool MenuOpen { get => _menuOpen; set { if (Set(ref _menuOpen, value)) Draw(); } }

        private bool _shoesShown = true;
        /// <summary>The shoes only show once the player has them, and not on the bicycle.</summary>
        public bool ShoesShown { get => _shoesShown; set { if (Set(ref _shoesShown, value)) Draw(); } }

        private bool _shoesOn;
        public bool ShoesOn { get => _shoesOn; set { if (Set(ref _shoesOn, value)) Draw(); } }

        private bool _registeredItems = true;
        public bool RegisteredItems { get => _registeredItems; set { if (Set(ref _registeredItems, value)) Draw(); } }

        public string[] ModeNames => HgssTouchScreen.ModeNames;

        private FieldTouchMenuButtons.MenuExtras _menuExtras;
        private int _modeIndex;
        public int ModeIndex { get => _modeIndex; set { if (Set(ref _modeIndex, value)) { _menuExtras = null; Draw(); } } }

        private bool _aHeld;
        public bool AHeld { get => _aHeld; set { if (Set(ref _aHeld, value)) Draw(); } }

        public ObservableCollection<string> ALabelNames { get; } = new();

        private int _aLabel;
        public int ALabelIndex { get => _aLabel; set { if (Set(ref _aLabel, value)) Draw(); } }

        private int _choiceCount = 2;
        public int ChoiceCount { get => _choiceCount; set { if (Set(ref _choiceCount, Math.Clamp(value, 2, 8))) Draw(); } }

        private bool _yesNo = true;
        public bool YesNo { get => _yesNo; set { if (Set(ref _yesNo, value)) Draw(); } }

        private bool _female;
        public bool Female { get => _female; set { if (Set(ref _female, value)) Draw(); } }

        private bool _backlight;
        public bool Backlight { get => _backlight; set { if (Set(ref _backlight, value)) Draw(); } }

        private bool _noPoketchYet;
        public bool NoPoketchYet { get => _noPoketchYet; set { if (Set(ref _noPoketchYet, value)) Draw(); } }

        private int _theme;
        public int Theme { get => _theme; set { if (Set(ref _theme, Math.Max(0, value))) Draw(); } }

        public ObservableCollection<string> AppNames { get; } = new();

        private int _app;
        /// <summary>0 is the Pokétch frame itself; 1 and up pick an application.</summary>
        public int SelectedApp
        {
            get => _app;
            set { if (Set(ref _app, value)) { Stop(); LoadAnimation(); Refresh(); } }
        }

        /// <summary>The game's Id for the app shown (not its list index), or -1 for the frame.</summary>
        public int PoketchAppId =>
            _app >= 1 && _app <= PoketchApps.All.Length ? PoketchApps.All[_app - 1].Id : -1;
    }
}
