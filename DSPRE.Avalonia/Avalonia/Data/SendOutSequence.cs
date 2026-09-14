using System;
using System.Collections.Generic;

namespace DSPRE.Avalonia.Data
{
    public enum SendOutKind { Wild, Trainer }
    public enum PreviewSides { Both, Theirs, Yours }
    public enum TextSpeed { Slow, Mid, Fast, Instant }
    public enum SendOutMessage { None, Challenged, WildAppeared, EnemySentOut, Go }

    /// <summary>What a send-out preview plays.</summary>
    public sealed class SendOutOptions
    {
        public SendOutKind Kind = SendOutKind.Trainer;
        public PreviewSides Sides = PreviewSides.Both;
        public int Ball = 4;
        /// <summary>Off plays a mid-battle send-out: no trainers, no slide, the scene already set.</summary>
        public bool TrainerIntro = true;
        /// <summary>A trainer battle whose Pokémon slides in the way a wild one does.</summary>
        public bool SlideIn;
        public bool ShowPartyBalls = true;
        public int PartyBalls = 3;
        public bool Shiny;
        public TextSpeed Speed = TextSpeed.Mid;
        public int EnemyCryDelay, PlayerCryDelay;
        /// <summary>HeartGold and SoulSilver never play a send-out cry with no delay.</summary>
        public bool CryZeroIsEight;
        /// <summary>How many animations the enemy trainer's class has, and how long its second one runs.</summary>
        public int TrainerSequences = 1, TrainerLandingTicks;
        public Func<SendOutMessage, string> Text = m => "";
    }

    /// <summary>
    /// A battle opening or mid-battle send-out, one 30 Hz tick per <see cref="Step"/>. The host sets the
    /// Busy inputs from its animation players before each step.
    /// </summary>
    public sealed class SendOutSequence
    {
        public sealed class MonState
        {
            public bool Visible = true;
            public double OffsetX;
            public double Scale = 1;
            public uint TintRgb;      // 0xRRGGBB
            public double Tint;       // 0..1, the fade strength over 16
        }

        public sealed class BallState
        {
            public bool Visible;
            public int X, Y;
            public int Rotation;      // 0x10000 per turn
            public int Sequence;      // NANR sequence: 0 spin, 1 open
            public bool Animating;    // advances one NANR unit per tick
            public double Flash;      // 0..1 blend toward white
        }

        public sealed class TrainerState
        {
            public bool Visible;
            public int X, Y;
            public int Sequence;          // the class animation shown (enemy)
            public int SequenceTicks;     // ticks into it
            public int AnimTicks = -1;    // ticks into the throw (player), -1 for the resting pose
        }

        public sealed class RowBall
        {
            public bool Visible;
            public double X, Y;
            public int Sequence;
            public bool Animating;
        }

        public sealed class RowState
        {
            public bool Visible;
            public double BarX, BarY;
            public double Alpha = 1;
            public readonly RowBall[] Balls = { new(), new(), new(), new(), new(), new() };
        }

        public MonState Enemy { get; } = new MonState();
        public MonState Player { get; } = new MonState();
        public BallState EnemyBall { get; } = new BallState();
        public BallState PlayerBall { get; } = new BallState();
        public TrainerState EnemyTrainer { get; } = new TrainerState();
        public TrainerState PlayerTrainer { get; } = new TrainerState();
        public RowState EnemyRow { get; } = new RowState();
        public RowState PlayerRow { get; } = new RowState();

        public int EnemyPlatformOffsetX { get; private set; }
        public int PlayerPlatformOffsetX { get; private set; }
        public int BackdropScrollX { get; private set; }
        public int EnemyGaugeOffsetX { get; private set; }
        public int PlayerGaugeOffsetX { get; private set; }
        public bool EnemyGaugeVisible { get; private set; }
        public bool PlayerGaugeVisible { get; private set; }
        /// <summary>0 black to 1 normal: the text box's colours fade in from black during an intro.</summary>
        public double TextBox { get; private set; } = 1;
        public SendOutMessage Message { get; private set; }
        /// <summary>The part of <see cref="Message"/> printed so far; a newline starts the second line.</summary>
        public string MessageText { get; private set; } = "";

        // Things that happen on the tick just stepped.
        public bool EnemyAnimStarts { get; private set; }
        public bool PlayerAnimStarts { get; private set; }
        public bool EnemyBallOpens { get; private set; }
        public bool PlayerBallOpens { get; private set; }
        public bool EnemyCry { get; private set; }
        public bool PlayerCry { get; private set; }
        public bool EnemySparkleStarts { get; private set; }
        public bool PlayerSparkleStarts { get; private set; }
        public List<string> Sounds { get; } = new List<string>();

