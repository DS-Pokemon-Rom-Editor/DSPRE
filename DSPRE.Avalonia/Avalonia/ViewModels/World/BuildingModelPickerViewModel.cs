using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using DSPRE.Avalonia.Data;
using DSPRE.Avalonia.Gl;
using LibNDSFormats.NSBMD;
using LibNDSFormats.NSBTX;

namespace DSPRE.Avalonia.ViewModels.World
{
    public sealed class BuildingModelPickerViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

        public sealed class Entry
        {
            public int Id { get; init; }
            public string Name { get; init; }
            public override string ToString() => Name == null ? Id.ToString("D3") : $"{Id:D3}  {Name}";
        }

        private readonly string _modelDir;
        private readonly byte[] _textures;
        private List<Entry> _all = new List<Entry>();

        public ObservableCollection<Entry> Shown { get; } = new ObservableCollection<Entry>();

        public event EventHandler PreviewChanged;
        public NsbmdRenderModel Preview { get; private set; }

        private string _search = "";
        public string Search
        {
            get => _search;
            set { _search = value ?? ""; OnPropertyChanged(); Filter(); }
        }

        private Entry _selected;
        public Entry Selected
        {
            get => _selected;
            set { if (ReferenceEquals(_selected, value)) return; _selected = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasSelection)); LoadPreview(); }
        }
        public bool HasSelection => _selected != null;

        private string _status = "";
        public string StatusText { get => _status; private set { _status = value; OnPropertyChanged(); } }

        private readonly int _current;

        // Design-time
        public BuildingModelPickerViewModel() { }

        /// <param name="texturePack">The building texture file the map uses, or null to show the models' own.</param>
        public BuildingModelPickerViewModel(string modelDir, string texturePack, int current)
        {
            _modelDir = modelDir;
            _current = current;
            try { _textures = texturePack != null && File.Exists(texturePack) ? File.ReadAllBytes(texturePack) : null; }
            catch (IOException) { _textures = null; }
        }

        public async Task LoadAsync()
        {
            string dir = _modelDir;
            _all = await Task.Run(() =>
            {
                List<Entry> list = new List<Entry>();
                if (dir == null || !Directory.Exists(dir)) return list;
                int count = NarcAPI.Narc.MemberCount(dir);
                for (int i = 0; i < count; i++)
                {
                    string name = null;
                    try { name = ModelAssets.NameInFile(File.ReadAllBytes(Path.Combine(dir, i.ToString("D4")))); }
                    catch (IOException) { }   // a model that can't be read is listed without a name
                    list.Add(new Entry { Id = i, Name = name });
                }
                return list;
            });
            Filter();
            Selected = _all.FirstOrDefault(e => e.Id == _current) ?? Shown.FirstOrDefault();
        }

        private void Filter()
        {
            Entry keep = _selected;
            Shown.Clear();
            foreach (Entry e in _all)
                if (_search.Length == 0 || SearchMatch.Contains(e.ToString(), _search)) Shown.Add(e);
            if (keep != null && !Shown.Contains(keep)) Selected = Shown.FirstOrDefault();
        }

        private void LoadPreview()
        {
            Preview = null;
            if (_selected != null)
            {
                try
                {
                    byte[] data = File.ReadAllBytes(Path.Combine(_modelDir, _selected.Id.ToString("D4")));
                    NSBMD nsbmd = NSBMDLoader.LoadNSBMD(new MemoryStream(data));
                    byte[] tex = _textures ?? NSBUtils.GetTexturesFromTexturedNSBMD(data);
                    bool painted = ModelAssets.Dress(nsbmd, tex);
                    if (nsbmd.Models != null && nsbmd.Models.Length > 0) Preview = NsbmdGeometry.BuildModel(nsbmd.Models[0]);
                    StatusText = painted ? "" : "Shown unpainted: this map's building textures don't fit it.";
                }
                catch (Exception ex) when (ex is IOException || ex is ArgumentException || ex is IndexOutOfRangeException || ex is InvalidDataException)
                {
                    StatusText = "Can't show model " + _selected.Id + ": " + ex.Message;
                }
            }
            PreviewChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
