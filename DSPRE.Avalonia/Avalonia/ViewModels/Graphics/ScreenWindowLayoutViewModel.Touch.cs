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
        // Moves the touch area as the Buttons tab does. A picture that carries areas moves them with it.
        private const string TouchGroup = "Buttons", TouchColour = "#FFFFFF";

        private static bool IsTouch(ScreenWindowRow row) => row.Window.Group == TouchGroup && row.Window.Part == "touch";

        private int TouchPage => _link == null ? -1 : _link.PageOf(HasStates ? _state : 0);

        private void AddTouchRows(bool mainOnTop)
        {
            int page = TouchPage;
            if (page < 0 || page >= _buttons.Buttons.PageNames.Count) return;
            HashSet<int> carried = (_link.Carries?.Values ?? Enumerable.Empty<(int Page, int Button)[]>())
                .SelectMany(c => c).Where(c => c.Page == page).Select(c => c.Button).ToHashSet();
            string[] names = _buttons.Buttons.ButtonsOf(page);
            for (int b = 0; b < names.Length; b++)
            {
                if (carried.Contains(b) || _buttons.Buttons.MoveLocked(page, b) != null) continue;
                ScreenWindowRow row = new ScreenWindowRow(new ScreenWindowLayout.Window
                {
                    Group = TouchGroup, Name = names[b] + " button", Pokemon = b, Part = "touch", Layer = mainOnTop ? 4 : 0,
                }, TouchColour, 0)
                {
                    MoveTo = (r, x, y) => { Move(r, x, y); Commit(); },
                    OnScreenTiles = true,
                    SizeElsewhere = "Touch area sizes are set in the Buttons tab.",
                };
                PlaceTouch(row, page);
                Windows.Add(row);
                BottomWindows.Add(row);
            }
        }

        private void PlaceTouch(ScreenWindowRow row, int page)
        {
            (int top, int bottom, int left, int right) = _buttons.Buttons.AreaOf(page, row.Window.Pokemon);
            row.Window.X = left / 8;
            row.Window.Y = top / 8;
            row.Window.Width = Math.Max(1, (right - 1) / 8 - left / 8 + 1);
            row.Window.Height = Math.Max(1, (bottom - 1) / 8 - top / 8 + 1);
            row.Refresh();
        }

        private void SyncTouch()
        {
            int page = TouchPage;
            if (page < 0) return;
            foreach (ScreenWindowRow row in Windows.Where(IsTouch)) PlaceTouch(row, page);
        }

        private void MoveTouch(ScreenWindowRow row, int x, int y)
        {
            int page = TouchPage, dx = x - row.Window.X, dy = y - row.Window.Y;
            if (page < 0 || (dx == 0 && dy == 0)) return;
            _buttons.Buttons.Move(page, row.Window.Pokemon, dx * 8, dy * 8);
            PlaceTouch(row, page);
            _buttons.Moved();
            Raise(nameof(HasUnsavedChanges));
            _ = RedrawAsync();
        }

        private void Carry(ScreenWindowLayout.Window w, int dx, int dy)
        {
            if (_link?.Carries == null || (dx == 0 && dy == 0) || !_link.Carries.TryGetValue(w.Name, out (int Page, int Button)[] areas)) return;
            foreach ((int page, int button) in areas) _buttons.Buttons.Move(page, button, dx * 8, dy * 8);
            _buttons.Moved();
        }
    }
}