        /// <summary>Set by the host before each step: that side's frame run or movement script is still going.</summary>
        public bool EnemyBusy, PlayerBusy;
        /// <summary>Set by the host before each step: that side's shiny sparkle is still going.</summary>
        public bool EnemySparkleBusy, PlayerSparkleBusy;
        /// <summary>Set by the host before each step: that side's ball burst still has particles.</summary>
        public bool EnemyBurstBusy, PlayerBurstBusy;

        public int Tick { get; private set; } = -1;
        public bool Done => _script == null && _tasks.Count == 0 && _starting.Count == 0 && !_printing;

        /// <summary>The tick the latest message finished printing, including any hold at its end.</summary>
        public int PrintFinishedTick { get; private set; } = -1;

        public SendOutOptions Options { get; }
        public uint BallColor { get; }

        private readonly bool _enemy, _player;
        private readonly int _enemyCryDelay, _playerCryDelay;
        private readonly List<IEnumerator<int>> _tasks = new(), _starting = new();
        private IEnumerator<int> _script;
        private bool _startedEarly;

        // Screen positions and speeds, the same in both games.
        private const int EnemyRestX = 192, PlayerRestX = 64;
        private const int EnemyTrainerY = 50, PlayerTrainerY = 112;
        private const int EnemyBallX = 192, EnemyBallY = 86;
        private const int PlayerBallEndX = 64, PlayerBallEndY = 144, ArcHeight = 48, ArcSteps = 20;
        private const int SlideDistance = 272, SlideStart = 26, Landing = 60;

        // In ticks: after something finishes, the next command lands on the third tick. After "appeared!" and
        // the challenge, a 31-pass button wait puts the next message 18 ticks on.
        private const int NextCommand = 3, AfterButtonWait = 18;
        private const int EnemyHealthbarDelay = 56, PlayerHealthbarDelay = 48, MidBattleHealthbarDelay = 36;
        // A message ending in a scroll mark holds this many printer runs.
        private const int AutoScrollRuns = 101;

        public SendOutSequence(SendOutOptions options)
        {
            Options = options ?? new SendOutOptions();
            BallColor = BallFadeColor(Options.Ball);
            _enemy = Options.Sides != PreviewSides.Yours;
            _player = Options.Sides != PreviewSides.Theirs;
            _enemyCryDelay = CryDelay(Options.EnemyCryDelay, Options.CryZeroIsEight);
            _playerCryDelay = CryDelay(Options.PlayerCryDelay, Options.CryZeroIsEight);

            // What is on screen before anything moves: the side not being sent out is already in battle.
            Enemy.Visible = EnemyGaugeVisible = !_enemy;
            Player.Visible = PlayerGaugeVisible = !_player;
            bool intro = Options.Kind == SendOutKind.Wild || Options.TrainerIntro;
            if (intro)
            {
                TextBox = 0;
                EnemyPlatformOffsetX = _enemy ? -SlideDistance : 0;
                PlayerPlatformOffsetX = _player ? SlideDistance : 0;
                BackdropScrollX = _player ? 132 : 0;
            }
            _script = intro ? IntroScript() : MidBattleScript();
            if (intro && Options.Kind == SendOutKind.Wild)
            {
                // The script's first run starts the wild slide and your trainer on tick 0 itself.
                _script.MoveNext();
                _tasks.AddRange(_starting);
                _starting.Clear();
                _startedEarly = true;
            }
        }

        // The countdown is checked after it is started on the same tick, so 0 and 1 both start at once.
        private static int CryDelay(int delay, bool zeroIsEight)
        {
            if (delay <= 0 && zeroIsEight) delay = 8;
            return Math.Max(delay, 1) - 1;
        }

        /// <summary>The colour a Pokémon comes out of its ball in, as 0xRRGGBB.</summary>
        public static uint BallFadeColor(int ball)
        {
            ushort bgr = ball >= 1 && ball < FadeColors.Length ? FadeColors[ball]
                       : ball >= FadeColors.Length && ball <= 24 ? (ushort)0x7ADF   // HGSS apricorn balls share the Poké Ball's
                       : FadeColors[4];
            uint r = (uint)((bgr & 0x1F) * 255 / 31), g = (uint)(((bgr >> 5) & 0x1F) * 255 / 31), b = (uint)(((bgr >> 10) & 0x1F) * 255 / 31);
            return (r << 16) | (g << 8) | b;
        }

