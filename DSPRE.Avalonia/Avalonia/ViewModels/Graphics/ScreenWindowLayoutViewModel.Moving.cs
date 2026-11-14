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

        private readonly IReadOnlyList<GameTextScreens.ScreenBand> _bands;
        private readonly IReadOnlyList<GameTextScreens.ScreenSwap> _swaps;

        // Where each swap's window was when its map was last saved.
        private readonly Dictionary<string, (int X, int Y)> _swapSaved = new Dictionary<string, (int X, int Y)>();

        private void KeepSwapSpots()
        {
            foreach (GameTextScreens.ScreenSwap swap in _swaps)
                if (_layout.Windows.FirstOrDefault(w => w.Name == swap.Window) is { } w) _swapSaved[swap.Window] = (w.X, w.Y);
        }

        private void StageSwaps()
        {
            foreach (GameTextScreens.ScreenSwap swap in _swaps)
            {
                if (_layout.Windows.FirstOrDefault(w => w.Name == swap.Window) is not { } w || !_swapSaved.TryGetValue(swap.Window, out (int X, int Y) was)) continue;
                ScriptNarc narc = new ScriptNarc(swap.Dir);
                if (!narc.Available || narc.GetFromDisk(swap.Member) is not byte[] saved || swap.Hidden() is not ushort[] away) continue;
                ushort[] shown = swap.Shown?.Invoke();
                byte[] map = WindowTileArt.Swap(saved, was, (w.X, w.Y), (swap.HomeX, swap.HomeY), swap.Width, swap.Height, away, shown, swap.PlainFrom);
                if (!map.AsSpan().SequenceEqual(saved) || _pending.Changes(swap.Dir, swap.Member)) narc.Put(swap.Member, map);
            }
        }

        private readonly Dictionary<string, int> _bandRows = new Dictionary<string, int>();

        private void KeepBandRows()
        {
            foreach (GameTextScreens.ScreenBand band in _bands)
                if (_layout.Windows.FirstOrDefault(w => w.Name == band.Window) is { } w) _bandRows[band.Window] = w.Y;
        }

        // Each map is rebuilt from the saved file, so moving back stages the file as it was.
        private void StageBands()
        {
            foreach (GameTextScreens.ScreenBand band in _bands)
            {
                if (!_bandRows.TryGetValue(band.Window, out int savedRow) || _layout.Windows.FirstOrDefault(w => w.Name == band.Window) is not { } w) continue;
                ScriptNarc narc = new ScriptNarc(band.Dir);
                if (!narc.Available) continue;
                foreach (int member in band.Members)
                    if ((w.Y != savedRow || _pending.Changes(band.Dir, member)) && narc.GetFromDisk(member) is byte[] saved)
                        narc.Put(member, WindowTileArt.Band(saved, savedRow, w.Y, band.Above, band.Rows));
            }
        }

        /// <summary>The change becomes an undo step when <see cref="Commit"/> runs.</summary>
        public void Move(ScreenWindowRow row, int x, int y)
        {
            if (row == null) return;
            if (IsPanel(row)) { MovePanel(row.Window.Pokemon - 1, x, y); return; }
            if (IsSprite(row)) { MoveSprite(row, x, y); return; }
            if (IsBall(row)) { MoveBall(row, x, y); return; }
            if (IsTouch(row)) { MoveTouch(row, x, y); return; }
            ScreenWindowLayout.Window w = row.Window;
            (int fromX, int fromY) = (w.X, w.Y);
            x = Math.Clamp(x, w.MinX, Math.Min(w.MaxX, ScreenWindowLayout.MapColumns - w.Width));
            y = Math.Clamp(y, w.MinY, Math.Min(w.MaxY, ScreenWindowLayout.MapHeight - w.Height));
            int dx = x - w.X, dy = y - w.Y;
            if (dx == 0 && dy == 0) return;
            bool bar = w.Group == SingleGroup && w.Part == "HP bar";
            List<(ScreenWindowLayout.Window Window, int X, int Y)> before = bar ? _layout.Windows.Select(o => (o, o.X, o.Y)).ToList() : null;
            foreach (ScreenWindowLayout.Window moved in Following(w))
            {
                if (moved.CanMoveAcross) moved.X = Math.Clamp(moved.X + dx, moved.MinX, Math.Min(moved.MaxX, ScreenWindowLayout.MapColumns - moved.Width));
                if (moved.CanMoveDown) moved.Y = Math.Clamp(moved.Y + dy, moved.MinY, Math.Min(moved.MaxY, ScreenWindowLayout.MapHeight - moved.Height));
                FollowLabel(moved);
            }
            Carry(w, w.X - fromX, w.Y - fromY);
            // A bar the strip can't follow goes back.
            if (bar && !FollowStrip())
                foreach ((ScreenWindowLayout.Window o, int ox, int oy) in before) (o.X, o.Y) = (ox, oy);
            foreach (ScreenWindowRow shown in Windows) shown.Refresh();
            Warn();
            Raise(nameof(HasUnsavedChanges));
            _ = RedrawAsync();
        }

        private Action<ScriptNarc> _stripArt;
        // Without the size patch, Platinum's strip position is written into arm9.
        private readonly PendingArchiveWrites.Target _ptStripTable;
        private (int Column, int Row) _ptStrip, _savedPtStrip;

        // False when the strip can't go where the bars are.
        private bool FollowStrip()
        {
            if (_ptStripTable != null) return FollowPtStrip();
            if (_panels == null || !_panels.Resizable || _members == null)
            {
                StatusText = "The HP bar frame needs the party panel size patch to move.";
                return false;
            }
            if (BarSpot() is not (int, int) spot)
            {
                StatusText = "All six HP bars need the same spot in their panels.";
                return true;
            }
            PartyPanelSizePatch.Size now = _panels.Size;
            (int column, int row) = _panels.StripUnder(spot);
            PartyPanelSizePatch.Size size = now with { StripColumn = column, StripRow = row };
            if (size == now) return true;
            if (PartyPanelSizePatch.WhyNotSize(size) is string why) { StatusText = why; return false; }
            byte[] art = PartyPanelArt.Build(Saved(_members.PanelArt), _savedSize ?? now, size, out string failed);
            if (art == null) { StatusText = failed; return false; }
            StatusText = "";
            _panels.Resize(size);
            _stripArt = narc => narc.Put(_members.PanelArt, art);
            RaiseSize();
            return true;
        }

        public void Resize(ScreenWindowRow row, int width, int height)
        {
            if (row == null || IsPanel(row) || IsSprite(row) || IsBall(row) || IsTouch(row) || row.Window.Code != null) return;
            if (width == row.Window.Width && height == row.Window.Height) return;
            foreach (ScreenWindowLayout.Window w in Following(row.Window))
            {
                w.Width = Math.Clamp(width, 1, Math.Min(w.MaxWidth, ScreenWindowLayout.MapColumns - w.X));
                w.Height = Math.Clamp(height, 1, Math.Min(w.MaxHeight, ScreenWindowLayout.MapHeight - w.Y));
                FollowLabel(w);
            }
            foreach (ScreenWindowRow shown in Windows) shown.Refresh();
            Warn();
            Raise(nameof(HasUnsavedChanges));
            _ = RedrawAsync();
        }

        private void MovePanel(int pokemon, int x, int y)
        {
            (int px, int py) = _panels.Panel(pokemon);
            int dx = Math.Clamp(x, 0, _panels.MaxX) - px, dy = Math.Clamp(y, 0, _panels.MaxY) - py;
            if (dx == 0 && dy == 0) return;
            foreach (int k in _moveAll ? Enumerable.Range(0, PartyPanelLayout.PartySize) : new[] { pokemon })
            {
                (int kx, int ky) = _panels.Panel(k);
                int nx = Math.Clamp(kx + dx, 0, _panels.MaxX), ny = Math.Clamp(ky + dy, 0, _panels.MaxY);
                _panels.Move(k, nx, ny);
                foreach (ScreenWindowLayout.Window w in _layout.Windows.Where(w => w.Group == SingleGroup && w.Pokemon == k + 1))
                {
                    w.X = Math.Clamp(w.X + nx - kx, 0, ScreenWindowLayout.MapColumns - w.Width);
                    w.Y = Math.Clamp(w.Y + ny - ky, 0, ScreenWindowLayout.MapHeight - w.Height);
                }
            }
            SyncPanels();
            foreach (ScreenWindowRow shown in Windows) shown.Refresh();
            Warn();
            Raise(nameof(HasUnsavedChanges));
            _ = RedrawAsync();
        }

        private IEnumerable<ScreenWindowLayout.Window> Following(ScreenWindowLayout.Window w)
        {
            yield return w;
            if (!_moveAll || w.Pokemon == 0) yield break;
            foreach (ScreenWindowLayout.Window other in _layout.Windows)
                if (!ReferenceEquals(other, w) && other.Group == w.Group && other.Pokemon > 0 && other.Part == w.Part) yield return other;
        }

        private (int X, int Y)? BarSpot() => _panels.SharedBarSpot(n =>
            _layout.Windows.FirstOrDefault(o => o.Group == SingleGroup && o.Pokemon == n + 1 && o.Part == "HP bar") is { } bar ? (bar.X, bar.Y) : null);

        private bool FollowPtStrip()
        {
            if (BarSpot() is not (int, int) spot) { StatusText = "All six HP bars need the same spot in their panels."; return true; }
            (int column, int row) = _panels.StripUnder(spot);
            if ((column, row) == _ptStrip) return true;
            if (PtPartyStrip.WhyNot(column, row) is string why) { StatusText = why; return false; }
            int member = RomLocator.PtPartyStripSites.PanelArt;
            byte[] art = PartyPanelArt.MoveStrip(Saved(member), PtPartyStrip.PanelHeight, _savedPtStrip, (column, row), PtPartyStrip.Length,
                                                 RomLocator.PartyStripTile, out string failed);
            if (art == null) { StatusText = failed; return false; }
            StatusText = "";
            _ptStrip = (column, row);
            _panels.MoveStrip(column, row);
            _stripArt = narc => narc.Put(member, art);
            return true;
        }
    }
}
