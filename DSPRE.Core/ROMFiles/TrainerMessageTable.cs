using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DSPRE.ROMFiles
{
    /// <summary>
    /// Trainer battle messages: a table of (trainer, trigger) rows whose row number is the message id in the trainer
    /// text archive, plus an offset file holding each trainer's first row.
    /// </summary>
    public static class TrainerMessageTable
    {
        public struct Entry { public int MessageId; public uint TrainerId; public ushort TriggerId; }

        private static string TablePath => Path.Combine(RomInfo.gameDirs[RomInfo.DirNames.trainerTextTable].unpackedDir, "0000");
        private static string OffsetPath => Path.Combine(RomInfo.gameDirs[RomInfo.DirNames.trainerTextOffset].unpackedDir, "0000");

        public static List<Entry> Read()
        {
            var entries = new List<Entry>();
            using var reader = new DSUtils.EasyReader(TablePath);
            while (reader.BaseStream.Position + 4 <= reader.BaseStream.Length)
            {
                int offset = (int)reader.BaseStream.Position;
                ushort trainerId = reader.ReadUInt16();
                ushort triggerId = reader.ReadUInt16();
                entries.Add(new Entry { MessageId = offset / 4, TrainerId = trainerId, TriggerId = triggerId });
            }
            return entries;
        }

        /// <summary>
        /// Rewrites the table and the offsets, and rebuilds the text archive in the new row order from
        /// <paramref name="messages"/>. The game finds a trainer's first row and reads on while the trainer matches,
        /// so each trainer's rows are kept together; otherwise rows stay in the order given, which is the game's own
        /// order for rows that were read.
        /// </summary>
        public static void Write(IEnumerable<Entry> entries, IReadOnlyList<string> messages)
        {
            var sorted = entries.GroupBy(e => e.TrainerId).SelectMany(g => g).ToList();
            var firstRow = new Dictionary<uint, ushort>();
            var text = new List<string>();

            // Truncate so entries deleted since the last save don't survive past the new end.
            using (var writer = new DSUtils.EasyWriter(TablePath, 0, FileMode.Create))
            {
                foreach (var e in sorted)
                {
                    if (!firstRow.ContainsKey(e.TrainerId)) firstRow[e.TrainerId] = (ushort)writer.BaseStream.Position;
                    writer.Write((ushort)e.TrainerId);
                    writer.Write(e.TriggerId);
                    text.Add(e.MessageId >= 0 && e.MessageId < messages.Count ? messages[e.MessageId] : "ERROR");
                }
            }

            new TextArchive(RomInfo.trainerMessageTextNumber, text).SaveToExpandedDir(RomInfo.trainerMessageTextNumber, false);

            using var offsetWriter = new DSUtils.EasyWriter(OffsetPath);
            foreach (var kvp in firstRow)
            {
                offsetWriter.Seek((int)kvp.Key * 2, SeekOrigin.Begin);
                offsetWriter.Write(kvp.Value);
            }
        }
    }
}
