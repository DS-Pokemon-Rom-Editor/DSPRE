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
        private const string Moves = "Move animations", Effects = "Battle effects", Seals = "Ball seals",
                             Bursts = "Poke Ball bursts", OtherBall = "Other ball particles";

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
            try { GatherLoose(); } catch (Exception ex) { AppLogger.Error("Particle library, other archives: " + ex.Message); }
        }

        /// <summary>Call on the UI thread after Gather.</summary>
        public void Ready()
        {
            Categories.Clear();
            Categories.Add(Everything);
            foreach (string c in _all.Select(r => r.Category).Distinct()) Categories.Add(c);
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
            foreach (var row in _all)
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
            var (emitters, textures) = Counts(file);
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
            var version = gameFamily switch
            {
                GameFamilies.DP => WazaSeqVersion.DP,
                GameFamilies.Plat => WazaSeqVersion.Plat,
                _ => WazaSeqVersion.HGSS,
            };
            var usedByMove = new Dictionary<int, List<int>>();
            var usedByEffect = new Dictionary<int, List<int>>();
            void Scan(DirNames dir, Dictionary<int, List<int>> into)
            {
                if (!gameDirs.ContainsKey(dir)) return;
                var narc = new ScriptNarc(dir);
                for (int i = 0; i < narc.Count; i++)
                {
                    List<WazaSeqCommand> cmds;
                    try { cmds = WestScript.Parse(narc.Get(i), version); } catch { continue; }
                    foreach (var c in cmds)
                    {
                        string op = WestOpcodes.Name(version, c.OpId);
                        if (op is not ("WEST_LOAD_PARTICLE" or "WEST_LOAD_PARTICLE_EX") || c.Args.Length < 2) continue;
                        if (!into.TryGetValue(c.Args[1], out var list)) into[c.Args[1]] = list = new List<int>();
                        if (!list.Contains(i)) list.Add(i);
                    }
                }
            }
            Scan(DirNames.wazaEffectScripts, usedByMove);
            Scan(DirNames.wazaEffectSub, usedByEffect);

            string[] moves;
            try { moves = GetAttackNames(); } catch { moves = Array.Empty<string>(); }
            string MoveName(int i) => i > 0 && i < moves.Length && !string.IsNullOrWhiteSpace(moves[i]) ? moves[i].Trim() : $"Move {i}";

            var source = ArchiveFiles.Mapped(DirNames.wazaParticle);
            var particles = new ScriptNarc(DirNames.wazaParticle);
            for (int f = 0; f < particles.Count; f++)
            {
                byte[] file = particles.Get(f);
                if (usedByMove.TryGetValue(f, out var byMoves))
                {
                    var names = byMoves.Select(MoveName).ToList();
                    string name = names.Count <= 3 ? string.Join(", ", names) : $"{string.Join(", ", names.Take(3))} and {names.Count - 3} more";
                    Add(Moves, name, source, "Move particles", f, file);
                }
                else if (usedByEffect.TryGetValue(f, out var byEffects))
                    Add(Effects, "Battle effect " + string.Join(", ", byEffects), source, "Move particles", f, file);
                else
                    Add(Moves, $"Particle file {f}", source, "Move particles", f, file);
            }
        }

        /// <summary>Seals, the burst each ball opens with, and whatever else the ball archive holds.</summary>
        private void GatherBall()
        {
            if (!gameDirs.ContainsKey(DirNames.ballParticles)) return;
            var source = ArchiveFiles.Mapped(DirNames.ballParticles);
            var narc = new ScriptNarc(DirNames.ballParticles);
            var named = new Dictionary<int, (string Category, string Name, bool Ortho)>();
            foreach (var seal in BallSeals.Read())
                if (seal != null) named[seal.Particle] = (Seals, seal.Name, true);
            foreach (var (ball, name) in SendOutGraphics.Balls())
            {
                int entry = SendOutGraphics.BurstEntry(ball);
                if (!named.ContainsKey(entry)) named[entry] = (Bursts, name + " opening", false);
            }
            for (int f = 0; f < narc.Count; f++)
            {
                var (category, name, ortho) = named.TryGetValue(f, out var n) ? n : (OtherBall, $"Ball particle file {f}", false);
                Add(category, name, source, "Ball particles", f, narc.Get(f), ortho);
            }
        }

        /// <summary>Unmapped archives that hold particle files.</summary>
        private void GatherLoose()
        {
            if (string.IsNullOrEmpty(dataPath) || !Directory.Exists(dataPath)) return;
            var mapped = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var kv in gameDirs)
            {
                try { mapped.Add(Path.GetFullPath(kv.Value.packedDir)); } catch { }
            }
            foreach (string path in Directory.EnumerateFiles(dataPath, "*", SearchOption.AllDirectories).OrderBy(p => p, StringComparer.Ordinal))
            {
                string full;
                try { full = Path.GetFullPath(path); } catch { continue; }
                if (mapped.Contains(full)) continue;
                byte[] head = new byte[4];
                try { using var fs = File.OpenRead(full); if (fs.Read(head, 0, 4) < 4) continue; } catch { continue; }
                if (head[0] != 'N' || head[1] != 'A' || head[2] != 'R' || head[3] != 'C') continue;

                string relative = Path.GetRelativePath(dataPath, full).Replace('\\', '/');
                var source = ArchiveFiles.Loose(full, relative);
                int count;
                try { count = source.Count; } catch { continue; }
                string category = CategoryOf(relative);
                for (int f = 0; f < count; f++)
                {
                    byte[] file;
                    try { file = source.Get(f); } catch { continue; }
                    Add(category, $"{category}, file {f}", source, relative, f, file);
                }
            }
        }

        private static string CategoryOf(string relative)
        {
            string r = relative.ToLowerInvariant();
            if (r.Contains("egg_demo")) return "Egg hatching";
            if (r.Contains("shinka")) return "Evolution";
            if (r.Contains("encounteffect")) return "Wild encounter";
            if (r.Contains("frontier")) return "Battle Frontier";
            if (r.Contains("pokelist")) return "Party menu";
            if (r.Contains("wifi_lobby") || r.Contains("wlmngm")) return "Wi-Fi lobby minigames";
            if (r.Contains("debug")) return "Debug";
            if (r.Contains("pl_etc")) return "Other effects";
            if (r.Contains("particledata")) return "Field and general";
            return "Other particles";
        }
    }
}
