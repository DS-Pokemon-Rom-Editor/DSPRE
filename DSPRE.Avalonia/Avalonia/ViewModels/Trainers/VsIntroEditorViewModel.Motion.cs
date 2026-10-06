using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using DSPRE.Avalonia.Data;
using DSPRE.ROMFiles;
using Kind = DSPRE.Avalonia.Data.GraphicAssets.Kind;

namespace DSPRE.Avalonia.ViewModels.Trainers
{
    /// <summary>One emblem of the grunt intro, as editable whole numbers.</summary>
    public sealed class FlightRow : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private readonly VsIntroMotion.Flight _f;
        private readonly Action _changed;

        public FlightRow(int number, VsIntroMotion.Flight f, Action changed) { Number = number; _f = f; _changed = changed; }

        public int Number { get; }
        public string Title => "Emblem " + Number;

        private decimal Get(Func<VsIntroMotion.Flight, int> read) => read(_f);
        private void Put(Action<VsIntroMotion.Flight> write, [CallerMemberName] string n = null)
        {
            write(_f);
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
            _changed();
        }

        public decimal StartX { get => Get(f => f.StartX); set => Put(f => f.StartX = (int)value); }
        public decimal StartY { get => Get(f => f.StartY); set => Put(f => f.StartY = (int)value); }
        public decimal EndX { get => Get(f => f.EndX); set => Put(f => f.EndX = (int)value); }
        public decimal EndY { get => Get(f => f.EndY); set => Put(f => f.EndY = (int)value); }
        public decimal SpeedX { get => Get(f => f.SpeedX); set => Put(f => f.SpeedX = (int)value); }
        public decimal SpeedY { get => Get(f => f.SpeedY); set => Put(f => f.SpeedY = (int)value); }
        public decimal Wait { get => Get(f => f.Wait); set => Put(f => f.Wait = (int)value); }
        public decimal Turns { get => Get(f => f.Turns); set => Put(f => f.Turns = (int)value); }
    }

    /// <summary>The motion tables the intro code reads: the grunt intro's emblem flights and the block wipe's column order.</summary>
    public partial class VsIntroEditorViewModel
    {
        private VsIntroMotion _motion;
        private string _motionWhy;

        public bool HasMotion => _motion != null;
        public string MotionNote => _motionWhy ?? "";
        public bool ShowMotionNote => _motion == null && !string.IsNullOrEmpty(_motionWhy);
        public ObservableCollection<FlightRow> FlightRows { get; } = new();

        public string GruntIntroName => RomInfo.gameFamily == RomInfo.GameFamilies.HGSS ? "Team Rocket grunt" : "Team Galactic grunt";

        private void LoadMotion()
        {
            try { _motion = VsIntroMotion.Load(out _motionWhy); }
            catch (Exception e) { _motion = null; _motionWhy = "The intro motion couldn't be read: " + e.Message; }
        }

        private void ReadyMotion()
        {
            FlightRows.Clear();
            if (_motion != null)
                for (int i = 0; i < _motion.Flights.Count; i++) FlightRows.Add(new FlightRow(i + 1, _motion.Flights[i], MotionChanged));
            _blockOrderText = _motion == null ? "" : string.Join(", ", _motion.BlockOrder);
            Raise(nameof(HasMotion), nameof(MotionNote), nameof(ShowMotionNote), nameof(BlockOrderText), nameof(BlockOrderError));
            RenderMotion();
        }

        private void MotionChanged()
        {
            _undo?.Record();
            Raise(nameof(HasUnsavedChanges));
            RenderMotion();
            // A class intro playing the grunt or block wipe style shows the same tables.
            if (!Animating) RenderPreview();
        }

        private string _blockOrderText = "", _blockOrderError = "";
        public string BlockOrderError { get => _blockOrderError; private set { if (_blockOrderError == value) return; _blockOrderError = value; Raise(); } }

        /// <summary>The eight columns in the order their blocks fall, as typed.</summary>
        public string BlockOrderText
        {
            get => _blockOrderText;
            set
            {
                if (_blockOrderText == value) return;
                _blockOrderText = value;
                Raise();
                if (_motion == null) return;
                int[] order = (value ?? "").Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                                           .Select(t => int.TryParse(t, out int n) ? n : -1).ToArray();
                if (!VsIntroMotion.IsColumnOrder(order)) { BlockOrderError = "List each column from 0 to 7 once."; return; }
                BlockOrderError = "";
                order.CopyTo(_motion.BlockOrder, 0);
                MotionChanged();
            }
        }

        private void RestoreMotion(byte[] state)
        {
            if (_motion == null || state.Length == 0) return;
            _motion.Restore(state);
            ReadyMotion();
            Raise(nameof(HasUnsavedChanges));
            if (!Animating) RenderPreview();
        }

        /// <summary>Puts back the shipped tables' values from the last save or load.</summary>
        private void ReloadMotion()
        {
            LoadMotion();
            ReadyMotion();
        }

        private async Task<bool> SaveMotionAsync()
        {
            if (_motion == null || !_motion.HasChanges) return true;
            string trouble;
            try { trouble = _motion.Save(); }
            catch (Exception e) when (e is System.IO.IOException || e is UnauthorizedAccessException) { trouble = e.Message; }
            if (trouble == null) return true;
            await DialogHelper.ShowError("The intro motion was not saved:\n" + trouble, Title);
            return false;
        }

        // ── Its own preview ──────────────────────────────────────────────────────────────────────

        private int[] Emblem()
        {
            int[] m = RomInfo.VsIntroMotionCodeSites?.EmblemMembers;
            Kind[] want = { Kind.Palette, Kind.TileGraphic, Kind.CellLayout, Kind.CellAnimation };
            if (m == null || m.Length != 4) return null;
            for (int i = 0; i < 4; i++)
                if (m[i] < 0 || m[i] >= _kinds.Length || _kinds[m[i]] != want[i]) return null;
            return m;
        }

        private int _motionShow;
        /// <summary>0 plays the grunt intro, 1 the block wipe.</summary>
        public int MotionShow { get => _motionShow; set { if (_motionShow == value) return; _motionShow = value; Raise(); StopMotion(); RenderMotion(); } }
        public List<string> MotionShows => new() { GruntIntroName, "Block wipe" };

        private VsIntroPreview.Scene MotionScene()
        {
            if (_motion == null || _preview == null) return null;
            return _motionShow == 0
                ? new VsIntroPreview.Scene
                {
                    Kind = VsIntroPreview.Layout.Special, Special = VsIntroPreview.SpecialVariant.Rocket,
                    Emblem = Emblem(), Flights = _motion.Flights,
                }
                : new VsIntroPreview.Scene
                {
                    Kind = VsIntroPreview.Layout.Balls, Variant = VsIntroPreview.BallVariant.CaveLate, BlockOrder = _motion.BlockOrder,
                };
        }

        private global::Avalonia.Media.Imaging.Bitmap _motionImage;
        public global::Avalonia.Media.Imaging.Bitmap MotionImage { get => _motionImage; private set { _motionImage = value; Raise(); } }

        private int _motionFrame = -1;
        private global::Avalonia.Threading.DispatcherTimer _motionTimer;
        public bool MotionPlaying => _motionTimer != null;
        public string MotionPlayLabel => MotionPlaying ? "Stop" : "Play";

        private void RenderMotion()
        {
            VsIntroPreview.Scene scene = MotionScene();
            if (scene == null) { MotionImage = null; return; }
            try { MotionImage = ImageConverter.FromRgba(_preview.Draw(scene, _motionFrame), DsBgScreen.Width, DsBgScreen.Height); }
            catch (Exception e) { AppLogger.Error("VS intro motion preview failed: " + e.Message); MotionImage = null; }
        }

        public void ToggleMotion()
        {
            if (MotionPlaying) { StopMotion(); RenderMotion(); return; }
            VsIntroPreview.Scene scene = MotionScene();
            if (scene == null) return;
            int length = VsIntroPreview.Length(scene);
            _motionFrame = 0;
            _motionTimer = new global::Avalonia.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1000.0 / 30) };
            _motionTimer.Tick += (_, _) =>
            {
                if (++_motionFrame >= length) StopMotion();
                RenderMotion();
            };
            _motionTimer.Start();
            Raise(nameof(MotionPlaying), nameof(MotionPlayLabel));
            RenderMotion();
        }

        public void StopMotion()
        {
            _motionTimer?.Stop();
            _motionTimer = null;
            _motionFrame = -1;
            Raise(nameof(MotionPlaying), nameof(MotionPlayLabel));
        }
    }
}
