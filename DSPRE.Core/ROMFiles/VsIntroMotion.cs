using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using static DSPRE.RomInfo;

namespace DSPRE.ROMFiles
{
    /// <summary>
    /// The motion tables the intro code reads rather than computes: how the grunt intro's six emblems fly in, and
    /// the column order of the black block wipe. Both are edited in place and written back only where changed.
    /// </summary>
    public sealed class VsIntroMotion
    {
        public const int FlightCount = 6, BlockColumns = 8;
        private const int FlightSize = 32, Fx = 4096, Turn = 0xFFFF;

        /// <summary>One emblem: start and end in pixels, starting speeds, frames to wait before it, and whole turns.</summary>
        public sealed class Flight
        {
            public int StartX, EndX, SpeedX, StartY, EndY, SpeedY, Wait, Turns;
        }

        public VsIntroMotionSites Sites { get; }
        public IReadOnlyList<Flight> Flights => _flights;
        public int[] BlockOrder { get; private set; }

        private readonly List<Flight> _flights = new();
        private readonly string _flightPath, _blockPath;
        private byte[] _flightSaved, _blockSaved;

        private VsIntroMotion(VsIntroMotionSites sites, string flightPath, string blockPath)
        {
            Sites = sites; _flightPath = flightPath; _blockPath = blockPath;
        }

        /// <summary>Why this ROM's intro motion can't be edited, or null.</summary>
        public static string WhyNot()
        {
            VsIntroMotionSites sites = VsIntroMotionCodeSites;
            if (sites == null) return "Intro motion is mapped for US English HeartGold, SoulSilver, Platinum, Diamond and Pearl only.";
            return null;
        }

        /// <summary>Reads both tables, or returns null with <paramref name="why"/> when they aren't what the game ships.</summary>
        public static VsIntroMotion Load(out string why)
        {
            why = WhyNot();
            if (why != null) return null;
            VsIntroMotionSites sites = VsIntroMotionCodeSites;
            string flightPath = OverlayFile(sites.FlightOverlay), blockPath = OverlayFile(sites.BlockOverlay);
            if (!Fits(flightPath, sites.FlightTable, FlightCount * FlightSize) || !Fits(blockPath, sites.BlockOrder, BlockColumns))
            {
                why = "The intro code is shorter than expected, so its motion tables can't be found.";
                return null;
            }

            VsIntroMotion m = new VsIntroMotion(sites, flightPath, blockPath);
            m._flightSaved = DSUtils.ReadFromFile(flightPath, sites.FlightTable, FlightCount * FlightSize);
            m._blockSaved = DSUtils.ReadFromFile(blockPath, sites.BlockOrder, BlockColumns);
            for (int i = 0; i < FlightCount; i++)
            {
                int[] v = new int[8];
                for (int k = 0; k < 8; k++) v[k] = BitConverter.ToInt32(m._flightSaved, i * FlightSize + k * 4);
                if (v.Take(6).Any(x => x % Fx != 0) || v[7] % Turn != 0 || v[6] < 0 || v[6] > 255)
                {
                    why = "The emblem table doesn't hold what the game ships, so it was left alone.";
                    return null;
                }
                m._flights.Add(new Flight
                {
                    StartX = v[0] / Fx, EndX = v[1] / Fx, SpeedX = v[2] / Fx,
                    StartY = v[3] / Fx, EndY = v[4] / Fx, SpeedY = v[5] / Fx,
                    Wait = v[6], Turns = v[7] / Turn,
                });
            }
            m.BlockOrder = m._blockSaved.Select(b => (int)b).ToArray();
            if (!IsColumnOrder(m.BlockOrder))
            {
                why = "The block wipe order isn't what the game ships, so it was left alone.";
                return null;
            }
            return m;
        }

        /// <summary>Each of the eight columns once.</summary>
        public static bool IsColumnOrder(int[] order) =>
            order != null && order.Length == BlockColumns && order.OrderBy(x => x).SequenceEqual(Enumerable.Range(0, BlockColumns));

        private byte[] FlightBytes()
        {
            byte[] bytes = new byte[FlightCount * FlightSize];
            for (int i = 0; i < FlightCount; i++)
            {
                Flight f = _flights[i];
                int[] v = { f.StartX * Fx, f.EndX * Fx, f.SpeedX * Fx, f.StartY * Fx, f.EndY * Fx, f.SpeedY * Fx, f.Wait, f.Turns * Turn };
                for (int k = 0; k < 8; k++) BitConverter.GetBytes(v[k]).CopyTo(bytes, i * FlightSize + k * 4);
            }
            return bytes;
        }

        /// <summary>The tables as they would be written, for undo.</summary>
        public byte[] Snapshot() => FlightBytes().Concat(BlockOrder.Select(x => (byte)x)).ToArray();

        public void Restore(byte[] state)
        {
            if (state == null || state.Length != FlightCount * FlightSize + BlockColumns) return;
            for (int i = 0; i < FlightCount; i++)
            {
                int[] v = new int[8];
                for (int k = 0; k < 8; k++) v[k] = BitConverter.ToInt32(state, i * FlightSize + k * 4);
                Flight f = _flights[i];
                f.StartX = v[0] / Fx; f.EndX = v[1] / Fx; f.SpeedX = v[2] / Fx;
                f.StartY = v[3] / Fx; f.EndY = v[4] / Fx; f.SpeedY = v[5] / Fx;
                f.Wait = v[6]; f.Turns = v[7] / Turn;
            }
            for (int i = 0; i < BlockColumns; i++) BlockOrder[i] = state[FlightCount * FlightSize + i];
        }

        public bool HasChanges => !FlightBytes().SequenceEqual(_flightSaved) || !BlockOrder.Select(x => (byte)x).SequenceEqual(_blockSaved);

        /// <returns>null once written, or why nothing was.</returns>
        public string Save()
        {
            if (!IsColumnOrder(BlockOrder)) return "The block wipe order must use each of the columns 0 to 7 once.";
            foreach (Flight f in _flights)
            {
                if (f.Wait < 0 || f.Wait > 255) return "An emblem's wait must be between 0 and 255 frames.";
                if (Math.Abs(f.Turns) > 16) return "An emblem can turn at most 16 times.";
                foreach (int px in new[] { f.StartX, f.EndX, f.SpeedX, f.StartY, f.EndY, f.SpeedY })
                    if (Math.Abs(px) > 4096) return "Positions and speeds must stay within 4096 pixels.";
            }

            byte[] flights = FlightBytes(), blocks = BlockOrder.Select(x => (byte)x).ToArray();
            if (!flights.SequenceEqual(_flightSaved)) DSUtils.WriteToFile(_flightPath, flights, (uint)Sites.FlightTable);
            if (!blocks.SequenceEqual(_blockSaved)) DSUtils.WriteToFile(_blockPath, blocks, (uint)Sites.BlockOrder);
            _flightSaved = flights;
            _blockSaved = blocks;
            return null;
        }

        private static bool Fits(string path, int offset, int length) =>
            File.Exists(path) && offset >= 0 && new FileInfo(path).Length >= offset + length;

        private static string OverlayFile(int ov)
        {
            if (OverlayUtils.IsStillCompressed(ov)) OverlayUtils.Decompress(ov);
            return OverlayUtils.GetPath(ov);
        }
    }
}
