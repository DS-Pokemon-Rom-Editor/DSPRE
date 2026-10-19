using System.IO;
using DSPRE.ROMFiles;
using System;
using System.Text;
using System.Collections.Generic;
using static DSPRE.RomInfo;

namespace DSPRE
{
    public class MoveData : RomFile
    {
        public enum AttackRange : ushort
        {
            SELECTION = 0,
            VARIABLE = (1 << 0),
            RANDOM = (1 << 1),
            BOTH = (1 << 2),
            FOES_AND_ALLY = (1 << 3),
            USER = (1 << 4),
            USER_SIDE = (1 << 5),
            ACTIVE_FIELD = (1 << 6),
            OPPONENTS_FIELD = (1 << 7),
            ALLY = (1 << 8),
            ACUPRESSURE = (1 << 9),
            ME_FIRST = (1 << 10)
        };
        public enum MoveSplit : byte
        {
            PHYSICAL = 0,
            SPECIAL,
            STATUS,
        };
        public enum ContestCondition : byte
        {
            COOL = 0,
            BEAUTIFUL,
            CUTE,
            SMART,
            TOUGH,
        };

        // contestAppeal is a contest effect id; each effect's appeal points, ten to a heart (pokeplatinum
        // src/unk_02094EDC.c). The same in DP and Pt; HGSS keeps the byte but has no contests.
        public static readonly byte[] ContestEffectAppeal =
            { 0, 20, 20, 0, 20, 30, 10, 20, 0, 0, 20, 0, 0, 20, 20, 20, 0, 20, 20, 20, 10, 0, 20, 10 };

        public enum MoveFlags : byte
        {
            NONE = 0,
            CONTACT = (1 << 0),
            PROTECT = (1 << 1),
            MAGIC_COAT = (1 << 2),
            SNATCH = (1 << 3),
            MIRROR_MOVE = (1 << 4),
            KINGSROCK = (1 << 5),
            KEEP_HP_BAR = (1 << 6),
            DEL_SHADOW = (1 << 7)
        }

        // User-friendly range descriptions mapped to their flag values
        public static readonly (ushort value, string name, string description)[] AttackRangeDescriptions = new (ushort, string, string)[] {
            (0, "Opponent or Ally", "May target one adjacent opponent or ally"),
            ((1 << 0), "Varies", "Variable based on move effect"),
            ((1 << 1), "One Random Opponent", "Affects a random opponent"),
            ((1 << 2), "All Opponents", "Affects all adjacent opponents"),
            ((1 << 3), "All Others", "Affects all adjacent opponents and allies"),
            ((1 << 4), "User", "Affects the user"),
            ((1 << 5), "User Side", "Affects the user's side of the field"),
            ((1 << 6), "All Sides", "Affects the user's and opponent's sides of the field"),
            ((1 << 7), "Opponent Side", "Affects the opponent's side of the field"),
            ((1 << 8), "One Ally", "May target one adjacent ally"),
            ((1 << 9), "User or Ally", "May target the user or one adjacent ally"),
            ((1 << 10), "One Opponent", "May target any one adjacent opponent")
        };

        /// <summary>
        /// Gets the user-friendly name for an attack range value.
        /// </summary>
        public static string GetAttackRangeName(ushort targetValue)
        {
            foreach ((ushort value, string name, string description) range in AttackRangeDescriptions)
            {
                if (range.value == targetValue)
                {
                    return range.name;
                }
            }
            // If no exact match, build a combined string for multiple flags (shouldn't happen in vanilla)
            List<string> names = new List<string>();
            for (int i = 0; i < AttackRangeDescriptions.Length; i++)
            {
                if (AttackRangeDescriptions[i].value != 0 && (targetValue & AttackRangeDescriptions[i].value) != 0)
                {
                    names.Add(AttackRangeDescriptions[i].name);
                }
            }
            return names.Count > 0 ? string.Join("|", names) : "Unknown";
        }


        public ushort Battleeffect;
        public MoveSplit Split;
        public byte Damage;

        public PokemonType Movetype;
        public byte Accuracy;
        public byte Pp;
        public byte SideEffectProbability;

        public ushort Target;
        public sbyte Priority;
        public byte FlagField;

        public byte ContestAppeal;
        public ContestCondition ContestConditionType;
        // The record's last halfword, unused by the game; kept as found.
        private ushort _trailing;

        public MoveData(Stream stream)
        {
            using (BinaryReader reader = new BinaryReader(stream))
            {
                this.Battleeffect = reader.ReadUInt16();
                this.Split = (MoveSplit)reader.ReadByte();
                this.Damage = reader.ReadByte();

                this.Movetype = (PokemonType)reader.ReadByte();
                this.Accuracy = reader.ReadByte();
                this.Pp = reader.ReadByte();
                this.SideEffectProbability = reader.ReadByte();
                this.Target = reader.ReadUInt16(); //bitfield
                this.Priority = reader.ReadSByte();
                this.FlagField = reader.ReadByte();

                this.ContestAppeal = reader.ReadByte();
                this.ContestConditionType = (ContestCondition)reader.ReadByte();
                if (reader.BaseStream.Length - reader.BaseStream.Position >= 2) this._trailing = reader.ReadUInt16();
            }
        }

        public MoveData(int ID) : this(new FileStream(Path.Combine(RomInfo.GameDirs[DirNames.MoveData].unpackedDir, ID.ToString("D4")), FileMode.Open)) { }

