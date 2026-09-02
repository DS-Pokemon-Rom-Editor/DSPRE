using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using Avalonia.Media.Imaging;
using DSPRE.Avalonia.Data;

namespace DSPRE.Avalonia.ViewModels.Graphics
{
    /// <summary>
    /// Making a background out of a picture: the colours, the tile sheet and the arrangement, with a
    /// look at exactly what the game would draw before anything is written.
    /// </summary>
    public sealed class TilesetBuilderViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
        private bool Set<T>(ref T f, T v, [CallerMemberName] string n = null)
        {
            if (EqualityComparer<T>.Default.Equals(f, v)) return false;
            f = v; OnPropertyChanged(n); return true;
        }

        private byte[] _rgba;
        private int _w, _h;
        private TilesetBuilder.Result _built;

        // ── the picture ───────────────────────────────────────────────────────────────────────────

        private string _pictureName;
        public string PictureName { get => _pictureName; private set => Set(ref _pictureName, value); }

        public bool HasPicture => _rgba != null;
        public bool HasNoPicture => _rgba == null;

        private Bitmap _before;
        public Bitmap Before { get => _before; private set => Set(ref _before, value); }

        private Bitmap _after;
        public Bitmap After { get => _after; private set => Set(ref _after, value); }

        /// <summary>Loads a picture and works out what it would come to. Returns why not, or null.</summary>
        public string Open(string path)
        {
            byte[] file;
            try { file = File.ReadAllBytes(path); }
            catch (Exception ex) { return "That file could not be read: " + ex.Message; }

            if (!AnyPng.TryReadRgba(file, out byte[] rgba, out int w, out int h, out string whynot))
                return whynot;

            _rgba = rgba; _w = w; _h = h;
            PictureName = Path.GetFileName(path);
            Before = ImageConverter.FromRgba(rgba, w, h);
            OnPropertyChanged(nameof(HasPicture));
            OnPropertyChanged(nameof(HasNoPicture));
            Rebuild();
            return null;
        }

        // ── the choices ───────────────────────────────────────────────────────────────────────────

        private bool _eightBit;
        /// <summary>One bank of 256 colours instead of sixteen banks of sixteen.</summary>
        public bool EightBit
        {
            get => _eightBit;
            set { if (Set(ref _eightBit, value)) { OnPropertyChanged(nameof(DepthNote)); Rebuild(); } }
        }

        public string DepthNote => _eightBit
            ? "One list for the whole picture. More colours, but twice the room."
            : "Each square picks a bank of sixteen. What almost every background here uses.";

        private bool _keepClearSlot = true;
        /// <summary>Leave the first colour of every bank clear, so whatever is behind shows through.</summary>
        public bool KeepClearSlot
        {
            get => _keepClearSlot;
            set { if (Set(ref _keepClearSlot, value)) Rebuild(); }
        }

        // ── what came of it ───────────────────────────────────────────────────────────────────────

        private string _whynot;
        /// <summary>Why nothing could be made. Null when something was.</summary>
        public string Whynot { get => _whynot; private set => Set(ref _whynot, value); }
        public bool Refused => _whynot != null;

        public ObservableCollection<string> Notes { get; } = new();
        public bool HasNotes => Notes.Count > 0;

        public ObservableCollection<Fact> Facts { get; } = new();
        public sealed class Fact
        {
            public string Name { get; init; }
            public string Value { get; init; }
            public string Detail { get; init; }
        }

        public bool CanSave => _built?.Whynot == null && _built?.Tiles != null;

        /// <summary>Why the button is greyed out. Nothing when it is not.</summary>
        public string CannotSaveBecause => _rgba == null ? "Open a PNG first." : null;

        /// <summary>A refusal already says why, right above the button, so it is not repeated there.</summary>
        public bool ShowSaveReason => CannotSaveBecause != null;

