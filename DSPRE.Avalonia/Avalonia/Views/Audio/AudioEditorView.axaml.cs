using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using DSPRE.Avalonia.Data;
using DSPRE.Avalonia.ViewModels;

namespace DSPRE.Avalonia.Views.Audio
{
    public partial class AudioEditorView : Window
    {
        private AudioEditorViewModel ViewModel => (AudioEditorViewModel)DataContext;

        // What is currently drawn, so playing and saving use the same sound the picture was made from.
        private short[] _pcm;
        private const int Rate = 32000;

        // Rendering a tune takes long enough to be felt, and somebody arrowing down a list starts a new one
        // every keystroke, so each render cancels the one before it.
        private CancellationTokenSource _rendering;

        private readonly DispatcherTimer _playhead = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) };
        private DateTime _startedAt;
        private double _startedFrom;

        // A key pressed while the new row is still rendering plays it once the render lands.
        private bool _drawing, _playWhenDrawn;

        public AudioEditorView() : this(new AudioEditorViewModel()) { }

        public AudioEditorView(AudioEditorViewModel vm)
        {
            InitializeComponent();
            DataContext = vm;
            EditorWindowChrome.Attach(this, vm);
            vm.PropertyChanged += OnViewModelChanged;
            _playhead.Tick += MovePlayhead;
            AddHandler(KeyDownEvent, OnPlayKeys, RoutingStrategies.Tunnel);
            // Arriving from the Pokemon editor, the cry is already picked before this window exists, so
            // nothing has told it to draw yet.
            Opened += (_, _) => { if (vm.Selected != null) _ = DrawSelectedAsync(); };
            Closed += (_, _) => { _playhead.Stop(); AudioOutput.Current.Stop(); vm.PropertyChanged -= OnViewModelChanged; };
        }


        private void OnViewModelChanged(object sender, PropertyChangedEventArgs e)
        {
            // Staging, saving and discarding change what the picked row sounds like.
            if (e.PropertyName == nameof(AudioEditorViewModel.Selected)
             || e.PropertyName == nameof(AudioEditorViewModel.HasUnsavedChanges)) _ = DrawSelectedAsync();
        }

        private async Task DrawSelectedAsync()
        {
            _rendering?.Cancel();
            CancellationTokenSource mine = _rendering = new CancellationTokenSource();
            CancellationToken token = mine.Token;

            AudioEditorViewModel vm = ViewModel;
            _drawing = vm?.Selected != null;
            if (vm?.Selected == null)
            {
                _playWhenDrawn = false;
                _pcm = null; Wave.Show(null, Rate);
                Notes.SetNotes(null, 0); ShowTime(0);
                return;
            }

            short[] pcm;
            try
            {
                pcm = await Task.Run(() => { try { return vm.RenderSelected(); } catch (Exception ex) { AppLogger.WarnOnce("AudioEditorView.DrawSelectedAsync", ex); return null; } }, token);
            }
            catch (OperationCanceledException)
            {
                return;   // somebody moved on to another row before this one finished
            }
            if (token.IsCancellationRequested) return;

            _pcm = pcm;
            Wave.Show(pcm, Rate);
            _drawing = false;
            if (_playWhenDrawn) { _playWhenDrawn = false; PlayFromStart(); }

            // Notes come from the sequence, so a cry has none.
            IReadOnlyList<SseqPlayer.Note> notes = await Task.Run(() => { try { return vm.ReadSelectedNotes(); } catch (Exception ex) { AppLogger.WarnOnce("AudioEditorView.DrawSelectedAsync", ex); return null; } }, token);
            if (token.IsCancellationRequested) return;
            Notes.SetNotes(notes, Wave.Seconds);

            ShowTime(0);
        }

        private void ShowTime(double at)
        {
            string Fmt(double s) => $"{(int)(s / 60)}:{s % 60:00.0}";
            TimeText.Text = $"{Fmt(at)} / {Fmt(Wave.Seconds)}";
        }


        private void List_DoubleTapped(object sender, TappedEventArgs e) => Play_Click(sender, null);

        private void Wave_PointerPressed(object sender, PointerPressedEventArgs e)
        {
            double w = Wave.Bounds.Width;
            if (w <= 0 || Wave.Seconds <= 0) return;
            Wave.MarkAt(e.GetPosition(Wave).X / w * Wave.Seconds);
            Notes.Playhead = Wave.MarkSeconds;
            ShowTime(Wave.MarkSeconds);
        }

        private void Notes_PointerPressed(object sender, PointerPressedEventArgs e)
        {
            if (Wave.Seconds <= 0) return;
            Wave.MarkAt(Notes.SecondsAt(e.GetPosition(Notes).X));
            Notes.Playhead = Wave.MarkSeconds;
            ShowTime(Wave.MarkSeconds);
        }

        private void Play_Click(object sender, RoutedEventArgs e)
        {
            if (_pcm == null || _pcm.Length == 0) return;
            StopButton.IsEnabled = true;

            int from = (int)(Wave.MarkSeconds * Rate) * 2;
            if (from < 0 || from >= _pcm.Length) from = 0;

            short[] part = _pcm;
            if (from > 0)
            {
                part = new short[_pcm.Length - from];
                Array.Copy(_pcm, from, part, 0, part.Length);
            }

            AudioOutput.Current.Stop();
            AudioOutput.Current.Play(part, Rate);
            _startedFrom = Wave.MarkSeconds;
            _startedAt = DateTime.UtcNow;
            _playhead.Start();
        }

        // Space plays or stops, Enter plays from the start and Up/Down move through the open list, anywhere
        // but a text box or dropdown. A focused list moves itself.
        private void OnPlayKeys(object sender, KeyEventArgs e)
        {
            if (e.KeyModifiers != KeyModifiers.None) return;
            IInputElement focused = FocusManager?.GetFocusedElement();
            if (focused is TextBox or ComboBox) return;
            if (e.Key is Key.Up or Key.Down)
            {
                if (focused is ListBoxItem or ListBox or TreeViewItem or TreeView) return;
                if (OpenList() is ListBox list && list.ItemCount > 0)
                {
                    list.SelectedIndex = Math.Clamp(list.SelectedIndex + (e.Key == Key.Down ? 1 : -1), 0, list.ItemCount - 1);
                    list.ScrollIntoView(list.SelectedIndex);
                    e.Handled = true;
                }
                return;
            }
            if (e.Key != Key.Space && e.Key != Key.Enter) return;
            e.Handled = true;
            if (e.Key == Key.Space && StopButton.IsEnabled) { Stop_Click(null, null); return; }
            if (_drawing) { _playWhenDrawn = true; return; }
            if (e.Key == Key.Enter) PlayFromStart();
            else Play_Click(null, null);
        }

        private ListBox OpenList() => Tabs.SelectedContent as ListBox;

        private void PlayFromStart()
        {
            Wave.MarkAt(0);
            Notes.Playhead = 0;
            Play_Click(null, null);
        }

        private void Stop_Click(object sender, RoutedEventArgs e)
        {
            StopButton.IsEnabled = false;
            AudioOutput.Current.Stop();
            _playhead.Stop();
            Wave.ShowPlayedTo(-1);
            Notes.Playhead = Wave.MarkSeconds;
            ShowTime(Wave.MarkSeconds);
        }

        private void MovePlayhead(object sender, EventArgs e)
        {
            double at = _startedFrom + (DateTime.UtcNow - _startedAt).TotalSeconds;
            if (at >= Wave.Seconds)
            {
                if (LoopBox.IsChecked == true) { Play_Click(null, null); return; }
                Stop_Click(null, null);
                return;
            }
            Wave.ShowPlayedTo(at);
            Notes.Playhead = at;
            ShowTime(at);
        }


        private void ClearSearch_Click(object sender, RoutedEventArgs e)
        {
            SearchBox.Text = "";
            SearchBox.Focus();
        }

        private async void ExportSoundFont_Click(object sender, RoutedEventArgs e)
        {
            AudioEditorViewModel vm = ViewModel;
            if (vm == null) return;

            byte[] sf2 = vm.BuildSoundFont(out string whynot, out string note);
            if (sf2 == null)
            {
                vm.StatusText = whynot;
                await DialogHelper.ShowInfoAsync(whynot, "Export SoundFont");
                return;
            }

            string path = await DialogHelper.SaveFileAsync(this, "Save these instruments as a SoundFont",
                new[] { new FilePickerFileType("SoundFont") { Patterns = new[] { "*.sf2" } } },
                vm.SuggestedSoundFontName());
            if (path == null) return;

            try
            {
                PlainFiles.WriteBytes(path, sf2);
                SaveNotice.Exported(path);
                vm.StatusText = note ?? "";
            }
            catch (System.Exception ex)
            {
                AppLogger.Error("ExportSoundFont failed: " + ex);
                await DialogHelper.ShowErrorAsync("That SoundFont couldn't be written: " + ex.Message,
                                             "Audio editor", this);
            }
        }

        private async void ExportMidi_Click(object sender, RoutedEventArgs e)
        {
            AudioEditorViewModel vm = ViewModel;
            if (vm == null) return;

            byte[] midi = vm.BuildMidi(out string whynot);
            if (midi == null)
            {
                vm.StatusText = whynot;
                await DialogHelper.ShowInfoAsync(whynot, "Export MIDI");
                return;
            }

            string path = await DialogHelper.SaveFileAsync(this, "Save these notes as a MIDI",
                new[] { new FilePickerFileType("MIDI file") { Patterns = new[] { "*.mid" } } },
                vm.SuggestedMidiName());
            if (path == null) return;

            try
            {
                PlainFiles.WriteBytes(path, midi);
                SaveNotice.Exported(path);
            }
            catch (Exception ex)
            {
                AppLogger.Error("Audio MIDI export failed: " + ex.Message);
                vm.StatusText = "That file couldn't be written.";
                await DialogHelper.ShowErrorAsync("That file couldn't be written:\n" + ex.Message, "Audio editor");
            }
        }

        private async void Export_Click(object sender, RoutedEventArgs e)
        {
            AudioEditorViewModel vm = ViewModel;
            if (vm?.Selected == null) return;

            string path = await DialogHelper.SaveFileAsync(this, "Save this sound",
                new[] { new FilePickerFileType("WAV sound") { Patterns = new[] { "*.wav" } } },
                vm.SuggestedFileName());
            if (path == null) return;

            try
            {
                // A cry is saved as its sample so it can go back in; other sounds are saved as they play.
                if (vm.Selected.IsCry)
                {
                    if (!vm.ExportSelectedSample(path))
                        await DialogHelper.ShowInfoAsync("This ROM has no cry for that Pokémon.", "Save sound");
                    else SaveNotice.Exported(path);
                    return;
                }

                // A sound is saved as stored, without a tune's volume and pitch.
                if (vm.Selected.IsSample)
                {
                    if (!vm.ExportSelectedSample(path))
                        await DialogHelper.ShowInfoAsync("This sound is empty, so there is nothing to save.", "Save sound");
                    else SaveNotice.Exported(path);
                    return;
                }

                if (_pcm == null || _pcm.Length == 0)
                {
                    await DialogHelper.ShowInfoAsync("This one plays silence, so there is nothing to save.", "Save sound");
                    return;
                }
                SseqPlayer.WriteWav(path, _pcm, Rate);
                SaveNotice.Exported(path);
            }
            catch (Exception ex) { await DialogHelper.ShowErrorAsync("It couldn't be saved:\n" + ex.Message, "Audio editor"); }
        }

        private async void Import_Click(object sender, RoutedEventArgs e)
        {
            AudioEditorViewModel vm = ViewModel;
            if (vm?.Selected == null || !vm.CanImport) return;

            bool sample = vm.Selected.IsSample;
            string title = sample ? "Import a sound" : "Import a cry";

            // A shared sound changes every tune that plays it, so warn first.
            if (sample && !await DialogHelper.AskYesNoAsync(
                    "This sound may be used by more than one tune or sound effect, and all of them will "
                    + "play the new one.\n\nReplace it with a WAV?", title, this))
                return;

            string path = await DialogHelper.OpenFileAsync(this,
                sample ? "Choose a sound to import" : "Choose a cry to import",
                new[] { new FilePickerFileType("WAV sound") { Patterns = new[] { "*.wav" } } });
            if (path == null) return;

            try
            {
                // Checked now so a bad file is refused at once, but nothing is written until Save.
                AudioItem item = vm.Selected;
                string why;
                if (!sample && SoundArchive.CriesGoToCheckout)
                {
                    SoundArchive.PendingCry cry = SoundArchive.PrepareCheckoutCry(item.Number, path, out why);
                    if (cry != null) { vm.StageCry(item, cry); SaveNotice.Imported(item.Name, path); return; }
                }
                else
                {
                    SoundArchive.PendingSample held = sample
                        ? SoundArchive.PrepareSample(item.WaveArc, item.SampleIndex, path, out why)
                        : SoundArchive.PrepareCry(item.Number, path, out why);
                    if (held != null) { vm.StageSample(item, held); SaveNotice.Imported(item.Name, path); return; }
                }
                await DialogHelper.ShowInfoAsync(why ?? "That couldn't be imported.", title);
            }
            catch (Exception ex) { await DialogHelper.ShowErrorAsync("It couldn't be imported:\n" + ex.Message, "Audio editor"); }
        }

        private async void Save_Click(object sender, RoutedEventArgs e)
        {
            string problem = ViewModel?.Save();
            if (problem != null) await DialogHelper.ShowErrorAsync(problem, "Save", this);
        }

        private async void Discard_Click(object sender, RoutedEventArgs e)
        {
            if (await DialogHelper.ConfirmDiscardAsync(this)) ViewModel?.DiscardChanges();
        }

    }
}
