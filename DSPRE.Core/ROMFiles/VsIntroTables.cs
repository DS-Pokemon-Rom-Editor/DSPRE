using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using static DSPRE.RomInfo;

namespace DSPRE.ROMFiles
{
    /// <summary>
    /// The intros special trainer battles open with: which classes get which intro and music, and the records
    /// the gym, league and HGSS Rocket executive intros draw from. Every table is edited in place; nothing grows.
    /// Edits live in memory until <see cref="Save"/>, which writes only the bytes that changed.
    /// </summary>
    public sealed class VsIntroTables
    {
        /// <summary>What an intro effect does, from the effect id ranges the games' code picks routines by.</summary>
        public enum IntroKind { Terrain, Generic, Gym, Rival, League, Legendary, TeamGrunt, TeamLeader, Kimono, OldBall, BallTrainer, DoubleBalls, Unknown }

        /// <summary>Combos the selection code picks itself rather than through a class.</summary>
        public enum ComboRole { None, Rival, Frontier, Link, Double, FrontierBrain, DoubleLeader, Ordinary, WildDouble, OrdinaryWild }

        public enum RecordKind { Gym, Rival, League, Executive }

        public enum RecordField { EndX, TrainerId, Class, FaceColumn, FaceNclr, FaceNcgr, FaceNcer, FaceNanr, BannerNclr, BannerNcgr, BannerNscr,
                            FaceBase, FramePalette, ClashFrames, CameraTurn, PanFrames }

        /// <summary>HGSS gym records print the save's rival name when their class field is the rival class.</summary>
        public const int HgssRivalClass = 23;
        /// <summary>A class table row set to this matches no class, so the row is free.</summary>
        public const int FreeClass = 1023;
        private const ushort UseTerrain = 0xFFFF;

        internal sealed class Region
        {
            public string Path;
            public int Offset;
            public byte[] Now, Saved;
            /// <summary>Bytes this copy may write; null for all. The rest belong to the other editor.</summary>
            public bool[] Owned;
            public bool Owns(int i) => Owned == null || Owned[i];
        }

        /// <summary>
        /// Which editor this copy serves. Each byte belongs to one: trainers get the class rules, the records and
        /// the music of trainer combos; wild Pokémon get the species rows and the music of every other combo.
        /// </summary>
        public enum Part { Trainers, Wild }

        public Part Scope { get; private set; }

        /// <summary>One gym, rival, league or executive record.</summary>
        public sealed class Record
        {
            public RecordKind Kind { get; internal set; }
            public int Index { get; internal set; }
            internal Region Region;
            internal int At;
            internal Dictionary<RecordField, (int Offset, int Size)> Layout;

            public bool Has(RecordField f) => Layout.ContainsKey(f);

            public int Get(RecordField f)
            {
                if (!Layout.TryGetValue(f, out var l)) return -1;
                byte[] b = Region.Now;
                int o = At + l.Offset;
                int v = l.Size switch { 1 => b[o], 2 => BitConverter.ToUInt16(b, o), _ => BitConverter.ToInt32(b, o) };
                return f == RecordField.EndX ? v >> 12 : v;
            }

            public void Set(RecordField f, int value)
            {
                if (!Layout.TryGetValue(f, out var l)) return;
                if (f == RecordField.EndX)
                {
                    if (Get(f) == value) return;   // keeps any fraction a record has
                    value <<= 12;
                }
                byte[] b = Region.Now;
                int o = At + l.Offset;
                switch (l.Size)
                {
                    case 1: b[o] = (byte)Math.Clamp(value, 0, 0xFF); break;
                    case 2: BitConverter.GetBytes((ushort)Math.Clamp(value, 0, 0xFFFF)).CopyTo(b, o); break;
                    default: BitConverter.GetBytes(value).CopyTo(b, o); break;
                }
            }

            /// <summary>The face's palette, tiles, cells and animation, or null when the record has no face art.</summary>
            public int[] FaceMembers()
            {
                if (Has(RecordField.FaceBase)) { int f = Get(RecordField.FaceBase); return new[] { f, f + 1, f + 2, f + 3 }; }
                if (Has(RecordField.FaceNclr)) return new[] { Get(RecordField.FaceNclr), Get(RecordField.FaceNcgr), Get(RecordField.FaceNcer), Get(RecordField.FaceNanr) };
                return null;
            }