        private static readonly ushort[] FadeColors =
        {
            0x0000, 0x7297, 0x3FFF, 0x7AF0, 0x7ADF, 0x53D7, 0x67F5, 0x7B2C, 0x2B7E,
            0x431F, 0x7BDD, 0x2A3F, 0x293F, 0x45CE, 0x731F, 0x7F51, 0x151E,
        };

        public void Step()
        {
            Tick++;
            EnemyAnimStarts = PlayerAnimStarts = EnemyBallOpens = PlayerBallOpens = EnemyCry = PlayerCry = false;
            EnemySparkleStarts = PlayerSparkleStarts = false;
            Sounds.Clear();

            // A task first runs on the tick after it was started.
            _tasks.AddRange(_starting);
            _starting.Clear();
            for (int i = 0; i < _tasks.Count; i++)
                if (!_tasks[i].MoveNext()) { _tasks.RemoveAt(i); i--; }

            if (_startedEarly) _startedEarly = false;
            else if (_script != null && !_script.MoveNext()) _script = null;
            PrinterRun();
            PrinterRun();
        }

        // ── Script plumbing ────────────────────────────────────────────────────────────────────────
        private void Start(IEnumerator<int> task) => _starting.Add(task);

        /// <summary>Starts a task and reports the tick it finishes through <paramref name="finished"/>.</summary>
        private void Start(IEnumerable<int> task, Action<int> finished) => _starting.Add(Tracked(task, finished));

        private IEnumerator<int> Tracked(IEnumerable<int> task, Action<int> finished)
        {
            foreach (var _ in task) yield return 0;
            finished(Tick);
        }

        private IEnumerable<int> Until(int tick) { while (Tick < tick) yield return 0; }

        private IEnumerable<int> UntilSet(Func<bool> done) { while (!done()) yield return 0; }

        // ── Scripts ────────────────────────────────────────────────────────────────────────────────
        // Tick 0 is the tick a wild Pokémon's slide task first runs. Everything the script starts runs a tick later.
        private IEnumerator<int> IntroScript()
        {
            bool wild = Options.Kind == SendOutKind.Wild;
            bool enemySlides = wild || Options.SlideIn;
            bool rows = !wild && Options.ShowPartyBalls;
            int enemyDone = _enemy ? -1 : 0, gaugeDone = _enemy ? -1 : 0, rowsDone = rows ? -1 : 0;

            if (_enemy)
            {
                if (enemySlides) Start(WildSlideTask(), t => enemyDone = t);
                else Start(EnemyTrainerSlideTask(), t => enemyDone = t);
            }
            if (_player) Start(PlayerTrainerSlideTask());
            Start(TextBoxFadeTask());

            if (rows)
            {
                foreach (var _ in Until(48)) yield return 0;
                int pending = (_enemy ? 1 : 0) + (_player ? 1 : 0);
                if (pending == 0) rowsDone = Tick;
                if (_enemy) Start(RowShowTask(EnemyRow, player: false, midBattle: false), t => { if (--pending == 0) rowsDone = t; });
                if (_player) Start(RowShowTask(PlayerRow, player: true, midBattle: false), t => { if (--pending == 0) rowsDone = t; });
            }

            if (enemySlides)
            {
                if (_enemy)
                {
                    foreach (var _ in Until(Landing)) yield return 0;
                    Start(HealthbarTask(enemySide: true), t => gaugeDone = t);
                    foreach (var _ in UntilSet(() => enemyDone >= 0 && gaugeDone >= 0 && rowsDone >= 0)) yield return 0;
                    foreach (var _ in Until(Math.Max(Math.Max(enemyDone, gaugeDone), rowsDone) + NextCommand)) yield return 0;
                    Print(wild ? SendOutMessage.WildAppeared : SendOutMessage.Challenged);
                    foreach (var _ in UntilSet(() => !_printing)) yield return 0;
                    if (!_player) yield break;
                    foreach (var _ in Until(PrintFinishedTick + AfterButtonWait)) yield return 0;
                }
                else
                {
                    foreach (var _ in UntilSet(() => rowsDone >= 0)) yield return 0;
                    foreach (var _ in Until(Math.Max(Tick, Landing + 1 + NextCommand))) yield return 0;
                }
                // A wild battle waits for "Go!" to finish printing before the throw.
                Print(SendOutMessage.Go);
                if (rows) Start(RowHideTask(PlayerRow, player: true, midBattle: false));
                foreach (var _ in UntilSet(() => !_printing)) yield return 0;
                foreach (var _ in Until(PrintFinishedTick + 1)) yield return 0;
            }
            else
            {
                if (_enemy)
                {
                    foreach (var _ in Until(50)) yield return 0;
                    Print(SendOutMessage.Challenged);
                    foreach (var _ in UntilSet(() => !_printing && enemyDone >= 0 && rowsDone >= 0)) yield return 0;
                    int cleared = Math.Max(PrintFinishedTick, Math.Max(enemyDone, rowsDone)) + 1;
                    foreach (var _ in Until(cleared + AfterButtonWait - 1)) yield return 0;

                    // The throw's task runs from the next tick, alongside "sent out" printing.
                    Print(SendOutMessage.EnemySentOut);
                    int throwDone = -1;
                    if (rows) Start(RowHideTask(EnemyRow, player: false, midBattle: false));
                    Start(EnemyThrowTask(), t => throwDone = t);
                    foreach (var _ in Until(Tick + EnemyHealthbarDelay)) yield return 0;
                    Start(HealthbarTask(enemySide: true), t => gaugeDone = t);
                    foreach (var _ in UntilSet(() => throwDone >= 0 && gaugeDone >= 0)) yield return 0;
                    if (!_player) yield break;
                    foreach (var _ in Until(Math.Max(throwDone, gaugeDone) + 2)) yield return 0;
                }
                else
                {
                    foreach (var _ in UntilSet(() => rowsDone >= 0)) yield return 0;
                    foreach (var _ in Until(Math.Max(Tick, 1 + Landing + NextCommand - 1))) yield return 0;
                }
            }

            // Your throw. A trainer's throw task runs from the tick "Go!" starts printing.
            int playerDone = -1, playerGauge = -1;
            Start(PlayerThrowTask(), t => playerDone = t);
            int throwStart = Tick + 1;
            if (!enemySlides)
            {
                yield return 0;
                Print(SendOutMessage.Go);
                if (rows) Start(RowHideTask(PlayerRow, player: true, midBattle: false));
            }
            foreach (var _ in Until(throwStart + PlayerHealthbarDelay - 1)) yield return 0;
            Start(HealthbarTask(enemySide: false), t => playerGauge = t);
            foreach (var _ in UntilSet(() => playerDone >= 0 && playerGauge >= 0)) yield return 0;
        }

