using Avalonia.Controls;
using Avalonia.Interactivity;
using DSPRE.Avalonia.ViewModels;

namespace DSPRE.Avalonia.Views.World
{
    /// <summary>The header detail form (fields). Shared by the Header editor and the Maps workspace.</summary>
    public partial class HeaderFieldsView : UserControl
    {
        private HeaderEditorViewModel VM => DataContext as HeaderEditorViewModel;

        public HeaderFieldsView()
        {
            InitializeComponent();
            OpenBattleSceneryButton.IsVisible = BetaEditors.Allows("BattleSceneBrowserView");
            DataContextChanged += (_, _) => Watch(VM);
            AttachedToVisualTree += (_, _) =>
            {
                DSPRE.GameCameraTable.Saved += OnPreviewSourceSaved;
                DSPRE.FlyTable.Saved += OnPreviewSourceSaved;
                AppEvents.MapSaved += OnMapSaved;
                ShowCamera();
            };
            DetachedFromVisualTree += (_, _) =>
            {
                DSPRE.GameCameraTable.Saved -= OnPreviewSourceSaved;
                DSPRE.FlyTable.Saved -= OnPreviewSourceSaved;
                AppEvents.MapSaved -= OnMapSaved;
            };
        }

        private HeaderEditorViewModel _watched;

        private void Watch(HeaderEditorViewModel vm)
        {
            if (_watched != null) _watched.PropertyChanged -= OnVmChanged;
            _watched = vm;
            if (_watched != null) _watched.PropertyChanged += OnVmChanged;
            ShowCamera();
        }

        private void OnVmChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName is null or nameof(HeaderEditorViewModel.CameraValue) or nameof(HeaderEditorViewModel.WeatherValue)
                or nameof(HeaderEditorViewModel.CurrentHeaderId))
                ShowCamera();
        }

        private void OnPreviewSourceSaved(object sender, System.EventArgs e) => global::Avalonia.Threading.Dispatcher.UIThread.Post(ShowCamera);

        private void OnMapSaved(object sender, int map) =>
            global::Avalonia.Threading.Dispatcher.UIThread.Post(() => { CameraPreviewBox.Reload(); ShowCamera(); });

        private void ShowCamera()
        {
            var vm = VM;
            // The Maps workspace is built at startup, before any project is open.
            if (vm == null || !IsAttachedToVisualTree() || !AvaloniaEditorLauncher.IsRomLoaded) return;
            var camera = DSPRE.ROMFiles.FieldCamera.Entry((int)vm.CameraValue, RomInfo.gameFamily);
            CameraPreviewBox.ShowWeather((int)vm.WeatherValue);
            // Framed where you arrive by Fly; a place without a fly spot borrows the starting town's.
            var spots = DSPRE.FlyTable.Spots();
            int own = spots.FindIndex(s => s.HeaderId == vm.CurrentHeaderId);
            if (own >= 0) CameraPreviewBox.Show(spots[own].HeaderId, camera, (spots[own].X, spots[own].Z));
            else if (spots.Count > 0) CameraPreviewBox.Show(spots[0].HeaderId, camera, (spots[0].X, spots[0].Z));
            else CameraPreviewBox.Show(vm.CurrentHeaderId, camera);
        }

        private bool IsAttachedToVisualTree() => TopLevel.GetTopLevel(this) != null;

        private void OpenMatrix_Click(object sender, RoutedEventArgs e) => VM?.OpenMatrix();
        private void OpenAreaData_Click(object sender, RoutedEventArgs e) => VM?.OpenAreaData();
        private void OpenScripts_Click(object sender, RoutedEventArgs e) => VM?.OpenScripts();
        private void OpenLevelScripts_Click(object sender, RoutedEventArgs e) => VM?.OpenLevelScripts();
        private void OpenEvents_Click(object sender, RoutedEventArgs e) => VM?.OpenEvents();
        private void OpenTexts_Click(object sender, RoutedEventArgs e) => VM?.OpenTexts();
        private void OpenEncounters_Click(object sender, RoutedEventArgs e) => VM?.OpenEncounters();
        private void OpenBattleScenery_Click(object sender, RoutedEventArgs e) => VM?.OpenBattleScenery();
    }
}
