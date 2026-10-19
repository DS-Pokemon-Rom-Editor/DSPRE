using DSPRE.Resources;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using static DSPRE.RomInfo;

namespace DSPRE.ROMFiles
{
    /// <summary>
    /// Classes to store event data in Pokémon NDS games
    /// </summary>
    public class EventFile : RomFile
    {
        public enum serializationOrder
        {
            Spawnables,
            Overworlds,
            Warps,
            Triggers
        }

        #region Fields
        public static readonly string DefaultFilter = "Event File (*.evt, *.ev)|*.evt;*.ev";

        /// <summary>The game runs a file whose only command is End for script 0.</summary>
        public const ushort NoScript = 0;

        public int ID { get; set; } = -1;
        public List<Spawnable> Spawnables = new List<Spawnable>();
        public List<Overworld> Overworlds = new List<Overworld>();
        public List<Warp> Warps = new List<Warp>();
        public List<Trigger> Triggers = new List<Trigger>();
        #endregion

        #region Constructors (1)
        public EventFile(Stream data)
        {
            using (BinaryReader reader = new BinaryReader(data))
            {
                /* Read spawnables */
                uint spawnablesCount = reader.ReadUInt32();
                for (int i = 0; i < spawnablesCount; i++)
                {
                    Spawnables.Add(new Spawnable(new MemoryStream(reader.ReadBytes(0x14))));
                }

                /* Read overworlds */
                uint overworldsCount = reader.ReadUInt32();
                for (int i = 0; i < overworldsCount; i++)
                {
                    Overworlds.Add(new Overworld(new MemoryStream(reader.ReadBytes(0x20))));
                }

                /* Read warps */
                uint warpsCount = reader.ReadUInt32();
                for (int i = 0; i < warpsCount; i++)
                {
                    Warps.Add(new Warp(new MemoryStream(reader.ReadBytes(0xC))));
                }

                /* Read triggers */
                uint triggersCount = reader.ReadUInt32();
                for (int i = 0; i < triggersCount; i++)
                {
                    Triggers.Add(new Trigger(new MemoryStream(reader.ReadBytes(0x10))));
                }
            }
        }
        public EventFile(int ID) : this(new FileStream(Path.Combine(RomInfo.GameDirs[DirNames.EventFiles].unpackedDir, ID.ToString("D4")), FileMode.Open))
        {
                    this.ID = ID;
                }
        public EventFile() { }
        #endregion

        #region Methods (1)
        public override string ToString()
        {
            return base.ToString();
        }
        public override byte[] ToByteArray()
        {
            MemoryStream newData = new MemoryStream();
            using (BinaryWriter writer = new BinaryWriter(newData))
            {
                /* Write spawnables */
                writer.Write((uint)Spawnables.Count);
                for (int i = 0; i < Spawnables.Count; i++)
                {
                    writer.Write(Spawnables[i].ToByteArray());
                }

                /* Write overworlds */
                writer.Write((uint)Overworlds.Count);
                for (int i = 0; i < Overworlds.Count; i++)
                {
                    writer.Write(Overworlds[i].ToByteArray());
                }

                /* Write warps */
                writer.Write((uint)Warps.Count);
                for (int i = 0; i < Warps.Count; i++)
                {
                    writer.Write(Warps[i].ToByteArray());
                }

                /* Write triggers */
                writer.Write((uint)Triggers.Count);
                for (int i = 0; i < Triggers.Count; i++)
                {
                    writer.Write(Triggers[i].ToByteArray());
                }
            }
            return newData.ToArray();
        }
        public void SaveToFileDefaultDir(int IDtoReplace, bool showSuccessMessage = true)
        {
            SaveToFileDefaultDir(DirNames.EventFiles, IDtoReplace, showSuccessMessage);
        }
        public void SaveToFileExplorePath(string suggestedFileName, bool showSuccessMessage = true)
        {
            SaveToFileExplorePath("Gen IV Event File", "ev", suggestedFileName, showSuccessMessage);
        }