        private IEnumerator<int> MidBattleScript()
        {
            bool rows = Options.Kind == SendOutKind.Trainer && Options.ShowPartyBalls;
            int done = -1;
            if (_enemy)
            {
                if (rows)
                {
                    Start(RowShowTask(EnemyRow, player: false, midBattle: true), t => done = t);
                    foreach (var _ in UntilSet(() => done >= 0)) yield return 0;
                    foreach (var _ in Until(done + NextCommand)) yield return 0;
                }
                Print(SendOutMessage.EnemySentOut);
                foreach (var _ in UntilSet(() => !_printing)) yield return 0;
                foreach (var _ in Until(PrintFinishedTick + NextCommand)) yield return 0;
                if (rows)
                {
                    done = -1;
                    Start(RowHideTask(EnemyRow, player: false, midBattle: true), t => done = t);
                    foreach (var _ in UntilSet(() => done >= 0)) yield return 0;
                    foreach (var _ in Until(done + NextCommand)) yield return 0;
                }
                foreach (var _ in SendOutAndHealthbar(enemySide: true)) yield return 0;
            }
            if (_player)
            {
                // Your row is never drawn mid-battle.
                Print(SendOutMessage.Go);
                foreach (var _ in UntilSet(() => !_printing)) yield return 0;
                foreach (var _ in Until(PrintFinishedTick + NextCommand)) yield return 0;
                foreach (var _ in SendOutAndHealthbar(enemySide: false)) yield return 0;
            }
        }

        private IEnumerable<int> SendOutAndHealthbar(bool enemySide)
        {
            int shown = -1, gauge = -1;
            Start(ShowPokemonTask(enemySide), t => shown = t);
            int sent = Tick;
            foreach (var _ in Until(sent + MidBattleHealthbarDelay)) yield return 0;
            Start(HealthbarTask(enemySide), t => gauge = t);
            foreach (var _ in UntilSet(() => shown >= 0 && gauge >= 0)) yield return 0;
            foreach (var _ in Until(Math.Max(shown, gauge) + NextCommand)) yield return 0;
        }

