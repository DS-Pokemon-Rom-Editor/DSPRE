using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DSPRE.ROMFiles
{
    /// <summary>
    /// Panel positions and everything placed from them (icon, ball, status icon, cursor stops, HGSS touch areas), so they move together.
    /// </summary>
    public sealed class PartyPanelLayout
    {
        public const int PartySize = 6;

        /// <summary>From the size patch, or the game's 16 by 6.</summary>
        public PartyPanelSizePatch.Size Size { get; private set; } = PartyPanelSizePatch.Original;
        /// <summary>Needs the panel size patch.</summary>
        public bool Resizable { get; private set; }
        public int Width => Size.Width;
        public int Height => Size.Height;
        // A panel stays on the visible 32 by 24 tiles.
        public int MaxX => ScreenWindowLayout.MapColumns - Width;
        public int MaxY => VisibleRows - Height;
        private const int VisibleRows = 24;

        private readonly RomInfo.PartyPanelSites _sites;
        private readonly RomInfo.PartyHpBarSites _hpBar = RomLocator.PartyHpBarSites;

        /// <summary>Used where the length can't be changed.</summary>
        public const int GameHpBarLength = 48;
        /// <summary>In pixels, at full HP.</summary>
        public int HpBarLength { get; set; } = GameHpBarLength;
        /// <summary>This game's code for it is known.</summary>
        public bool HpBarLengthEditable { get; private set; }
        // The tables' bytes for all six Pokémon, in the order of _regions.
        private readonly List<(uint Offset, int Length)> _regions = new List<(uint, int)>();
        private byte[] _data;

        private PartyPanelLayout(RomInfo.PartyPanelSites sites)
        {
            _sites = sites;
            _regions.Add((sites.Panels, PartySize * 12));
            foreach (uint cursor in sites.Cursors) _regions.Add((cursor, PartySize * 8));
            _regions.Add((sites.SpriteTemplates + 4 * 40, PartySize * 40));
            foreach (uint touch in sites.Touch) _regions.Add((touch, PartySize * 4));
        }

        private int Start(int region) => _regions.Take(region).Sum(r => r.Length);
        private int CursorRegion(int k) => 1 + k;
        private int StatusRegion => 1 + _sites.Cursors.Length;
        private int TouchRegion(int k) => 2 + _sites.Cursors.Length + k;

        /// <summary>Null when this ROM's tables aren't known or don't look like the game's.</summary>
        public static PartyPanelLayout Read()
        {
            RomInfo.PartyPanelSites sites = RomLocator.PartyPanelSites;
            if (sites == null || !File.Exists(RomInfo.Arm9Path)) return null;
            byte[] arm9 = File.ReadAllBytes(RomInfo.Arm9Path);
            PartyPanelLayout layout = new PartyPanelLayout(sites);
            if (layout._regions.Any(r => r.Offset + r.Length > arm9.Length)) return null;
            PartyPanelSizePatch.Size patched = PartyPanelSizePatch.Read();
            layout.Resizable = patched != null;
            layout.Size = patched ?? PartyPanelSizePatch.Original;
            layout._data = layout._regions.SelectMany(r => arm9.Skip((int)r.Offset).Take(r.Length)).ToArray();
            if (layout.ReadHpBar(arm9) is int length) (layout.HpBarLength, layout.HpBarLengthEditable) = (length, true);
            return layout.Sane(arm9) ? layout : null;
        }

        // Null when the code there isn't movs r2, #length.
        private int? ReadHpBar(byte[] arm9)
        {
            if (_hpBar == null) return null;
            int at = (int)(_hpBar.Length - ARM9.Address);
            if (at < 0 || at + 2 > arm9.Length || arm9[at + 1] != 0x22 || arm9[at] == 0) return null;
            return arm9[at];
        }

        // Checks what a move never changes: panels on the map, cursor stops without a size, and the button touch areas after the Pokémon's.
        private bool Sane(byte[] arm9)
        {
            for (int n = 0; n < PartySize; n++)
            {
                (int x, int y) = Panel(n);
                if (x > MaxX || y > MaxY) return false;
                for (int k = 0; k < _sites.Cursors.Length; k++)
                {
                    int at = Start(CursorRegion(k)) + n * 8;
                    if (_data[at + 2] != 0 || _data[at + 3] != 0) return false;
                }
            }
            byte[] cancel = { 0x98, 0xC0, 0xC8, 0x00, 0xFF }, frontier = { 0xB0, 0xC0, 0xC8, 0x00, 0xA0, 0xB0, 0xC8, 0x00, 0xFF };
            for (int k = 0; k < _sites.Touch.Length; k++)
            {
                byte[] after = k == 0 ? cancel : frontier;
                uint at = _sites.Touch[k] + PartySize * 4;
                if (at + after.Length > arm9.Length || !arm9.Skip((int)at).Take(after.Length).SequenceEqual(after)) return false;
            }
            return true;
        }

        private int U16(int at) => _data[at] | _data[at + 1] << 8;
        private void SetU16(int at, int value) { _data[at] = (byte)value; _data[at + 1] = (byte)(value >> 8); }

        /// <summary>In tiles.</summary>
        public (int X, int Y) Panel(int pokemon) => (U16(pokemon * 12), U16(pokemon * 12 + 2));
        /// <summary>Its centre, in pixels.</summary>
        public (int X, int Y) Icon(int pokemon) => (U16(pokemon * 12 + 4), U16(pokemon * 12 + 6));
        /// <summary>Its centre, in pixels.</summary>
        public (int X, int Y) Ball(int pokemon) => (U16(pokemon * 12 + 8), U16(pokemon * 12 + 10));
        /// <summary>Its centre, in pixels.</summary>
        public (int X, int Y) Status(int pokemon)
        {
            int at = Start(StatusRegion) + pokemon * 40;
            return ((short)U16(at + 4), (short)U16(at + 6));
        }
        /// <summary>Where the cursor's centre stops in the normal party menu, in pixels.</summary>
        public (int X, int Y) Cursor(int pokemon)
        {
            int at = Start(CursorRegion(0)) + pokemon * 8;
            return (_data[at], _data[at + 1]);
        }

        /// <summary>The held item and capsule follow the icon in the game's code.</summary>
        public enum Sprite { Icon, Ball, Status }

        /// <summary>Its centre, in pixels.</summary>
        public (int X, int Y) SpriteAt(int pokemon, Sprite sprite) => sprite switch
        {
            Sprite.Icon => Icon(pokemon),
            Sprite.Ball => Ball(pokemon),
            _ => Status(pokemon),
        };

        /// <summary>Moves it by dx, dy pixels, kept on screen.</summary>
        public void MoveSprite(int pokemon, Sprite sprite, int dx, int dy)
        {
            (int x, int y) = SpriteAt(pokemon, sprite);
            int nx = Math.Clamp(x + dx, 0, 255), ny = Math.Clamp(y + dy, 0, 191);
            int at = sprite switch
            {
                Sprite.Icon => pokemon * 12 + 4,
                Sprite.Ball => pokemon * 12 + 8,
                _ => Start(StatusRegion) + pokemon * 40 + 4,
            };
            SetU16(at, nx);
            SetU16(at + 2, ny);
        }

        /// <summary>Everything placed from the panel moves by the same step.</summary>
        public void Move(int pokemon, int x, int y)
        {
            (int px, int py) = Panel(pokemon);
            x = Math.Clamp(x, 0, MaxX);
            y = Math.Clamp(y, 0, MaxY);
            int dx = x - px, dy = y - py;
            if (dx == 0 && dy == 0) return;
            int p = pokemon * 12;
            SetU16(p, x);
            SetU16(p + 2, y);
            for (int k = 4; k < 12; k += 4)
            {
                SetU16(p + k, U16(p + k) + dx * 8);
                SetU16(p + k + 2, U16(p + k + 2) + dy * 8);
            }
            for (int k = 0; k < _sites.Cursors.Length; k++)
            {
                int at = Start(CursorRegion(k)) + pokemon * 8;
                _data[at] = (byte)(_data[at] + dx * 8);
                _data[at + 1] = (byte)(_data[at + 1] + dy * 8);
            }
            int s = Start(StatusRegion) + pokemon * 40;
            SetU16(s + 4, (short)U16(s + 4) + dx * 8);
            SetU16(s + 6, (short)U16(s + 6) + dy * 8);
            for (int k = 0; k < _sites.Touch.Length; k++)
            {
                // Top, bottom, left, right; a right edge of 0 means 256.
                int at = Start(TouchRegion(k)) + pokemon * 4;
                int right = _data[at + 3] == 0 ? 256 : _data[at + 3];
                _data[at] = (byte)(_data[at] + dy * 8);
                _data[at + 1] = (byte)(_data[at + 1] + dy * 8);
                _data[at + 2] = (byte)(_data[at + 2] + dx * 8);
                _data[at + 3] = (byte)(right + dx * 8);
            }
        }

        public void ReadPatch()
        {
            PartyPanelSizePatch.Size patched = PartyPanelSizePatch.Read();
            Resizable = patched != null;
            if (patched != null) Size = patched;
        }

        /// <summary>
        /// Each panel keeps its top left corner where it fits; cursor stops and touch areas are recalculated and sprites stay put.
        /// </summary>
        public void Resize(PartyPanelSizePatch.Size size)
        {
            if (!Resizable || PartyPanelSizePatch.WhyNotSize(size) != null) return;
            Size = size;
            for (int n = 0; n < PartySize; n++)
            {
                (int px, int py) = Panel(n);
                Move(n, Math.Min(px, MaxX), Math.Min(py, MaxY));
                (px, py) = Panel(n);
                for (int k = 0; k < _sites.Cursors.Length; k++)
                {
                    int at = Start(CursorRegion(k)) + n * 8;
                    // The game's stops are a pixel below the panel's centre.
                    _data[at] = (byte)(px * 8 + 4 * Width);
                    _data[at + 1] = (byte)(py * 8 + 4 * Height + 1);
                }
                for (int k = 0; k < _sites.Touch.Length; k++)
                {
                    int at = Start(TouchRegion(k)) + n * 4;
                    _data[at] = (byte)(py * 8);
                    _data[at + 1] = (byte)(py * 8 + 8 * Height);
                    _data[at + 2] = (byte)(px * 8);
                    _data[at + 3] = (byte)(px * 8 + 8 * Width);
                }
            }
        }

        // Undo steps cover the tables, the size and the HP bar length.
        private int Length => _data.Length + 7;
        public byte[] ToBytes() => _data.Concat(PartyPanelSizePatch.ToBytes(Size)).Append((byte)HpBarLength).ToArray();

        public void FromBytes(byte[] bytes)
        {
            if (bytes == null || bytes.Length != Length) return;
            Array.Copy(bytes, _data, _data.Length);
            Size = PartyPanelSizePatch.FromBytes(bytes.Skip(_data.Length).Take(6).ToArray()) ?? Size;
            HpBarLength = bytes[Length - 1];
        }

        /// <summary>The bytes arm9 holds now, in <see cref="ToBytes"/> order.</summary>
        public byte[] FileBytes()
        {
            byte[] arm9 = File.ReadAllBytes(RomInfo.Arm9Path);
            byte[] tables = _regions.SelectMany(r => arm9.Skip((int)r.Offset).Take(r.Length)).ToArray();
            return tables.Concat(PartyPanelSizePatch.ToBytes(PartyPanelSizePatch.Read() ?? PartyPanelSizePatch.Original))
                         .Append((byte)(ReadHpBar(arm9) ?? GameHpBarLength)).ToArray();
        }

        /// <summary>Null when the bytes can be written.</summary>
        public string WhyNotWrite(byte[] bytes)
        {
            if (bytes == null || bytes.Length != Length) return "The panel tables don't match this ROM.";
            if (bytes[Length - 1] == 0) return "The HP bar needs to be at least a pixel long.";
            if (bytes[Length - 1] != GameHpBarLength && !HpBarLengthEditable) return "This game's HP bar length can't be changed.";
            PartyPanelSizePatch.Size size = PartyPanelSizePatch.FromBytes(bytes.Skip(_data.Length).Take(6).ToArray());
            if (size == PartyPanelSizePatch.Original) return null;
            if (PartyPanelSizePatch.Read() == null) return "The panel size patch isn't in, so the panels can't change size.";
            return PartyPanelSizePatch.WhyNotSize(size);
        }

        /// <summary>Writes the tables, and the size into the patched code.</summary>
        public void WriteFile(byte[] bytes)
        {
            if (WhyNotWrite(bytes) is string why) throw new InvalidOperationException(why);
            int from = 0;
            foreach ((uint offset, int length) in _regions)
            {
                DSUtils.WriteToFile(RomInfo.Arm9Path, bytes, offset, from, length);
                from += length;
            }
            PartyPanelSizePatch.Size size = PartyPanelSizePatch.FromBytes(bytes.Skip(_data.Length).Take(6).ToArray());
            if (PartyPanelSizePatch.Read() != null) PartyPanelSizePatch.Write(size);
            if (HpBarLengthEditable)
            {
                DSUtils.WriteToFile(RomInfo.Arm9Path, new[] { bytes[Length - 1], (byte)0x22 }, _hpBar.Length - ARM9.Address);
            }
        }
    }
}