            /// <summary>Sets the face; a league record can only take four files in a row.</summary>
            public bool SetFaceMembers(int[] m)
            {
                if (m == null || m.Length != 4) return false;
                if (Has(RecordField.FaceBase))
                {
                    if (m[1] != m[0] + 1 || m[2] != m[0] + 2 || m[3] != m[0] + 3) return false;
                    Set(RecordField.FaceBase, m[0]);
                    return true;
                }
                if (!Has(RecordField.FaceNclr) || m.Any(x => x < 0 || x > 0xFF)) return false;
                Set(RecordField.FaceNclr, m[0]); Set(RecordField.FaceNcgr, m[1]); Set(RecordField.FaceNcer, m[2]); Set(RecordField.FaceNanr, m[3]);
                return true;
            }

            /// <summary>The banner's palette, tiles and arrangement, or null.</summary>
            public int[] BannerMembers() => Has(RecordField.BannerNclr)
                ? new[] { Get(RecordField.BannerNclr), Get(RecordField.BannerNcgr), Get(RecordField.BannerNscr) } : null;

            public bool SetBannerMembers(int[] m)
            {
                if (m == null || m.Length != 3 || !Has(RecordField.BannerNclr) || m.Any(x => x < 0 || x > 0xFF)) return false;
                Set(RecordField.BannerNclr, m[0]); Set(RecordField.BannerNcgr, m[1]); Set(RecordField.BannerNscr, m[2]);
                return true;
            }
        }

        // Record layouts. HGSS and Platinum gym records match; Diamond and Pearl cut the face from the class's
        // battle sprite at a column offset instead, and pan the field camera at the end of a league intro.
        private static readonly Dictionary<RecordField, (int, int)> GymLayout = new()
        {
            [RecordField.EndX] = (0, 4), [RecordField.TrainerId] = (4, 4), [RecordField.Class] = (8, 2),
            [RecordField.FaceNclr] = (12, 1), [RecordField.FaceNcgr] = (13, 1), [RecordField.FaceNcer] = (14, 1), [RecordField.FaceNanr] = (15, 1),
            [RecordField.BannerNclr] = (16, 1), [RecordField.BannerNcgr] = (17, 1), [RecordField.BannerNscr] = (18, 1),
        };
        private static readonly Dictionary<RecordField, (int, int)> DpGymLayout = new()
        {
            [RecordField.EndX] = (0, 4), [RecordField.Class] = (4, 2), [RecordField.FaceColumn] = (6, 2),
        };
        private static readonly Dictionary<RecordField, (int, int)> LeagueLayout = new()
        {
            [RecordField.FaceBase] = (0, 2), [RecordField.FramePalette] = (2, 1), [RecordField.ClashFrames] = (3, 1),
            [RecordField.Class] = (4, 2), [RecordField.TrainerId] = (6, 2),
        };
        private static readonly Dictionary<RecordField, (int, int)> DpLeagueLayout = new()
        {
            [RecordField.CameraTurn] = (0, 4), [RecordField.PanFrames] = (4, 1), [RecordField.Class] = (5, 1), [RecordField.FramePalette] = (6, 1),
        };
        private static readonly Dictionary<RecordField, (int, int)> ExecutiveLayout = new()
        {
            [RecordField.FaceNclr] = (0, 1), [RecordField.FaceNcgr] = (1, 1), [RecordField.FaceNcer] = (2, 1), [RecordField.FaceNanr] = (3, 1),
            [RecordField.TrainerId] = (4, 4),
        };

        public GameFamilies Family { get; }
        public VsIntroSites Sites { get; }
        public BattleMusicTables Music { get; }

        private readonly List<Region> _regions = new();
        private Region _combos, _classRows, _speciesRows, _jumps;
        private readonly List<Record> _records = new();
        private readonly Dictionary<int, Record> _byEffect = new();

        public IReadOnlyList<Record> Records => _records;

        /// <summary>The two league particle files, and how many emitters each starts where that is known (else -1).</summary>
        public int[] ParticleFiles { get; private set; } = Array.Empty<int>();
        public int[] ParticleEmitters { get; private set; } = Array.Empty<int>();

        private VsIntroTables(VsIntroSites sites, BattleMusicTables music)
        {
            Family = gameFamily; Sites = sites; Music = music;
        }