        public override byte[] ToByteArray()
        {
            using (MemoryStream memoryStream = new MemoryStream())
            {
                using (BinaryWriter writer = new BinaryWriter(memoryStream))
                {
                    writer.Write(this.Battleeffect);
                    writer.Write((byte)this.Split);
                    writer.Write(this.Damage);

                    writer.Write((byte)this.Movetype);
                    writer.Write(this.Accuracy);
                    writer.Write(this.Pp);
                    writer.Write(this.SideEffectProbability);
                    writer.Write((ushort)this.Target);
                    writer.Write(this.Priority);
                    writer.Write(this.FlagField);

                    writer.Write(this.ContestAppeal);
                    writer.Write((byte)this.ContestConditionType);

                    writer.Write(this._trailing);
                }

                return memoryStream.ToArray();
            }
        }


        public void SaveToFileDefaultDir(int IDtoReplace, bool showSuccessMessage = true)
        {
            SaveToFileDefaultDir(DirNames.MoveData, IDtoReplace, showSuccessMessage);
        }
        public void SaveToFileExplorePath(string suggestedFileName, bool showSuccessMessage = true)
        {
            SaveToFileExplorePath("Gen IV Move Data", "bin", suggestedFileName, showSuccessMessage);
        }

        public static void UpdateFromText(MoveData m, string[] split)
        {
            int target = 1;
            if (split.Length < target + 1)
            {
                return;
            }
            else
            {
                if (!Enum.TryParse(split[target], true, out m.Movetype))
                {
                    AppMessages.Error($"Malformed entry: \"{string.Join(" ", split)}\".\nMove type is unreadable: \"{split[target]}\"", "Parser error");
                    return;
                }
                target++;

                if (split.Length < target + 1)
                {
                    return;
                }
                else
                {
                    if (!Enum.TryParse(split[target], true, out m.Split))
                    {
                        AppMessages.Error($"Malformed entry: \"{string.Join(" ", split)}\".\nMove split is unreadable: \"{split[target]}\"", "Parser error");
                        return;
                    }
                    target++;

                    if (split.Length < target + 1)
                    {
                        return;
                    }
                    else
                    {
                        if (!byte.TryParse(split[target], out m.Damage))
                        {
                            if (split[target].StartsWith("-") || split[target].StartsWith("—"))
                            {
                                m.Damage = 0;
                            }
                            else
                            {
                                AppMessages.Error($"Malformed entry: \"{string.Join(" ", split)}\".\nMove power is unreadable: \"{split[target]}\"", "Parser error");
                                return;
                            }

                        }
                        target++;

                        if (split.Length < target + 1)
                        {
                            return;
                        }
                        else
                        {
                            if (!byte.TryParse(split[target].Replace('%', ' '), out m.Accuracy))
                            {
                                if (split[target].StartsWith("-") || split[target].StartsWith("—"))
                                {
                                    m.Accuracy = 0;
                                }
                                else
                                {
                                    AppMessages.Error($"Malformed entry: \"{string.Join(" ", split)}\".\nMove accuracy is unreadable: \"{split[target]}\"", "Parser error");
                                    return;
                                }
                            }
                            target++;

                            if (split.Length < target + 1)
                            {
                                return;
                            }
                            else
                            {
                                if (!byte.TryParse(split[target].Replace('%', ' '), out m.Pp))
                                {
                                    AppMessages.Error($"Malformed entry: \"{string.Join(" ", split)}\".\nMove PP count is unreadable: \"{split[target]}\"", "Parser error");
                                    return;
                                }
                            }
                        }
                    }
                }
            }

            return;
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(Movetype.ToString());
            sb.Append('\t');

            sb.Append(Split.ToString());
            sb.Append('\t');

            if (Damage == 0)
            {
                sb.Append("-");
            }
            else
            {
                sb.Append(Damage);
            }
            sb.Append('\t');

            if (Accuracy == 0)
            {
                sb.Append("-");
            }
            else
            {
                sb.Append(Accuracy);
                sb.Append('%');
            }
            sb.Append('\t');

            sb.Append(Pp);

            return sb.ToString();
        }

        public override bool Equals(object obj)
        {
            return obj is MoveData data &&
                   Battleeffect == data.Battleeffect &&
                   Split == data.Split &&
                   Damage == data.Damage &&
                   Movetype == data.Movetype &&
                   Accuracy == data.Accuracy &&
                   Pp == data.Pp &&
                   SideEffectProbability == data.SideEffectProbability &&
                   Target == data.Target &&
                   Priority == data.Priority &&
                   FlagField == data.FlagField &&
                   ContestAppeal == data.ContestAppeal &&
                   ContestConditionType == data.ContestConditionType;
        }

        public override int GetHashCode()
        {
            int hashCode = -1756630415;
            hashCode = hashCode * -1521134295 + Battleeffect.GetHashCode();
            hashCode = hashCode * -1521134295 + Split.GetHashCode();
            hashCode = hashCode * -1521134295 + Damage.GetHashCode();
            hashCode = hashCode * -1521134295 + Movetype.GetHashCode();
            hashCode = hashCode * -1521134295 + Accuracy.GetHashCode();
            hashCode = hashCode * -1521134295 + Pp.GetHashCode();
            hashCode = hashCode * -1521134295 + SideEffectProbability.GetHashCode();
            hashCode = hashCode * -1521134295 + Target.GetHashCode();
            hashCode = hashCode * -1521134295 + Priority.GetHashCode();
            hashCode = hashCode * -1521134295 + FlagField.GetHashCode();
            hashCode = hashCode * -1521134295 + ContestAppeal.GetHashCode();
            hashCode = hashCode * -1521134295 + ContestConditionType.GetHashCode();
            return hashCode;
        }
    }
}
