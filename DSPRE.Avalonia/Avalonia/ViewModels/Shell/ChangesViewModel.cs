using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Avalonia.Threading;
using DSPRE.Tracking;

namespace DSPRE.Avalonia.ViewModels.Shell
{
    /// <summary>Track changes for the open project: the summary bar and the changes panel.</summary>
    public sealed class ChangesViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

        /// <summary>One record in the panel, with every changes folder file it has.</summary>
        public sealed class ChangeRow : INotifyPropertyChanged
        {
            public event PropertyChangedEventHandler PropertyChanged;
            public string Pill { get; init; }
            public string Title { get; init; }
            public string Where { get; init; }
            public IReadOnlyList<string> Paths { get; init; }
            public ChangeKind Kind { get; init; }
            public string Id { get; init; }
            public IncomingRecord Incoming { get; init; }
            /// <summary>Committed with every commit, so it has no tick box.</summary>
            public bool Always { get; init; }
            public bool CanInclude => Paths != null && !Always;
            private bool _include = true;
            public bool Include
            {
                get => _include;
                set { _include = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Include))); }
            }
            public override string ToString() => Title;
        }

        /// <summary>A commit waiting to be pushed or pulled.</summary>
        public sealed record CommitRow(string Sha, string Subject, string Detail);

        public ObservableCollection<ChangeRow> Changed { get; } = new();
        public ObservableCollection<ChangeRow> Untracked { get; } = new();
        public ObservableCollection<ChangeRow> Incoming { get; } = new();
        public ObservableCollection<ChangeRow> Conflicts { get; } = new();

        /// <summary>The folder's own files (schemas, manifest, git settings), always committed with the records.</summary>
        public ObservableCollection<ChangeRow> FolderFiles { get; } = new();
        public bool HasFolderFiles => FolderFiles.Count > 0;
        public ObservableCollection<CommitRow> Outgoing { get; } = new();
        public ObservableCollection<CommitRow> Behind { get; } = new();

        private readonly DispatcherTimer _fetchTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(5) };
        private DateTime _lastFetch = DateTime.MinValue;
        private bool _fetching;

        /// <summary>A background fetch is running.</summary>
        public bool IsFetching => _fetching;

        public ChangesViewModel()
        {
            ChangeTracker.StateChanged += () => Dispatcher.UIThread.Post(() => _ = RefreshViewAsync());
            ChangeTracker.LastApplyChanged += () => Dispatcher.UIThread.Post(() => OnPropertyChanged(nameof(CanUndoApply)));
            ChangeTracker.Applied += files => Dispatcher.UIThread.Post(() => TellEditors(files));
            ChangeTracker.WorkingChanged += working => Dispatcher.UIThread.Post(() => IsUpdating = working);
            _fetchTimer.Tick += async (_, _) => await FetchQuietlyAsync(force: true);
            _fetchTimer.Start();
        }

        /// <summary>
        /// Fetches so the to-pull count is real. Fetch only moves the remote branch, never the folder's files.
        /// Focus changes call this often, so it waits a minute between fetches unless forced.
        /// </summary>
        public async Task FetchQuietlyAsync(bool force = false)
        {
            if (_fetching || IsWorking || IsMerging || !CanPullPush || FolderPath == null) return;
            if (!force && DateTime.UtcNow - _lastFetch < TimeSpan.FromMinutes(1)) return;
            _fetching = true;
            try
            {
                CliRunner.Result r = await GitCli.FetchAsync(FolderPath);
                if (r.Success) _lastFetch = DateTime.UtcNow;
            }
            finally { _fetching = false; }
            await RefreshViewAsync();
        }

        public string LastFetchText
        {
            get
            {
                if (_lastFetch == DateTime.MinValue) return "";
                TimeSpan ago = DateTime.UtcNow - _lastFetch;
                return ago < TimeSpan.FromMinutes(1) ? "checked just now" : "checked " + Plural.Of((int)ago.TotalMinutes, "min") + " ago";
            }
        }

        public int AheadCount => _status?.Ahead ?? 0;
        public int BehindCount => _status?.Behind ?? 0;
        public bool HasOutgoing => Outgoing.Count > 0;
        public bool HasAhead => AheadCount > 0;
        public bool HasBehindCount => BehindCount > 0;
        public bool UpToDate => _status?.Upstream != null && AheadCount == 0 && BehindCount == 0;
        public string PullLabel => BehindCount > 0 ? "Pull " + BehindCount : "Pull";
        public string PushLabel => AheadCount > 0 ? "Push " + AheadCount : "Push";
        public bool HasBehind => Behind.Count > 0;

        private bool _updating;
        /// <summary>The tracker is scanning, exporting or applying in the background.</summary>
        public bool IsUpdating
        {
            get => _updating;
            private set { if (_updating == value) return; _updating = value; OnPropertyChanged(); }
        }

        /// <summary>Open editors reload what a patch or pull rewrote, as they do after a save in another window.</summary>
        private void TellEditors(IReadOnlyCollection<TrackedFile> files)
        {
            HashSet<RomInfo.DirNames> archives = new HashSet<RomInfo.DirNames>();
            bool code = false;
            foreach (TrackedFile f in files)
            {
                if (f.TryMember(out RomInfo.DirNames dir, out int id))
                {
                    archives.Add(dir);
                    switch (dir)
                    {
                        case RomInfo.DirNames.EventFiles: AppEvents.RaiseEventFileSaved(this, id); break;
                        case RomInfo.DirNames.Maps: AppEvents.RaiseMapSaved(this, id); break;
                        case RomInfo.DirNames.Matrices: AppEvents.RaiseMatrixSaved(this, id); break;
                        case RomInfo.DirNames.AreaData: AppEvents.RaiseAreaDataSaved(this, id); break;
                        case RomInfo.DirNames.PersonalPokeData: AppEvents.RaisePersonalDataSaved(this, id); break;
                        case RomInfo.DirNames.DynamicHeaders: AppEvents.RaiseHeaderSaved(this, id); break;
                        case RomInfo.DirNames.TextArchives: DSPRE.ROMFiles.TextArchive.RaiseSaved(this, id); break;
                    }
                }
                else if (f.TryRow(out string table, out int row))
                {
                    code = true;
                    if (table == "MapHeaders") AppEvents.RaiseHeaderSaved(this, row);
                }
                else if (f.IsSource)
                {
                    string path = Path.Combine(RomInfo.DspreDir, f.SourcePath.Replace('/', Path.DirectorySeparatorChar));
                    if (f.SourcePath.StartsWith("expanded/textArchives/", StringComparison.Ordinal)
                        && int.TryParse(Path.GetFileNameWithoutExtension(path), out int text))
                        DSPRE.ROMFiles.TextArchive.RaiseSaved(this, text);
                    else if (f.SourcePath.StartsWith("expanded/scripts/", StringComparison.Ordinal)) AppEvents.RaiseScriptSourceSaved(this, path);
                }
                else code = true;
            }
            foreach (RomInfo.DirNames dir in archives) AppEvents.RaiseArchiveMemberSaved(dir);
            if (code) AppEvents.RaiseRomPatchStateChanged();
            AppEvents.RaiseNamesChanged();
        }

        private bool _romLoaded;
        public bool IsRomLoaded
        {
            get => _romLoaded;
            set { _romLoaded = value; Raise(); }
        }

        public bool IsTracking => ChangeTracker.Current != null;
        public bool CanUndoApply => IsRomLoaded && ChangeTracker.CanUndoApply;
        public bool CanTrack => IsRomLoaded && TrackingSetup.WhyNot() == null;
        public bool ShowPrompt => CanTrack && !IsTracking && !TrackingSetup.Declined;

        public string FolderName => ChangeTracker.Current == null ? "" : Path.GetFileName(ChangeTracker.Current.Folder);
        public string FolderPath => ChangeTracker.Current?.Folder;

        private GitCli.Status _status;
        public bool GitAvailable { get; private set; }
        public bool IsRepository => GitAvailable && FolderPath != null && GitCli.IsRepository(FolderPath);
        public bool CanUseGit => IsRepository && !IsWorking;
        public bool CanPullPush => CanUseGit && _status?.HasRemote == true;
        public bool CanPush => CanPullPush && !IsMerging;
        public string CloneTip => GitAvailable ? null : "Install git, or set where it is in Settings > git";
        public string GitTip => !GitAvailable ? "Install git, or set where it is in Settings > git"
            : !IsRepository ? "Not a git repo yet. Use Help me set it up to start."
            : _status?.HasRemote != true ? "No remote is configured, use \"Help me set it up\" to get set up" : null;

        public string Branch => _status?.Branch ?? "";
        public bool HasBranch => !string.IsNullOrEmpty(Branch);
        public string PushPull => _status?.Upstream == null ? "" : $"{_status.Ahead} to push, {_status.Behind} to pull";
        public bool HasPushPull => !string.IsNullOrEmpty(PushPull);

        public int ChangedCount => Changed.Count;
        public int UntrackedCount => Untracked.Count;
        public int IncomingCount => Incoming.Count;
        public int ConflictCount => Conflicts.Count;
        public string ConflictLine => Plural.Of(ConflictCount, "conflict");
        public int Total => Changed.Count + Untracked.Count + Incoming.Count + Conflicts.Count;
        public bool HasIncoming => Incoming.Count > 0;
        public bool HasConflicts => Conflicts.Count > 0;
        public bool HasChanged => Changed.Count > 0;
        public bool HasUntracked => Untracked.Count > 0;
        public bool HasNothing => Total == 0 && !HasFolderFiles;
        public bool CanCommit => CanUseGit && !IsMerging && (Changed.Concat(Untracked).Any(r => r.Include) || HasFolderFiles);

        /// <summary>A git merge is open in the changes folder.</summary>
        public bool IsMerging => ChangeTracker.Current?.Merging == true;
        public string IncomingLine => Plural.Of(Incoming.Count + Conflicts.Count(c => c.Incoming != null), "change") + " came in";

        public string KeptWholeNote => ChangeTracker.Current?.KeptWhole.Count > 0
            ? "Not readable as JSON: " + string.Join(", ", ChangeTracker.Current.KeptWhole)
            : null;
        public bool HasKeptWhole => KeptWholeNote != null;

        private bool _panelOpen = true;
        public bool PanelOpen
        {
            get => _panelOpen;
            set { _panelOpen = value; Raise(); }
        }

        private bool _working;
        public bool IsWorking
        {
            get => _working;
            private set { _working = value; Raise(); }
        }

        private string _message;
        public string Message
        {
            get => _message;
            set { _message = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasMessage)); }
        }
        public bool HasMessage => !string.IsNullOrEmpty(Message);

        private void Raise([CallerMemberName] string n = null)
        {
            OnPropertyChanged(n);
            foreach (string p in new[]
            {
                nameof(IsTracking), nameof(CanTrack), nameof(ShowPrompt), nameof(FolderName), nameof(FolderPath),
                nameof(GitAvailable), nameof(IsRepository), nameof(CanUseGit), nameof(CanPullPush), nameof(GitTip), nameof(CloneTip), nameof(Branch),
                nameof(HasBranch), nameof(PushPull), nameof(HasPushPull), nameof(ChangedCount), nameof(UntrackedCount), nameof(IncomingCount),
                nameof(ConflictCount), nameof(ConflictLine), nameof(Total), nameof(HasIncoming), nameof(HasConflicts), nameof(HasChanged), nameof(HasUntracked),
                nameof(HasNothing), nameof(CanCommit), nameof(IncomingLine), nameof(KeptWholeNote), nameof(HasKeptWhole), nameof(IsMerging), nameof(CanPush), nameof(LastFetchText), nameof(AheadCount), nameof(BehindCount), nameof(HasOutgoing), nameof(HasBehind), nameof(PullLabel), nameof(PushLabel), nameof(CommitTip), nameof(HasFolderFiles), nameof(HasAhead), nameof(HasBehindCount), nameof(UpToDate),
            })
                if (p != n) OnPropertyChanged(p);
        }

        /// <summary>After a project opens: picks tracking back up if the project has a changes folder.</summary>
        public async Task OnProjectLoadedAsync()
        {
            ChangeTracker.Stop();
            IsRomLoaded = !string.IsNullOrEmpty(RomInfo.WorkDir);
            OnPropertyChanged(nameof(CanUndoApply));
            Message = null;
            if (CanTrack)
            {
                try
                {
                    string problem = await TrackingSetup.ResumeAsync();
                    if (problem != null) Message = problem;
                }
                catch (Exception ex)
                {
                    CrashReporter.LogHandled(ex);
                    Message = "Tracking couldn't start: " + ex.Message;
                }
            }
            await RefreshViewAsync();
        }

        public void Decline()
        {
            TrackingSetup.Decline();
            Raise();
        }

        /// <summary>Rescans the project and the changes folder, then shows the result.</summary>
        public async Task RefreshAsync()
        {
            ChangeTracker tracker = ChangeTracker.Current;
            if (tracker != null)
            {
                try { await Task.Run(tracker.Refresh); }
                catch (Exception ex)
                {
                    CrashReporter.LogHandled(ex);
                    Message = "The changes folder couldn't be updated: " + ex.Message;
                }
            }
            await RefreshViewAsync();
        }

        private bool _refreshing, _again;

        /// <summary>Reads git status and the tracker's incoming list into the panel's rows.</summary>
        public async Task RefreshViewAsync()
        {
            if (_refreshing)
            {
                // Callers read the rows next, so they wait for the pass that includes their change.
                _again = true;
                while (_refreshing) await Task.Delay(25);
                return;
            }
            _refreshing = true;
            try
            {
                do
                {
                    _again = false;
                    await LoadRowsAsync();
                }
                while (_again);
            }
            catch (Exception ex)
            {
                CrashReporter.LogHandled(ex);
                Message = "The changes list couldn't be read: " + ex.Message;
            }
            finally { _refreshing = false; }
        }

        private async Task LoadRowsAsync()
        {
            GitAvailable = GitCli.IsAvailable;
            ChangeTracker tracker = ChangeTracker.Current;
            HashSet<string> excluded = _resetTicks ? new HashSet<string>(StringComparer.Ordinal)
                : new HashSet<string>(Changed.Concat(Untracked).Where(r => !r.Include).Select(r => r.Title), StringComparer.Ordinal);
            _resetTicks = false;
            List<ChangeRow> folderFiles = new List<ChangeRow>();
            List<ChangeRow> changed = new List<ChangeRow>(), untracked = new List<ChangeRow>(), incoming = new List<ChangeRow>(), conflicts = new List<ChangeRow>();
            _status = null;
            if (tracker != null)
            {
                ChangeContext ctx = tracker.Context();
                if (GitAvailable && GitCli.IsRepository(tracker.Folder))
                {
                    _status = await GitCli.ReadStatusAsync(tracker.Folder);
                    IReadOnlyList<GitCli.Entry> entries = _status?.Entries ?? Array.Empty<GitCli.Entry>();
                    // Record names read text archives, so they're worked out off the UI thread.
                    List<IGrouping<(ChangeKind, string, string), GitCli.Entry>> groups =
                        await Task.Run(() => entries.GroupBy(e => RecordOf(tracker, ctx, e.Path)).ToList());
                    foreach (IGrouping<(ChangeKind, string, string), GitCli.Entry> group in groups)
                    {
                        (ChangeKind kind, string id, string title) = group.Key;
                        bool conflicted = group.Any(e => e.Conflicted);
                        bool isNew = group.All(e => e.Untracked);
                        bool gone = !conflicted && !isNew && group.All(e => !File.Exists(Path.Combine(tracker.Folder, e.Path)));
                        ChangeRow row = new ChangeRow
                        {
                            Pill = conflicted ? "CLASH" : isNew ? "NEW" : gone ? "BACK" : "EDIT",
                            Title = title + (gone ? " reverted to original" : ""),
                            Where = string.Join(" + ", group.Select(e => e.Path)),
                            Paths = group.Select(e => e.Path).ToList(),
                            Kind = kind,
                            Id = id,
                            Always = kind == null && !conflicted && IsFolderFile(group.Key.Item3),
                        };
                        if (excluded.Contains(row.Title) && !row.Always) row.Include = false;
                        row.PropertyChanged += (_, _) => { OnPropertyChanged(nameof(CanCommit)); OnPropertyChanged(nameof(CommitTip)); };
                        if (row.Always) folderFiles.Add(row);
                        else (conflicted ? conflicts : isNew ? untracked : changed).Add(row);
                    }
                }
                foreach (IncomingRecord r in tracker.Incoming)
                {
                    ChangeRow row = new ChangeRow
                    {
                        Pill = r.AlsoChangedHere ? "CLASH" : "IN",
                        Title = r.Title + (r.AlsoChangedHere ? " has local changes" : ""),
                        Where = r.Kind.Folder,
                        Kind = r.Kind,
                        Id = r.Id,
                        Incoming = r,
                    };
                    (r.AlsoChangedHere ? conflicts : incoming).Add(row);
                }
            }
            Replace(Changed, changed);
            Replace(Untracked, untracked);
            Replace(Incoming, incoming);
            Replace(Conflicts, conflicts);
            Replace(FolderFiles, folderFiles);
            List<CommitRow> outgoing = new List<CommitRow>(), behind = new List<CommitRow>();
            if (tracker != null && _status?.Upstream != null)
            {
                foreach ((string sha, string subject, string author, DateTimeOffset when) in await GitCli.LogAsync(tracker.Folder, "@{u}..HEAD"))
                    outgoing.Add(new CommitRow(sha, subject, Ago(when)));
                foreach ((string sha, string subject, string author, DateTimeOffset when) in await GitCli.LogAsync(tracker.Folder, "HEAD..@{u}"))
                    behind.Add(new CommitRow(sha, subject, author + ", " + Ago(when)));
            }
            Outgoing.Clear();
            foreach (CommitRow c in outgoing) Outgoing.Add(c);
            Behind.Clear();
            foreach (CommitRow c in behind) Behind.Add(c);
            Raise();
        }

        private static string Ago(DateTimeOffset when)
        {
            TimeSpan ago = DateTimeOffset.UtcNow - when;
            if (ago < TimeSpan.FromMinutes(1)) return "just now";
            if (ago < TimeSpan.FromHours(1)) return Plural.Of((int)ago.TotalMinutes, "min") + " ago";
            if (ago < TimeSpan.FromDays(1)) return Plural.Of((int)ago.TotalHours, "hour") + " ago";
            return when.LocalDateTime.ToString("d MMM", System.Globalization.CultureInfo.CurrentCulture);
        }

        private static (ChangeKind, string, string) RecordOf(ChangeTracker tracker, ChangeContext ctx, string path)
        {
            if (tracker.Kinds.TryRecordAt(path, out ChangeKind kind, out string id)) return (kind, id, kind.Title(id, ctx));
            return (null, null, path);
        }

        private static void Replace(ObservableCollection<ChangeRow> into, List<ChangeRow> rows)
        {
            into.Clear();
            foreach (ChangeRow r in rows.OrderBy(r => r.Title, StringComparer.Ordinal)) into.Add(r);
        }

        public int CommitCount => Changed.Concat(Untracked).Count(r => r.Include);

        private bool _resetTicks;

        private static bool IsFolderFile(string path) =>
            path == ChangesManifest.FileName || path.StartsWith(".", StringComparison.Ordinal);

        /// <summary>Why Commit is off, or null.</summary>
        public string CommitTip => !GitAvailable || !IsRepository ? GitTip : (IsMerging ? "Finish the merge first"
            : !Changed.Concat(Untracked).Any(r => r.Include) && !HasFolderFiles ? (HasChanged || HasUntracked ? "Tick at least one record" : "Nothing to commit") : null);

        /// <summary>Runs a git step with the panel marked busy; returns its error or null.</summary>
        public async Task<string> RunAsync(Func<Task<CliRunner.Result>> step, string done)
        {
            IsWorking = true;
            Message = null;
            try
            {
                CliRunner.Result result = await step();
                if (!result.Success) return GitMessages.Explain(result);
                if (done != null) SaveNotice.Show(done);
                return null;
            }
            finally
            {
                IsWorking = false;
                await RefreshAsync();
            }
        }

        /// <summary>Why the ticked rows can't be committed now, or null.</summary>
        public string WhyNotCommit()
        {
            if (FolderPath == null) return null;
            if (GitCli.IsMerging(FolderPath)) return "A merge is still open. Finish it or abort it first.";
            if (Conflicts.Any(r => r.Incoming == null)) return "Some files still have git conflicts.";
            List<string> paths = Changed.Concat(Untracked).Where(r => r.Include).SelectMany(r => r.Paths).Distinct().ToList();
            string bad = GitSync.FirstUnreadableRecord(FolderPath, paths);
            return bad == null ? null : bad + " isn't valid JSON. Fix or revert it before committing.";
        }

        public Task<string> CommitAsync(string message)
        {
            if (WhyNotCommit() is string why) return Task.FromResult(why);
            List<ChangeRow> rows = Changed.Concat(Untracked).ToList();
            bool all = rows.All(r => r.Include);
            List<string> paths = rows.Where(r => r.Include).Concat(FolderFiles).SelectMany(r => r.Paths).Distinct().ToList();
            _resetTicks = true;
            return RunAsync(() => GitCli.CommitAsync(FolderPath, message, all ? null : paths), "Committed");
        }

        /// <summary>Fetches and merges; a merge with clashes comes back for the conflict window.</summary>
        public async Task<GitSync.PullResult> PullAsync()
        {
            ChangeTracker tracker = ChangeTracker.Current;
            if (tracker == null) return new GitSync.PullResult(GitSync.Outcome.Failed, "Nothing is being tracked.");
            IsWorking = true;
            Message = null;
            try
            {
                GitSync.PullResult result = await GitSync.PullAsync(tracker);
                if (result.Outcome == GitSync.Outcome.FastForwarded || result.Outcome == GitSync.Outcome.Merged) SaveNotice.Show("Pulled");
                else if (result.Outcome == GitSync.Outcome.UpToDate) SaveNotice.Show("Already up to date");
                return result;
            }
            finally
            {
                IsWorking = false;
                await RefreshAsync();
            }
        }

        /// <summary>Commits the open merge with the picks made in the conflict window.</summary>
        public async Task<string> FinishMergeAsync(PendingMerge merge)
        {
            IsWorking = true;
            try
            {
                string error = await GitSync.FinishAsync(merge);
                if (error == null) SaveNotice.Show("Merged");
                return error;
            }
            finally
            {
                IsWorking = false;
                await RefreshAsync();
            }
        }

        public async Task<string> AbortMergeAsync()
        {
            IsWorking = true;
            try { return FolderPath == null ? null : await GitSync.AbortAsync(FolderPath); }
            finally
            {
                IsWorking = false;
                await RefreshAsync();
            }
        }
        public Task<string> PushAsync() => RunAsync(() => GitCli.PushAsync(FolderPath), "Pushed");

        /// <summary>Writes incoming records into the project.</summary>
        public async Task<string> ApplyIncomingAsync(IEnumerable<IncomingRecord> records)
        {
            ChangeTracker tracker = ChangeTracker.Current;
            List<IncomingRecord> list = records.ToList();
            if (tracker == null || list.Count == 0) return null;
            IsWorking = true;
            try
            {
                string error = null;
                bool ok = await Task.Run(() => tracker.ApplyIncoming(list, out error));
                if (!ok) return error ?? "The changes couldn't be applied.";
                SaveNotice.Show(error == null ? "Applied " + Plural.Of(list.Count, "change") : "Applied the rest");
                return error;
            }
            finally
            {
                IsWorking = false;
                await RefreshViewAsync();
            }
        }

        /// <summary>Keeps the project's version of records that also came in, writing it over the changes folder's.</summary>
        public async Task KeepProjectAsync(IEnumerable<IncomingRecord> records)
        {
            ChangeTracker tracker = ChangeTracker.Current;
            if (tracker == null) return;
            List<IncomingRecord> list = records.ToList();
            IsWorking = true;
            try { await Task.Run(() => tracker.KeepProject(list)); }
            finally
            {
                IsWorking = false;
                await RefreshViewAsync();
            }
        }

        public void StopTracking()
        {
            TrackingSetup.Unlink();
            Raise();
            _ = RefreshViewAsync();
        }
    }
}