        internal bool IsEmpty() => (Spawnables is null || Spawnables.Count == 0) &&
                (Overworlds is null || Overworlds.Count == 0) &&
                (Warps is null || Warps.Count == 0) &&
                (Triggers is null || Triggers.Count == 0);
        #endregion

    }

    public abstract class Event
    {
        public enum EventType : byte
        {
            Spawnable,
            Overworld,
            Warp,
            Trigger
        }
        #region Fields (6)
        public EventType EvType;

        public short XMapPosition;
        public short YMapPosition;
        public int ZPosition; //fixed point!
        public ushort XMatrixPosition;
        public ushort YMatrixPosition;
        #endregion

        #region Methods (1)
        public abstract byte[] ToByteArray();
        #endregion
    }

    public class Spawnable : Event
    {
        public const int TypeMisc = 0;
        public const int TypeBoard = 1;
        public const int TypeHiddenitem = 2;

        #region Fields (7)
        public ushort ScriptNumber;
        public ushort Type;
        public ushort Dir;
        public ushort Unknown5;
        #endregion

        #region Constructors (2)
        public Spawnable(Stream data)
        {
            EvType = EventType.Spawnable;
            using (BinaryReader reader = new BinaryReader(data))
            {
                // Record layout: u16 script, u16 type, s32 x, s32 z, s32 height, u16 dir, u16 padding.
                ScriptNumber = reader.ReadUInt16();
                Type = reader.ReadUInt16();

                /* Decompose x coordinate in matrix and map positions */
                int xPosition = reader.ReadInt32();
                XMapPosition = (short)(xPosition % MapFile.MapSize);
                XMatrixPosition = (ushort)(xPosition / MapFile.MapSize);

                /* Decompose y coordinate in matrix and map positions */
                int yPosition = reader.ReadInt32();
                YMapPosition = (short)(yPosition % MapFile.MapSize);
                YMatrixPosition = (ushort)(yPosition / MapFile.MapSize);

                ZPosition = reader.ReadInt32();
                Dir = reader.ReadUInt16();
                Unknown5 = reader.ReadUInt16();
            }
        }
        public Spawnable(int xMatrixPosition, int yMatrixPosition)
        {
            EvType = EventType.Spawnable;

            ScriptNumber = 0;
            Type = 0;
            Unknown5 = 0;
            Dir = 0;

            XMapPosition = 0;
            YMapPosition = 0;
            ZPosition = 0;
            this.XMatrixPosition = (ushort)xMatrixPosition;
            this.YMatrixPosition = (ushort)yMatrixPosition;
        }
        public Spawnable(Spawnable toCopy)
        {
            EvType = EventType.Spawnable;

            ScriptNumber = toCopy.ScriptNumber;
            Type = toCopy.Type;
            Unknown5 = toCopy.Unknown5;
            Dir = toCopy.Dir;

            XMapPosition = toCopy.XMapPosition;
            YMapPosition = toCopy.YMapPosition;
            ZPosition = toCopy.ZPosition;
            this.XMatrixPosition = toCopy.XMatrixPosition;
            this.YMatrixPosition = toCopy.YMatrixPosition;
        }
        #endregion

        #region Methods (1)
        public override byte[] ToByteArray()
        {
            using (BinaryWriter writer = new BinaryWriter(new MemoryStream()))
            {
                writer.Write(ScriptNumber);
                writer.Write(Type);
                writer.Write((int)(XMapPosition + MapFile.MapSize * XMatrixPosition));
                writer.Write((int)(YMapPosition + MapFile.MapSize * YMatrixPosition));
                writer.Write(ZPosition);
                writer.Write(Dir);
                writer.Write(Unknown5);

                return ((MemoryStream)writer.BaseStream).ToArray();
            }
        }
        public override string ToString()
        {
            string msg = "";
            switch (this.Type)
            {
                case TypeMisc:
                    msg += $"Misc, {PokeDatabase.EventEditor.Spawnables.OrientationsArray[Dir].ToLower()}";
                    break;

                case TypeBoard:
                    msg += $"Board, {PokeDatabase.EventEditor.Spawnables.OrientationsArray[Dir].ToLower()}";

                    break;

                case TypeHiddenitem:
                    msg += "Hidden Item";
                    break;
            }
            return msg + (ScriptNumber == EventFile.NoScript ? ", [No script]" : $", [Scr {ScriptNumber}]");
        }
        #endregion
    }

