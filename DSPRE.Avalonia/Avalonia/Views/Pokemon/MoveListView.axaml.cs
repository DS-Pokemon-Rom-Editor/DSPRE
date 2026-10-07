using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Input;
using DSPRE.Avalonia.ViewModels;
using DSPRE.ROMFiles;

namespace DSPRE.Avalonia.Views.Pokemon
{
    /// <summary>The moves on one ability list, as the Move Data editor holds them now; a double-click opens the move there.</summary>
    public partial class MoveListView : Window
    {
        private readonly MoveDataEditorViewModel _vm;
        private readonly MoveCategoryTable.Kind _kind;

        private sealed class Row
        {
            public int Id;
            public string Text;
            public override string ToString() => Text;
        }

        public MoveListView() { InitializeComponent(); }

        public MoveListView(MoveDataEditorViewModel vm, MoveCategoryTable.Kind kind) : this()
        {
            _vm = vm;
            _kind = kind;
            Title = kind == MoveCategoryTable.Kind.Punching ? "Punching moves" : "Sound moves";
            Fill();
            _vm.ListsChanged += Fill;
            Closed += (_, _) => _vm.ListsChanged -= Fill;
        }

        public MoveCategoryTable.Kind Kind => _kind;

        private void Fill()
        {
            List<Row> rows = new List<Row>();
            foreach ((int id, string name) in _vm.ListedMoves(_kind)) rows.Add(new Row { Id = id, Text = $"{id:D3} - {name}" });
            Moves.ItemsSource = rows;
            CountText.Text = rows.Count == 1 ? "1 move" : $"{rows.Count} moves";
        }

        // The editor's own move picker decides whether unsaved edits allow the switch.
        private void Open()
        {
            if (Moves.SelectedItem is Row row) _vm.SelectedMoveIndex = row.Id;
        }

        private void Moves_DoubleTapped(object sender, TappedEventArgs e) => Open();

        private void Moves_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) { Open(); e.Handled = true; }
        }
    }
}
