using System.Collections.Generic;
using System.Linq;

namespace DSPRE.ROMFiles
{
    /// <summary>
    /// The overworld event_type values the games actually define (fieldobj_code.h). DP/Pt has one more
    /// than HGSS. Every trainer variant is a real trainer to the engine (ev_trainer.c folds 0x02 and
    /// 0x04-0x08 back to EV_TYPE_TRAINER before doing sight checks); what differs is how the NPC moves
    /// and which extra parameter it reads.
    /// </summary>
    public sealed class OverworldEventType
    {
        public ushort Value;
        public string Name;
        /// <summary>Engine treats it as a trainer: it does sight detection and its script is a trainer id.</summary>
        public bool IsTrainer;
        /// <summary>Meaning of param1 for this type, or null when the engine never reads it.</summary>
        public string Param1Label;
        /// <summary>Extra explanation shown under the picker.</summary>
        public string Note;

        public override string ToString() => $"[{Value:D2}]  {Name}";
    }

    public static class OverworldEventTypes
    {
        private const string SpinNote =
            "The engine still does normal trainer sight detection; the type only changes how the NPC turns.";

        private static readonly OverworldEventType[] Shared =
        {
            new OverworldEventType { Value = 0,  Name = "Standard" },
            new OverworldEventType { Value = 1,  Name = "Trainer", IsTrainer = true,
                Note = "Sees straight ahead, as far as Sight range." },
            new OverworldEventType { Value = 2,  Name = "Trainer, all-way sight", IsTrainer = true,
                Note = "Same sight range, but checked in all four directions instead of only where it faces." },
            new OverworldEventType { Value = 3,  Name = "Item" },
            new OverworldEventType { Value = 4,  Name = "Trainer, glancing", IsTrainer = true,
                Param1Label = "Glance interval", Note = "Looks around on the spot. " + SpinNote },
            new OverworldEventType { Value = 5,  Name = "Trainer, spin ↺", IsTrainer = true,
                Param1Label = "Spin interval", Note = "Turns on the spot, anticlockwise. " + SpinNote },
            new OverworldEventType { Value = 6,  Name = "Trainer, spin ↻", IsTrainer = true,
                Param1Label = "Spin interval", Note = "Turns on the spot, clockwise. " + SpinNote },
            new OverworldEventType { Value = 7,  Name = "Trainer, moving spin ↺", IsTrainer = true,
                Note = "Turns anticlockwise as it walks its route. " + SpinNote },
            new OverworldEventType { Value = 8,  Name = "Trainer, moving spin ↻", IsTrainer = true,
                Note = "Turns clockwise as it walks its route. " + SpinNote },
            new OverworldEventType { Value = 9,  Name = "Message",
                Note = "Runs the game's shared message script instead of this map's scripts, so the number below is a Message ID." },
        };

        private static readonly OverworldEventType PtEscape =
            new OverworldEventType { Value = 10, Name = "Trainer, flees", IsTrainer = true,
                Note = "Diamond/Pearl/Platinum only." };

        /// <summary>Types this game family defines. HGSS stops at Message; DP/Pt adds the fleeing trainer.</summary>
        public static IReadOnlyList<OverworldEventType> For(RomInfo.GameFamilies family)
            => family == RomInfo.GameFamilies.HGSS
                ? Shared
                : Shared.Concat(new[] { PtEscape }).ToArray();

        public static OverworldEventType Find(RomInfo.GameFamilies family, ushort value)
            => For(family).FirstOrDefault(t => t.Value == value);
    }
}
