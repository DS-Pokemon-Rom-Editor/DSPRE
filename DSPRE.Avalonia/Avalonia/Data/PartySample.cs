using System;

namespace DSPRE.Avalonia.Data
{
    /// <summary>The party the party screen previews are drawn with; the Screens editor's Sample panel changes it.</summary>
    public static class PartySample
    {
        public sealed class Pokemon
        {
            /// <summary>0 for an empty slot.</summary>
            public int Species;
            public int Level;
            public int Hp;
            public int MaxHp;
            /// <summary>0 male, 1 female, 2 none.</summary>
            public int Gender;
            /// <summary>0 none, then the game's order: 1 paralysis, 2 freeze, 3 sleep, 4 poison, 5 burn.</summary>
            public int Condition;
            /// <summary>0 nothing, 1 an item, 2 mail.</summary>
            public int Item;
            public bool Egg;
            public bool Empty => Species == 0 && !Egg;
        }

        // One of every panel look: full, holding an item, yellow and red HP, a condition, fainted, an egg.
        public static readonly Pokemon[] Party =
        {
            new() { Species = 157, Level = 65, Hp = 237, MaxHp = 237, Gender = 0 },
            new() { Species = 149, Level = 73, Hp = 284, MaxHp = 284, Gender = 0, Item = 1 },
            new() { Species = 212, Level = 65, Hp = 70, MaxHp = 206, Gender = 1 },
            new() { Species = 25, Level = 40, Hp = 15, MaxHp = 95, Gender = 1, Condition = 1 },
            new() { Species = 445, Level = 50, Hp = 0, MaxHp = 180, Gender = 0 },
            new() { Species = 175, Level = 1, Hp = 1, MaxHp = 1, Gender = 2, Egg = true },
        };

        public static event Action Changed;

        public static void Raise() => Changed?.Invoke();
    }
}
