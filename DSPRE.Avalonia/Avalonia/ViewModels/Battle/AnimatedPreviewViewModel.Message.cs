using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using DSPRE.Avalonia.Gl;
using DSPRE.ROMFiles;

namespace DSPRE.Avalonia.ViewModels.Battle
{
    public sealed partial class AnimatedPreviewViewModel
    {
        private FieldTextPrinter _printer;
        private bool _boxOpen;
        private string _boxText;
        private List<FieldMessageFrame> _frames = new List<FieldMessageFrame>();
        private bool _aPressed, _aHeld;

        public Func<string, int> MeasureText { get; set; } = t => (t ?? "").Length * 6;

        public ObservableCollection<string> TextSpeedNames { get; } =
            new ObservableCollection<string> { "Slow text", "Mid text", "Fast text" };

        private static readonly FieldTextSpeed[] TextSpeeds = { FieldTextSpeed.Slow, FieldTextSpeed.Mid, FieldTextSpeed.Fast };

        private int _textSpeedIndex = 1;

        /// <summary>The Options menu's text speed. New games start on mid.</summary>
        public int TextSpeedIndex
        {
            get => _textSpeedIndex;
            set { if (value >= 0 && value < TextSpeeds.Length) Set(ref _textSpeedIndex, value); }
        }

        public FieldTextSpeed TextSpeed => TextSpeeds[_textSpeedIndex];

        public string MessageText => !_boxOpen ? null : _printer?.Text ?? _boxText ?? "";

        public bool MessageVisible => _boxOpen;

        public bool MessageHasMore => _printer?.WaitingForPress == true;

        /// <summary>Arrow bob, in DS pixels.</summary>
        public int MessageArrowOffset => _printer?.ArrowOffset ?? 0;

        public int MessageScrollPixels => _printer?.ScrollPixels ?? 0;

        public string MessageWaitText
        {
            get
            {
                if (!ScriptRunning) return _boxOpen ? "Any key takes the box down" : "";
                if (MessageHasMore) return "A turns the page";
                if (_printer != null && !_printer.Finished) return "A hurries the text along";
                if (_runner.Waiting == FieldScriptRunner.WaitKind.Button)
                    return _runner.ButtonTakesPad ? "A, B or a direction carries on" : "A or B carries on";
                return "";
            }
        }

        public string MessageWarning
        {
            get
            {
                if (!_boxOpen || _frames.Count == 0) return null;
                bool wide = _frames.Any(f => f.TooWide), tall = _frames.Any(f => f.TooManyLines);
                if (wide && tall) return "This runs past the edge and past the bottom of the box.";
                if (wide) return "A line here runs past the edge of the box.";
                if (tall) return "There are more lines here than the box can show.";
                return null;
            }
        }

        public bool HasMessageWarning => MessageWarning != null;

        /// <summary>The twenty borders the games let the player pick between in Options.</summary>
        public ObservableCollection<string> BorderNames { get; } = new ObservableCollection<string>();

        private int _borderIndex;

        public int BorderIndex
        {
            get => _borderIndex;
            set { if (Set(ref _borderIndex, value)) BorderChanged?.Invoke(this, EventArgs.Empty); }
        }

        public event EventHandler BorderChanged;

        public string MessageFontNote { get; set; }
        public bool HasMessageFontNote => !string.IsNullOrEmpty(MessageFontNote);

        // The script's own words, so a changed placeholder can be put back in.
        private string _spoken;

        private void ClearMessage()
        {
            _printer = null;
            _boxOpen = false;
            _boxText = null;
            _spoken = null;
            _frames = new List<FieldMessageFrame>();
            RaiseMessageChanged();
        }

        private bool StartMessage(ScriptEffect effect)
        {
            if (string.IsNullOrEmpty(effect?.Text)) return false;
            _spoken = effect.Text;
            _frames = FieldMessageScript.Frames(ExpandVars(effect.Text), MeasureText);
            _printer = new FieldTextPrinter(_frames, TextSpeed, skippable: effect.B != 1, instant: effect.A == 1)
            {
                PageTurned = () => { if (_playSounds) PlaySound?.Invoke(ScriptEffectKind.SoundEffect, MenuSound); },
            };
            _boxOpen = true;
            if (_printer.Finished) { _boxText = _printer.Text; _printer = null; }
            RaiseMessageChanged();
            return true;
        }

        public void ShowMessage(string text) => StartMessage(new ScriptEffect(ScriptEffectKind.Message) { Text = text });

        private void LayOutMessage()
        {
            if (_spoken == null || _printer != null) return;
            List<FieldMessageFrame> frames = FieldMessageScript.Frames(ExpandVars(_spoken), MeasureText);
            if (frames.Count == 0) return;
            _frames = frames;
            _boxText = frames[frames.Count - 1].Text;
            RaiseMessageChanged();
        }

        /// <summary>The words quoted inside the sentence the walker writes for a message step.</summary>
        public static string Spoken(string stepText)
        {
            if (string.IsNullOrEmpty(stepText)) return "";
            foreach ((char open, char close) in new[] { ('“', '”'), ('"', '"') })
            {
                int a = stepText.IndexOf(open);
                int b = stepText.LastIndexOf(close);
                if (a >= 0 && b > a) return stepText.Substring(a + 1, b - a - 1);
            }
            return stepText;
        }

        /// <summary>A or B went down. Read on the next field frame, as the games read the keys.</summary>
        public void PressA()
        {
            _aPressed = true;
            _aHeld = true;
        }

        public void ReleaseA() => _aHeld = false;

        private void RaiseMessageChanged()
        {
            OnPropertyChanged(nameof(MessageText));
            OnPropertyChanged(nameof(MessageVisible));
            OnPropertyChanged(nameof(MessageHasMore));
            OnPropertyChanged(nameof(MessageArrowOffset));
            OnPropertyChanged(nameof(MessageScrollPixels));
            OnPropertyChanged(nameof(MessageWaitText));
            OnPropertyChanged(nameof(MessageWarning));
            OnPropertyChanged(nameof(HasMessageWarning));
            OnPropertyChanged(nameof(MessageFontNote));
            OnPropertyChanged(nameof(HasMessageFontNote));
        }

        private FieldTimeZone _timeOfDay = FieldTimeOfDay.Now;
        public FieldTimeZone TimeOfDay
        {
            get => _timeOfDay;
            set
            {
                if (!Set(ref _timeOfDay, value)) return;
                OnPropertyChanged(nameof(TimeOfDayName));
                Reload();
            }
        }

        public string TimeOfDayName => $"{FieldTimeOfDay.Name(_timeOfDay)} ({FieldTimeOfDay.Hours(_timeOfDay)})";

        public ObservableCollection<string> TimesOfDay { get; } = new ObservableCollection<string>(
            new[] { FieldTimeZone.Morning, FieldTimeZone.Noon, FieldTimeZone.Evening, FieldTimeZone.Night, FieldTimeZone.Midnight }
                .Select(z => $"{FieldTimeOfDay.Name(z)}  ·  {FieldTimeOfDay.Hours(z)}"));

        public int TimeOfDayIndex
        {
            get => (int)_timeOfDay;
            set { if (value >= 0 && value <= 4) TimeOfDay = (FieldTimeZone)value; }
        }
    }
}
