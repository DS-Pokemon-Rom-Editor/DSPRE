using System;
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

        public AudioEditorView() : this(new AudioEditorViewModel()) { }

        public AudioEditorView(AudioEditorViewModel vm)
        {
            InitializeComponent();
            DataContext = vm;
            vm.PropertyChanged += OnViewModelChanged;
            _playhead.Tick += MovePlayhead;
            // Arriving from the Pokemon editor, the cry is already picked before this window exists, so
            // nothing has told it to draw yet.
            Opened += (_, _) => { if (vm.Selected != null) _ = DrawSelected(); };
            Closed += (_, _) => { _playhead.Stop(); AudioOutput.Current.Stop(); vm.PropertyChanged -= OnViewModelChanged; };
        }

        // ── keeping the picture up to date ──────────────────────────────────────────

        private void OnViewModelChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(AudioEditorViewModel.Selected)) _ = DrawSelected();
        }

        private async Task DrawSelected()
        {
            _rendering?.Cancel();
            var mine = _rendering = new CancellationTokenSource();
            var token = mine.Token;

            var vm = ViewModel;
            if (vm?.Selected == null)
            {
                _pcm = null; Wave.Show(null, Rate);
                Notes.SayWhyEmpty(null); Notes.SetNotes(null, 0); ShowTime(0);
                return;
            }

            short[] pcm;
            try
            {
                pcm = await Task.Run(() => { try { return vm.RenderSelected(); } catch { return null; } }, token);
            }
            catch (OperationCanceledException)
            {
                return;   // somebody moved on to another row before this one finished
            }
            if (token.IsCancellationRequested) return;

            _pcm = pcm;
            Wave.Show(pcm, Rate);

            // The notes come from the sequence itself rather than from the sound, so a cry has none.
            var notes = await Task.Run(() => { try { return vm.ReadSelectedNotes(); } catch { return null; } }, token);
            if (token.IsCancellationRequested) return;
            // A cry and a sound have no notes of their own: what is heard is the sound itself. Say that
            // rather than leaving a panel that reads as though nothing was picked.
            Notes.SayWhyEmpty(vm.Selected == null
                ? null
                : vm.Selected.IsCry
                    ? "A cry is one sound rather than notes, so there is nothing to show here."
                    : vm.Selected.IsSample
                        ? "This is one of the sounds the game plays rather than notes, so there is nothing "
                          + "to show here. The tunes that play it are on the Music tab."
                        : null);
            Notes.SetNotes(notes, Wave.Seconds);

            ShowTime(0);
        }

        private void ShowTime(double at)
        {
            string Fmt(double s) => $"{(int)(s / 60)}:{s % 60:00.0}";
            TimeText.Text = $"{Fmt(at)} / {Fmt(Wave.Seconds)}";
        }

        // ── playing ─────────────────────────────────────────────────────────────────

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

            var part = _pcm;
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

        // ── taking it out and putting it back ───────────────────────────────────────

        /// <summary>Empties the search box, which is what the button beside it is for.</summary>
        private void ClearSearch_Click(object sender, RoutedEventArgs e)
        {
            SearchBox.Text = "";
            SearchBox.Focus();
        }

        private async void ExportSoundFont_Click(object sender, RoutedEventArgs e)
        {
            var vm = ViewModel;
            if (vm == null) return;

            var sf2 = vm.BuildSoundFont(out string whynot, out string note);
            if (sf2 == null)
            {
                vm.Status = whynot;
                await DialogHelper.ShowInfo(whynot, "Export SoundFont");
                return;
            }

            string path = await DialogHelper.SaveFile(this, "Save these instruments as a SoundFont",
                new[] { new FilePickerFileType("SoundFont") { Patterns = new[] { "*.sf2" } } },
                vm.SuggestedSoundFontName());
            if (path == null) return;

            try
            {
                System.IO.File.WriteAllBytes(path, sf2);
                vm.Status = $"Saved to {path}. {note}";
            }
            catch (System.Exception ex)
            {
                AppLogger.Error("ExportSoundFont failed: " + ex);
                await DialogHelper.ShowError("That SoundFont could not be written: " + ex.Message,
                                             "Export SoundFont", this);
            }
        }

        private async void ExportMidi_Click(object sender, RoutedEventArgs e)
        {
            var vm = ViewModel;
            if (vm == null) return;

            var midi = vm.BuildMidi(out string whynot);
            if (midi == null)
            {
                vm.Status = whynot;
                await DialogHelper.ShowInfo(whynot, "Export MIDI");
                return;
            }

            string path = await DialogHelper.SaveFile(this, "Save these notes as a MIDI",
                new[] { new FilePickerFileType("MIDI file") { Patterns = new[] { "*.mid" } } },
                vm.SuggestedMidiName());
            if (path == null) return;

            try
            {
                System.IO.File.WriteAllBytes(path, midi);
                vm.Status = $"Saved to {path}. The notes and their timing are exact; the instruments are "
                          + "whatever the program you open it in has under those numbers.";
            }
            catch (Exception ex)
            {
                AppLogger.Error("Audio MIDI export failed: " + ex.Message);
                vm.Status = "That file could not be written.";
                await DialogHelper.ShowInfo("That file could not be written.", "Export MIDI");
            }
        }

        private async void Export_Click(object sender, RoutedEventArgs e)
        {
            var vm = ViewModel;
            if (vm?.Selected == null) return;

            string path = await DialogHelper.SaveFile(this, "Save this sound",
                new[] { new FilePickerFileType("WAV sound") { Patterns = new[] { "*.wav" } } },
                vm.SuggestedFileName());
            if (path == null) return;

            try
            {
                // A cry is saved as the sample it is made from, so it can be worked on and put back.
                // Everything else has no sample of its own, so what is saved is how it sounds when played.
                if (vm.Selected.IsCry)
                {
                    if (!SoundArchive.ExportCry(vm.Selected.Number, path))
                        await DialogHelper.ShowInfo("This ROM has no cry for that Pokémon.", "Save sound");
                    return;
                }

                // A sound is a sample too, so it is saved as it sits in the ROM rather than as it sounds
                // once a tune has put volume and pitch on it.
                if (vm.Selected.IsSample)
                {
                    if (!SoundArchive.ExportSample(vm.Selected.WaveArc, vm.Selected.SampleIndex, path))
                        await DialogHelper.ShowInfo("There is nothing in that one to save.", "Save sound");
                    return;
                }

                if (_pcm == null || _pcm.Length == 0)
                {
                    await DialogHelper.ShowInfo("Nothing came out of that one.", "Save sound");
                    return;
                }
                SseqPlayer.WriteWav(path, _pcm, Rate);
            }
            catch (Exception ex) { await DialogHelper.ShowError("It could not be saved:\n" + ex.Message, "Save sound"); }
        }

        private async void Import_Click(object sender, RoutedEventArgs e)
        {
            var vm = ViewModel;
            if (vm?.Selected == null || !vm.CanImport) return;

            bool sample = vm.Selected.IsSample;
            string title = sample ? "Put in a sound" : "Put in a cry";

            // Replacing a shared sound changes everything that plays it, so say so before it happens
            // rather than leaving somebody to find out by playing a tune that now sounds wrong.
            if (sample && !await DialogHelper.AskYesNo(
                    "This sound may be used by more than one tune or sound effect, and all of them will "
                    + "play the new one.\n\nPut a WAV in over it?", title, this))
                return;

            string path = await DialogHelper.OpenFile(this,
                sample ? "Choose a sound to put in" : "Choose a cry to put in",
                new[] { new FilePickerFileType("WAV sound") { Patterns = new[] { "*.wav" } } });
            if (path == null) return;

            try
            {
                bool done = sample
                    ? SoundArchive.ImportSample(vm.Selected.WaveArc, vm.Selected.SampleIndex, path, out string problem)
                    : SoundArchive.ImportCry(vm.Selected.Number, path, out problem);

                if (done)
                {
                    await DrawSelected();   // the picture has to show what is in the ROM now, not what was
                    await DialogHelper.ShowInfo(
                        (sample ? "The sound has been put in." : "The cry has been put in.")
                        + " Press Play to hear it.", title);
                }
                else
                {
                    await DialogHelper.ShowInfo(problem ?? "That could not be put in.", title);
                }
            }
            catch (Exception ex) { await DialogHelper.ShowError("It could not be put in:\n" + ex.Message, title); }
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();
    }
}