        // ── Availability and loading ─────────────────────────────────────────────────────────────

        /// <summary>Why this ROM's intros can't be edited, or null.</summary>
        public static string WhyNot()
        {
            if (isHGE) return "VS intros can't be edited in hg-engine projects yet.";
            if (gameFamily != GameFamilies.DP && gameFamily != GameFamilies.Plat && gameFamily != GameFamilies.HGSS)
                return "This game has no VS intros DSPRE knows about.";
            if (gameLanguage != GameLanguages.English) return "VS intros are not supported for this language yet. Only US English games can be edited.";
            var sites = VsIntroCodeSites;
            if (sites == null) return "This version isn't supported yet.";
            if (!File.Exists(arm9Path)) return "arm9 is missing from this project.";
            if (!IsDsRomProject && ARM9.CheckCompressionMark()) return "arm9 is still compressed. Convert this project to ds-rom format first.";
            foreach (int ov in new[] { sites.TaskOverlay, sites.RecordOverlay, sites.ExecutiveOverlay })
                if (ov >= 0 && !File.Exists(OverlayUtils.GetPath(ov))) return $"Overlay {ov} is missing from this project.";
            return null;
        }

        /// <summary>Reads everything, checking each table is where it should be. Throws when one is not.</summary>
        public static VsIntroTables Load(Part scope = Part.Trainers)
        {
            string why = WhyNot();
            if (why != null) throw new InvalidOperationException(why);
            var sites = VsIntroCodeSites;

            var music = BattleMusicTables.LoadRom() ?? throw Mismatch("the battle music tables");
            if (!music.MusicPointerAgrees) throw Mismatch("the music half of the intro table");
            var t = new VsIntroTables(sites, music);
            bool hgss = gameFamily == GameFamilies.HGSS;

            if (hgss && sites.ComboMusicCount >= 0 && ARM9.ReadByte((uint)sites.ComboMusicCount) != music.Combos.Rows.Count)
                throw Mismatch("the intro table's length");
            t._combos = t.AddRegion(music.Combos.Path, (int)music.Combos.Start, 4 * music.Combos.Rows.Count);

            if (hgss)
            {
                t._classRows = t.AddRegion(music.Classes.Path, (int)music.Classes.Start, 2 * music.Classes.Rows.Count);
                t._speciesRows = t.AddRegion(music.Species.Path, (int)music.Species.Start, 2 * music.Species.Rows.Count);
            }
            else
            {
                var jumps = music.ClassJumps ?? throw Mismatch("the trainer class switch");
                t._jumps = t.AddRegion(arm9Path, jumps.Start, 2 * jumps.Count);
            }

            t.ReadRecords();
            t.BindEffects();
            t.ReadParticles();
            t.Claim(scope);
            return t;
        }

        // Decided once from what was read, so both editors agree on every combo whatever is edited later.
        private void Claim(Part scope)
        {
            Scope = scope;
            bool trainers = scope == Part.Trainers;
            foreach (var r in _regions)
                r.Owned = Enumerable.Repeat(r == _speciesRows ? !trainers : r != _combos && trainers, r.Now.Length).ToArray();
            for (int c = 0; c < ComboCount; c++)
                if (IsTrainerCombo(c) == trainers) _combos.Owned[4 * c + 2] = _combos.Owned[4 * c + 3] = true;
        }

        /// <summary>Whether this copy edits the music of <paramref name="combo"/>.</summary>
        public bool OwnsMusic(int combo) => _combos.Owns(4 * combo + 2);

        private static InvalidDataException Mismatch(string what) =>
            new InvalidDataException($"DSPRE could not find {what} where it expects it in this ROM, so it won't edit VS intros here.");

        private Region AddRegion(string path, int offset, int length)
        {
            if (!File.Exists(path) || offset < 0 || new FileInfo(path).Length < offset + length)
                throw new InvalidDataException($"{Path.GetFileName(path)} is too short for the VS intro tables.");
            byte[] now = DSUtils.ReadFromFile(path, offset, length);
            var r = new Region { Path = path, Offset = offset, Now = now, Saved = (byte[])now.Clone() };
            _regions.Add(r);
            return r;
        }

        private static string OverlayFile(int ov)
        {
            if (OverlayUtils.IsStillCompressed(ov)) OverlayUtils.Decompress(ov);
            return OverlayUtils.GetPath(ov);
        }

