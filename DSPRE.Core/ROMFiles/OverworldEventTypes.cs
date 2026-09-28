using System.Collections.Generic;
using System.Linq;

namespace DSPRE.ROMFiles
{
    /// <summary>An overworld type value (u16 at +0x06) the games actually give a meaning.</summary>
    public sealed class OverworldEventType
    {
        public ushort Value;
        public string Name;
        /// <summary>The game treats it as a trainer: it watches for the player and its script is a trainer id.</summary>
        public bool IsTrainer;
        /// <summary>Meaning of the first data field for a non-trainer type that reads it, else null.</summary>
        public string Param0Label;
        /// <summary>Meaning of the second data field for this type, or null when the game never reads it.</summary>
        public string Param1Label;
        /// <summary>Short note shown under the picker.</summary>
        public string Note;

        public override string ToString() => $"[{Value:D2}]  {Name}";
    }

    public static class OverworldEventTypes
    {
        // Types 4-6 count steps the object itself walks, so one that never moves never looks.
        private const string WalkingNote = "Only looks while walking.";

        private static readonly OverworldEventType[] Shared =
        {
            new OverworldEventType { Value = 0, Name = "Standard" },
            new OverworldEventType { Value = 1, Name = "Trainer", IsTrainer = true },
            new OverworldEventType { Value = 2, Name = "Trainer, sees all ways", IsTrainer = true },
            new OverworldEventType { Value = 3, Name = "Item" },
            new OverworldEventType { Value = 4, Name = "Trainer, looks to the sides", IsTrainer = true,
                Param1Label = "Steps between looks", Note = WalkingNote },
            new OverworldEventType { Value = 5, Name = "Trainer, looks round anticlockwise", IsTrainer = true,
                Param1Label = "Steps between looks", Note = WalkingNote },
            new OverworldEventType { Value = 6, Name = "Trainer, looks round clockwise", IsTrainer = true,
                Param1Label = "Steps between looks", Note = WalkingNote },
            new OverworldEventType { Value = 7, Name = "Trainer, turns anticlockwise on a route", IsTrainer = true },
            new OverworldEventType { Value = 8, Name = "Trainer, turns clockwise on a route", IsTrainer = true },
            new OverworldEventType { Value = 9, Name = "Silent", Note = "Talking to it runs nothing." },
        };

        // Not a trainer: it never battles, and Vs Seeker and partner searches skip it.
        private static readonly OverworldEventType PtJumper =
            new OverworldEventType { Value = 10, Name = "Jumps when approached",
                Param0Label = "Jump distance", Note = "Only with movements 37 to 44." };

        /// <summary>Types this game family defines. Only Platinum has type 10.</summary>
        public static IReadOnlyList<OverworldEventType> For(RomInfo.GameFamilies family)
            => family == RomInfo.GameFamilies.Plat
                ? Shared.Concat(new[] { PtJumper }).ToArray()
                : Shared;

        public static OverworldEventType Find(RomInfo.GameFamilies family, ushort value)
            => For(family).FirstOrDefault(t => t.Value == value);
    }
}