        private void Rebuild()
        {
            Notes.Clear();
            Facts.Clear();
            After = null;
            _built = null;

            if (_rgba == null) { Whynot = null; RaiseAll(); return; }

            _built = TilesetBuilder.Build(_rgba, _w, _h, _eightBit, _keepClearSlot);
            Whynot = _built.Whynot;

            foreach (string n in _built.Notes) Notes.Add(n);

            if (_built.Whynot == null)
            {
                var img = NitroBgCodec.Composite(_built.Tiles, _built.Colours, _built.Arrangement,
                                                 _built.ClearSlotKept);
                After = ImageConverter.FromRgba(img.Rgba, img.Width, img.Height);

                int shared = _built.RepeatedAsIs + _built.RepeatedTurnedOver;
                Facts.Add(new Fact { Name = "Size", Value = $"{_built.Width} by {_built.Height}",
                    Detail = $"{_built.Squares} squares of eight pixels" });
                Facts.Add(new Fact { Name = "Tiles", Value = _built.TilesKept.ToString(),
                    Detail = $"out of {TilesetBuilder.MostTiles} an arrangement can point at" });
                Facts.Add(new Fact { Name = "Shared", Value = shared.ToString(),
                    Detail = $"{_built.RepeatedAsIs} squares repeat a tile as it is, "
                           + $"{_built.RepeatedTurnedOver} repeat one turned over" });
                Facts.Add(new Fact { Name = "Colour banks", Value = _built.Banks.ToString(),
                    Detail = _eightBit ? "one list of 256, which is what 256 colours means"
                                       : $"out of {TilesetBuilder.MostBanks} a screen holds" });
                Facts.Add(new Fact { Name = "Colours", Value = _built.ColoursKept.ToString(),
                    Detail = _built.ClearSlotKept ? "with the first slot of each bank left clear"
                           : _keepClearSlot ? "nothing in this picture is see-through, so no slot had to "
                                            + "be held clear and every colour of a bank is free"
                           : "with no slot held clear" });
                Facts.Add(new Fact { Name = "Room taken", Value = Size(_built.Colours.Length
                                                                     + _built.Tiles.Length
                                                                     + _built.Arrangement.Length),
                    Detail = $"{Size(_built.Colours.Length)} of colours, {Size(_built.Tiles.Length)} of "
                           + $"tiles, {Size(_built.Arrangement.Length)} of arrangement" });
            }

            RaiseAll();
        }

        private static string Size(int bytes) => bytes < 1024 ? bytes + " bytes" : (bytes / 1024) + "kb";

        private void RaiseAll()
        {
            foreach (string n in new[] { nameof(Refused), nameof(HasNotes), nameof(CanSave),
                                         nameof(CannotSaveBecause), nameof(ShowSaveReason), nameof(Summary) })
                OnPropertyChanged(n);
        }

        public string Summary => _rgba == null
            ? "Open a PNG to see what it would come to."
            : _built?.Whynot ?? _built?.Summary ?? "";

        // ── writing it out ────────────────────────────────────────────────────────────────────────

        /// <summary>The three files, under the name given, beside each other. Returns why not, or null.</summary>
        public string Save(string basePath)
        {
            if (!CanSave) return CannotSaveBecause ?? _built?.Whynot ?? "There is nothing to save.";
            try
            {
                string dir = Path.GetDirectoryName(basePath);
                string stem = Path.GetFileNameWithoutExtension(basePath);
                File.WriteAllBytes(Path.Combine(dir, stem + ".NCLR"), _built.Colours);
                File.WriteAllBytes(Path.Combine(dir, stem + ".NCGR"), _built.Tiles);
                File.WriteAllBytes(Path.Combine(dir, stem + ".NSCR"), _built.Arrangement);
                SavedAs = $"{stem}.NCLR, {stem}.NCGR and {stem}.NSCR written beside each other in {dir}.";
                OnPropertyChanged(nameof(SavedAs));
                return null;
            }
            catch (Exception ex) { return "Those files could not be written: " + ex.Message; }
        }

        public string SavedAs { get; private set; }

        /// <summary>The three files as they stand, for putting straight into a ROM.</summary>
        public (byte[] Colours, byte[] Tiles, byte[] Arrangement) Files
            => (_built?.Colours, _built?.Tiles, _built?.Arrangement);
    }
}
