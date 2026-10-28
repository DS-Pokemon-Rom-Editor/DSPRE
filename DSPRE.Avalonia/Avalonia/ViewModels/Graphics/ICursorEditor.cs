using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia.Media.Imaging;
using DSPRE.ROMFiles;

namespace DSPRE.Avalonia.ViewModels.Graphics
{
    /// <summary>The model behind a Cursor tab.</summary>
    public interface ICursorEditor : INotifyPropertyChanged, DSPRE.Editors.IEditorWithUnsavedChanges
    {
        string CursorSetLabel { get; }
        ObservableCollection<string> CursorSets { get; }
        int CursorSetIndex { get; set; }
        ObservableCollection<CursorStopRow> CursorStops { get; }
        CursorStopRow SelectedStop { get; set; }
        List<string> CursorChoices { get; }
        ObservableCollection<CursorBackRow> BackRows { get; }
        bool HasBackRows { get; }
        string BackRowsLabel { get; }
        string BackRowsTip { get; }
        Bitmap CursorPicture { get; }
        string CursorStatus { get; }
        event Action CursorChanged;

        string StopName(int stop);
        int TargetOf(int stop, CursorDirection direction);
        void SetDirection(int stop, CursorDirection direction, int target);
        void SetDirectionChoice(int stop, CursorDirection direction, int choice);
        /// <summary>Null, or why the game doesn't read this arrow.</summary>
        string FixedWhy(int stop, CursorDirection direction) => null;
        /// <summary>The picker entry the arrow shows.</summary>
        int ChoiceFor(int stop, CursorDirection direction);
        /// <summary>In board pixels, two per screen pixel.</summary>
        (double X, double Y) StopAt(int stop);
    }

    public sealed class CursorStopRow : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void Raise(string n) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

        private readonly ICursorEditor _owner;

        public CursorStopRow(ICursorEditor owner, int stop, string colour)
        {
            _owner = owner;
            Stop = stop;
            Colour = colour;
        }

        public int Stop { get; }
        public string Name => _owner.StopName(Stop);
        public string Colour { get; }
        public List<string> Choices => _owner.CursorChoices;

        private int Get(CursorDirection d) => _owner.ChoiceFor(Stop, d);
        private void Put(CursorDirection d, int choice) => _owner.SetDirectionChoice(Stop, d, choice);

        public int Up { get => Get(CursorDirection.Up); set => Put(CursorDirection.Up, value); }
        public int Down { get => Get(CursorDirection.Down); set => Put(CursorDirection.Down, value); }
        public int Left { get => Get(CursorDirection.Left); set => Put(CursorDirection.Left, value); }
        public int Right { get => Get(CursorDirection.Right); set => Put(CursorDirection.Right, value); }

        public string UpFixed => _owner.FixedWhy(Stop, CursorDirection.Up);
        public string DownFixed => _owner.FixedWhy(Stop, CursorDirection.Down);
        public string LeftFixed => _owner.FixedWhy(Stop, CursorDirection.Left);
        public string RightFixed => _owner.FixedWhy(Stop, CursorDirection.Right);
        public bool UpOpen => UpFixed == null;
        public bool DownOpen => DownFixed == null;
        public bool LeftOpen => LeftFixed == null;
        public bool RightOpen => RightFixed == null;

        /// <summary>The stop the arrow is drawn to, or -1 for none.</summary>
        public int Target(CursorDirection d) => _owner.TargetOf(Stop, d);
        public (double X, double Y) At => _owner.StopAt(Stop);

        public void Refresh()
        {
            foreach (string n in new[] { nameof(Up), nameof(Down), nameof(Left), nameof(Right) }) Raise(n);
        }
    }

}
