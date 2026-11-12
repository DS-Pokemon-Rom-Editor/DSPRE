using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using DSPRE.HgEngine;
using DSPRE.ROMFiles;
using DSPRE.Resources;
using DSPRE.Avalonia.Views.Shell;
using DSPRE.Csv;
using IEditorWithUnsavedChanges = global::DSPRE.Editors.IEditorWithUnsavedChanges;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.ViewModels.Pokemon
{
    public partial class PersonalDataEditorViewModel
    {
        private int _owGfxIndex = -1;
        private bool _owHasSpriteFiles;
        private List<string> _owSizeClassValues = new();

        public ObservableCollection<string> OwSizeClassOptions { get; } = new();

        private bool _owHasEntry;
        public bool OwHasEntry { get => _owHasEntry; private set => Set(ref _owHasEntry, value); }

        private string _owStatusText = "";
        public string OwStatusText { get => _owStatusText; private set => Set(ref _owStatusText, value); }

        private Bitmap[] _owFrames = Array.Empty<Bitmap>();
        public Bitmap OwFollowerPreview => _owFrameIndex >= 0 && _owFrameIndex < _owFrames.Length ? _owFrames[_owFrameIndex] : null;

        public int OwFrameCount => _owFrames.Length;
        public int OwMaxFrameIndex => Math.Max(0, _owFrames.Length - 1);

        private int _owFrameIndex;
        public int OwFrameIndex
        {
            get => _owFrameIndex;
            set
            {
                if (_owFrames.Length == 0) return;
                int clamped = Math.Clamp(value, 0, _owFrames.Length - 1);
                if (!Set(ref _owFrameIndex, clamped)) return;
                OnPropertyChanged(nameof(OwFollowerPreview));
            }
        }

        private bool _owAnimating;
        public bool OwAnimating
        {
            get => _owAnimating;
            set { if (Set(ref _owAnimating, value)) OnPropertyChanged(nameof(OwAnimateButtonText)); }
        }
        public string OwAnimateButtonText => OwAnimating ? "Stop" : "Animate";
        public bool OwCanAnimate => _owFrames.Length > 1;

        private readonly global::Avalonia.Threading.DispatcherTimer _owFrameTimer =
            new() { Interval = TimeSpan.FromMilliseconds(150) };

        private void WireOwFrameTimer()
        {
            _owFrameTimer.Tick += (_, _) =>
            {
                if (OwAnimating) OwFrameIndex = (OwFrameIndex + 1) % Math.Max(1, _owFrames.Length);
            };
            _owFrameTimer.Start();
        }

        // Shown when the species has no sheet yet: ImportFollower copies a template's layout and palettes.
        public bool OwNeedsTemplate => _owHasEntry && !_owHasSpriteFiles;

        private int _owTemplateSpeciesIndex = -1;
        public int OwTemplateSpeciesIndex { get => _owTemplateSpeciesIndex; set => Set(ref _owTemplateSpeciesIndex, value); }

        private int _owSizeClassIndex = -1;
        public int OwSizeClassIndex
        {
            get => _owSizeClassIndex;
            set { if (!RejectCleared(value, nameof(OwSizeClassIndex)) && Set(ref _owSizeClassIndex, value) && _owHasEntry) StageHg(); }
        }

        // Done by Save, dropped by Discard or a species switch.
        private bool _owPendingCreate;
        private string _owPendingSpritePath;
        private string _owPendingShinyPath;
        private string _owDiskPngPath;
        private string _owPreviewPath;

        private string _owPendingText = "";
        public string OwPendingText
        {
            get => _owPendingText;
            private set { if (Set(ref _owPendingText, value)) OnPropertyChanged(nameof(HasOwPending)); }
        }
        public bool HasOwPending => _owPendingText.Length > 0;
        public bool CanCreateOwEntry => !_owHasEntry && !_owPendingCreate;

        private void RefreshOwPending()
        {
            List<string> parts = new List<string>();
            if (_owPendingCreate) parts.Add("The follower entry is created on save.");
            if (_owPendingSpritePath != null) parts.Add($"{Path.GetFileName(_owPendingSpritePath)} is imported on save"
                + (_owPendingShinyPath != null ? $", with {Path.GetFileName(_owPendingShinyPath)} as its shiny colours." : "."));
            OwPendingText = string.Join(" ", parts);
            OnPropertyChanged(nameof(CanCreateOwEntry));

            string preview = _owPendingSpritePath ?? _owDiskPngPath;
            if (preview == _owPreviewPath) return;
            _owPreviewPath = preview;
            Bitmap[] frames = Array.Empty<Bitmap>();
            if (preview != null)
            {
                try { frames = DSPRE.Avalonia.ImageConverter.LoadHgeOverworldFrames(preview); }
                catch (Exception ex) { AppLogger.WarnOnce("PersonalDataEditorViewModel.RefreshOwPending", ex); frames = Array.Empty<Bitmap>(); }
            }
            SetOwFrames(frames);
        }

        private void SetOwFrames(Bitmap[] frames)
        {
            _owFrames = frames;
            _owFrameIndex = 0;
            OwAnimating = false;
            OnPropertyChanged(nameof(OwFollowerPreview));
            OnPropertyChanged(nameof(OwFrameIndex));
            OnPropertyChanged(nameof(OwFrameCount));
            OnPropertyChanged(nameof(OwMaxFrameIndex));
            OnPropertyChanged(nameof(OwCanAnimate));
        }

        private void LoadOwFollower()
        {
            SetOwFrames(Array.Empty<Bitmap>());
            _owHasSpriteFiles = false;
            _owDiskPngPath = null;
            _owPreviewPath = null;

            if (!DSPRE.HgEngine.HgEngineProject.IsActive)
            {
                OwHasEntry = false;
                OwStatusText = "";
            }
            else if (!HgEngineOverworldFollowerSprite.TryGetAssignment(_currentId, out _owGfxIndex, out string sizeClass, out _))
            {
                OwHasEntry = false;
                OwStatusText = "No overworld follower entry for this species yet.";
            }
            else
            {
                OwHasEntry = true;
                _owSizeClassValues = HgEngineOverworldFollowerSprite.GetSizeClassOptions();
                OwSizeClassOptions.Clear();
                foreach (string s in _owSizeClassValues) OwSizeClassOptions.Add(s);
                _owSizeClassIndex = _owSizeClassValues.IndexOf(sizeClass);
                OnPropertyChanged(nameof(OwSizeClassIndex));

                // pokegra.mk builds every follower from the species' own sprites/<name>/overworld.png.
                _owDiskPngPath = HgEngineOverworldSprite.TryGetSpritePngPath(_currentId, out string ownPath) ? ownPath : null;
                _owHasSpriteFiles = _owDiskPngPath != null;
                OwStatusText = $"Overworld graphic {_owGfxIndex}" + (_owHasSpriteFiles ? "" : " (no sprite art yet, import one below)");
            }
            OnPropertyChanged(nameof(OwNeedsTemplate));
            RefreshOwPending();
        }

        public void CreateOwFollowerEntry()
        {
            if (_current == null || !CanCreateOwEntry) return;
            _owPendingCreate = true;
            RefreshOwPending();
            SetDirty();
        }

        /// <summary>Stages the sheet and an optional shiny recolour, then previews it.</summary>
        public void ImportOwFollowerSprite(string pngSourcePath, string shinyPngPath = null)
        {
            if (_current == null || !_owHasEntry || _owGfxIndex < 0) return;
            _owPendingSpritePath = pngSourcePath;
            _owPendingShinyPath = shinyPngPath;
            RefreshOwPending();
            SetDirty();
            SaveNotice.Imported("the follower sprites", pngSourcePath);
        }
    }
}
