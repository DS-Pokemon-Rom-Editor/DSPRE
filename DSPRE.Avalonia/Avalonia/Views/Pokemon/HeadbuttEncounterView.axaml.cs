using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using DSPRE.Avalonia.Gl;
using DSPRE.Avalonia.ViewModels;

namespace DSPRE.Avalonia.Views.Pokemon
{
    public partial class HeadbuttEncounterView : UserControl
    {
        private HeadbuttEncounterViewModel VM => DataContext as HeadbuttEncounterViewModel;
        private bool _setupDone;
        private Gl3DPointerNavigation _nav;

        public HeadbuttEncounterView()
        {
            InitializeComponent();

            // Left-drag pans, right-drag orbits, wheel zooms. In edit mode a left-press grabs a gizmo
            // axis (to drag the tree) or picks the nearest tree. See Gl3DPointerNavigation.
            _nav = new Gl3DPointerNavigation(GlHost, GlView)
            {
                IsEditModeActive = () => VM?.EditMode3D == true,
                BeginGizmoDrag = () => VM?.BeginGizmoDrag(),
                Pick = PickTree,
                NudgeAxis = (axis, normDelta) =>
                {
                    if (VM == null) return;
                    float scale = VM.ModelScale; if (scale <= 0) scale = 1f;
                    VM.NudgeSelectedTreeRaw(axis, normDelta / scale);
                },
            };

            // Arrow keys nudge the selected tree, but only while the 3D viewport itself has
            // keyboard focus (Gl3DPointerNavigation focuses it on click); otherwise they'd steal
            // input from a focused dropdown/spinner in the side panel.
            GlHost.KeyDown += (s, e) =>
            {
                if (VM != null && VM.EditMode3D && VM.HasSelectedTree)
                {
                    switch (e.Key)
                    {
                        case Key.Left:  VM.NudgeSelectedTreeTiles(-1, 0); e.Handled = true; break;
                        case Key.Right: VM.NudgeSelectedTreeTiles(1, 0);  e.Handled = true; break;
                        case Key.Up:    VM.NudgeSelectedTreeTiles(0, -1); e.Handled = true; break;
                        case Key.Down:  VM.NudgeSelectedTreeTiles(0, 1);  e.Handled = true; break;
                    }
                }
            };

            Loaded += OnLoadedSetup;
            GlHost.AddHandler(PointerMovedEvent, Host_Moved, RoutingStrategies.Bubble, handledEventsToo: true);
            GlHost.PointerExited += (_, _) => HideCard();
        }

        private (bool Special, int Group)? _hovered;

        private void HideCard()
        {
            _hovered = null;
            TreeCard.IsOpen = false;
        }

        private void Host_Moved(object sender, PointerEventArgs e)
        {
            if (VM == null) return;
            PointerPoint pt = e.GetCurrentPoint(GlView);
            PointerPointProperties pr = pt.Properties;
            if (pr.IsLeftButtonPressed || pr.IsRightButtonPressed || pr.IsMiddleButtonPressed) { HideCard(); return; }

            (bool Special, int Group)? hit = null;
            if (GlView.ScreenToRay((float)pt.Position.X, (float)pt.Position.Y,
                                   out float ox, out float oy, out float oz, out float dx, out float dy, out float dz))
                hit = VM.TreeAlong(ox, oy, oz, dx, dy, dz);
            if (hit == _hovered) return;
            _hovered = hit;
            HeadbuttEncounterViewModel.HoverCard card = hit is { } h ? VM.CardFor(h.Special, h.Group) : null;
            TreeCard.IsOpen = false;
            if (card == null) return;
            FillCard(card);
            TreeCard.HorizontalOffset = 16;
            TreeCard.VerticalOffset = 16;
            TreeCard.IsOpen = true;
        }

