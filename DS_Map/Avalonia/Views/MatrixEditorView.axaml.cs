using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using DSPRE.Avalonia.ViewModels;

namespace DSPRE.Avalonia.Views
{
    public partial class MatrixEditorView : Window
    {
        private MatrixEditorViewModel VM => DataContext as MatrixEditorViewModel;
        private bool _setupDone;

        public MatrixEditorView()
        {
            InitializeComponent();
            MapGrid.ColorByValue = true;
            HeaderGrid.ColorByValue = true;
            HeightGrid.ColorByValue = false;

            MapGrid.Changed += (_, _) => VM?.MarkDirty();
            HeaderGrid.Changed += (_, _) => VM?.MarkDirty();
            HeightGrid.Changed += (_, _) => VM?.MarkDirty();
            MapGrid.CellSelected += (_, e) => SetCellInfo("Map", e);
            HeaderGrid.CellSelected += (_, e) => SetCellInfo("Header", e);
            HeightGrid.CellSelected += (_, e) => SetCellInfo("Height", e);

            Loaded += OnLoadedSetup;
        }

        public MatrixEditorView(MatrixEditorViewModel vm) : this() { DataContext = vm; EditorWindowChrome.Attach(this, vm); }

        private void SetCellInfo(string which, (int col, int row, int value) e)
        {
            if (VM == null) return;
            VM.CellInfo = $"{which} [{e.col}, {e.row}] = {e.value}";
            VM.SetSelectedCell(e.col, e.row);
        }

        private void SetSpawn_Click(object sender, RoutedEventArgs e)
        {
            if (VM == null || !VM.InBounds) return;
            var names = HeaderLists.GetHeaderListBoxNames();
            new SpawnEditorView(null, names, VM.SpawnHeaderNumber, VM.SelCol, VM.SelRow).Show();
        }

        private async void OnLoadedSetup(object sender, RoutedEventArgs e)
        {
            if (_setupDone || Design.IsDesignMode) return;
            var vm = VM;
            if (vm == null) return;
            _setupDone = true;
            vm.MatrixLoaded += OnMatrixLoaded;
            vm.PropertyChanged += OnVmChanged;
            await vm.SetupAsync(this);
        }

        private void OnMatrixLoaded(object sender, EventArgs e)
        {
            MapGrid.PaintValue = (int)VM.MapPaint;
            HeaderGrid.PaintValue = (int)VM.HeaderPaint;
            HeightGrid.PaintValue = (int)VM.HeightPaint;
            MapGrid.SetSource(VM.Width, VM.Height, VM.GetMap, VM.SetMap);
            if (VM.HasHeaders) HeaderGrid.SetSource(VM.Width, VM.Height, VM.GetHeader, VM.SetHeader);
            if (VM.HasHeights) HeightGrid.SetSource(VM.Width, VM.Height, VM.GetHeight, VM.SetHeight);
        }

        private void OnVmChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(MatrixEditorViewModel.MapPaint): MapGrid.PaintValue = (int)VM.MapPaint; break;
                case nameof(MatrixEditorViewModel.HeaderPaint): HeaderGrid.PaintValue = (int)VM.HeaderPaint; break;
                case nameof(MatrixEditorViewModel.HeightPaint): HeightGrid.PaintValue = (int)VM.HeightPaint; break;
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e) => VM?.Save();
        private void AddHeaders_Click(object sender, RoutedEventArgs e) => VM?.AddHeaderSection();
        private void AddHeights_Click(object sender, RoutedEventArgs e) => VM?.AddHeightsSection();
        private async void Import_Click(object sender, RoutedEventArgs e) => await Safe(VM?.ImportAsync());
        private async void Export_Click(object sender, RoutedEventArgs e) => await Safe(VM?.ExportAsync());

        private static async Task Safe(Task task)
        {
            if (task == null) return;
            try { await task; } catch { }
        }
    }
}