        private void ReadRecords()
        {
            bool dp = Family == GameFamilies.DP;
            string records = OverlayFile(Sites.RecordOverlay);
            var gym = AddRegion(records, Sites.GymTable, Sites.GymCount * Sites.GymSize);
            for (int i = 0; i < Sites.GymCount; i++)
                _records.Add(new Record { Kind = RecordKind.Gym, Index = i, Region = gym, At = i * Sites.GymSize, Layout = dp ? DpGymLayout : GymLayout });
            if (Sites.RivalRecord >= 0)
                _records.Add(new Record { Kind = RecordKind.Rival, Index = 0, Region = AddRegion(records, Sites.RivalRecord, Sites.GymSize), At = 0, Layout = GymLayout });
            var league = AddRegion(records, Sites.LeagueTable, 5 * 8);
            for (int i = 0; i < 5; i++)
                _records.Add(new Record { Kind = RecordKind.League, Index = i, Region = league, At = i * 8, Layout = dp ? DpLeagueLayout : LeagueLayout });
            if (Sites.ExecutiveOverlay >= 0)
            {
                var exec = AddRegion(OverlayFile(Sites.ExecutiveOverlay), Sites.ExecutiveTable, 5 * 8);
                for (int i = 0; i < 5; i++)
                    _records.Add(new Record { Kind = RecordKind.Executive, Index = i, Region = exec, At = i * 8, Layout = ExecutiveLayout });
            }
        }

        /// <summary>
        /// Follows each mugshot effect from the task table to its routine, and from the routine's first literal
        /// that names a record to that record. A routine that names none means the code is not the known one.
        /// </summary>
        private void BindEffects()
        {
            string taskPath = OverlayFile(Sites.TaskOverlay);
            byte[] task = File.ReadAllBytes(taskPath);
            if (Sites.TaskTable + 4 * Sites.TaskCount > task.Length) throw Mismatch("the intro routine table");

            var address = new Dictionary<uint, Record>();
            foreach (var r in _records)
            {
                int ov = r.Kind == RecordKind.Executive ? Sites.ExecutiveOverlay : Sites.RecordOverlay;
                address[OverlayUtils.OverlayTable.GetRAMAddress(ov) + (uint)(r.Region.Offset + r.At)] = r;
            }

            var cache = new Dictionary<int, byte[]>();
            for (int effect = 0; effect < Sites.TaskCount; effect++)
            {
                var kind = KindOfEffect(effect);
                bool hasRecord = kind is IntroKind.Gym or IntroKind.Rival or IntroKind.League
                                 || (kind == IntroKind.TeamLeader && Sites.ExecutiveOverlay >= 0);
                if (!hasRecord) continue;

                int ov = kind == IntroKind.TeamLeader ? Sites.ExecutiveOverlay : Sites.RecordOverlay;
                if (!cache.TryGetValue(ov, out byte[] code)) cache[ov] = code = File.ReadAllBytes(OverlayUtils.GetPath(ov));
                uint baseRam = OverlayUtils.OverlayTable.GetRAMAddress(ov);
                long routine = (BitConverter.ToUInt32(task, Sites.TaskTable + 4 * effect) & ~1u) - (long)baseRam;
                if (routine < 0 || routine >= code.Length) throw Mismatch("the intro routines");

                Record found = null;
                for (long k = (routine + 3) & ~3L; k + 4 <= code.Length && k < routine + 0x40; k += 4)
                    if (address.TryGetValue(BitConverter.ToUInt32(code, (int)k), out found)) break;
                if (found == null) throw Mismatch("the intro records");
                _byEffect[effect] = found;
            }
        }

        private void ReadParticles()
        {
            if (Sites.ParticleSites.Length == 0) return;
            byte[] code = File.ReadAllBytes(OverlayUtils.GetPath(Sites.RecordOverlay));
            // movs rN, #imm is 0x20..0x27 in the high byte.
            int Imm(int at) => at >= 0 && at + 2 <= code.Length && (code[at + 1] & 0xF8) == 0x20 ? code[at] : -1;
            ParticleFiles = Sites.ParticleSites.Select(Imm).ToArray();
            ParticleEmitters = Sites.ParticleSites.Select((_, i) => i < Sites.EmitterSites.Length ? Imm(Sites.EmitterSites[i]) : -1).ToArray();
            if (ParticleFiles.Any(f => f < 0)) throw Mismatch("the league particle files");
        }