        // ── Text printer ───────────────────────────────────────────────────────────────────────────
        // Twice a tick. A letter costs the speed's runs, a line break one less, and a scroll mark adds 101 runs
        // plus two letter slots.
        private bool _printing;
        private string _full = "";
        private int _run, _nextRun, _printed, _finishRun;

        private void Print(SendOutMessage message)
        {
            Message = message;
            _full = Options.Text?.Invoke(message) ?? "";
            _printed = 0;
            _run = -1;
            _nextRun = 0;
            _finishRun = -1;
            MessageText = "";
            _printing = true;
        }

        private static bool EndsInScroll(SendOutMessage message) => message is SendOutMessage.Challenged or SendOutMessage.WildAppeared;

        private int PrintDelay => Options.Speed switch { TextSpeed.Fast => 1, TextSpeed.Slow => 8, _ => 4 };

        private void PrinterRun()
        {
            if (!_printing) return;
            _run++;
            if (_printed < _full.Length)
            {
                // Preview-only speed: the whole message at once.
                if (Options.Speed == TextSpeed.Instant)
                {
                    _printed = _full.Length;
                    MessageText = _full;
                    _finishRun = _run + 1;
                    return;
                }
                if (_run < _nextRun) return;
                char c = _full[_printed++];
                MessageText = _full.Substring(0, _printed);
                _nextRun = _run + (c == '\n' ? Math.Max(0, PrintDelay - 1) : PrintDelay);
                if (_printed == _full.Length)
                    _finishRun = _run + PrintDelay + (EndsInScroll(Message) ? PrintDelay + AutoScrollRuns : 0);
                else if (c == '\n' && _nextRun <= _run) PrinterRunAgain();
                return;
            }
            if (_finishRun < 0) _finishRun = _run;
            if (_run < _finishRun) return;
            _printing = false;
            PrintFinishedTick = Tick;
        }

        // With the fastest speed a line break costs nothing, so the next letter lands on the same run.
        private void PrinterRunAgain()
        {
            _run--;
            PrinterRun();
        }

        // ── Display tasks ──────────────────────────────────────────────────────────────────────────
        private static int SlideOffset(int t, int sign)
        {
            int moved = t <= SlideStart ? 0 : Math.Min(8 * (t - SlideStart), SlideDistance);
            return sign * (SlideDistance - moved);
        }

        // From black: two steps in sixteen on tick 50, fully in by tick 57.
        private IEnumerator<int> TextBoxFadeTask()
        {
            while (true)
            {
                TextBox = Tick < 50 ? 0 : Math.Min(16, 2 * (Tick - 49)) / 16.0;
                if (TextBox >= 1) yield break;
                yield return 0;
            }
        }

        private IEnumerable<int> EnemyTrainerSlideTask()
        {
            var tr = EnemyTrainer;
            tr.Visible = true;
            tr.Y = EnemyTrainerY;
            // A class with a third animation holds its first frame during the slide.
            tr.Sequence = Options.TrainerSequences > 2 ? 2 : 0;
            tr.SequenceTicks = 0;
            for (int t = 0; t <= Landing; t++)
            {
                EnemyPlatformOffsetX = SlideOffset(t, -1);
                tr.X = EnemyRestX + EnemyPlatformOffsetX;
                yield return 0;
            }
            // On landing the second animation plays once, and the intro waits for it.
            if (Options.TrainerSequences > 1)
            {
                tr.Sequence = 1;
                for (tr.SequenceTicks = 0; tr.SequenceTicks < Options.TrainerLandingTicks; tr.SequenceTicks++) yield return 0;
            }
        }

        private IEnumerator<int> PlayerTrainerSlideTask()
        {
            var tr = PlayerTrainer;
            tr.Visible = true; tr.Y = PlayerTrainerY; tr.AnimTicks = -1;
            for (int t = 0; t <= Landing; t++)
            {
                PlayerPlatformOffsetX = SlideOffset(t, 1);
                // Your side's intro starts the backdrop 132 px to the right and scrolls it home 4 px a tick.
                BackdropScrollX = Math.Max(0, 132 - 4 * Math.Max(0, t - 27));
                tr.X = PlayerRestX + PlayerPlatformOffsetX;
                yield return 0;
            }
        }