    public class Overworld : Event
    {
        #region Fields (14)
        public static string MovementCodeKW = "Move";
        public enum OwType : ushort { NORMAL = 0, TRAINER = 1, ITEM = 3 };

        public ushort OwID;
        public ushort OverlayTableEntry;
        public ushort Movement;
        public ushort Type;
        public ushort Flag;
        public ushort ScriptNumber;
        public short Orientation;   // dir; the engine allows -1 for "no direction"
        public ushort SightRange;
        // The three data fields at +0x0E, +0x10 and +0x12 (param0-2 of MapObject_CreateWithParams in
        // pokeheartgold src/map_object.c). +0x0E is the trainer sight range; +0x10 is the steps walked
        // between looks for types 4-6; +0x12 is never read for a map-placed object.
        public ushort Param1;
        public ushort Param2;
        // Signed: -1 means it isn't fenced in on that axis at all, anything else is how many tiles
        // either side of where it starts it may walk.
        public short XRange;
        public short YRange;
        public bool Is3D = new bool();
        #endregion

        #region Constructors (2)
        public Overworld(Stream data)
        {
            EvType = EventType.Overworld;
            using (BinaryReader reader = new BinaryReader(data))
            {
                OwID = reader.ReadUInt16();
                OverlayTableEntry = reader.ReadUInt16();
                Movement = reader.ReadUInt16();
                Type = reader.ReadUInt16();
                Flag = reader.ReadUInt16();
                ScriptNumber = reader.ReadUInt16();
                Orientation = reader.ReadInt16();
                SightRange = reader.ReadUInt16();
                Param1 = reader.ReadUInt16();
                Param2 = reader.ReadUInt16();
                XRange = reader.ReadInt16();
                YRange = reader.ReadInt16();

                /* Decompose x-y coordinates in matrix and map positions */
                int xPosition = reader.ReadInt16();
                int yPosition = reader.ReadInt16();
                XMapPosition = (short)(xPosition % MapFile.MapSize);
                YMapPosition = (short)(yPosition % MapFile.MapSize);
                XMatrixPosition = (ushort)(xPosition / MapFile.MapSize);
                YMatrixPosition = (ushort)(yPosition / MapFile.MapSize);

                ZPosition = reader.ReadInt32();
            }
        }
        public Overworld(int owID, int xMatrixPosition, int yMatrixPosition)
        {
            EvType = EventType.Overworld;

            this.OwID = (ushort)owID;
            OverlayTableEntry = 1;
            Movement = 0;
            Type = 0;
            Flag = 0;
            ScriptNumber = 0;
            Orientation = 1;
            SightRange = 0;
            Param1 = 0;
            Param2 = 0;
            XRange = 0;
            YRange = 0;

            XMapPosition = 16;
            YMapPosition = 16;
            ZPosition = 0;
            this.XMatrixPosition = (ushort)xMatrixPosition;
            this.YMatrixPosition = (ushort)yMatrixPosition;
        }
        public Overworld(Overworld toCopy)
        {
            EvType = EventType.Overworld;

            this.OwID = toCopy.OwID;
            OverlayTableEntry = toCopy.OverlayTableEntry;
            Movement = toCopy.Movement;
            Type = toCopy.Type;
            Flag = toCopy.Flag;
            ScriptNumber = toCopy.ScriptNumber;
            Orientation = toCopy.Orientation;
            SightRange = toCopy.SightRange;
            Param1 = toCopy.Param1;
            Param2 = toCopy.Param2;
            XRange = toCopy.XRange;
            YRange = toCopy.YRange;

            XMapPosition = toCopy.XMapPosition;
            YMapPosition = toCopy.YMapPosition;
            ZPosition = toCopy.ZPosition;
            this.XMatrixPosition = toCopy.XMatrixPosition;
            this.YMatrixPosition = toCopy.YMatrixPosition;
        }
        #endregion

