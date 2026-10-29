using System;
using System.Collections.Generic;
using System.IO;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using DSPRE.Avalonia.ViewModels.Pokemon;
using DSPRE.Avalonia.Views.Controls;
using DSPRE.ROMFiles;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.Views.Pokemon
{
    public partial class FollowerTalkPreviewView : UserControl
    {
        private FollowerTalkPreviewViewModel _vm;

        public FollowerTalkPreviewView()
        {
            InitializeComponent();
            DataContextChanged += (_, _) => Attach(DataContext as FollowerTalkPreviewViewModel);
        }

        private void Attach(FollowerTalkPreviewViewModel vm)
        {
            if (_vm != null) { _vm.Redraw -= Draw; _vm.StageChanged -= ShowStage; }
            _vm = vm;
            if (vm == null) return;
            vm.Redraw += Draw;
            vm.StageChanged += ShowStage;
            ShowStage();
        }

        private void ShowStage()
        {
            if (_vm?.CurrentStage is not { } s) return;
            Field.Show(s.Header, s.Camera, (s.X, s.Z));
            Draw();
        }

        // The follower stands one tile right of the player, who faces it.
        private const int Tile = OverworldSprites.PixelsPerTile;

        private void Draw()
        {
            FollowerTalkPreviewViewModel p = _vm;
            if (p?.CurrentStage == null) return;
            List<CameraPreview.Actor> actors = new List<CameraPreview.Actor>();
            int right = (int)MoveFacing.Right;
            actors.Add(new CameraPreview.Actor(OverworldSprites.Get(0, (ushort)right, FieldSpriteAnimation.PictureFor(OverworldSprites.FrameCount(0), right, null)), 0, 0, 0));

            int entry = p.FollowerEntry;
            int count = entry < 0 ? 0 : OverworldSprites.FrameCount((ushort)entry);
            OverworldSprites.SpritePixels follower = null;
            if (count > 0)
            {
                int per = Math.Max(1, count / 4);
                follower = OverworldSprites.Get((ushort)entry, (ushort)p.Facing, p.Facing * per + (p.Tick >> 4) % per);
                actors.Add(new CameraPreview.Actor(follower, Tile + p.X, p.Z, p.Hop));
            }
            List<OverworldSprites.SpritePixels> bubble = BubbleFrames(p.Bubble);
            if (bubble != null && bubble.Count > 0)
                actors.Add(new CameraPreview.Actor(bubble[p.BubbleFrame % bubble.Count], Tile + p.X, p.Z, p.Hop + (follower?.Height ?? 32) - 8));
            Field.ShowActors(actors);
        }

        // Bubble N is field effect member N + 1, each with two pictures, read once.
        private List<OverworldSprites.SpritePixels>[] _bubbles;

        private List<OverworldSprites.SpritePixels> BubbleFrames(int bubble)
        {
            if (bubble <= 0) return null;
            if (_bubbles == null)
            {
                _bubbles = new List<OverworldSprites.SpritePixels>[FollowerTalk.BubbleCount + 1];
                try
                {
                    DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.FieldEffectModels });
                    for (int i = 1; i <= FollowerTalk.BubbleCount; i++)
                    {
                        string path = ArchiveMembers.PathOf(DirNames.FieldEffectModels, i + 1);
                        _bubbles[i] = File.Exists(path) ? OverworldSprites.Pictures(File.ReadAllBytes(path)) : new List<OverworldSprites.SpritePixels>();
                    }
                }
                catch (Exception e) when (e is IOException || e is InvalidDataException) { AppLogger.WarnOnce("FollowerTalkPreview.Bubbles", e); }
            }
            return bubble < _bubbles.Length ? _bubbles[bubble] : null;
        }

        private void Play_Click(object sender, RoutedEventArgs e) => _vm?.Replay();
        private void Next_Click(object sender, RoutedEventArgs e) => _vm?.PressA();
        private void Stop_Click(object sender, RoutedEventArgs e) => _vm?.Stop();
        private void Yes_Click(object sender, RoutedEventArgs e) => _vm?.Answer(true);
        private void No_Click(object sender, RoutedEventArgs e) => _vm?.Answer(false);
        private void Field_PointerPressed(object sender, PointerPressedEventArgs e) => _vm?.PressA();
    }
}
