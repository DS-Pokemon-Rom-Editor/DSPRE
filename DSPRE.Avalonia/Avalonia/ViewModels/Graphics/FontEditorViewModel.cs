using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Avalonia.Controls;
using DSPRE.Avalonia.Data;
using DSPRE.Editors;
using DSPRE.ROMFiles;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.ViewModels.Graphics
{
    /// <summary>One letter in the grid.</summary>
    public sealed class GlyphRow
    {
        public int Index { get; init; }
        public string Letter { get; init; }     // the letter itself, where the character map knows it
        public string Number => Index.ToString();

        /// <summary>Most of a font is kana and symbols an English map never asks for, and a blank
        /// column beside them reads as something missing rather than something unused.</summary>
        public bool IsMapped => !string.IsNullOrEmpty(Letter);

        /// <summary>Whether anything is drawn here. A picture with nothing in it is a free slot.</summary>
        public bool HasPicture { get; init; }

        /// <summary>
        /// What the list says beside the number. Nearly every letter is drawn, so saying so on each row
        /// just shouts about what is normal; and the old line, that a letter was not in the character
        /// map, read as "nothing here" when the picture was right there. Only the rare case is called
        /// out: the couple of dozen slots with nothing in them.
        /// </summary>
        public string Describe => IsMapped ? Letter : HasPicture ? "" : "empty";
    }

    /// <summary>
    /// The letters a ROM writes with: every font it carries, every letter in one, and what a sentence
    /// looks like in it.
    /// </summary>
    public sealed class FontEditorViewModel : INotifyPropertyChanged, IEditorWithUnsavedChanges, ISupportsUndo
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
        private bool Set<T>(ref T f, T v, [CallerMemberName] string n = null)
        {
            if (EqualityComparer<T>.Default.Equals(f, v)) return false;
            f = v; OnPropertyChanged(n); return true;
        }

        private readonly List<int> _fontEntries = new();   // archive entry per row of FontNames
        private FieldFont _font;
        private bool _loading = true;
        private bool _dirty;

        public FontEditorViewModel()
        {
            if (Design.IsDesignMode) { _loading = false; return; }
            try
            {
                DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.fonts });
                // Touching the character map here means the letters are known by the time the list
                // is filled, rather than one load too late.
                try { FieldFontCharacters.GlyphFor('A'); } catch { }
                LoadFontList();
            }
            catch (Exception ex)
            {
                StatusText = "The fonts could not be read: " + ex.Message;
                AppLogger.Error("FontEditor: " + ex);
            }
            _loading = false;
            if (FontNames.Count > 0) SelectedFontIndex = 0;
        }

        // ── The fonts this ROM carries ────────────────────────────────────────────
        public ObservableCollection<string> FontNames { get; } = new();

        private int _selectedFontIndex = -1;
        public int SelectedFontIndex
        {
            get => _selectedFontIndex;
            set { if (Set(ref _selectedFontIndex, value) && !_loading) LoadFont(value); }
        }

        private void LoadFontList()
        {
            FontNames.Clear();
            _fontEntries.Clear();
            if (!gameDirs.TryGetValue(DirNames.fonts, out var dirs)) return;
            string dir = dirs.unpackedDir;
            if (!Directory.Exists(dir)) return;

            var files = Directory.GetFiles(dir).OrderBy(x => x).ToArray();
            for (int i = 0; i < files.Length; i++)
            {
                FieldFont f;
                try { f = FieldFont.Read(File.ReadAllBytes(files[i])); } catch { continue; }
                if (f == null) continue;      // the width tables in the same archive are not fonts
                _fontEntries.Add(i);
                // The games name these themselves; anything unnamed still says which entry it is.
                string named = NamedArchives.NameOf(DirNames.fonts, i);
                FontNames.Add(string.IsNullOrEmpty(named) ? "Font " + i : named);
            }
            StatusText = _fontEntries.Count == 0
                ? "This ROM's font archive holds no fonts this editor can read."
                : $"{_fontEntries.Count} fonts in this ROM.";
        }

        // ── The letters ───────────────────────────────────────────────────────────
        /// <summary>Every letter in the font. Glyphs is what the list shows, once filtered.</summary>
        private readonly List<GlyphRow> _all = new();
        public ObservableCollection<GlyphRow> Glyphs { get; } = new();

        // ── Finding a letter ──────────────────────────────────────────────────────
        //
        // A font holds five hundred odd pictures and only a couple of hundred are written by any
        // character, so without these you scroll blind looking for one letter.

        private string _search = "";
        /// <summary>A letter to look for, or a number to jump straight to.</summary>
        public string Search { get => _search; set { if (Set(ref _search, value)) ApplyFilter(); } }

        private int _showWhat;
        /// <summary>0 all, 1 only drawn, 2 only empty, 3 only in the map, 4 only not in the map.</summary>
        public int ShowWhat { get => _showWhat; set { if (Set(ref _showWhat, value)) ApplyFilter(); } }

        private string _filterSummary;
        public string FilterSummary { get => _filterSummary; private set => Set(ref _filterSummary, value); }

        private void ApplyFilter()
        {
            int keep = _selectedGlyphIndex;
            Glyphs.Clear();

            string looking = (_search ?? "").Trim();
            bool byNumber = int.TryParse(looking, out int wanted);

            foreach (var row in _all)
            {
                bool passes = _showWhat switch
                {
                    1 => row.HasPicture,
                    2 => !row.HasPicture,
                    3 => row.IsMapped,
                    4 => !row.IsMapped,
                    _ => true,
                };
                if (!passes) continue;

                if (looking.Length > 0)
                {
                    bool hit = byNumber ? row.Index == wanted
                             : row.IsMapped && row.Letter.Contains(looking, StringComparison.OrdinalIgnoreCase);
                    if (!hit) continue;
                }
                Glyphs.Add(row);
            }

            FilterSummary = Glyphs.Count == _all.Count
                ? $"{_all.Count} letters"
                : $"{Glyphs.Count} of {_all.Count} letters";

            // keep whatever was being drawn if it survived the filter
            int at = Glyphs.ToList().FindIndex(r => r.Index == keep);
            _selectedGlyphIndex = at >= 0 ? Glyphs[at].Index : (Glyphs.Count > 0 ? Glyphs[0].Index : -1);
            OnPropertyChanged(nameof(SelectedRowIndex));
            RaiseGlyph();
        }

        /// <summary>Which row of the filtered list is picked, which is not the letter's own number.</summary>
        public int SelectedRowIndex
        {
            get => Glyphs.ToList().FindIndex(r => r.Index == _selectedGlyphIndex);
            set
            {
                if (value < 0 || value >= Glyphs.Count) return;
                SelectedGlyphIndex = Glyphs[value].Index;
            }
        }

        private int _selectedGlyphIndex = -1;
        public int SelectedGlyphIndex
        {
            get => _selectedGlyphIndex;
            set { if (Set(ref _selectedGlyphIndex, value)) { RaiseGlyph(); RestartSteps(); } }
        }

        public bool HasGlyph => _font != null && _selectedGlyphIndex >= 0;
        public int CellSize => FieldFont.CellSize;

        private int _glyphWidth;
        public int GlyphWidth
        {
            get => _glyphWidth;
            set
            {
                if (!Set(ref _glyphWidth, value) || _font == null || _selectedGlyphIndex < 0) return;
                _font.SetWidth(_selectedGlyphIndex, value);
                RecordStep();
                MarkDirty();
                RaisePreview();
            }
        }

        public string GlyphTitle => !HasGlyph ? "No letter picked"
            : $"Letter {_selectedGlyphIndex}" + (LetterFor(_selectedGlyphIndex) is string s && s.Length > 0
                                                 ? $", which writes {s}" : "");

        /// <summary>What one spot of the picked letter holds, for the painter.</summary>
        public byte PixelAt(int x, int y) =>
            _font == null || _selectedGlyphIndex < 0 ? (byte)0 : _font.PixelAt(_selectedGlyphIndex, x, y);

        /// <summary>Paints one spot of the picked letter.</summary>
        public void SetPixel(int x, int y, byte value)
        {
            if (_font == null || _selectedGlyphIndex < 0) return;
            if (_font.PixelAt(_selectedGlyphIndex, x, y) == value) return;
            _font.SetPixel(_selectedGlyphIndex, x, y, value);
            RecordStep();
            MarkDirty();
            RaiseGlyph();
        }

        // ── Taking a step back ────────────────────────────────────────────────────
        //
        // A painting tool without undo means one stray click is repaired by hand. Only the letter being
        // worked on is remembered, not the whole font, so a step costs a few hundred bytes.

        private sealed class Step
        {
            public int Glyph;
            public byte[] Pixels;
            public int Width;
        }

        private readonly UndoHistory<Step> _steps = new();
        private DateTime _lastStep = DateTime.MinValue;
        private int _lastStepGlyph = -1;
        private const int SameStrokeMs = 400;

        public bool CanUndo => _steps.CanUndo;
        public bool CanRedo => _steps.CanRedo;

        private Step Snapshot()
        {
            if (_font == null || _selectedGlyphIndex < 0) return null;
            int cell = FieldFont.CellSize;
            var pixels = new byte[cell * cell];
            for (int y = 0; y < cell; y++)
                for (int x = 0; x < cell; x++)
                    pixels[y * cell + x] = _font.PixelAt(_selectedGlyphIndex, x, y);
            return new Step { Glyph = _selectedGlyphIndex, Pixels = pixels, Width = _font.WidthOf(_selectedGlyphIndex) };
        }

        /// <summary>
        /// Remembers the letter as it now stands, which is what the history wants: it records where an
        /// edit arrived, and stepping back hands you where it came from. Recording the state beforehand
        /// instead leaves the last change unrecorded, so undoing a width did nothing.
        /// </summary>
        private void RecordStep()
        {
            var step = Snapshot();
            if (step == null) return;
            // One drag is one step, so long as it stays on the same letter.
            bool sameStroke = (DateTime.UtcNow - _lastStep).TotalMilliseconds < SameStrokeMs
                              && _lastStepGlyph == step.Glyph;
            _lastStepGlyph = step.Glyph;
            _steps.Capture(step, sameStroke);
            _lastStep = DateTime.UtcNow;
            RaiseUndo();
        }

        /// <summary>
        /// Starts the history again on the letter now being worked on. Steps hold one letter each, so a
        /// shared history would let undo put a change back onto whichever letter happened to be first.
        /// </summary>
        private void RestartSteps()
        {
            var step = Snapshot();
            if (step != null) _steps.Reset(step);
            _lastStep = DateTime.MinValue;
            _lastStepGlyph = step?.Glyph ?? -1;
            RaiseUndo();
        }

        public void Undo() { if (_steps.CanUndo) PutBack(_steps.Undo()); }
        public void Redo() { if (_steps.CanRedo) PutBack(_steps.Redo()); }

        private void PutBack(Step step)
        {
            if (step == null || _font == null) return;
            int cell = FieldFont.CellSize;
            for (int y = 0; y < cell; y++)
                for (int x = 0; x < cell; x++)
                    _font.SetPixel(step.Glyph, x, y, step.Pixels[y * cell + x]);
            _font.SetWidth(step.Glyph, step.Width);

            _selectedGlyphIndex = step.Glyph;
            OnPropertyChanged(nameof(SelectedGlyphIndex));
            _glyphWidth = step.Width;
            OnPropertyChanged(nameof(GlyphWidth));
            MarkDirty();
            RaiseGlyph();
            RaiseUndo();
        }

        private void RaiseUndo()
        {
            OnPropertyChanged(nameof(CanUndo));
            OnPropertyChanged(nameof(CanRedo));
        }

        // ── In and out as a picture ───────────────────────────────────────────────
        //
        // Four shades, written out as four greys so any paint program can hold them: nothing, ink,
        // second ink, paper. Reading back, the nearest of those four wins, so a picture that has been
        // through a program that nudged the colours still comes back as the right shades.
        private bool _wholeFontForPictures;
        /// <summary>Whether saving and reading a picture covers every letter or just this one.</summary>
        public bool WholeFontForPictures
        {
            get => _wholeFontForPictures;
            set
            {
                if (Set(ref _wholeFontForPictures, value))
                    OnPropertyChanged(nameof(CanUsePictureCommand));
            }
        }

        /// <summary>A single-letter picture needs a selection; a whole-font sheet does not.</summary>
        public bool CanUsePictureCommand => _font != null && (_wholeFontForPictures || HasGlyph);

        private static readonly byte[] ShadeGrey = { 0x00, 0x60, 0xA0, 0xFF };

        private static byte NearestShade(byte r, byte g, byte b)
        {
            int grey = (r * 299 + g * 587 + b * 114) / 1000;
            byte best = 0;
            int gap = int.MaxValue;
            for (byte i = 0; i < ShadeGrey.Length; i++)
            {
                int d = Math.Abs(grey - ShadeGrey[i]);
                if (d < gap) { gap = d; best = i; }
            }
            return best;
        }

        /// <summary>
        /// Writes out the letter being worked on, or the whole font as a sheet with every letter in its
        /// own cell, thirty two to a row. Returns what went wrong, or null.
        /// </summary>
        public string ExportPng(string path, bool wholeFont)
        {
            if (_font == null) return "No font is open.";
            if (!wholeFont && _selectedGlyphIndex < 0) return "Pick a letter first.";

            int cell = FieldFont.CellSize;
            int perRow = 32;
            int across = wholeFont ? perRow : 1;
            int down = wholeFont ? (_font.GlyphCount + perRow - 1) / perRow : 1;

            try
            {
                using var bmp = new System.Drawing.Bitmap(across * cell, down * cell);
                for (int i = 0; i < (wholeFont ? _font.GlyphCount : 1); i++)
                {
                    int glyph = wholeFont ? i : _selectedGlyphIndex;
                    int ox = (i % perRow) * cell, oy = (i / perRow) * cell;
                    for (int y = 0; y < cell; y++)
                        for (int x = 0; x < cell; x++)
                        {
                            byte shade = _font.PixelAt(glyph, x, y);
                            byte grey = ShadeGrey[shade & 3];
                            bmp.SetPixel(ox + x, oy + y, System.Drawing.Color.FromArgb(grey, grey, grey));
                        }
                }
                bmp.Save(path, System.Drawing.Imaging.ImageFormat.Png);
                return null;
            }
            catch (Exception ex) { return "That picture could not be written: " + ex.Message; }
        }

        /// <summary>
        /// Reads a picture back in, over the letter being worked on or over the whole font. The picture
        /// has to be the size the font is, so a sheet cannot be dropped onto one letter by mistake.
        /// </summary>
        public string ImportPng(string path, bool wholeFont)
        {
            if (_font == null) return "No font is open.";
            if (!wholeFont && _selectedGlyphIndex < 0) return "Pick a letter first.";

            int cell = FieldFont.CellSize;
            int perRow = 32;
            int wantAcross = (wholeFont ? perRow : 1) * cell;
            int wantDown = (wholeFont ? (_font.GlyphCount + perRow - 1) / perRow : 1) * cell;

            try
            {
                using var bmp = new System.Drawing.Bitmap(path);
                if (bmp.Width != wantAcross || bmp.Height != wantDown)
                    return $"That picture is {bmp.Width} by {bmp.Height} and this wants "
                         + $"{wantAcross} by {wantDown}. Save one out first to get the right size.";

                for (int i = 0; i < (wholeFont ? _font.GlyphCount : 1); i++)
                {
                    int glyph = wholeFont ? i : _selectedGlyphIndex;
                    int ox = (i % perRow) * cell, oy = (i / perRow) * cell;
                    for (int y = 0; y < cell; y++)
                        for (int x = 0; x < cell; x++)
                        {
                            var c = bmp.GetPixel(ox + x, oy + y);
                            _font.SetPixel(glyph, x, y, NearestShade(c.R, c.G, c.B));
                        }
                }
                RecordStep();
                MarkDirty();
                RaiseGlyph();
                RaisePreview();
                return null;
            }
            catch (Exception ex) { return "That picture could not be read: " + ex.Message; }
        }

        /// <summary>The font as it stands, for anything that draws with it.</summary>
        public FieldFont Font => _font;

        // ── The sentence ──────────────────────────────────────────────────────────
        private string _sample = "The quick brown fox jumps over the lazy dog.";
        public string Sample { get => _sample; set { if (Set(ref _sample, value)) RaisePreview(); } }

        public string PreviewNote => _font == null ? null
            : FieldFontCharacters.Ready ? null
            : "The character map for this ROM is not loaded, so letters cannot be matched to pictures. "
              + "Open Tools, Char Map Manager.";

        // ── Saving ────────────────────────────────────────────────────────────────
        public bool HasUnsavedChanges => _dirty;
        public string UnsavedChangesDescription => "Font";
        public void DiscardChanges() { if (_selectedFontIndex >= 0) LoadFont(_selectedFontIndex); }

        public void SaveChanges()
        {
            if (_font == null || _selectedFontIndex < 0) return;
            try
            {
                string dir = gameDirs[DirNames.fonts].unpackedDir;
                var files = Directory.GetFiles(dir).OrderBy(x => x).ToArray();
                int entry = _fontEntries[_selectedFontIndex];
                File.WriteAllBytes(files[entry], _font.Write());
                _dirty = false;
                OnPropertyChanged(nameof(HasUnsavedChanges));
                StatusText = $"Saved {FontNames[_selectedFontIndex]}.";
                // Anything already drawing with the ROM's font picks the change up.
                Views.Controls.FieldMessageBoxView.Font = FieldFont.LoadTalkFont();
            }
            catch (Exception ex)
            {
                StatusText = "That font could not be saved: " + ex.Message;
                AppLogger.Error("FontEditor.Save: " + ex);
            }
        }

        private string _statusText = "";
        public string StatusText { get => _statusText; private set => Set(ref _statusText, value); }

        private void MarkDirty()
        {
            if (_dirty) return;
            _dirty = true;
            OnPropertyChanged(nameof(HasUnsavedChanges));
        }

        private void LoadFont(int which)
        {
            if (which < 0 || which >= _fontEntries.Count) return;
            try
            {
                string dir = gameDirs[DirNames.fonts].unpackedDir;
                var files = Directory.GetFiles(dir).OrderBy(x => x).ToArray();
                _font = FieldFont.Read(File.ReadAllBytes(files[_fontEntries[which]]));
                _dirty = false;

                _all.Clear();
                if (_font != null)
                    for (int g = 0; g < _font.GlyphCount; g++)
                    {
                        // Only the two inks count. Shade 0 is nothing and shade 3 is the paper the box
                        // has already painted, so a letter made entirely of those is blank however many
                        // non-zero values it holds. Counting anything non-zero called 489 of the 509
                        // drawn, including ones whose canvas is plainly empty.
                        bool drawn = false;
                        int wide = Math.Min(_font.WidthOf(g), FieldFont.CellSize);
                        int tall = Math.Min(_font.Height, FieldFont.CellSize);
                        for (int y = 0; y < tall && !drawn; y++)
                            for (int x = 0; x < wide; x++)
                            {
                                byte shade = _font.PixelAt(g, x, y);
                                if (shade == 1 || shade == 2) { drawn = true; break; }
                            }
                        _all.Add(new GlyphRow { Index = g, Letter = LetterFor(g), HasPicture = drawn });
                    }
                ApplyFilter();

                _selectedGlyphIndex = Glyphs.Count > 0 ? 0 : -1;
                OnPropertyChanged(nameof(SelectedGlyphIndex));
                int mapped = Glyphs.Count(r => r.IsMapped);
                StatusText = _font == null
                    ? "That entry is not a font."
                    : $"{_font.GlyphCount} letters, up to {_font.MaxWidth} by {_font.Height}, "
                      + $"{1 << _font.BitsPerPixel} shades. {mapped} of them are written by a "
                      + "character in this ROM's map; the rest are kana and symbols it never asks for.";
                RaiseGlyph();
                OnPropertyChanged(nameof(HasUnsavedChanges));
            }
            catch (Exception ex)
            {
                StatusText = "That font could not be read: " + ex.Message;
                AppLogger.Error("FontEditor.LoadFont: " + ex);
            }
        }

        // The character map turns letters into picture numbers; going the other way is a search, done
        // once when a font loads rather than per letter drawn.
        private static Dictionary<int, string> _lettersByGlyph;
        private static string LetterFor(int glyph)
        {
            // Only keep it once there is something to keep. The character map loads on first use, so
            // building this too early once left every letter blank for the rest of the session.
            if (_lettersByGlyph == null || _lettersByGlyph.Count == 0)
            {
                var built = new Dictionary<int, string>();
                try
                {
                    if (FieldFontCharacters.Ready)
                        for (char c = ' '; c < (char)0x2100; c++)
                        {
                            int g = FieldFontCharacters.GlyphFor(c);
                            if (g >= 0 && !built.ContainsKey(g)) built[g] = c.ToString();
                        }
                }
                catch { }
                if (built.Count > 0) _lettersByGlyph = built;
                else return "";
            }
            return _lettersByGlyph.TryGetValue(glyph, out string s) ? s : "";
        }

        /// <summary>Forgets the letter lookup, for when a different ROM is opened.</summary>
        public static void Forget() => _lettersByGlyph = null;

        private void RaiseGlyph()
        {
            foreach (var n in new[]
            {
                nameof(HasGlyph), nameof(GlyphTitle), nameof(GlyphChanged),
                nameof(CanUsePictureCommand),
            })
                OnPropertyChanged(n);
            if (_font != null && _selectedGlyphIndex >= 0)
            {
                _glyphWidth = _font.WidthOf(_selectedGlyphIndex);
                OnPropertyChanged(nameof(GlyphWidth));
            }
            RaisePreview();
        }

        /// <summary>Bumped whenever the picked letter changes, so the painter redraws.</summary>
        public int GlyphChanged { get; private set; }

        private void RaisePreview()
        {
            GlyphChanged++;
            OnPropertyChanged(nameof(GlyphChanged));
            OnPropertyChanged(nameof(PreviewNote));
            OnPropertyChanged(nameof(Sample));
        }
    }
}
