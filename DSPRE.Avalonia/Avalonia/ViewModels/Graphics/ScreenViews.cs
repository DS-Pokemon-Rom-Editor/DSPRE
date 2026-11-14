using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using DSPRE.Avalonia.Data;
using DSPRE.ROMFiles;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.ViewModels.Graphics
{
    public abstract class ScreenView
    {
        protected ScreenView(string name) { Name = name; }
        public string Name { get; }
    }

    public sealed class ScreenPreviewView : ScreenView
    {
        public ScreenPreviewView(ScreenTabViewModel page) : base("Preview") { Page = page; }
        public ScreenTabViewModel Page { get; }
    }

    /// <summary>A screen's own editor, made the first time its view shows.</summary>
    public sealed class ScreenEditorView : ScreenView, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private readonly Func<Control> _make;
        private readonly Action _openApart, _prepare;
        private readonly string _busy;
        private readonly List<Action<object>> _then = new List<Action<object>>();
        private bool _making;

        public ScreenEditorView(string name, Func<Control> make, Action openApart = null, Action prepare = null, string busy = null,
                                Action forgotten = null) : base(name)
        {
            _make = make;
            _openApart = openApart;
            _prepare = prepare;
            _busy = busy;
            _forgotten = forgotten;
        }

        private readonly Action _forgotten;

        public Control Control { get; private set; }
        public object Model => Control?.DataContext;
        public bool OpensApart => _make == null;

        public void OpenApart() => _openApart?.Invoke();

        /// <summary>Runs on the editor's model now, or once it's made.</summary>
        internal void WhenMade(Action<object> action)
        {
            if (Control != null) action(Model);
            else _then.Add(action);
        }

        /// <summary>Drops an editor with nothing unsaved, so it's made fresh next time.</summary>
        internal void ForgetIfClean()
        {
            if (Control == null || (Model as DSPRE.Editors.IEditorWithUnsavedChanges)?.HasUnsavedChanges == true) return;
            (Model as IDetachable)?.Detach();
            Control = null;
            _forgotten?.Invoke();
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Control)));
        }

        internal async void Make(ScreensEditorViewModel owner)
        {
            if (Control != null || _make == null || _making) return;
            _making = true;
            try
            {
                if (_prepare != null) await BusyOverlay.RunAsync(_busy ?? $"Opening {Name}…", "", _prepare, () => Control = _make());
                else Control = _make();
            }
            catch (Exception ex) { AppLogger.Error("Screens editor: " + Name + ": " + ex.Message); }
            finally { _making = false; }
            if (Control == null) return;
            EditorTours.Attach(Control, Control.GetType().Name);
            owner.Watch(Model);
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Control)));
            foreach (Action<object> action in _then) action(Model);
            _then.Clear();
        }
    }
}
