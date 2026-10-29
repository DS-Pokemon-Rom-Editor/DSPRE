using System;
using System.Collections.Generic;
using System.IO;
using static DSPRE.RomInfo;

namespace DSPRE.ROMFiles
{
    /// <summary>What HGSS followers do when talked to: the slots, the reactions they pick, and movements.</summary>
    // Records keep their raw bytes, so bits nothing reads round-trip unchanged.
    public static class FollowerTalk
    {
        public const int SlotSize = 20, GeneralSlots = 70, PlaceSlots = 30;
        public const int ReactionSize = 52, StepCount = 5, StepSize = 8;
        public const int MoveSize = 80, FrameCount = 10, FrameSize = 8;
        public const int SpeciesCount = 493;
        /// <summary>The reaction number is 10 bits in a slot.</summary>
        public const int ReactionLimit = 1023;
        public const ushort Cry = 2379, WeakCry = 2380;
        public const int BubbleCount = 14;

        /// <summary>The game tries the first 12 general slots, then the place's 30, then the other 58 general ones.</summary>
        public const int FirstGeneralSlots = 12;

        public static string WhyNot()
        {
            if (GameFamily != GameFamilies.HGSS) return "Followers exist only in HeartGold and SoulSilver.";
            if (FollowerTalkTextArchive < 0) return "DSPRE doesn't know where follower lines are in this language.";
            DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.FollowerTalkTriggers, DirNames.FollowerTalkReactions, DirNames.FollowerTalkMoves, DirNames.FollowerSpeciesGroups });
            if (Size(DirNames.FollowerTalkTriggers, 0) != GeneralSlots * SlotSize) return "Follower slots aren't laid out like the original game.";
            if (Size(DirNames.FollowerTalkReactions, 0) != ReactionSize) return "Follower reactions aren't laid out like the original game.";
            if (Size(DirNames.FollowerTalkMoves, 0) != MoveSize) return "Follower movements aren't laid out like the original game.";
            if (Size(DirNames.FollowerSpeciesGroups, 0) < SpeciesCount) return "The follower species group table is too short.";
            return null;
        }

        private static long Size(DirNames dir, int id)
        {
            string path = ArchiveMembers.PathOf(dir, id);
            return File.Exists(path) ? new FileInfo(path).Length : -1;
        }

        public static List<Slot> ReadSlots(byte[] member)
        {
            List<Slot> slots = new List<Slot>();
            for (int at = 0; at + SlotSize <= member.Length; at += SlotSize) slots.Add(new Slot(member.AsSpan(at, SlotSize).ToArray()));
            return slots;
        }

        public static byte[] ToBytes(IReadOnlyList<Slot> slots)
        {
            byte[] data = new byte[slots.Count * SlotSize];
            for (int i = 0; i < slots.Count; i++) slots[i].Raw.CopyTo(data, i * SlotSize);
            return data;
        }

        /// <summary>Member 0 holds the general slots; place P's slots are in member P + 1.</summary>
        public static int MemberOfPlace(int place) => place + 1;

        private static int Bits(int value, int shift, int width) => (value >> shift) & ((1 << width) - 1);
        private static int WithBits(int value, int shift, int width, int bits)
        {
            int mask = ((1 << width) - 1) << shift;
            return (value & ~mask) | ((bits << shift) & mask);
        }

        /// <summary>One trigger slot. Every condition is 0 for "any".</summary>
        public sealed class Slot
        {
            public byte[] Raw { get; }
            public Slot(byte[] raw)
            {
                if (raw == null || raw.Length != SlotSize) throw new ArgumentException($"A follower slot is {SlotSize} bytes.");
                Raw = raw;
            }
            public static Slot Empty() => new Slot(new byte[SlotSize]);
            public Slot Clone() => new Slot((byte[])Raw.Clone());

            private int U16(int at) => BitConverter.ToUInt16(Raw, at);
            private void U16(int at, int v) { Raw[at] = (byte)v; Raw[at + 1] = (byte)(v >> 8); }

            public int Hp { get => Raw[0]; set => Raw[0] = (byte)value; }
            public int Mood { get => Bits(Raw[1], 0, 4); set => Raw[1] = (byte)WithBits(Raw[1], 0, 4, value); }
            public int Friendship { get => Bits(Raw[1], 4, 4); set => Raw[1] = (byte)WithBits(Raw[1], 4, 4, value); }
            public int Nature { get => Bits(Raw[2], 0, 3); set => Raw[2] = (byte)WithBits(Raw[2], 0, 3, value); }
            public int Gender { get => Bits(Raw[2], 3, 2); set => Raw[2] = (byte)WithBits(Raw[2], 3, 2, value); }
            public int Status { get => Bits(Raw[2], 5, 3); set => Raw[2] = (byte)WithBits(Raw[2], 5, 3, value); }
            public int HeldItem { get => Bits(Raw[3], 0, 5); set => Raw[3] = (byte)WithBits(Raw[3], 0, 5, value); }
            public int Type { get => Bits(Raw[4], 0, 5); set => Raw[4] = (byte)WithBits(Raw[4], 0, 5, value); }
            public int Pokeathlon { get => Bits(Raw[4], 5, 3); set => Raw[4] = (byte)WithBits(Raw[4], 5, 3, value); }
            public int EncounterTile { get => Raw[5]; set => Raw[5] = (byte)value; }
            public int SpeciesGroup { get => Raw[6]; set => Raw[6] = (byte)value; }
            public int Leaf { get => Raw[7]; set => Raw[7] = (byte)value; }
            public int Weather { get => Bits(U16(8), 0, 3); set => U16(8, WithBits(U16(8), 0, 3, value)); }
            public int Facing { get => Bits(U16(8), 13, 3); set => U16(8, WithBits(U16(8), 13, 3, value)); }
            public int People { get => Bits(U16(10), 0, 3); set => U16(10, WithBits(U16(10), 0, 3, value)); }
            public int Time { get => Bits(U16(10), 3, 3); set => U16(10, WithBits(U16(10), 3, 3, value)); }
            public int Reaction { get => Bits(U16(10), 6, 10); set => U16(10, WithBits(U16(10), 6, 10, value)); }
            /// <summary>Map header number + 1.</summary>
            public int Map { get => U16(12); set => U16(12, value); }
            public int Tile { get => U16(14); set => U16(14, value); }
            public int Level { get => Bits(Raw[16], 1, 2); set => Raw[16] = (byte)WithBits(Raw[16], 1, 2, value); }
            public int FieldMove { get => Bits(Raw[16], 3, 2); set => Raw[16] = (byte)WithBits(Raw[16], 3, 2, value); }
            public int HiddenItems { get => Bits(Raw[16], 5, 3); set => Raw[16] = (byte)WithBits(Raw[16], 5, 3, value); }
            public int Chance { get => Raw[17]; set => Raw[17] = (byte)value; }
            public int Flag { get => U16(18); set => U16(18, value); }

            /// <summary>The game skips a slot with no reaction.</summary>
            public bool IsEmpty => Reaction == 0;
        }

        /// <summary>One reaction: up to five steps, then an optional question, changes and gift.</summary>
        public sealed class ReactionData
        {
            public byte[] Raw { get; }
            public ReactionData(byte[] raw)
            {
                if (raw == null || raw.Length != ReactionSize) throw new ArgumentException($"A follower reaction is {ReactionSize} bytes.");
                Raw = raw;
            }

            private int U16(int at) => BitConverter.ToUInt16(Raw, at);
            private void U16(int at, int v) { Raw[at] = (byte)v; Raw[at + 1] = (byte)(v >> 8); }

            /// <summary>Steps run until one whose movement is 0xFFFF.</summary>
            public int Steps
            {
                get
                {
                    int n = 0;
                    while (n < StepCount && U16(n * StepSize) != 0xFFFF) n++;
                    return n;
                }
            }

            public int Move(int step) => U16(step * StepSize);
            public void SetMove(int step, int v) => U16(step * StepSize, v);
            public int Line(int step) => U16(step * StepSize + 2);
            public void SetLine(int step, int v) => U16(step * StepSize + 2, v);
            public int Sound(int step) => U16(step * StepSize + 4);
            public void SetSound(int step, int v) => U16(step * StepSize + 4, v);
            public int Bubble(int step) => Raw[step * StepSize + 6];
            public void SetBubble(int step, int v) => Raw[step * StepSize + 6] = (byte)v;
            public int Wait(int step) => Raw[step * StepSize + 7];
            public void SetWait(int step, int v) => Raw[step * StepSize + 7] = (byte)v;

            // Unused steps are all 0xFF in retail.
            public void AddStep()
            {
                int n = Steps;
                if (n >= StepCount) return;
                Array.Clear(Raw, n * StepSize, StepSize);
            }

            public void RemoveStep(int step)
            {
                int n = Steps;
                if (step < 0 || step >= n) return;
                Array.Copy(Raw, (step + 1) * StepSize, Raw, step * StepSize, (n - step - 1) * StepSize);
                Raw.AsSpan((n - 1) * StepSize, StepSize).Fill(0xFF);
            }

            public void SwapSteps(int a, int b)
            {
                byte[] t = Raw.AsSpan(a * StepSize, StepSize).ToArray();
                Array.Copy(Raw, b * StepSize, Raw, a * StepSize, StepSize);
                t.CopyTo(Raw, b * StepSize);
            }

            public bool Question { get => BitConverter.ToInt32(Raw, 40) != 0; set => BitConverter.GetBytes(value ? 1 : 0).CopyTo(Raw, 40); }
            public int YesReaction { get => U16(44); set => U16(44, value); }
            public int NoReaction { get => U16(46); set => U16(46, value); }
            public int FriendshipChange { get => (sbyte)Raw[48]; set => Raw[48] = (byte)(sbyte)value; }
            public int MoodChange { get => (sbyte)Raw[49]; set => Raw[49] = (byte)(sbyte)value; }
            /// <summary>1-based accessory for the fashion case; 0 for none.</summary>
            public int Accessory { get => Raw[50]; set => Raw[50] = (byte)value; }
            /// <summary>Shiny Leaf 1-5; 0 for none.</summary>
            public int Leaf { get => Raw[51]; set => Raw[51] = (byte)value; }
        }

        /// <summary>One movement: up to ten frames, ending at a frame whose facing is 0xFF.</summary>
        public sealed class MoveData
        {
            public byte[] Raw { get; }
            public MoveData(byte[] raw)
            {
                if (raw == null || raw.Length != MoveSize) throw new ArgumentException($"A follower movement is {MoveSize} bytes.");
                Raw = raw;
            }
            public static MoveData New()
            {
                byte[] raw = new byte[MoveSize];
                raw.AsSpan().Fill(0xFF);
                return new MoveData(raw);
            }

            public int Frames
            {
                get
                {
                    int n = 0;
                    while (n < FrameCount && Raw[n * FrameSize] != 0xFF) n++;
                    return n;
                }
            }

            /// <summary>0 keeps the facing; 1 up, 2 down, 3 left, 4 right.</summary>
            public int Facing(int f) => Raw[f * FrameSize];
            public void SetFacing(int f, int v) => Raw[f * FrameSize] = (byte)v;
            public int Length(int f) => Raw[f * FrameSize + 1];
            public void SetLength(int f, int v) => Raw[f * FrameSize + 1] = (byte)v;
            public int X(int f) => (sbyte)Raw[f * FrameSize + 2];
            public void SetX(int f, int v) => Raw[f * FrameSize + 2] = (byte)(sbyte)v;
            /// <summary>Added to the follower's hop height; skipped for Diglett and Dugtrio.</summary>
            public int Hop(int f) => (sbyte)Raw[f * FrameSize + 3];
            public void SetHop(int f, int v) => Raw[f * FrameSize + 3] = (byte)(sbyte)v;
            public int Z(int f) => (sbyte)Raw[f * FrameSize + 4];
            public void SetZ(int f, int v) => Raw[f * FrameSize + 4] = (byte)(sbyte)v;
            /// <summary>The step's sound plays on frames with this set.</summary>
            public bool PlaysSound(int f) => Raw[f * FrameSize + 5] != 0;
            public void SetPlaysSound(int f, bool v) => Raw[f * FrameSize + 5] = (byte)(v ? 1 : 0);

            public void AddFrame()
            {
                int n = Frames;
                if (n >= FrameCount) return;
                Array.Clear(Raw, n * FrameSize, 6);
                Raw[n * FrameSize + 1] = 4;
                Raw[n * FrameSize + 6] = Raw[n * FrameSize + 7] = 0xFF;
            }

            public void RemoveFrame(int f)
            {
                int n = Frames;
                if (f < 0 || f >= n) return;
                Array.Copy(Raw, (f + 1) * FrameSize, Raw, f * FrameSize, (n - f - 1) * FrameSize);
                Raw.AsSpan((n - 1) * FrameSize, FrameSize).Fill(0xFF);
            }
        }
    }
}