        private IEnumerable<int> WildSlideTask()
        {
            var mon = Enemy;
            mon.Visible = true;
            mon.Scale = 1;
            mon.TintRgb = 0x000000;
            for (int t = 0; t < Landing; t++)
            {
                EnemyPlatformOffsetX = SlideOffset(t, -1);
                mon.OffsetX = EnemyPlatformOffsetX;
                mon.Tint = 8 / 16.0;
                yield return 0;
            }
            EnemyPlatformOffsetX = 0;
            mon.OffsetX = 0;
            EnemyAnimStarts = true;
            if (_enemyCryDelay == 0) EnemyCry = true;
            Start(FadeTask(mon, from: 8, everyTicks: 1));
            if (_enemyCryDelay > 0) Start(CryTask(_enemyCryDelay, enemySide: true));
            // The task holds until the frame run and movement script are over.
            yield return 0;
            while (EnemyBusy) yield return 0;
            foreach (var _ in Finish(enemySide: true)) yield return 0;
        }

        private IEnumerator<int> FadeTask(MonState mon, int from, int everyTicks)
        {
            // The fade takes its first step on the task's first run.
            for (int step = 1; ; step++)
            {
                int value = Math.Max(0, from - step / everyTicks);
                mon.Tint = value / 16.0;
                if (value == 0) yield break;
                yield return 0;
            }
        }

        private IEnumerator<int> CryTask(int delay, bool enemySide)
        {
            for (int t = 1; t < delay; t++) yield return 0;
            if (enemySide) EnemyCry = true; else PlayerCry = true;
        }

        // The task notices the animation ended a tick late, then plays the sparkle if shiny.
        private IEnumerable<int> Finish(bool enemySide)
        {
            yield return 0;
            if (!Options.Shiny) yield break;
            if (enemySide) EnemySparkleStarts = true; else PlayerSparkleStarts = true;
            yield return 0;
            while (enemySide ? EnemySparkleBusy : PlayerSparkleBusy) yield return 0;
        }

        private IEnumerable<int> HealthbarTask(bool enemySide)
        {
            for (int k = 0; ; k++)
            {
                int offset = Math.Max(0, 160 - 24 * k);
                if (enemySide) { EnemyGaugeVisible = true; EnemyGaugeOffsetX = -offset; }
                else { PlayerGaugeVisible = true; PlayerGaugeOffsetX = offset; }
                if (offset == 0) yield break;
                yield return 0;
            }
        }

        private IEnumerable<int> EnemyThrowTask()
        {
            var tr = EnemyTrainer;
            for (int e = 0; ; e++)
            {
                tr.Visible = e <= 20;
                tr.X = EnemyRestX + 5 * e;
                StepEnemyBall(EnemyBall, e);
                if (e >= 23 && !StepAppearance(Enemy, e - 23, _enemyCryDelay, enemySide: true)) break;
                yield return 0;
            }
            EnemyBall.Visible = false;
            foreach (var _ in Finish(enemySide: true)) yield return 0;
        }

        private static void StepEnemyBall(BallState ball, int e)
        {
            ball.Visible = e >= 1 && e <= 32;
            ball.X = EnemyBallX; ball.Y = EnemyBallY; ball.Rotation = 0;
            ball.Sequence = 1;
            ball.Animating = e >= 16;
            ball.Flash = e >= 23 && e <= 30 ? (e - 23) * 2 / 16.0 : e > 30 ? 14 / 16.0 : 0;
        }

        private IEnumerable<int> PlayerThrowTask()
        {
            var tr = PlayerTrainer;
            var ball = PlayerBall;
            for (int p = 0; ; p++)
            {
                tr.Visible = p <= 21;
                tr.X = p <= 1 ? PlayerRestX : PlayerRestX - 5 * (p - 1);
                tr.Y = PlayerTrainerY;
                tr.AnimTicks = p >= 1 ? p - 1 : -1;

                ball.Sequence = 0;
                ball.Animating = p >= 16;
                ball.Rotation = 0;
                ball.Flash = p >= 38 && p <= 45 ? (p - 38) * 2 / 16.0 : p > 45 ? 14 / 16.0 : 0;
                if (p >= 5 && p <= 12) { ball.Visible = true; ball.X = tr.X - 34; ball.Y = PlayerTrainerY + 4; }
                else if (p >= 13 && p <= 15) { ball.Visible = true; ball.X = tr.X - 28; ball.Y = PlayerTrainerY - 11; }
                else if (p >= 16 && p <= 46)
                {
                    const int startX = PlayerRestX - 5 * 15 + 50, startY = PlayerTrainerY - 12;
                    int k = p <= 16 ? 0 : p <= 26 ? p - 16 : p <= 30 ? 10 : Math.Min(ArcSteps, p - 20);
                    (ball.X, ball.Y) = ArcPoint(startX, startY, PlayerBallEndX, PlayerBallEndY, ArcHeight, ArcSteps, k);
                    ball.Visible = true;
                    if (p >= 17 && p <= 40)
                    {
                        const int quarter = 0x2000;
                        ball.Rotation = (0x14000 + quarter * (Math.Min(p, 37) - 16) + 2 * quarter * Math.Max(0, p - 37)) & 0xFFFF;
                    }
                }
                else ball.Visible = false;

                if (p >= 47 && !StepAppearance(Player, p - 47, _playerCryDelay, enemySide: false)) break;
                yield return 0;
            }
            foreach (var _ in Finish(enemySide: false)) yield return 0;
        }

