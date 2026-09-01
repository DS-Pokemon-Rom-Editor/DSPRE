using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DSPRE.ROMFiles
{
    /// <summary>One entry of the building-animation list (the engine's F3D_MDL_INFO, 24 bytes). </summary>
    public class BuildingAnimationInfo
    {
        /// <summary>HeartGold and SoulSilver write 24 bytes a record. </summary>
        public const int Size = 24;
        public const int ShortSize = 20;
        public const int MaxAnimations = 4;          // ONE_MODEL_ANM_NUM_MAX
        public const uint NoAnimation = 0xFFFFFFFF;  // ANIME_NONE_CODE
        public const byte TypeTimeOfDay = 0x8;       // TYPE_TIME_ANIME, HeartGold and SoulSilver only
        public const byte ConditionalBit = 0x1;      // set means something has to start it off
        public const byte SetConditionalBit = 0x2;   // set means something has to put it on the map

        public byte Flag;          // whether this model animates at all
        public byte Type;          // animation type; bit 0x8 means it depends on the time of day
        public byte Suicide;       // plays once and takes itself away, rather than looping
        public byte RepeatEntry;   // may be registered more than once
        public byte Door;          // this is a door animation
        public byte Dummy;
        // Not a count of the filled Code slots: plenty of entries say 1 while using two. Treat the
        // slots themselves as the source of truth and these two as engine bookkeeping.
        public byte AnimationCount;
        public byte SetCount;
        public int[] Codes = new int[MaxAnimations];

        /// <summary>False when the model has no animation at all (the list marks those with 0xFF).</summary>
        public bool Animates => Flag != 0xFF && Flag != 0 && UsedCodes.Any();

        /// <summary>Only plays at certain times of day.</summary>
        public bool IsTimeOfDay => !ShortLayout && Type == TypeTimeOfDay;

        /// <summary>
        /// The animation waits for something to set it off rather than running by itself.
        /// </summary>
        public bool IsConditional => Type != 0xFF
                                  && (ShortLayout ? (Type & ConditionalBit) != 0
                                                  : Type != TypeTimeOfDay && (Type & ConditionalBit) != 0);

        /// <summary>
        /// Something has to put this animation on the map in the first place, rather than it being there
        /// from the start.
        /// </summary>
        public bool NeedsSetting => Type != 0xFF
                                 && (ShortLayout ? (Type & SetConditionalBit) != 0
                                                 : Type == TypeTimeOfDay || (Type & SetConditionalBit) != 0);

        /// <summary>
        /// A door: the engine opens and closes it when you go through, with its own sound.
        /// </summary>
        public bool IsDoor => !ShortLayout && Door != 0;

        /// <summary>Plays through once instead of looping. </summary>
        public bool PlaysOnce => Suicide != 0;

        /// <summary>
        /// True when the animation simply runs while you are on the map, with nothing needed to start it.
        /// </summary>
        public bool PlaysUnprompted => Animates && !IsConditional && !IsTimeOfDay && !PlaysOnce;

        /// <summary>How many times it repeats: once for a play-once animation, forever for the rest.</summary>
        public int LoopCount => PlaysOnce ? 1 : LoopForever;

        /// <summary>The engine's LOOP_INFINIT.</summary>
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
        /// none of the fields after Suicide.
        /// </summary>
        public bool ShortLayout { get; private set; }

        public BuildingAnimationInfo(byte[] data)
        {
            ShortLayout = data != null && data.Length < Size;
            using (BinaryReader reader = new BinaryReader(new MemoryStream(data)))
            {
                Flag = reader.ReadByte();
                Type = reader.ReadByte();
                Suicide = reader.ReadByte();
                if (ShortLayout)
                {
                    Dummy = reader.ReadByte();      // there for four byte alignment and nothing else
                }
                else
                {
                    RepeatEntry = reader.ReadByte();
                    Door = reader.ReadByte();
                    Dummy = reader.ReadByte();
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
                writer.Write(Flag); writer.Write(Type); writer.Write(Suicide);
                if (ShortLayout)
                {
                    writer.Write(Dummy);
                }
                else
                {
                    writer.Write(RepeatEntry);
                    writer.Write(Door); writer.Write(Dummy); writer.Write(AnimationCount); writer.Write(SetCount);
                }
                for (int i = 0; i < MaxAnimations; i++) writer.Write(Codes[i]);
                return ms.ToArray();
            }
        }
    }
}
