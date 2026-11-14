using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using DSPRE.Avalonia.Data;
using DSPRE.ROMFiles;

namespace DSPRE.Avalonia.ViewModels.Graphics
{
    public sealed partial class ScreenWindowLayoutViewModel
    {
        private List<string> PanelNotes(ScreenWindowRow row, int state)
        {
            List<string> notes = new List<string>();
            ScreenWindowLayout.Window p = row.Window;
            int n = p.Pokemon - 1;
            foreach (ScreenWindowRow other in Windows.Where(IsPanel))
            {
                ScreenWindowLayout.Window o = other.Window;
                if (!ReferenceEquals(other, row) && o.X < p.X + p.Width && p.X < o.X + o.Width && o.Y < p.Y + p.Height && p.Y < o.Y + o.Height)
                    notes.Add($"Overlaps {o.Name}");
            }
            foreach (ScreenWindowRow other in Windows.Where(r => !IsPanel(r) && r.Window.Pokemon == 0 && r.Window.Layer < p.Layer))
            {
                (int ox, int oy, int ow, int oh) = GameTextScreens.Placed(_text, other.Window, state);
                if (ox < p.X + p.Width && p.X < ox + ow && oy < p.Y + p.Height && p.Y < oy + oh) notes.Add($"Under {Said(other.Window)}");
            }
            // The game slides even slots left and odd slots right by the panel width, wherever they are.
            if (p.X != (n % 2 == 0 ? 0 : _panels.MaxX))
                notes.Add($"Slides toward the {(n % 2 == 0 ? "left" : "right")} edge when Pokémon switch places");
            return notes;
        }

        // Problems the saved layout already has are the game's own, so only new ones are shown.
        private void Warn()
        {
            if (_text == null || _layout == null) return;
            int state = HasStates ? _state : 0;
            Dictionary<(string, string), List<string>> now = Notes(_layout, state);
            Dictionary<(string, string), List<string>> before = SavedLayout() is ScreenWindowLayout saved ? Notes(saved, state) : new();
            foreach (ScreenWindowRow row in Windows)
            {
                ScreenWindowLayout.Window w = row.Window;
                if (IsPanel(row)) { row.Warning = string.Join("\n", PanelNotes(row, state)); continue; }
                if (IsSprite(row) || IsBall(row) || IsTouch(row)) continue;
                List<string> notes = now.TryGetValue((w.Group, w.Name), out List<string> n) ? n : new List<string>();
                if (before.TryGetValue((w.Group, w.Name), out List<string> old)) notes = notes.Except(old).ToList();
                ScreenWindowLayout.Window was = _savedLayout?.Windows.FirstOrDefault(o => o.Group == w.Group && o.Name == w.Name);
                if (row.FixedTop is int fixedTop && was != null && was.Y != w.Y) notes.Add($"Stays on row {fixedTop} in this state");
                row.Warning = string.Join("\n", notes);
            }
        }

        private static string Said(ScreenWindowLayout.Window w) =>
            w.Pokemon > 0 ? w.Name : "the " + char.ToLowerInvariant(w.Name[0]) + w.Name.Substring(1);

        private Dictionary<(string, string), List<string>> Notes(ScreenWindowLayout layout, int state)
        {
            List<ScreenWindowLayout.Window> shown = layout.Windows.Where(w => !HasStates || _text.Shows(state, w.Group, w.Name)).ToList();
            Dictionary<string, bool[]> kept = GameTextScreens.ShownTiles(_text, layout, state);
            Dictionary<(string, string), List<string>> all = new Dictionary<(string, string), List<string>>();
            foreach (ScreenWindowLayout.Window w in shown)
            {
                (int x, int y, int width, int height) = GameTextScreens.Placed(_text, w, state);
                List<string> notes = new List<string>();
                foreach (ScreenWindowLayout.Window other in shown)
                {
                    if (ReferenceEquals(other, w) || other.Layer != w.Layer || !GameTextScreens.Together(_text, w, other)
                        || !kept.TryGetValue(other.Name, out bool[] otherKept)) continue;
                    (int ox, int oy, int ow, int oh) = GameTextScreens.Placed(_text, other, state);
                    bool overlap = false, lost = false;
                    for (int ty = Math.Max(y, oy); ty < Math.Min(y + height, oy + oh) && ty < 32; ty++)
                        for (int tx = Math.Max(x, ox); tx < Math.Min(x + width, ox + ow) && tx < 32; tx++)
                        {
                            overlap = true;
                            if (otherKept[ty * 32 + tx]) lost = true;
                        }
                    if (overlap) notes.Add(lost ? $"Partly hidden by {Said(other)}" : $"Hides part of {Said(other)}");
                }
                // A box on a background in front (lower number, same screen) covers it, frame included.
                foreach (ScreenWindowLayout.Window a in shown)
                {
                    if (a.Layer >= w.Layer || a.Layer / 4 != w.Layer / 4 || !GameTextScreens.Together(_text, w, a)) continue;
                    (int ax, int ay, int aw, int ah) = GameTextScreens.Placed(_text, a, state);
                    (int l, int t, int r, int b) = _text.Frame?.Invoke(a) ?? (0, 0, 0, 0);
                    if (ax - l < x + width && x < ax + aw + r && ay - t < y + height && y < ay + ah + b) notes.Add($"Under {Said(a)}");
                }
                if (_text.Block?.Invoke(w) is (int bx, int by, int bw, int bh)
                    && (x < bx || y < by || x + width > bx + bw || y + height > by + bh))
                    notes.Add("Left behind when Pokémon switch places");
                all[(w.Group, w.Name)] = notes;
            }
            return all;
        }
    }
}
