using System;
using DSPRE.ROMFiles;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.ViewModels.World
{
    /// <summary>Map model editor window: Tiles and Shape tabs over the same map file.</summary>
    public class MapModelEditorViewModel : INotifyPropertyChanged
    {
        public MapTilesViewModel Tiles { get; } = new MapTilesViewModel();
        public MapGeometryViewModel Shape { get; } = new MapGeometryViewModel();

        public string Which { get; private set; } = "";

        /// <summary>Headers that show this map file; the map editor fills this in.</summary>
        public Func<List<ushort>> HeadersOfMap { get; set; }

        private string _shared, _sharedTip;
        public string Shared { get => _shared; private set => Set(ref _shared, value); }
        public string SharedTip { get => _sharedTip; private set => Set(ref _sharedTip, value); }

        // Edits land in the map file, so every header that shows it gets them.
        public void CountHeaders()
        {
            var headers = HeadersOfMap?.Invoke();
            Shared = headers != null && headers.Count > 1 ? $"Shown by {headers.Count} headers" : null;
            SharedTip = Shared == null ? null : $"Headers {string.Join(", ", headers)} all change with this map.";
        }

        private string _note = "";
        public string Note { get => _note; private set => Set(ref _note, value); }

        public string Unsaved { get => _unsaved; private set => Set(ref _unsaved, value); }
        private string _unsaved;

        public bool Changed => Tiles.Dirty || Shape.Dirty;

        public MapModelEditorViewModel()
        {
            foreach (INotifyPropertyChanged half in new INotifyPropertyChanged[] { Tiles, Shape })
                half.PropertyChanged += (_, e) =>
                {
                    if (e.PropertyName != nameof(MapTilesViewModel.Dirty)) return;
                    Unsaved = Changed
                        ? "Unsaved: save from the map editor."
                        : null;
                    Raise(nameof(Changed));
                };
        }

        public void Open(MapFile map, byte areaId, GameFamilies family, string called)
        {
            Which = string.IsNullOrEmpty(called) ? "Map" : called;
            Raise(nameof(Which));

            Tiles.Open(map, areaId, family);
            Shape.Open(map, areaId, family);

            Unsaved = null;
            Raise(nameof(Changed));
        }

        public void Showing(int half)
        {
            if (half == 0) Tiles.Revisit();
            else Shape.Reread();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void Raise([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        private bool Set<T>(ref T field, T value, [CallerMemberName] string name = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            Raise(name);
            return true;
        }
    }
}