        #region Methods (1)
        public override byte[] ToByteArray()
        {
            using (BinaryWriter writer = new BinaryWriter(new MemoryStream()))
            {
                writer.Write(OwID);
                writer.Write(OverlayTableEntry);
                writer.Write(Movement);
                writer.Write(Type);
                writer.Write(Flag);
                writer.Write(ScriptNumber);
                writer.Write(Orientation);
                writer.Write(SightRange);
                writer.Write(Param1);
                writer.Write(Param2);
                writer.Write(XRange);
                writer.Write(YRange);

                short xCoordinate = (short)(XMapPosition + MapFile.MapSize * XMatrixPosition);
                writer.Write(xCoordinate);

                short yCoordinate = (short)(YMapPosition + MapFile.MapSize * YMatrixPosition);
                writer.Write(yCoordinate);

                writer.Write(ZPosition);

                return ((MemoryStream)writer.BaseStream).ToArray();
            }
        }

        public override string ToString()
        {
            string entityName = ", " + "Entry " + OverlayTableEntry;
            return $"{(this.IsAlias() ? "AliasOf" : "ID")} {this.OwID} {entityName}";
        }

        private bool IsAlias()
        {
            return ScriptNumber == 0xFFFF;
        }
        #endregion
    }

    public class Warp : Event
    {
        #region Fields (4)
        public ushort Header;
        public ushort Anchor;
        public uint Height;
        #endregion

        #region Constructors (2)
        public Warp(Stream data)
        {
            EvType = EventType.Warp;
            using (BinaryReader reader = new BinaryReader(data))
            {
                /* Decompose x-y coordinates in matrix and map positions */
                int xPosition = reader.ReadInt16();
                int yPosition = reader.ReadInt16();
                XMapPosition = (short)(xPosition % MapFile.MapSize);
                YMapPosition = (short)(yPosition % MapFile.MapSize);
                XMatrixPosition = (ushort)(xPosition / MapFile.MapSize);
                YMatrixPosition = (ushort)(yPosition / MapFile.MapSize);

                Header = reader.ReadUInt16();
                Anchor = reader.ReadUInt16();
                Height = reader.ReadUInt32();
            }
        }
        public Warp(int xMatrixPosition, int yMatrixPosition)
        {
            EvType = EventType.Warp;

            Header = 0;
            Anchor = 0;

            XMapPosition = 0;
            YMapPosition = 0;
            this.XMatrixPosition = (ushort)xMatrixPosition;
            this.YMatrixPosition = (ushort)yMatrixPosition;
        }
        public Warp(Warp toCopy)
        {
            EvType = EventType.Warp;

            Header = toCopy.Header;
            Anchor = toCopy.Anchor;
            Height = toCopy.Height;

            XMapPosition = toCopy.XMapPosition;
            YMapPosition = toCopy.YMapPosition;
            this.XMatrixPosition = toCopy.XMatrixPosition;
            this.YMatrixPosition = toCopy.YMatrixPosition;
        }
        #endregion

        #region Methods (1)
        public override byte[] ToByteArray()
        {
            using (BinaryWriter writer = new BinaryWriter(new MemoryStream()))
            {
                ushort xCoordinate = (ushort)(XMapPosition + MapFile.MapSize * XMatrixPosition);
                writer.Write(xCoordinate);

                ushort yCoordinate = (ushort)(YMapPosition + MapFile.MapSize * YMatrixPosition);
                writer.Write(yCoordinate);

                writer.Write(Header);
                writer.Write(Anchor);
                writer.Write(Height);

                return ((MemoryStream)writer.BaseStream).ToArray();
            }
        }
        public override string ToString()
        {
            return "To Header " + Header.ToString("D3") + ", " + "Hook " + Anchor.ToString("D2");
        }
        #endregion

    }

