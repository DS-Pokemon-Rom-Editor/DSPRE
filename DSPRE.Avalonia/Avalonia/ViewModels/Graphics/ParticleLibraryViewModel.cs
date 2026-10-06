using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Avalonia.Controls;
using DSPRE.Avalonia.Data;
using DSPRE.ROMFiles;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.ViewModels.Graphics
{
    /// <summary>One particle file, named by what the game uses it for.</summary>
    public sealed class ParticleFileRow
    {
        public string Category { get; init; }
        public string Name { get; init; }
        public string ArchiveName { get; init; }
        public int Index { get; init; }
        public int Emitters { get; init; }
        public int Textures { get; init; }
        public ArchiveFiles Source { get; init; }
        /// <summary>Seal bursts are drawn with the orthographic camera.</summary>
        public bool Orthographic { get; init; }

        public string Title => Name;
        public string Detail => $"{Category} · {ArchiveName} file {Index} · {Emitters} emitter{(Emitters == 1 ? "" : "s")}, {Textures} texture{(Textures == 1 ? "" : "s")}";
    }

    /// <summary>Lists every particle file in the game, grouped by what it is for.</summary>
    public sealed class ParticleLibraryViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
        private bool Set<T>(ref T f, T v, [CallerMemberName] string n = null)
        {
            if (EqualityComparer<T>.Default.Equals(f, v)) return false;
            f = v; OnPropertyChanged(n); return true;
        }

        public const string Everything = "Everything";

        private readonly List<ParticleFileRow> _all = new();

        public ObservableCollection<ParticleFileRow> Shown { get; } = new();
        public ObservableCollection<string> Categories { get; } = new();

        public ParticleLibraryViewModel() { }

        /// <summary>Reads the archives. Slow, so call it off the UI thread.</summary>
        public void Gather()
        {
            if (Design.IsDesignMode) return;
            _all.Clear();
            try { GatherScripted(); } catch (Exception ex) { AppLogger.Error("Particle library, scripts: " + ex.Message); }
            try { GatherBall(); } catch (Exception ex) { AppLogger.Error("Particle library, ball particles: " + ex.Message); }
            try { GatherEncounter(); } catch (Exception ex) { AppLogger.Error("Particle library, battle intros: " + ex.Message); }
            try { GatherLoose(); } catch (Exception ex) { AppLogger.Error("Particle library, other archives: " + ex.Message); }
        }

        /// <summary>Call on the UI thread after Gather.</summary>
        public void Ready()
        {
            Categories.Clear();
            Categories.Add(Everything);
            foreach (string c in ParticleFileNames.Order)
                if (_all.Any(r => r.Category == c)) Categories.Add(c);
            _category = Everything;
            OnPropertyChanged(nameof(Category));
            Show();
        }

        private string _category = Everything;
        public string Category { get => _category; set { if (Set(ref _category, value ?? Everything)) Show(); } }

        private string _search = "";
        public string Search { get => _search; set { if (Set(ref _search, value ?? "")) Show(); } }

        private int _selectedIndex = -1;
        public int SelectedIndex { get => _selectedIndex; set => Set(ref _selectedIndex, value); }
        public ParticleFileRow Selected => _selectedIndex >= 0 && _selectedIndex < Shown.Count ? Shown[_selectedIndex] : null;

        private string _status = "";
        public string StatusText { get => _status; private set => Set(ref _status, value); }

        private void Show()
        {
            Shown.Clear();
            string want = _search.Trim();
            foreach (ParticleFileRow row in _all)
            {
                if (_category != Everything && row.Category != _category) continue;
                if (want.Length > 0 && row.Name.IndexOf(want, StringComparison.OrdinalIgnoreCase) < 0
                    && row.Index.ToString() != want) continue;
                Shown.Add(row);
            }
            SelectedIndex = Shown.Count > 0 ? 0 : -1;
            StatusText = $"{Shown.Count} of {_all.Count} particle files.";
        }

        // ── the sources ─────────────────────────────────────────────────────────────────────────────

        private static (int Emitters, int Textures) Counts(byte[] file)
            => file != null && file.Length >= 12 && file[0] == ' ' && file[1] == 'A' && file[2] == 'P' && file[3] == 'S'
                ? (file[8] | file[9] << 8, file[10] | file[11] << 8) : (-1, -1);

        private void Add(string category, string name, ArchiveFiles source, string archiveName, int index, byte[] file, bool orthographic = false)
        {
            (int emitters, int textures) = Counts(file);
            if (emitters < 0) return;
            _all.Add(new ParticleFileRow
            {
                Category = category, Name = name, Source = source, ArchiveName = archiveName, Index = index,
                Emitters = emitters, Textures = textures, Orthographic = orthographic,
            });
        }

        /// <summary>Move animations and effect scripts, named by the scripts that load each file.</summary>
        private void GatherScripted()
        {
            if (!gameDirs.ContainsKey(DirNames.wazaParticle)) return;
            WazaSeqVersion version = gameFamily switch
            {
                GameFamilies.DP => WazaSeqVersion.DP,
                GameFamilies.Plat => WazaSeqVersion.Plat,
                _ => WazaSeqVersion.HGSS,
            };
            Dictionary<int, List<int>> usedByMove = new Dictionary<int, List<int>>();
            Dictionary<int, List<int>> usedByEffect = new Dictionary<int, List<int>>();
            void Scan(DirNames dir, Dictionary<int, List<int>> into)
            {
                if (!gameDirs.ContainsKey(dir)) return;
                ScriptNarc narc = new ScriptNarc(dir);
                for (int i = 0; i < narc.Count; i++)
                {
                    List<WazaSeqCommand> cmds;
                    try { cmds = BattleAnimScript.Parse(narc.Get(i), version); } catch { continue; }
                    foreach (WazaSeqCommand c in cmds)
                    {
                        string op = BattleAnimCommands.Name(version, c.OpId);
                        // The extended load names the archive before the file.
                        int at = op == "LoadDebugParticleSystem" ? 2 : op == "LoadParticleSystem" ? 1 : -1;
                        if (at < 0 || c.Args.Length <= at) continue;
                        if (!into.TryGetValue(c.Args[at], out List<int> list)) into[c.Args[at]] = list = new List<int>();
                        if (!list.Contains(i)) list.Add(i);
                    }
                }
            }
            Scan(DirNames.wazaEffectScripts, usedByMove);
            Scan(DirNames.wazaEffectSub, usedByEffect);

            string[] moves;
            try { moves = GetAttackNames(); } catch { moves = Array.Empty<string>(); }
            string MoveName(int i) => i > 0 && i < moves.Length && !string.IsNullOrWhiteSpace(moves[i]) ? moves[i].Trim() : $"Move {i}";

            ArchiveFiles source = ArchiveFiles.Mapped(DirNames.wazaParticle);
            ScriptNarc particles = new ScriptNarc(DirNames.wazaParticle);
            for (int f = 0; f < particles.Count; f++)
            {
                byte[] file = particles.Get(f);
                // The leading files belong to battle code and effect scripts, whatever move happens to reuse one.
                if (ParticleFileNames.MoveArchive(gameFamily, f) is { } fixedName)
                    Add(fixedName.Category, fixedName.Name, source, "Move particles", f, file);
                else if (usedByMove.TryGetValue(f, out List<int> byMoves))
                {
                    List<string> names = byMoves.Select(MoveName).ToList();
                    string name = names.Count <= 3 ? string.Join(", ", names) : $"{string.Join(", ", names.Take(3))} and {names.Count - 3} more";
                    Add(ParticleFileNames.Moves, name, source, "Move particles", f, file);
                }
                else if (usedByEffect.TryGetValue(f, out List<int> byEffects))
                    Add(ParticleFileNames.BattleEffects, "Battle effect " + string.Join(", ", byEffects), source, "Move particles", f, file);
                else
                    Add(ParticleFileNames.Unused, $"Move particle file {f} (no move loads it)", source, "Move particles", f, file);
            }
        }

        /// <summary>Seals, the burst each ball opens with, and whatever else the ball archive holds.</summary>
        private void GatherBall()
        {
            if (!gameDirs.ContainsKey(DirNames.ballParticles)) return;
            ArchiveFiles source = ArchiveFiles.Mapped(DirNames.ballParticles);
            ScriptNarc narc = new ScriptNarc(DirNames.ballParticles);
            Dictionary<int, (string Category, string Name, bool Ortho)> named = new Dictionary<int, (string Category, string Name, bool Ortho)>();
            foreach (BallSeal seal in BallSeals.Read())
                if (seal != null) named[seal.Particle] = (ParticleFileNames.Seals, "Seal: " + seal.Name, true);
            Dictionary<int, string> ballNames = new Dictionary<int, string>();
            foreach ((int ball, string name) in SendOutGraphics.Balls())
            {
                ballNames[ball] = name;
                int entry = SendOutGraphics.BurstEntry(ball);
                if (!named.ContainsKey(entry)) named[entry] = (ParticleFileNames.Balls, name + " opening", false);
            }
            for (int f = 0; f < narc.Count; f++)
            {
                (string category, string name, bool ortho) = named.TryGetValue(f, out (string Category, string Name, bool Ortho) n) ? n
                    : ParticleFileNames.BallArchive(gameFamily, f, ballNames) is { } b ? (b.Category, b.Name, false)
                    : (ParticleFileNames.Other, $"Ball particle file {f}", false);
                Add(category, name, source, "Ball particles", f, narc.Get(f), ortho);
            }
        }

        /// <summary>
        /// The battle intro archive is edited unpacked by the VS Intro Editor, so its particle files are read and
        /// written through the same unpacked copy; a loose copy would be overwritten when the ROM is saved.
        /// </summary>
        private void GatherEncounter()
        {
            if (!gameDirs.TryGetValue(DirNames.encounterEffectGraphics, out (string packedDir, string unpackedDir) dirs)) return;
            ArchiveFiles source = ArchiveFiles.Mapped(DirNames.encounterEffectGraphics);
            ScriptNarc narc = new ScriptNarc(DirNames.encounterEffectGraphics);
            string relative = string.IsNullOrEmpty(dataPath) ? dirs.packedDir : Path.GetRelativePath(dataPath, dirs.packedDir).Replace('\\', '/');
            for (int f = 0; f < narc.Count; f++)
            {
                (string category, string name) = ParticleFileNames.Loose(gameFamily, relative, f);
                Add(category, name, source, relative, f, narc.Get(f));
            }
        }

        /// <summary>Unmapped archives that hold particle files.</summary>
        private void GatherLoose()
        {
            if (string.IsNullOrEmpty(dataPath) || !Directory.Exists(dataPath)) return;
            HashSet<string> mapped = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<DirNames, (string packedDir, string unpackedDir)> kv in gameDirs)
            {
                try { mapped.Add(Path.GetFullPath(kv.Value.packedDir)); } catch { }
            }
            foreach (string path in Directory.EnumerateFiles(dataPath, "*", SearchOption.AllDirectories).OrderBy(p => p, StringComparer.Ordinal))
            {
                string full;
                try { full = Path.GetFullPath(path); } catch { continue; }
                if (mapped.Contains(full)) continue;
                byte[] head = new byte[4];
                try { using FileStream fs = File.OpenRead(full); if (fs.Read(head, 0, 4) < 4) continue; } catch { continue; }
                if (head[0] != 'N' || head[1] != 'A' || head[2] != 'R' || head[3] != 'C') continue;

                string relative = Path.GetRelativePath(dataPath, full).Replace('\\', '/');
                ArchiveFiles source = ArchiveFiles.Loose(full, relative);
                int count;
                try { count = source.Count; } catch { continue; }
                for (int f = 0; f < count; f++)
                {
                    byte[] file;
                    try { file = source.Get(f); } catch { continue; }
                    (string category, string name) = ParticleFileNames.Loose(gameFamily, relative, f);
                    Add(category, name, source, relative, f, file);
                }
            }
        }
    }
}