        // Mid-battle: the enemy's ball appears where it opens, yours is thrown in from the left edge.
        private IEnumerable<int> ShowPokemonTask(bool enemySide)
        {
            for (int f = 0; ; f++)
            {
                if (enemySide)
                {
                    StepEnemyBall(EnemyBall, f);
                    if (f >= 23 && !StepAppearance(Enemy, f - 23, _enemyCryDelay, enemySide: true)) break;
                }
                else
                {
                    var ball = PlayerBall;
                    ball.Sequence = 0;
                    ball.Animating = f >= 1;
                    ball.Visible = f >= 1 && f <= 31;
                    int k = f <= 1 ? 0 : f <= 11 ? f - 1 : f <= 15 ? 10 : Math.Min(ArcSteps, f - 5);
                    (ball.X, ball.Y) = ArcPoint(10, 100, PlayerBallEndX, PlayerBallEndY, ArcHeight, ArcSteps, k);
                    const int quarter = 0x2000;
                    ball.Rotation = (quarter * Math.Min(f, 21) + 2 * quarter * Math.Max(0, f - 21)) & 0xFFFF;
                    ball.Flash = f >= 23 && f <= 30 ? (f - 22) * 2 / 16.0 : f > 30 ? 1 : 0;
                    if (f >= 32 && !StepAppearance(Player, f - 32, _playerCryDelay, enemySide: false)) break;
                }
                yield return 0;
            }
            (enemySide ? EnemyBall : PlayerBall).Visible = false;
            foreach (var _ in Finish(enemySide)) yield return 0;
        }

        // Ticks from the ball opening: 8 to grow, then the animation, cry and tint fade (slower while the burst
        // lasts). False once the burst and the animation are over.
        private bool StepAppearance(MonState mon, int a, int cryDelay, bool enemySide)
        {
            mon.Visible = true;
            mon.OffsetX = 0;
            mon.TintRgb = BallColor;
            mon.Scale = Math.Min(1.0, a * 0x20 / 256.0);
            if (a <= 9) mon.Tint = 1.0;
            if (a == 0) { if (enemySide) EnemyBallOpens = true; else PlayerBallOpens = true; }
            if (a == 9)
            {
                if (enemySide) EnemyAnimStarts = true; else PlayerAnimStarts = true;
                if (cryDelay == 0) { if (enemySide) EnemyCry = true; else PlayerCry = true; }
                else Start(CryTask(cryDelay + 1, enemySide));
                bool burst = enemySide ? EnemyBurstBusy : PlayerBurstBusy;
                Start(FadeTask(mon, from: 16, everyTicks: burst ? 2 : 1));
            }
            bool busy = enemySide ? EnemyBusy || EnemyBurstBusy : PlayerBusy || PlayerBurstBusy;
            return a <= 10 || busy;
        }

        // ── Party ball rows ────────────────────────────────────────────────────────────────────────
        private const double PlayerBarRestX = 224, PlayerBarY = 120, PlayerRowBallY = 114;
        private const double EnemyBarRestX = 32, EnemyBarY = 56, EnemyRowBallY = 50;
        private static double RestX(bool player, int slot) => player ? 162 + 16 * slot : 94 - 16 * slot;
        private static double OvershootX(bool player, int slot) => player ? 156 + 15 * slot : 100 - 15 * slot;
        private static int HealthySeq(bool player) => player ? 3 : 0;
        private int SlotSeq(bool player, int slot) => slot < Math.Clamp(Options.PartyBalls, 1, 6) ? HealthySeq(player) : 6;