        // ── Effects and combos ───────────────────────────────────────────────────────────────────

        public IntroKind KindOfEffect(int effect)
        {
            if (effect == UseTerrain) return IntroKind.Generic;
            if (effect < 0) return IntroKind.Unknown;
            if (effect < 12) return IntroKind.Terrain;
            if (Family == GameFamilies.HGSS)
                return effect switch
                {
                    <= 27 => IntroKind.Gym,
                    28 => IntroKind.Rival,
                    <= 33 => IntroKind.League,
                    <= 36 => IntroKind.Legendary,
                    37 => IntroKind.BallTrainer,
                    38 => IntroKind.DoubleBalls,
                    39 => IntroKind.TeamGrunt,
                    <= 44 => IntroKind.TeamLeader,
                    45 => IntroKind.Kimono,
                    46 => IntroKind.OldBall,
                    _ => IntroKind.Unknown,
                };
            return effect switch
            {
                <= 19 => IntroKind.Gym,
                <= 24 => IntroKind.League,
                <= 26 => IntroKind.Legendary,
                27 => IntroKind.TeamGrunt,
                28 => IntroKind.TeamLeader,
                29 => IntroKind.BallTrainer,
                30 => IntroKind.DoubleBalls,
                _ => IntroKind.Unknown,
            };
        }

        /// <summary>Combos the battle setup code chooses by battle type, and the one every other class falls back to.</summary>
        public ComboRole RoleOf(int combo)
        {
            if (Family == GameFamilies.HGSS)
                return combo switch { 35 => ComboRole.Frontier, 36 => ComboRole.Link, 37 => ComboRole.Double, 38 => ComboRole.WildDouble,
                                      39 => ComboRole.FrontierBrain, 41 => ComboRole.Ordinary, 42 => ComboRole.OrdinaryWild, _ => ComboRole.None };
            if (Music.ClassJumps != null && combo == Music.ClassJumps.DefaultCombo) return ComboRole.Ordinary;
            if (Family == GameFamilies.Plat)
                return combo switch { 13 => ComboRole.Rival, 27 => ComboRole.Frontier, 28 => ComboRole.Link, 29 => ComboRole.Double,
                                      30 => ComboRole.WildDouble, 31 => ComboRole.FrontierBrain, 32 => ComboRole.DoubleLeader,
                                      34 => ComboRole.OrdinaryWild, _ => ComboRole.None };
            return combo switch { 13 => ComboRole.Rival, 24 => ComboRole.Frontier, 25 => ComboRole.Link, 26 => ComboRole.Double,
                                  27 => ComboRole.WildDouble, 28 => ComboRole.FrontierBrain, 30 => ComboRole.OrdinaryWild, _ => ComboRole.None };
        }

        /// <summary>
        /// Whether a combo belongs to trainer battles rather than wild ones: a trainer intro kind, a combo the
        /// battle setup picks for trainers, or one a class points at. Everything else is left to wild Pokémon.
        /// </summary>
        public bool IsTrainerCombo(int combo)
        {
            var role = RoleOf(combo);
            if (role is ComboRole.WildDouble or ComboRole.OrdinaryWild) return false;
            if (role != ComboRole.None) return true;
            var kind = KindOfEffect(EffectOf(combo));
            if (kind is IntroKind.Gym or IntroKind.Rival or IntroKind.League or IntroKind.TeamGrunt or IntroKind.TeamLeader
                     or IntroKind.Kimono or IntroKind.OldBall or IntroKind.BallTrainer) return true;
            return ClassesUsing(combo).Count > 0;
        }

        /// <summary>HGSS: the species table's rows; the first row naming a species wins.</summary>
        public int SpeciesRowCount => _speciesRows == null ? 0 : _speciesRows.Now.Length / 2;
        public (int Species, int Combo) SpeciesRow(int row)
        {
            ushort v = BitConverter.ToUInt16(_speciesRows.Now, 2 * row);
            return (v & 0x3FF, v >> 10);
        }
        public void SetSpeciesRow(int row, int species, int combo) =>
            BitConverter.GetBytes((ushort)((species & 0x3FF) | ((combo & 0x3F) << 10))).CopyTo(_speciesRows.Now, 2 * row);

