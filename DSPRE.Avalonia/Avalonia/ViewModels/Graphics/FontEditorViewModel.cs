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
    }

    /// <summary>
    /// The letters a ROM writes with: every font it carries, every letter in one, and what a sentence
    /// looks like in it.
    /// </summary>
    public sealed class FontEditorViewModel : INotifyPropertyChanged, IEditorWithUnsavedChanges
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
        public ObservableCollection<GlyphRow> Glyphs { get; } = new();

        private int _selectedGlyphIndex = -1;
        public int SelectedGlyphIndex
        {
            get => _selectedGlyphIndex;
            set { if (Set(ref _selectedGlyphIndex, value)) RaiseGlyph(); }
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
            MarkDirty();
            RaiseGlyph();
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

                Glyphs.Clear();
                if (_font != null)
                    for (int g = 0; g < _font.GlyphCount; g++)
                        Glyphs.Add(new GlyphRow { Index = g, Letter = LetterFor(g) });

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
            foreach (var n in new[] { nameof(HasGlyph), nameof(GlyphTitle), nameof(GlyphChanged) })
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