        private IEnumerable<int> RowShowTask(RowState row, bool player, bool midBattle)
        {
            row.Visible = true;
            row.Alpha = 1;
            row.BarY = player ? PlayerBarY : EnemyBarY;
            row.BarX = player ? 352 : -96;
            double barRest = player ? PlayerBarRestX : EnemyBarRestX;
            for (int s = 0; s < 6; s++)
            {
                var b = row.Balls[s];
                b.Visible = true;
                b.X = player ? 276 : -20;
                b.Y = player ? PlayerRowBallY : EnemyRowBallY;
                b.Sequence = SlotSeq(player, s);
                b.Animating = false;
            }

            if (midBattle)
            {
                for (int g = 1; g <= 8; g++)
                {
                    yield return 0;
                    if (g == 1) Sounds.Add("SEQ_SE_DP_TB_START");
                    row.BarX = Toward(row.BarX, barRest, 18);
                    for (int s = 0; s < 6; s++) row.Balls[s].X = Toward(row.Balls[s].X, RestX(player, s), 18);
                }
                yield break;
            }

            var arrived = new bool[6];
            for (int g = 1; ; g++)
            {
                yield return 0;
                if (g == 1) Sounds.Add("SEQ_SE_DP_TB_START");
                if (g <= 8) row.BarX = Toward(row.BarX, barRest, 18);
                for (int s = 0; s < 6; s++)
                {
                    var b = row.Balls[s];
                    if (g < 3 * s + 6 || arrived[s]) continue;
                    b.Animating = true;
                    b.X = Toward(b.X, OvershootX(player, s), 18);
                    if (b.X == OvershootX(player, s))
                    {
                        arrived[s] = true;
                        if (player) Sounds.Add(SlotSeq(player, s) == 6 ? "SEQ_SE_DP_TB_KARA" : "SEQ_SE_DP_TB_KON");
                    }
                }
                if (Array.TrueForAll(arrived, x => x)) break;
            }
            // Roll back to rest the other way, then settle on the first frame.
            for (int s = 0; s < 6; s++)
                if (row.Balls[s].Sequence != 6) row.Balls[s].Sequence = HealthySeq(!player);
            bool moving = true;
            while (moving)
            {
                yield return 0;
                moving = false;
                for (int s = 0; s < 6; s++)
                {
                    var b = row.Balls[s];
                    b.X = Toward(b.X, RestX(player, s), 6);
                    if (b.X != RestX(player, s)) moving = true;
                }
            }
            yield return 0;
            for (int s = 0; s < 6; s++)
            {
                row.Balls[s].Animating = false;
                row.Balls[s].Sequence = SlotSeq(player, s);
            }
        }

        private IEnumerable<int> RowHideTask(RowState row, bool player, bool midBattle)
        {
            if (midBattle)
            {
                for (int h = 1; h <= 16; h++)
                {
                    yield return 0;
                    row.Alpha = (16 - h) / 16.0;
                }
                row.Visible = false;
                yield break;
            }
            double dir = player ? -1 : 1;
            for (int t = 1; t <= 20; t++)
            {
                yield return 0;
                if (t < 5) continue;
                row.Alpha = Math.Max(0, 15 - (t - 5)) / 16.0;
                row.BarX += 4 * dir;
                for (int s = 0; s < 5; s++)
                {
                    if (t < 5 + 3 * s) continue;
                    row.Balls[s].Animating = true;
                    row.Balls[s].X += 12 * dir;
                }
            }
            row.Visible = false;
        }

        private void Start(IEnumerable<int> task) => _starting.Add(task.GetEnumerator());

        private static double Toward(double value, double target, double step) =>
            value < target ? Math.Min(target, value + step) : Math.Max(target, value - step);

        /// <summary>Where a thrown ball is after <paramref name="k"/> of <paramref name="steps"/> steps: a straight
        /// line in fixed point plus a cosine dip of <paramref name="height"/> pixels at the middle.</summary>
        public static (int X, int Y) ArcPoint(int sx, int sy, int ex, int ey, int height, int steps, int k)
        {
            if (k <= 0) return (sx, sy);
            long divX = ((long)(ex - sx) * 4096 << 12) / ((long)steps * 4096);
            long divY = ((long)(ey - sy) * 4096 << 12) / ((long)steps * 4096);
            int x = FloorDiv(sx * 4096L + k * divX, 4096);
            int y = FloorDiv(sy * 4096L + k * divY, 4096);
            int cos = CosIdx(16383 + k * (32768 / steps));
            long dip = ((long)cos * (height * 4096L) + 0x800) >> 12;
            return (x, y + FloorDiv(dip, 4096));
        }

        private static int FloorDiv(long a, int b) => (int)Math.Floor(a / (double)b);

        private static int CosIdx(int idx) => (int)Math.Round(Math.Cos(((idx & 0xFFFF) >> 4) * 2.0 * Math.PI / 4096) * 4096);
    }
}