        /// <summary>Species that pick <paramref name="combo"/>: the HGSS table's rows, or DP/Pt's fixed lists.</summary>
        public List<int> SpeciesUsing(int combo)
        {
            var list = new List<int>();
            if (_speciesRows != null)
            {
                var seen = new HashSet<int>();
                for (int i = 0; i < SpeciesRowCount; i++)
                {
                    var (sp, k) = SpeciesRow(i);
                    if (seen.Add(sp) && k == combo) list.Add(sp);
                }
                return list;
            }
            foreach (var kv in Music.CodeSpeciesCombos)
                if (kv.Value == combo) list.Add(kv.Key);
            list.Sort();
            return list;
        }

        public int ComboCount => _combos.Now.Length / 4;
        public int EffectOf(int combo) => BitConverter.ToUInt16(_combos.Now, 4 * combo);
        public int SequenceOf(int combo) => BitConverter.ToUInt16(_combos.Now, 4 * combo + 2);
        public void SetSequence(int combo, int sequence) =>
            BitConverter.GetBytes((ushort)Math.Clamp(sequence, 0, 0xFFFF)).CopyTo(_combos.Now, 4 * combo + 2);

        /// <summary>The record an intro effect draws from, or null.</summary>
        public Record RecordFor(int effect) => _byEffect.TryGetValue(effect, out var r) ? r : null;

        /// <summary>Every effect that draws from <paramref name="record"/>.</summary>
        public IEnumerable<int> EffectsUsing(Record record) => _byEffect.Where(kv => kv.Value == record).Select(kv => kv.Key);

        // ── Classes ──────────────────────────────────────────────────────────────────────────────

        /// <summary>HGSS: the class table's rows; the first row naming a class wins.</summary>
        public int ClassRowCount => _classRows == null ? 0 : _classRows.Now.Length / 2;
        public (int Class, int Combo) ClassRow(int row)
        {
            ushort v = BitConverter.ToUInt16(_classRows.Now, 2 * row);
            return (v & 0x3FF, v >> 10);
        }
        public void SetClassRow(int row, int trainerClass, int combo) =>
            BitConverter.GetBytes((ushort)((trainerClass & 0x3FF) | ((combo & 0x3F) << 10))).CopyTo(_classRows.Now, 2 * row);

        /// <summary>DP/Pt: the classes the switch covers; every other class gets the default combo.</summary>
        public int FirstSwitchClass => Music.ClassJumps?.FirstClass ?? 0;
        public int SwitchCount => _jumps == null ? 0 : _jumps.Now.Length / 2;
        public bool InSwitch(int trainerClass) => _jumps != null && trainerClass >= FirstSwitchClass && trainerClass < FirstSwitchClass + SwitchCount;

        /// <summary>The combo a class in the switch gets, or -1 when its entry jumps somewhere unknown.</summary>
        public int SwitchCombo(int trainerClass) =>
            Music.ClassJumps.ComboOfEntry(BitConverter.ToInt16(_jumps.Now, 2 * (trainerClass - FirstSwitchClass)));

        /// <summary>Whether some existing stub picks <paramref name="combo"/>, so a class can be pointed at it.</summary>
        public bool CanSwitchTo(int combo) => Music.ClassJumps != null && Music.ClassJumps.TargetFor(combo) >= 0;

        public bool SetSwitchCombo(int trainerClass, int combo)
        {
            if (!InSwitch(trainerClass)) return false;
            int target = Music.ClassJumps.TargetFor(combo);
            if (target < 0) return false;
            BitConverter.GetBytes(Music.ClassJumps.EntryFor(target)).CopyTo(_jumps.Now, 2 * (trainerClass - FirstSwitchClass));
            return true;
        }

        /// <summary>The combo a battle against this class picks before the battle type overrides.</summary>
        public int ComboForClass(int trainerClass)
        {
            if (_classRows != null)
            {
                for (int i = 0; i < ClassRowCount; i++)
                {
                    var (c, combo) = ClassRow(i);
                    if (c == trainerClass) return combo;
                }
                return 41;
            }
            return InSwitch(trainerClass) ? SwitchCombo(trainerClass) : Music.ClassJumps.DefaultCombo;
        }

