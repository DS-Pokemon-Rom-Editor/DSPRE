using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DSPRE.ROMFiles
{
    /// <summary>One entry of the building-animation list, 24 bytes.</summary>
    public class BuildingAnimationInfo
    {
        /// <summary>HeartGold and SoulSilver write 24 bytes a record. </summary>
        public const int Size = 24;
        public const int ShortSize = 20;
        public const int MaxAnimations = 4;
        public const uint NoAnimation = 0xFFFFFFFF;
        public const byte TypeTimeOfDay = 0x8;       // HeartGold and SoulSilver only
        public const byte ConditionalBit = 0x1;      // set means something has to start it off
        public const byte SetConditionalBit = 0x2;   // set means something has to put it on the map

        public byte HasAnimations;
        public byte Flags;         // 8 means it follows the time of day
        public byte IsBicycleSlope; // plays once instead of looping
        public byte MayRepeat;
        public byte DoorKind;      // which door sound it plays
        public byte Padding;
        // Not a count of the filled Code slots: plenty of entries say 1 while using two. Treat the
        // slots themselves as the source of truth and these two as engine bookkeeping.
        public byte AnimationCount;
        public byte SetCount;
        public int[] Codes = new int[MaxAnimations];

        /// <summary>False when the model has no animation at all (the list marks those with 0xFF).</summary>
        public bool Animates => HasAnimations != 0xFF && HasAnimations != 0 && UsedCodes.Any();

        /// <summary>Only plays at certain times of day.</summary>
        public bool IsTimeOfDay => !ShortLayout && Flags == TypeTimeOfDay;

        /// <summary>
        /// The animation waits for something to set it off rather than running by itself.
        /// </summary>
        public bool IsConditional => Flags != 0xFF
                                  && (ShortLayout ? (Flags & ConditionalBit) != 0
                                                  : Flags != TypeTimeOfDay && (Flags & ConditionalBit) != 0);

        /// <summary>
        /// Something has to put this animation on the map in the first place, rather than it being there
        /// from the start.
        /// </summary>
        public bool NeedsSetting => Flags != 0xFF
                                 && (ShortLayout ? (Flags & SetConditionalBit) != 0
                                                 : Flags == TypeTimeOfDay || (Flags & SetConditionalBit) != 0);

        /// <summary>
        /// A door: the engine opens and closes it when you go through, with its own sound.
        /// </summary>
        public bool IsDoor => !ShortLayout && DoorKind != 0;

        /// <summary>Plays through once instead of looping. </summary>
        public bool PlaysOnce => IsBicycleSlope != 0;

        /// <summary>
        /// True when the animation simply runs while you are on the map, with nothing needed to start it.
        /// </summary>
        public bool PlaysUnprompted => Animates && !IsConditional && !IsTimeOfDay && !PlaysOnce;

        /// <summary>How many times it repeats: once for a play-once animation, forever for the rest.</summary>
        public int LoopCount => PlaysOnce ? 1 : LoopForever;

        public const int LoopForever = -1;

        /// <summary>The animation archive indices actually in use, skipping the empty slots.</summary>
        public IEnumerable<int> UsedCodes
        {
            get
            {
                for (int i = 0; i < MaxAnimations; i++)
                    if (unchecked((uint)Codes[i]) != NoAnimation) yield return Codes[i];
            }
        }

        /// <summary>
        /// True when this record came from the shorter Diamond, Pearl and Platinum layout, which carries
        /// none of the fields after IsBicycleSlope.
        /// </summary>
        public bool ShortLayout { get; private set; }

        public BuildingAnimationInfo(byte[] data)
        {
            ShortLayout = data != null && data.Length < Size;
            using (BinaryReader reader = new BinaryReader(new MemoryStream(data)))
            {
                HasAnimations = reader.ReadByte();
                Flags = reader.ReadByte();
                IsBicycleSlope = reader.ReadByte();
                if (ShortLayout)
                {
                    Padding = reader.ReadByte();      // there for four byte alignment and nothing else
                }
                else
                {
                    MayRepeat = reader.ReadByte();
                    DoorKind = reader.ReadByte();
                    Padding = reader.ReadByte();
                    AnimationCount = reader.ReadByte();
                    SetCount = reader.ReadByte();
                }
                for (int i = 0; i < MaxAnimations; i++) Codes[i] = reader.ReadInt32();
            }
        }

        /// <summary>Writes the record back in whichever layout it was read in, so a Platinum file stays a
        /// Platinum file.</summary>
        public byte[] ToByteArray()
        {
            using (MemoryStream ms = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(ms))
            {
                writer.Write(HasAnimations); writer.Write(Flags); writer.Write(IsBicycleSlope);
                if (ShortLayout)
                {
                    writer.Write(Padding);
                }
                else
                {
                    writer.Write(MayRepeat);
                    writer.Write(DoorKind); writer.Write(Padding); writer.Write(AnimationCount); writer.Write(SetCount);
                }
                for (int i = 0; i < MaxAnimations; i++) writer.Write(Codes[i]);
                return ms.ToArray();
            }
        }
    }
}
