using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DSPRE.HgEngine;
using DSPRE.ROMFiles;
using AvaBitmap = global::Avalonia.Media.Imaging.Bitmap;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.Data
{
    /// <summary>The balls, bursts, trainers, party rows, sparkle and sounds a send-out uses, read once from the ROM.</summary>
    public sealed class SendOutGraphics
    {
        /// <summary>(ball id, name) for every ball but the Park Ball, which never has a send-out.</summary>
        public static List<(int Ball, string Name)> Balls()
        {
            var list = new List<(int, string)>();
            string[] items = null;
            try { items = GetItemNames(); } catch { }
            string Name(int item, string fallback) =>
                items != null && item > 0 && item < items.Length && !string.IsNullOrWhiteSpace(items[item]) ? items[item].Trim() : fallback;

            for (int ball = 1; ball <= 16; ball++) list.Add((ball, Name(ball, "Ball " + ball)));
            if (gameFamily == GameFamilies.HGSS)
                for (int ball = 17; ball <= 24; ball++) list.Add((ball, Name(492 + ball - 17, "Ball " + ball)));
            return list;
        }

        // Platinum keeps the thrown balls in pl_batt_obj in its own drawing order; HGSS numbers them by ball id.
        private static readonly int[] SinnohDrawingForBall = { 1, 2, 3, 0, 4, 5, 6, 7, 8, 9, 10, 11, 13, 14, 12, 15, 16 };

        /// <summary>The four battle OBJ archive entries a thrown ball is drawn from.</summary>
        public static (int Ncgr, int Nclr, int Ncer, int Nanr) BallFiles(int ball)
        {
            if (gameFamily == GameFamilies.HGSS)
            {
                int n = ball >= 1 && ball <= 27 ? ball : 4;
                return (258 + 3 * n, 82 + n, 257 + 3 * n, 256 + 3 * n);
            }
            int d = ball >= 1 && ball <= SinnohDrawingForBall.Length ? SinnohDrawingForBall[ball - 1] : 0;
            return (285 + 3 * d, 91 + d, 284 + 3 * d, 283 + 3 * d);
        }

        /// <summary>The ball particle archive entry that bursts when this ball opens.</summary>
        public static int BurstEntry(int ball) => ball >= 1 && ball <= (gameFamily == GameFamilies.HGSS ? 24 : 16) ? ball : 4;

        /// <summary>Unpacks every archive a send-out reads. Slow, so call it off the UI thread.</summary>
        public static void Unpack()
        {
            var dirs = new List<DirNames>();
            foreach (var d in new[] { DirNames.battleObj, DirNames.ballParticles, DirNames.trainerGraphics, DirNames.trainerBackGraphics,
                                      DirNames.trainerProperties, DirNames.wazaEffectSub, DirNames.wazaParticle })
                if (gameDirs.ContainsKey(d)) dirs.Add(d);
            DSUtils.TryUnpackNarcs(dirs);
        }

        private readonly Dictionary<int, (WeCellAnimRenderer Cells, CellSequence[] Seqs)> _balls = new();
        private readonly Dictionary<(int Ball, int Cell), AvaBitmap> _ballCells = new();
        private readonly ScriptNarc _burstNarc = new ScriptNarc(DirNames.ballParticles);
        private readonly Dictionary<int, SpaArchive> _bursts = new();

        public CellSequence[] BallSequences(int ball) => LoadBall(ball).Seqs;

        /// <summary>One cell of a ball, drawn 64×64 around the ball's own centre.</summary>
        public AvaBitmap BallCell(int ball, int cell)
        {
            if (_ballCells.TryGetValue((ball, cell), out var bmp)) return bmp;
            bmp = LoadBall(ball).Cells?.RenderCell(cell, 64, 64);
            _ballCells[(ball, cell)] = bmp;
            return bmp;
        }

        private (WeCellAnimRenderer Cells, CellSequence[] Seqs) LoadBall(int ball)
        {
            if (_balls.TryGetValue(ball, out var loaded)) return loaded;
            var cells = new WeCellAnimRenderer();
            var f = BallFiles(ball);
            bool ok = gameDirs.ContainsKey(DirNames.battleObj)
                   && cells.Load(DirNames.battleObj, f.Ncgr, DirNames.battleObj, f.Nclr, DirNames.battleObj, f.Ncer, DirNames.battleObj, f.Nanr);
            loaded = ok ? (cells, cells.BuildSequences()) : (null, Array.Empty<CellSequence>());
            _balls[ball] = loaded;
            return loaded;
        }

        // On the ground below each Pokémon; the enemy's sits further back, so it draws smaller.
        private const double EnemyBurstX = 193.0, EnemyBurstY = 85.1, PlayerBurstX = 57.9, PlayerBurstY = 149.6;
        private const double EnemyDepth = -5248.0 / 172.0, PlayerDepth = 64.0 / 172.0;

        /// <summary>Adds every emitter of a ball's open burst to <paramref name="into"/>. False when the ROM has none.</summary>
        public bool AddBurst(SpaParticlePreview into, int ball, bool enemySide)
        {
            int entry = BurstEntry(ball);
            if (!_bursts.TryGetValue(entry, out var arc))
            {
                var bytes = _burstNarc.Available ? _burstNarc.Get(entry) : null;
                arc = bytes != null ? SpaArchive.Parse(bytes) : null;
                _bursts[entry] = arc;
            }
            if (arc == null || arc.Emitters.Count == 0) return false;

            double baseX = enemySide ? EnemyBurstX : PlayerBurstX, baseY = enemySide ? EnemyBurstY : PlayerBurstY;
            double depth = enemySide ? EnemyDepth : PlayerDepth;
            foreach (var em in arc.Emitters)
            {
                var tex = em.TexNo >= 0 && em.TexNo < arc.Textures.Count ? arc.Textures[em.TexNo] : null;
                double cx = baseX + em.PosX, cy = baseY - em.PosY;
                var sim = new SpaSimulator(em, em.AxisX, em.AxisY) { AnchorX = cx, AnchorY = cy };
                into.AddLayer(new SpaParticlePreview.Layer(sim, arc.Textures, tex, cx, cy, em.DrawType,
                    em.RepeatS, em.RepeatT, em.Aspect, em.DbbScale, em.OffsetX, em.OffsetY,
                    baseZ: depth + em.PosZ, viewReversed: false, flipS: em.FlipS, flipT: em.FlipT, em: em));
            }
            return true;
        }

        // ── Trainers ────────────────────────────────────────────────────────────────────────────
        private TrainerClassSpriteRenderer _front, _back;
        private int _frontClass = -1;
        private readonly Dictionary<(bool Back, int Bank), AvaBitmap> _trainerBanks = new();
        private readonly Dictionary<int, AvaBitmap> _trainerFrames = new();
        private int[] _backFrameStarts;

        /// <summary>Every trainer as "id: Class Name", or empty when they cannot be read.</summary>
        public static List<string> TrainerList()
        {
            var list = new List<string>();
            try
            {
                foreach (string entry in TrainerNames.GetAll())
                {
                    // "[07] Youngster Joey" reads better in a picker as "7: Youngster Joey".
                    int close = entry.IndexOf(']');
                    list.Add(close > 1 && int.TryParse(entry.Substring(1, close - 1), out int id)
                        ? $"{id}: {entry.Substring(close + 1).Trim()}" : entry);
                }
            }
            catch { list.Clear(); }
            return list;
        }

        /// <summary>A trainer's class and name as the battle shows them.</summary>
        public static (int Class, string ClassName, string Name) TrainerInfo(int trainerId)
        {
            int cls = 0;
            string name = "";
            try
            {
                if (HgEngineProject.IsActive && HgEngineTrainerSource.TryLoad(trainerId, out var block, out _))
                {
                    block.TryGetSymbol(new[] { FieldPathSegment.Field("data"), FieldPathSegment.Field("trainerClass") },
                        "include/constants/trainerclass.h", out cls);
                    block.TryGetString(new[] { FieldPathSegment.Field("name") }, out name);
                }
                else
                {
                    // The class is the second byte of the trainer's record.
                    string path = Filesystem.GetTrainerPropertiesPath(trainerId);
                    var bytes = System.IO.File.Exists(path) ? System.IO.File.ReadAllBytes(path) : null;
                    if (bytes != null && bytes.Length > 1) cls = bytes[1];
                    var names = GetSimpleTrainerNames();
                    if (trainerId >= 0 && trainerId < names.Length) name = names[trainerId];
                }
            }
            catch { }
            string className = "";
            try
            {
                var classes = GetTrainerClassNames();
                if (cls >= 0 && cls < classes.Length) className = classes[cls];
            }
            catch { }
            return (cls, className?.Trim() ?? "", name?.Trim() ?? "");
        }

        /// <summary>The first Youngster in the trainer list, or trainer 1, as the default opponent.</summary>
        public static int DefaultTrainer()
        {
            try
            {
                var classes = GetTrainerClassNames();
                int youngster = Array.FindIndex(classes, c => c != null && c.Trim().Equals("Youngster", StringComparison.OrdinalIgnoreCase));
                int count = Filesystem.GetTrainerPropertiesCount();
                for (int id = 1; youngster >= 0 && id < count; id++)
                {
                    var bytes = System.IO.File.ReadAllBytes(Filesystem.GetTrainerPropertiesPath(id));
                    if (bytes.Length > 1 && bytes[1] == youngster) return id;
                }
            }
            catch { }
            return 1;
        }

        private TrainerClassSpriteRenderer Front(int trainerClass)
        {
            if (_front == null || _frontClass != trainerClass)
            {
                _front = LoadTrainer(trainerClass, DirNames.trainerGraphics);
                _frontClass = trainerClass;
                foreach (var key in new List<(bool, int)>(_trainerBanks.Keys)) if (!key.Item1) _trainerBanks.Remove(key);
                _trainerFrames.Clear();
            }
            return _front;
        }

        /// <summary>How many animations the class has.</summary>
        public int EnemyTrainerSequenceCount(int trainerClass) => Front(trainerClass).SequenceCount;

        /// <summary>How long a class animation runs at the battle's two animation units a tick.</summary>
        public int EnemyTrainerSequenceTicks(int trainerClass, int seq)
        {
            int units = 0;
            foreach (var f in Front(trainerClass).Sequence(seq)) units += Math.Max(1, f.Duration);
            return (units + 1) / 2;
        }

        /// <summary>The enemy trainer <paramref name="ticks"/> into an animation, 160×160 around its position.</summary>
        public AvaBitmap EnemyTrainer(int trainerClass, int seq, int ticks)
        {
            var r = Front(trainerClass);
            var frames = r.Sequence(seq);
            if (frames.Length == 0)
            {
                if (!_trainerFrames.TryGetValue(r.DefaultFrame, out var still)) _trainerFrames[r.DefaultFrame] = still = r.Render(r.DefaultFrame, 160, 160);
                return still;
            }
            int units = Math.Max(0, ticks) * 2, at = 0, bank = frames[frames.Length - 1].Bank;
            foreach (var f in frames)
            {
                if (units < at + Math.Max(1, f.Duration)) { bank = f.Bank; break; }
                at += Math.Max(1, f.Duration);
            }
            if (!_trainerBanks.TryGetValue((false, bank), out var bmp)) _trainerBanks[(false, bank)] = bmp = r.RenderBank(bank, 160, 160);
            return bmp;
        }

        /// <summary>The player's back sprite <paramref name="ticks"/> into its throw, or its resting pose for −1.</summary>
        public AvaBitmap PlayerTrainer(int ticks)
        {
            var r = _back ??= LoadTrainer(0, DirNames.trainerBackGraphics);
            if (r.FrameCount == 0) return null;
            int frame = r.DefaultFrame;
            if (ticks >= 0)
            {
                // Trainer sprites advance two animation units a tick.
                if (_backFrameStarts == null)
                {
                    _backFrameStarts = new int[r.FrameCount];
                    int at = 0;
                    for (int i = 0; i < r.FrameCount; i++) { _backFrameStarts[i] = at; at += Math.Max(1, r.GetFrameDuration(i) / 2); }
                }
                frame = 0;
                for (int i = 0; i < _backFrameStarts.Length; i++) if (ticks >= _backFrameStarts[i]) frame = i;
            }
            if (!_trainerBanks.TryGetValue((true, frame), out var bmp)) _trainerBanks[(true, frame)] = bmp = r.Render(frame, 160, 160);
            return bmp;
        }

        private static TrainerClassSpriteRenderer LoadTrainer(int trainerClass, DirNames archive)
        {
            var r = new TrainerClassSpriteRenderer();
            if (gameDirs.ContainsKey(archive)) r.Load(trainerClass, archive);
            return r;
        }

        // ── Party ball row ──────────────────────────────────────────────────────────────────────
        // Battle OBJ archive: palette 110, tiles 340, cells 341, animations 342. Sequences 0-2 enemy balls,
        // 3-5 yours, 6 an empty slot, 7-8 the bars.
        public const int RowEnemyHealthy = 0, RowPlayerHealthy = 3, RowEmpty = 6, RowEnemyBar = 7, RowPlayerBar = 8;
        private WeCellAnimRenderer _row;
        private CellSequence[] _rowSeqs;

        public CellSequence[] PartyRowSequences()
        {
            if (_rowSeqs != null) return _rowSeqs;
            _row = new WeCellAnimRenderer();
            bool ok = gameDirs.ContainsKey(DirNames.battleObj)
                   && _row.Load(DirNames.battleObj, 340, DirNames.battleObj, 110, DirNames.battleObj, 341, DirNames.battleObj, 342);
            _rowSeqs = ok ? _row.BuildSequences() : Array.Empty<CellSequence>();
            if (!ok) _row = null;
            return _rowSeqs;
        }

        /// <summary>Both rows drawn into one 256×192 picture, or null when neither is showing.</summary>
        public AvaBitmap ComposeRows(params (SendOutSequence.RowState Row, CellActor[] Balls, bool Player)[] rows)
        {
            var seqs = PartyRowSequences();
            if (_row == null) return null;
            var buffer = new byte[256 * 192 * 4];
            bool any = false;
            foreach (var (row, balls, player) in rows)
            {
                if (!row.Visible || row.Alpha <= 0) continue;
                any = true;
                int barSeq = player ? RowPlayerBar : RowEnemyBar;
                int barCell = barSeq < seqs.Length && seqs[barSeq].Frames?.Length > 0 ? seqs[barSeq].Frames[0].Cell : (player ? 26 : 25);
                Blit(buffer, _row.RenderCellRgba(barCell), (int)Math.Round(row.BarX), (int)Math.Round(row.BarY), row.Alpha);
                for (int s = 0; s < row.Balls.Length && s < balls.Length; s++)
                    if (row.Balls[s].Visible)
                        Blit(buffer, _row.RenderCellRgba(balls[s].CellIndex), (int)Math.Round(row.Balls[s].X), (int)Math.Round(row.Balls[s].Y), row.Alpha);
            }
            return any ? ImageConverter.FromRgba(buffer, 256, 192) : null;
        }

        // Straight-alpha "over" of a centred cell buffer onto the 256×192 picture.
        private static void Blit(byte[] dst, WeCellAnimRenderer.CellPixels cell, int x, int y, double alpha)
        {
            if (cell.Rgba == null) return;
            int half = cell.Size / 2;
            for (int cy = 0; cy < cell.Size; cy++)
            {
                int sy = y - half + cy;
                if (sy < 0 || sy >= 192) continue;
                for (int cx = 0; cx < cell.Size; cx++)
                {
                    int sx = x - half + cx;
                    if (sx < 0 || sx >= 256) continue;
                    int si = (cy * cell.Size + cx) * 4;
                    double a = cell.Rgba[si + 3] / 255.0 * alpha;
                    if (a <= 0) continue;
                    int di = (sy * 256 + sx) * 4;
                    double da = dst[di + 3] / 255.0, oa = a + da * (1 - a);
                    for (int k = 0; k < 3; k++)
                        dst[di + k] = (byte)Math.Round((cell.Rgba[si + k] * a + dst[di + k] * da * (1 - a)) / oa);
                    dst[di + 3] = (byte)Math.Round(oa * 255);
                }
            }
        }

        // ── Shiny sparkle ───────────────────────────────────────────────────────────────────────
        private const int ShinySparkleSubscript = 11;
        private byte[] _sparkleBytes;
        private bool _sparkleTried;
        private ScriptNarc _effectParticles;

        /// <summary>The shiny sparkle on the battler at (<paramref name="x"/>, <paramref name="y"/>), or null.</summary>
        public WestPlayer Sparkle(bool enemySide, double x, double y)
        {
            var version = gameFamily == GameFamilies.HGSS ? WazaSeqVersion.HGSS : WazaSeqVersion.Plat;
            if (!_sparkleTried)
            {
                _sparkleTried = true;
                _sparkleBytes = gameDirs.ContainsKey(DirNames.wazaEffectSub) ? new ScriptNarc(DirNames.wazaEffectSub).Get(ShinySparkleSubscript) : null;
            }
            if (_sparkleBytes == null) return null;
            var cmds = WestScript.Parse(_sparkleBytes, version);
            if (cmds == null || cmds.Count == 0) return null;
            _effectParticles ??= new ScriptNarc(DirNames.wazaParticle);
            return new WestPlayer(cmds, version, _effectParticles, x, y, x, y, 256, 192, attackerIsEnemy: enemySide, selfTarget: true);
        }

        // ── Sound ───────────────────────────────────────────────────────────────────────────────
        private readonly Dictionary<string, Task<short[]>> _named = new();
        private readonly Dictionary<int, Task<short[]>> _numbered = new();

        /// <summary>Renders a sound effect by its sequence name in the background, once.</summary>
        public Task<short[]> Sound(string seqName)
        {
            if (_named.TryGetValue(seqName, out var task)) return task;
            return _named[seqName] = Task.Run(() =>
            {
                try
                {
                    var sdat = SoundArchive.Load();
                    if (sdat == null) return null;
                    foreach (var kv in sdat.SeqNames)
                        if (kv.Value == seqName) return SseqPlayer.Render(sdat, kv.Key);
                }
                catch { }
                return null;
            });
        }

        /// <summary>Renders a sound effect by its sequence number in the background, once.</summary>
        public Task<short[]> Sound(int seqId)
        {
            if (_numbered.TryGetValue(seqId, out var task)) return task;
            return _numbered[seqId] = Task.Run(() =>
            {
                try
                {
                    var sdat = SoundArchive.Load();
                    return sdat != null ? SseqPlayer.Render(sdat, seqId) : null;
                }
                catch { return null; }
            });
        }

        public Task<short[]> BallOpenSound() => Sound("SEQ_SE_DP_BOWA2");

        // ── Battle music ─────────────────────────────────────────────────────────────────────────
        private BattleMusicTables _musicTables;
        private bool _musicTablesRead;
        private readonly Dictionary<int, Task<short[]>> _music = new();

        // Long enough to outlast a send-out at the slowest text speed.
        private const double MusicSeconds = 60;
        // The theme starts at the transition, 150 frames before a HeartGold trainer intro's first tick.
        private const double MusicLeadInSeconds = 150 / 59.8261;

        /// <summary>The theme for this trainer class or wild species, or -1.</summary>
        public int BattleMusic(bool trainer, int trainerClass, int species, bool kanto)
        {
            lock (_music)
            {
                if (!_musicTablesRead)
                {
                    _musicTablesRead = true;
                    try { _musicTables = BattleMusicTables.Load(); }
                    catch (Exception ex) { AppLogger.Error("Battle music tables could not be read: " + ex.Message); }
                }
            }
            if (_musicTables == null) return -1;
            return trainer ? _musicTables.TrainerSequence(trainerClass, kanto) : _musicTables.WildSequence(species, kanto);
        }

        public static string SequenceName(int seqId)
        {
            var sdat = SoundArchive.Load();
            return sdat?.SeqNames != null && sdat.SeqNames.TryGetValue(seqId, out var name) ? name : $"Sequence {seqId}";
        }

        /// <summary>Renders a battle theme once, starting where it is when the intro begins.</summary>
        public Task<short[]> Music(int seqId)
        {
            if (seqId < 0) return Task.FromResult<short[]>(null);
            if (_music.TryGetValue(seqId, out var task)) return task;
            return _music[seqId] = Task.Run(() =>
            {
                try
                {
                    var sdat = SoundArchive.Load();
                    var pcm = sdat != null ? SseqPlayer.Render(sdat, seqId, 32000, MusicSeconds + MusicLeadInSeconds) : null;
                    int skip = (int)(MusicLeadInSeconds * 32000) * 2;
                    return pcm == null || pcm.Length <= skip ? pcm : pcm[skip..];
                }
                catch { return null; }
            });
        }

        /// <summary>Starts rendered music and returns a handle for <see cref="StopMusic"/>.</summary>
        public static object StartMusic(short[] pcm)
        {
            try { return pcm != null && pcm.Length > 0 ? AudioOutput.Current.Start(pcm, 32000) : null; } catch { return null; }
        }

        public static void StopMusic(object handle)
        {
            if (handle == null) return;
            try { AudioOutput.Current.Stop(handle); } catch { }
        }

        /// <summary>Renders a species' cry in the background.</summary>
        public static Task<short[]> Cry(int species) => Task.Run(() =>
        {
            try { return SoundArchive.RenderCry(species); } catch { return null; }
        });

        public static void Play(Task<short[]> sound)
        {
            if (sound == null) return;
            if (sound.IsCompletedSuccessfully) Start(sound.Result);
            else sound.ContinueWith(t => { if (t.IsCompletedSuccessfully) Start(t.Result); }, TaskScheduler.Default);
        }

        private static void Start(short[] pcm)
        {
            try { if (pcm != null && pcm.Length > 0) AudioOutput.Current.Play(pcm, 32000); } catch { }
        }
    }
}