        /// <summary>
        /// Points a class at a combo. HGSS reuses the class's first row, or else a free row, and says false
        /// when every row is taken. DP/Pt needs the class inside the switch and a stub for the combo.
        /// </summary>
        public bool AssignClass(int trainerClass, int combo)
        {
            if (_classRows == null) return SetSwitchCombo(trainerClass, combo);
            int free = -1;
            for (int i = 0; i < ClassRowCount; i++)
            {
                var (c, _) = ClassRow(i);
                if (c == trainerClass) { SetClassRow(i, trainerClass, combo); return true; }
                if (free < 0 && c == FreeClass) free = i;
            }
            if (free < 0) return false;
            SetClassRow(free, trainerClass, combo);
            return true;
        }

        /// <summary>Takes a class off its intro: HGSS frees its rows, DP/Pt points it at the default stub.</summary>
        public bool UnassignClass(int trainerClass)
        {
            if (_classRows == null) return SetSwitchCombo(trainerClass, Music.ClassJumps.DefaultCombo);
            bool any = false;
            for (int i = 0; i < ClassRowCount; i++)
                if (ClassRow(i).Class == trainerClass) { SetClassRow(i, FreeClass, ClassRow(i).Combo); any = true; }
            return any;
        }

        /// <summary>Classes that pick <paramref name="combo"/> directly, in table order.</summary>
        public List<int> ClassesUsing(int combo)
        {
            var list = new List<int>();
            if (_classRows != null)
            {
                var seen = new HashSet<int>();
                for (int i = 0; i < ClassRowCount; i++)
                {
                    var (c, k) = ClassRow(i);
                    if (c == FreeClass || !seen.Add(c)) continue;   // a later row for the same class is never reached
                    if (k == combo) list.Add(c);
                }
                return list;
            }
            for (int c = FirstSwitchClass; c < FirstSwitchClass + SwitchCount; c++)
                if (SwitchCombo(c) == combo) list.Add(c);
            return list;
        }

        public int FreeClassRows => Enumerable.Range(0, ClassRowCount).Count(i => ClassRow(i).Class == FreeClass);

        public const string ClassTableMarker = "VSCLASSTBL01";
        /// <summary>The most rows the class search can walk: its count is a byte immediate.</summary>
        public const int MostClassRows = 255;

        /// <summary>HGSS: the class table already sits in the expanded ARM9 area.</summary>
        public bool ClassTableMoved => Music.Classes.Repointed;

        /// <summary>Why the class table can't be given more rows, or null.</summary>
        public string WhyNoClassRoom()
        {
            if (_classRows == null) return "Only HeartGold and SoulSilver keep their class rules in a table.";
            if (ClassTableMoved) return "The class table has already been given room.";
            if (HasChanges) return "Save or discard your changes first.";
            if (!SyntheticOverlaySpace.Available()) return "Apply the ARM9 expansion in the ROM Patch Toolbox first.";
            // cmp r2, #count: the count is the low byte, 0x2A the opcode.
            byte[] cmp = ARM9.ReadBytes(vsTrainerEntryTableOffsetToSizeLimiter, 2);
            if (cmp[1] != 0x2A || cmp[0] != ClassRowCount) return "The class search doesn't look like the game's, so DSPRE won't move its table.";
            return null;
        }

