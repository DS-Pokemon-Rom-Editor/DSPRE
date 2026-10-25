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

        /// <summary>Most of a font is kana and symbols an English map never uses; a blank column beside
        /// them looks like missing data.</summary>
        public bool IsMapped => !string.IsNullOrEmpty(Letter);

        /// <summary>Whether anything is drawn here. A picture with nothing in it is a free slot.</summary>
        public bool HasPicture { get; init; }

        /// <summary>What the list shows beside the number: the letter, or a note for the few empty slots.</summary>
        public string Describe => IsMapped ? Letter : HasPicture ? "" : "empty";
    }

    /// <summary>
    /// The letters a ROM writes with: every font it carries, every letter in one, and what a sentence
    /// looks like in it.
    /// </summary>
    public sealed class FontEditorViewModel : INotifyPropertyChanged, IEditorWithUnsavedChanges, ISupportsUndo, IDetachable
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

        public FontEditorViewModel()
        {
            if (Design.IsDesignMode) { _loading = false; return; }
            try
            {
                DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.Fonts });
                // Load the character map here so the letters are known when the list fills.
                try { FieldFontCharacters.GlyphFor('A'); } catch (Exception ex) { AppLogger.Warn("Font editor: field font character map: " + ex.Message); }
                LoadFontList();
            }
            catch (Exception ex)
            {
                StatusText = "The fonts couldn't be read: " + ex.Message;
                AppLogger.Error("FontEditor: " + ex);
            }
            _loading = false;
            if (FontNames.Count > 0) SelectedFontIndex = 0;
            AppEvents.RomPatchStateChanged += OnPatchStateChanged;
        }

        public void Detach() => AppEvents.RomPatchStateChanged -= OnPatchStateChanged;

        private void OnPatchStateChanged(object sender, EventArgs e) => RaiseAdding();

        // ── Adding a font ─────────────────────────────────────────────────────────
        // An added font stays a draft until Save gives it an archive entry and the next font number.

        private (int Entry, int LikeFont)? _adding;

        private int OpenFontNumber => _selectedFontIndex >= 0 && _selectedFontIndex < _fontEntries.Count
            ? GameFonts.All.FirstOrDefault(f => f.Entry == _fontEntries[_selectedFontIndex])?.Number ?? -1 : -1;

        public bool OffersFontPatch => !IsHGE && ExtraFontsPatch.SupportsCurrentRom && !ExtraFontsPatch.IsApplied;

        private static bool FontsFull => ExtraFontsPatch.IsApplied && GameFonts.All.Count >= ExtraFontsPatch.FontCount;

        public bool CanAddFont => _font != null && !HasUnsavedChanges && ExtraFontsPatch.IsApplied && OpenFontNumber >= 0 && !FontsFull;

        public void OpenFontPatch() => AvaloniaEditorLauncher.OpenPatchToolboxAt("extraFonts");

        public void AddFont()
        {
            if (!CanAddFont) return;
            int likeFont = OpenFontNumber, entry = FieldFont.UnpackedEntries().Length;
            FieldFont copy = FieldFont.Read(_font.Write());
            _adding = (entry, likeFont);
            _fontEntries.Add(entry);
            FontNames.Add("Extra font " + (GameFonts.All.Count - ExtraFontsPatch.GameFontCount + 1));
            _selectedFontIndex = _fontEntries.Count - 1;
            OnPropertyChanged(nameof(SelectedFontIndex));
            Show(copy);
            StatusText = $"Save to add this copy of the {GameFonts.All[likeFont].Name}.";
        }


        private void DropAdding()
        {
            if (_adding == null) return;
            _adding = null;
            _fontEntries.RemoveAt(_fontEntries.Count - 1);
            FontNames.RemoveAt(FontNames.Count - 1);
        }

        private void RaiseAdding()
        {
            OnPropertyChanged(nameof(OffersFontPatch));
            OnPropertyChanged(nameof(CanAddFont));
        }

        // ── The fonts this ROM carries ────────────────────────────────────────────
        public ObservableCollection<string> FontNames { get; } = new();

        private int _selectedFontIndex = -1;
        public int SelectedFontIndex
        {
            get => _selectedFontIndex;
            set
            {
                if (RecordSwitchGuard.IsSnappingBack || value == _selectedFontIndex || value < 0) return;
                if (HasUnsavedChanges && !_loading)
                {
                    int requested = value;
                    RecordSwitchGuard.SnapBack(() => _selectedFontIndex, v => _selectedFontIndex = v, () => OnPropertyChanged(nameof(SelectedFontIndex)));
                    _ = SwitchFontAsync(requested);
                    return;
                }
                if (Set(ref _selectedFontIndex, value) && !_loading) LoadFont(value);
            }
        }

        private async System.Threading.Tasks.Task SwitchFontAsync(int requested)
        {
            if (!await RecordSwitchGuard.ConfirmLeaveAsync(this, null, "font")) return;
            _selectedFontIndex = requested;
            OnPropertyChanged(nameof(SelectedFontIndex));
            LoadFont(requested);
        }

        private void LoadFontList()
        {
            FontNames.Clear();
            _fontEntries.Clear();
            string[] files = FieldFont.UnpackedEntries();
            for (int i = 0; i < files.Length; i++)
            {
                FieldFont f;
                try { f = FieldFont.Read(File.ReadAllBytes(files[i])); } catch (Exception ex) { AppLogger.WarnOnce("FontEditorViewModel.LoadFontList", ex); continue; }
                if (f == null) continue;      // the width tables in the same archive are not fonts
                _fontEntries.Add(i);
                // The games name these themselves; anything unnamed still says which entry it is.
                string named = NamedArchives.NameOf(DirNames.Fonts, i);
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

            foreach (GlyphRow row in _all)
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
        // Undo keeps only the letter being edited, so a step is a few hundred bytes.

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
            byte[] pixels = new byte[cell * cell];
            for (int y = 0; y < cell; y++)
                for (int x = 0; x < cell; x++)
                    pixels[y * cell + x] = _font.PixelAt(_selectedGlyphIndex, x, y);
            return new Step { Glyph = _selectedGlyphIndex, Pixels = pixels, Width = _font.WidthOf(_selectedGlyphIndex) };
        }

        /// <summary>
        /// Records the letter after the edit, not before, or the last change gets no undo step.
        /// </summary>
        private void RecordStep()
        {
            Step step = Snapshot();
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
            Step step = Snapshot();
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
                RawImage image = new DSPRE.RawImage(across * cell, down * cell);
                for (int i = 0; i < (wholeFont ? _font.GlyphCount : 1); i++)
                {
                    int glyph = wholeFont ? i : _selectedGlyphIndex;
                    int ox = (i % perRow) * cell, oy = (i / perRow) * cell;
                    for (int y = 0; y < cell; y++)
                        for (int x = 0; x < cell; x++)
                        {
                            byte shade = _font.PixelAt(glyph, x, y);
                            byte grey = ShadeGrey[shade & 3];
                            int at = ((oy + y) * image.Width + ox + x) * 4;
                            image.Bgra[at] = image.Bgra[at + 1] = image.Bgra[at + 2] = grey;
                            image.Bgra[at + 3] = 255;
                        }
                }
                ImageConverter.ToAvaloniaBitmap(image).Save(path, global::Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
                return null;
            }
            catch (Exception ex) { return "That picture couldn't be written: " + ex.Message; }
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
                DSPRE.RawImage image;
                using (FileStream stream = System.IO.File.OpenRead(path)) image = ImageConverter.DecodeRawImage(stream);
                if (image == null) return "That picture couldn't be read.";
                if (image.Width != wantAcross || image.Height != wantDown)
                    return $"That picture is {image.Width} by {image.Height} and this wants "
                         + $"{wantAcross} by {wantDown}. Save one out first to get the right size.";

                for (int i = 0; i < (wholeFont ? _font.GlyphCount : 1); i++)
                {
                    int glyph = wholeFont ? i : _selectedGlyphIndex;
                    int ox = (i % perRow) * cell, oy = (i / perRow) * cell;
                    for (int y = 0; y < cell; y++)
                        for (int x = 0; x < cell; x++)
                        {
                            int at = ((oy + y) * image.Width + ox + x) * 4;
                            _font.SetPixel(glyph, x, y, NearestShade(image.Bgra[at + 2], image.Bgra[at + 1], image.Bgra[at]));
                        }
                }
                RecordStep();
                MarkDirty();
                RaiseGlyph();
                RaisePreview();
                return null;
            }
            catch (Exception ex) { return "That picture couldn't be read: " + ex.Message; }
        }

        /// <summary>The font as it stands, for anything that draws with it.</summary>
        public FieldFont Font => _font;

        // ── The sentence ──────────────────────────────────────────────────────────
        private string _sample = "The quick brown fox jumps over the lazy dog.";
        public string Sample { get => _sample; set { if (Set(ref _sample, value)) RaisePreview(); } }

        public string PreviewNote => _font == null ? null
            : FieldFontCharacters.Ready ? null
            : "The character map for this ROM isn't loaded, so letters can't be matched to pictures. "
              + "Open the Character map manager.";

        // ── Saving ────────────────────────────────────────────────────────────────
        // Compared as bytes, so undoing back to the saved letters counts as clean.
        public bool HasUnsavedChanges => _adding != null || _font != null && _savedBytes != null && !_font.Write().AsSpan().SequenceEqual(_savedBytes);
        private byte[] _savedBytes;
        public string UnsavedChangesDescription => "Font";
        public void DiscardChanges()
        {
            if (_adding is (_, int likeFont))
            {
                DropAdding();
                _selectedFontIndex = Math.Max(0, _fontEntries.IndexOf(GameFonts.EntryOf(likeFont)));
                OnPropertyChanged(nameof(SelectedFontIndex));
                LoadFont(_selectedFontIndex);
                return;
            }
            if (_selectedFontIndex >= 0) LoadFont(_selectedFontIndex);
        }

        public void SaveChanges()
        {
            if (_font == null || _selectedFontIndex < 0) return;
            try
            {
                if (_adding is (int entry, int likeFont))
                {
                    // The entry goes in before the font number points at it, so the game never loads a missing entry.
                    ArchiveMembers.Put(DirNames.Fonts, entry, _font.Write());
                    ExtraFontsPatch.Add(entry, likeFont);
                    _adding = null;
                    AppEvents.RaiseArchiveMemberSaved(DirNames.Fonts);
                }
                else _font.Save(_fontEntries[_selectedFontIndex]);
                _savedBytes = _font.Write();
                RaiseAdding();
                SaveNotice.Saved(UnsavedChangesDescription);
                OnPropertyChanged(nameof(HasUnsavedChanges));
                StatusText = FontsFull ? $"All {ExtraFontsPatch.FontCount} fonts are in use."
                                       : $"Saved {FontNames[_selectedFontIndex]}.";
                // Anything already drawing with the ROM's font picks the change up.
                Views.Controls.FieldMessageBoxView.Font = FieldFont.LoadTalkFont();
            }
            catch (Exception ex)
            {
                StatusText = "That font couldn't be saved: " + ex.Message;
                AppLogger.Error("FontEditor.Save: " + ex);
            }
        }

        private string _statusText = "";
        public string StatusText { get => _statusText; private set => Set(ref _statusText, value); }

        private void MarkDirty()
        {
            OnPropertyChanged(nameof(HasUnsavedChanges));
            OnPropertyChanged(nameof(CanAddFont));
        }

        private void LoadFont(int which)
        {
            if (which < 0 || which >= _fontEntries.Count) return;
            // Leaving an unsaved added font drops it; the switch guard has already asked.
            if (_adding != null && which != _fontEntries.Count - 1) DropAdding();
            try
            {
                Show(FieldFont.LoadFromArchive(_fontEntries[which]));
                if (_font != null && HgEngine.HgEngineSourceAssets.VerbatimSourceFor(
                        HgEngine.HgEngineOwnedFiles.ArchiveOf(DirNames.Fonts), _fontEntries[which]) is HgEngine.HgEngineOwnedFile source)
                    StatusText += $" hg-engine builds this font from {source.RelPath}, so saving writes there too.";
            }
            catch (Exception ex)
            {
                StatusText = "That font couldn't be read: " + ex.Message;
                AppLogger.Error("FontEditor.LoadFont: " + ex);
            }
        }

        // An added font has no saved bytes, so it counts as unsaved until Save.
        private void Show(FieldFont font)
        {
            _font = font;
            _savedBytes = _adding == null ? _font?.Write() : null;

            _all.Clear();
            if (_font != null)
                for (int g = 0; g < _font.GlyphCount; g++)
                {
                    // Only the two inks count: shade 0 is empty and shade 3 is the box's paper.
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
                ? "That entry isn't a font."
                : $"{_font.GlyphCount} letters, up to {_font.MaxWidth} by {_font.Height}, "
                  + $"{1 << _font.BitsPerPixel} shades. {mapped} of them have a character in this ROM's "
                  + "character map; the rest are kana and symbols it doesn't use."
                  + (FontsFull ? $" All {ExtraFontsPatch.FontCount} fonts are in use." : "");
            RaiseGlyph();
            // Steps hold pixels of the font that was open; undoing one here would paint them into this one.
            RestartSteps();
            OnPropertyChanged(nameof(HasUnsavedChanges));
            RaiseAdding();
        }

        // The character map turns letters into picture numbers; the reverse is a search, so it runs once
        // per font load, not per letter.
        private static Dictionary<int, string> _lettersByGlyph;
        private static string LetterFor(int glyph)
        {
            // Only keep it once there is something to keep. The character map loads on first use, so
            // building this too early once left every letter blank for the rest of the session.
            if (_lettersByGlyph == null || _lettersByGlyph.Count == 0)
            {
                Dictionary<int, string> built = new Dictionary<int, string>();
                try
                {
                    if (FieldFontCharacters.Ready)
                        for (char c = ' '; c < (char)0x2100; c++)
                        {
                            int g = FieldFontCharacters.GlyphFor(c);
                            if (g >= 0 && !built.ContainsKey(g)) built[g] = c.ToString();
                        }
                }
                catch (Exception ex) { AppLogger.Warn("Font editor: letters by glyph: " + ex.Message); }
                if (built.Count > 0) _lettersByGlyph = built;
                else return "";
            }
            return _lettersByGlyph.TryGetValue(glyph, out string s) ? s : "";
        }

        /// <summary>Forgets the letter lookup, for when a different ROM is opened.</summary>
        public static void Forget() => _lettersByGlyph = null;

        private void RaiseGlyph()
        {
            foreach (string n in new[]
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