        private void FillCard(HeadbuttEncounterViewModel.HoverCard card)
        {
            StackPanel body = TreeCardBody;
            body.Children.Clear();
            body.Children.Add(new TextBlock { Text = card.Title, FontWeight = global::Avalonia.Media.FontWeight.SemiBold });
            if (!string.IsNullOrEmpty(card.Rule))
                body.Children.Add(new TextBlock { Text = card.Rule, FontSize = 11, Opacity = 0.75 });
            StackPanel tables = new StackPanel { Orientation = global::Avalonia.Layout.Orientation.Horizontal, Spacing = 16 };
            body.Children.Add(tables);
            foreach (HeadbuttEncounterViewModel.HoverTable table in card.Tables)
            {
                StackPanel column = new StackPanel { Spacing = 2 };
                tables.Children.Add(column);
                column.Children.Add(new TextBlock { Text = table.Header, FontSize = 12, Margin = new Thickness(0, 4, 0, 0), Opacity = 0.85 });
                if (table.Slots.Count == 0) { column.Children.Add(new TextBlock { Text = "No Pokémon", FontSize = 12, Opacity = 0.6 }); continue; }
                Grid grid = new Grid { ColumnDefinitions = new ColumnDefinitions("34,*,Auto,Auto") };
                for (int r = 0; r < table.Slots.Count; r++)
                {
                    HeadbuttEncounterViewModel.HoverSlot slot = table.Slots[r];
                    grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
                    Image icon = new Image { Source = slot.Icon, Width = 32, Height = 32 };
                    TextBlock name = new TextBlock { Text = slot.Name, VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center, Margin = new Thickness(4, 0, 12, 0) };
                    TextBlock lv = new TextBlock { Text = slot.Levels, VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 0), Opacity = 0.8 };
                    TextBlock pc = new TextBlock { Text = slot.Chance, VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center, HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Right, FontWeight = global::Avalonia.Media.FontWeight.SemiBold };
                    Grid.SetRow(icon, r); Grid.SetRow(name, r); Grid.SetRow(lv, r); Grid.SetRow(pc, r);
                    Grid.SetColumn(name, 1); Grid.SetColumn(lv, 2); Grid.SetColumn(pc, 3);
                    grid.Children.Add(icon); grid.Children.Add(name); grid.Children.Add(lv); grid.Children.Add(pc);
                }
                column.Children.Add(grid);
            }
        }

        public HeadbuttEncounterView(HeadbuttEncounterViewModel vm) : this() { DataContext = vm; }

        private async void OnLoadedSetup(object sender, RoutedEventArgs e)
        {
            if (_setupDone || Design.IsDesignMode) return;
            HeadbuttEncounterViewModel vm = VM;
            if (vm == null) return;
            _setupDone = true;
            vm.MapLoaded += (_, _) => { GlView.SetModel(VM.Model3D); RefreshGizmo(); };
            vm.MarkersChanged += (_, _) =>
            {
                GlView.SetMarkers(VM.MarkerMesh, VM.MarkerVertexCount);
                GlView.SetHighlight(VM.Highlight);
            };
            vm.EditModeChanged += (_, _) => RefreshGizmo();
            vm.GizmoTargetChanged += (_, _) => RefreshGizmo();
            await vm.SetupAsync(TopLevel.GetTopLevel(this) as Window);
        }

        private void RefreshGizmo()
        {
            if (VM == null) return;
            GlView.EditMode = VM.EditMode3D;
            if (VM.EditMode3D && VM.TrySelectedTreeAnchorNorm(out float nx, out float ny, out float nz))
                GlView.SetGizmoTarget(nx, ny, nz);
            else
                GlView.ClearGizmoTarget();
        }

        private void PickTree(Point p)
        {
            if (VM == null) return;
            int best = -1; float bestD = 18f;
            foreach ((int index, float nx, float ny, float nz) in VM.TreeAnchorsNorm())
            {
                if (!GlView.WorldToScreen(nx, ny, nz, out float sx, out float sy)) continue;
                float d = (float)Math.Sqrt((p.X - sx) * (p.X - sx) + (p.Y - sy) * (p.Y - sy));
                if (d < bestD) { bestD = d; best = index; }
            }
            if (best >= 0) VM.SelectedTreeIndex = best;
        }

        private void Save_Click(object sender, RoutedEventArgs e) => VM?.Save();
        private void AddTree_Click(object sender, RoutedEventArgs e) => VM?.AddTree();
        private void AddNormalGroup_Click(object sender, RoutedEventArgs e) => VM?.AddGroup(special: false);
        private void RemoveNormalGroup_Click(object sender, RoutedEventArgs e) => VM?.RemoveGroup(special: false);
        private void AddSpecialGroup_Click(object sender, RoutedEventArgs e) => VM?.AddGroup(special: true);
        private void RemoveSpecialGroup_Click(object sender, RoutedEventArgs e) => VM?.RemoveGroup(special: true);
        private void RemoveTree_Click(object sender, RoutedEventArgs e) => VM?.RemoveSelectedTree();
        private void CamTop_Click(object sender, RoutedEventArgs e) => GlView.SetOrientation(0f, 89f);
        private void CamIso_Click(object sender, RoutedEventArgs e) => GlView.SetOrientation(30f, 30f);
        private void CamReset_Click(object sender, RoutedEventArgs e) { GlView.SetOrientation(30f, 20f); GlView.ResetView(); }
    }
}