        /// <summary>
        /// Moves the class table into its own marked block in the expanded ARM9 area with room for
        /// <see cref="MostClassRows"/> rows, the new ones free, and points the class search and its count at it.
        /// Writes at once, like the type chart's move.
        /// </summary>
        public void MakeClassRoom()
        {
            string why = WhyNoClassRoom();
            if (why != null) throw new InvalidOperationException(why);

            byte[] rows = new byte[2 * MostClassRows];
            _classRows.Now.CopyTo(rows, 0);
            for (int i = ClassRowCount; i < MostClassRows; i++) BitConverter.GetBytes((ushort)FreeClass).CopyTo(rows, 2 * i);

            byte[] block = new byte[(SyntheticOverlaySpace.HeaderSize + rows.Length + 3) & ~3];
            System.Text.Encoding.ASCII.GetBytes(ClassTableMarker).CopyTo(block, 0);
            BitConverter.GetBytes(1u).CopyTo(block, 0x0C);
            BitConverter.GetBytes((uint)block.Length).CopyTo(block, 0x10);
            BitConverter.GetBytes((uint)MostClassRows).CopyTo(block, 0x14);
            rows.CopyTo(block, SyntheticOverlaySpace.HeaderSize);

            byte[] synth = File.ReadAllBytes(Filesystem.expArmPath);
            int at = SyntheticOverlaySpace.FindFree(synth, block.Length, 4, SyntheticOverlaySpace.Reserved(synth));
            if (at < 0) throw new InvalidOperationException("No free space was found in the expanded ARM9 area for the class table.");

            byte[] arm9 = File.ReadAllBytes(arm9Path);
            byte[] synthBefore = (byte[])synth.Clone(), arm9Before = (byte[])arm9.Clone();
            block.CopyTo(synth, at);
            uint ram = synthOverlayLoadAddress + (uint)(at + SyntheticOverlaySpace.HeaderSize);
            BitConverter.GetBytes(ram).CopyTo(arm9, (int)vsTrainerEntryTableOffsetToRAMAddress);
            arm9[vsTrainerEntryTableOffsetToSizeLimiter] = MostClassRows;
            try
            {
                File.WriteAllBytes(Filesystem.expArmPath, synth);
                File.WriteAllBytes(arm9Path, arm9);
            }
            catch
            {
                File.WriteAllBytes(Filesystem.expArmPath, synthBefore);
                File.WriteAllBytes(arm9Path, arm9Before);
                throw;
            }

            Music.Classes.Repointed = true;
            Music.Classes.Path = Filesystem.expArmPath;
            Music.Classes.Start = (uint)(at + SyntheticOverlaySpace.HeaderSize);
            _classRows.Path = Filesystem.expArmPath;
            _classRows.Offset = at + SyntheticOverlaySpace.HeaderSize;
            _classRows.Now = (byte[])rows.Clone();
            _classRows.Saved = (byte[])rows.Clone();
            _classRows.Owned = Enumerable.Repeat(Scope == Part.Trainers, rows.Length).ToArray();
        }

        // ── State ────────────────────────────────────────────────────────────────────────────────

        public bool HasChanges => _regions.Any(r => Enumerable.Range(0, r.Now.Length).Any(i => r.Owns(i) && r.Now[i] != r.Saved[i]));

        /// <summary>Every editable byte, for undo.</summary>
        public byte[] Snapshot() => _regions.SelectMany(r => r.Now).ToArray();

        public void Restore(byte[] state)
        {
            if (state == null || state.Length != _regions.Sum(r => r.Now.Length)) return;
            int at = 0;
            foreach (var r in _regions) { Array.Copy(state, at, r.Now, 0, r.Now.Length); at += r.Now.Length; }
        }

        /// <summary>
        /// Writes the bytes that differ from what was read or last saved, and nothing else. The files are read
        /// again first: bytes this editor did not touch take what is on disk now, and a byte it is about to write
        /// that changed on disk since it was read stops the save rather than overwrite that change.
        /// </summary>
        public void Save()
        {
            var disk = _regions.Select(r => DSUtils.ReadFromFile(r.Path, r.Offset, r.Now.Length)).ToList();
            for (int k = 0; k < _regions.Count; k++)
            {
                var r = _regions[k];
                for (int i = 0; i < r.Now.Length; i++)
                    if (r.Owns(i) && r.Now[i] != r.Saved[i] && disk[k][i] != r.Saved[i])
                        throw new IOException($"{Path.GetFileName(r.Path)} was changed by something else since the VS intros were read. Discard and try again.");
            }
            for (int k = 0; k < _regions.Count; k++)
            {
                var r = _regions[k];
                for (int i = 0; i < r.Now.Length; i++)
                    if (!r.Owns(i) || r.Now[i] == r.Saved[i]) r.Now[i] = r.Saved[i] = disk[k][i];
            }
            foreach (var r in _regions)
            {
                int i = 0;
                while (i < r.Now.Length)
                {
                    if (r.Now[i] == r.Saved[i]) { i++; continue; }
                    int start = i;
                    while (i < r.Now.Length && r.Now[i] != r.Saved[i]) i++;
                    DSUtils.WriteToFile(r.Path, r.Now[start..i], (uint)(r.Offset + start));
                }
                r.Saved = (byte[])r.Now.Clone();
            }
        }
    }
}