    public class Trigger : Event
    {
        #region Fields (7)
        public ushort ScriptNumber;
        public ushort WidthX;
        public ushort HeightY;
        // No zPosition of its own: it inherits Event.zPosition. A separate field here shadowed the base
        // one, so anything holding the trigger as an Event wrote a value this class never saved.
        public ushort ExpectedVarValue;
        public ushort VariableWatched;
        #endregion Fields

        #region Constructors (2)
        public Trigger(Stream data)
        {
            EvType = EventType.Trigger;
            using (BinaryReader reader = new BinaryReader(data))
            {
                ScriptNumber = reader.ReadUInt16();

                /* Decompose x-y coordinates in matrix and map positions */
                int xPosition = reader.ReadInt16();
                int yPosition = reader.ReadInt16();
                XMapPosition = (short)(xPosition % MapFile.MapSize);
                YMapPosition = (short)(yPosition % MapFile.MapSize);
                XMatrixPosition = (ushort)(xPosition / MapFile.MapSize);
                YMatrixPosition = (ushort)(yPosition / MapFile.MapSize);

                WidthX = reader.ReadUInt16();
                HeightY = reader.ReadUInt16();

                ZPosition = reader.ReadUInt16();
                ExpectedVarValue = reader.ReadUInt16();
                VariableWatched = reader.ReadUInt16();
            }
        }
        public Trigger(int xMatrixPosition, int yMatrixPosition)
        {
            EvType = EventType.Trigger;

            ScriptNumber = 0;
            VariableWatched = 0;
            ExpectedVarValue = 0;
            WidthX = 1;
            HeightY = 1;

            XMapPosition = 0;
            YMapPosition = 0;
            this.XMatrixPosition = (ushort)xMatrixPosition;
            this.YMatrixPosition = (ushort)yMatrixPosition;
        }
        public Trigger(Trigger toCopy)
        {
            EvType = EventType.Trigger;

            ScriptNumber = toCopy.ScriptNumber;
            VariableWatched = toCopy.VariableWatched;
            ExpectedVarValue = toCopy.ExpectedVarValue;
            WidthX = toCopy.WidthX;
            HeightY = toCopy.HeightY;

            XMapPosition = toCopy.XMapPosition;
            YMapPosition = toCopy.YMapPosition;
            ZPosition = toCopy.ZPosition;
            this.XMatrixPosition = toCopy.XMatrixPosition;
            this.YMatrixPosition = toCopy.YMatrixPosition;
        }
        #endregion

        #region Methods (1)
        public override byte[] ToByteArray()
        {
            using (BinaryWriter writer = new BinaryWriter(new MemoryStream()))
            {
                writer.Write(ScriptNumber);
                ushort xCoordinate = (ushort)(XMapPosition + MapFile.MapSize * XMatrixPosition);
                writer.Write(xCoordinate);
                ushort yCoordinate = (ushort)(YMapPosition + MapFile.MapSize * YMatrixPosition);
                writer.Write(yCoordinate);
                writer.Write(WidthX);
                writer.Write(HeightY);
                writer.Write((ushort)ZPosition);
                writer.Write(ExpectedVarValue);
                writer.Write(VariableWatched);

                return ((MemoryStream)writer.BaseStream).ToArray();
            }
        }
        public override string ToString()
        {
            return ToString(hexMode: false);
        }
        public string ToString(bool hexMode)
        {
            string msg = ScriptNumber == EventFile.NoScript ? "No script" : "Run script " + ScriptNumber;
            if (VariableWatched != 0)
            {
                if (hexMode)
                {
                    msg += $" when Var 0x{VariableWatched:X4} is 0x{ExpectedVarValue:X4}";
                }
                else
                {
                    msg += $" when Var {VariableWatched} is {ExpectedVarValue}";
                }
            }
            return msg;
        }
        #endregion
    }
}
