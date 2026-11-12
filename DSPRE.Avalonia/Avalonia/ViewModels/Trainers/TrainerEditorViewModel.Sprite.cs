using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using global::Avalonia.Controls;
using global::Avalonia.Media.Imaging;
using global::Avalonia.Platform.Storage;
using DSPRE.Avalonia;
using DSPRE.Editors;
using DSPRE.HgEngine;
using DSPRE.ROMFiles;
using static DSPRE.RomInfo;
using Avalonia.Threading;

namespace DSPRE.Avalonia.ViewModels.Trainers
{
    public partial class TrainerEditorViewModel
    {
        private IDisposable _spriteSaved;
        private IDisposable _propertiesSaved;

        // The trainer flag editor writes these records too. Reloading keeps Save from undoing its flags.
        private void OnPropertiesSaved()
        {
            if (IsHgeActive || _trainer == null || _loadedTrainerId < 0) return;
            string path = ArchiveMembers.PathOf(DirNames.TrainerProperties, _loadedTrainerId);
            if (!PlainFiles.Exists(path) || PlainFiles.ReadBytes(path).AsSpan().SequenceEqual(_trainer.Trp.ToByteArray())) return;
            if (_dirty)
            {
                StatusText = $"Saving here replaces trainer {_loadedTrainerId}'s changes from another editor.";
                return;
            }
            _statusAfterLoad = $"Trainer {_loadedTrainerId} was changed in another editor and has been reloaded.";
            LoadTrainer(_loadedTrainerId);
        }

        private void UpdateTrainerSprite()
        {
            StopPlaying();
            if (_trainerClassIndex < 0) { ClassImage = null; return; }
            int maxFrame = _sprite.Load(_trainerClassIndex);
            ClassFrameMax = maxFrame;
            OnPropertyChanged(nameof(HasClassSprite));
            OnPropertyChanged(nameof(CanPlay));
            _classFrame = _sprite.DefaultFrame;
            OnPropertyChanged(nameof(ClassFrame));
            RenderSprite();
        }
        private void RenderSprite() => ClassImage = _sprite.Render((int)_classFrame, 96, 96);

        // Plays the class sprite's frames at their own lengths, as the Classes tab does.
        private global::Avalonia.Threading.DispatcherTimer _animTimer;
        private int _playCountdown;
        private bool _isPlaying;
        public bool IsPlaying { get => _isPlaying; private set { if (Set(ref _isPlaying, value)) OnPropertyChanged(nameof(PlayIcon)); } }
        public string PlayIcon => _isPlaying ? "stop" : "play";
        public bool CanPlay => _classFrameMax > 0;

        public void TogglePlay()
        {
            if (_isPlaying) { StopPlaying(); return; }
            if (!CanPlay) return;
            _animTimer ??= new global::Avalonia.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1000.0 / 60) };
            _animTimer.Tick -= AnimTick;
            _animTimer.Tick += AnimTick;
            _classFrame = 0; OnPropertyChanged(nameof(ClassFrame)); RenderSprite();
            _playCountdown = _sprite.GetFrameDuration(0);
            IsPlaying = true;
            _animTimer.Start();
        }

        private void StopPlaying()
        {
            _animTimer?.Stop();
            if (!_isPlaying) return;
            IsPlaying = false;
            _classFrame = _sprite.DefaultFrame; OnPropertyChanged(nameof(ClassFrame)); RenderSprite();
        }

        private void AnimTick(object sender, EventArgs e)
        {
            if (--_playCountdown > 0) return;
            _classFrame = ((int)_classFrame + 1) % Math.Max(1, (int)_classFrameMax + 1);
            OnPropertyChanged(nameof(ClassFrame));
            RenderSprite();
            _playCountdown = _sprite.GetFrameDuration((int)_classFrame);
        }
    }
}
