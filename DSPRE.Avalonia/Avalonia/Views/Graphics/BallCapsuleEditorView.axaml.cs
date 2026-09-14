using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using DSPRE.Avalonia.Data;
using DSPRE.Avalonia.ViewModels.Graphics;
using DSPRE.ROMFiles;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.Views.Graphics
{
    public partial class BallCapsuleEditorView : Window
    {
        // The lower screen at double size.
        private const double Zoom = 2.0;

        private BallCapsuleEditorViewModel VM => DataContext as BallCapsuleEditorViewModel;
        private Canvas _canvas;
        private Image _ghost;
        private int _dragging = -1;

        // A seal dragged from the list onto the board.
        private SealChoice _carrying;
        private Point _carryFrom;
        private bool _carryMoved;

        public BallCapsuleEditorView()
        {
            AvaloniaXamlLoader.Load(this);
            _canvas = this.FindControl<Canvas>("BoardCanvas");
            _ghost = this.FindControl<Image>("Ghost");
            var ring = this.FindControl<global::Avalonia.Controls.Shapes.Ellipse>("BoardRing");
            if (ring != null)
            {
                Canvas.SetLeft(ring, ToCanvasX(BallCapsule.BoardCentreX - BallCapsule.BoardRadius));
                Canvas.SetTop(ring, ToCanvasY(BallCapsule.BoardCentreY - BallCapsule.BoardRadius));
            }
            this.FindControl<ListBox>("SealList")?.AddHandler(PointerPressedEvent, SealList_PointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
            AddHandler(PointerMovedEvent, Window_PointerMoved, RoutingStrategies.Tunnel, handledEventsToo: true);
            AddHandler(PointerReleasedEvent, Window_PointerReleased, RoutingStrategies.Tunnel, handledEventsToo: true);
        }

        // Stored positions are the game's placement coordinates; the capsule is drawn where the overview has it.
        private static double ToCanvasX(double placedX) => (placedX + BallCapsuleGraphics.PlacedToBoardX) * Zoom;
        private static double ToCanvasY(double placedY) => (placedY + BallCapsuleGraphics.PlacedToBoardY) * Zoom;
        private static int ToPlacedX(double canvasX) => (int)(canvasX / Zoom) - BallCapsuleGraphics.PlacedToBoardX;
        private static int ToPlacedY(double canvasY) => (int)(canvasY / Zoom) - BallCapsuleGraphics.PlacedToBoardY;

        public BallCapsuleEditorView(BallCapsuleEditorViewModel vm) : this()
        {
            DataContext = vm;
            vm.BoardChanged += Redraw;
            Closed += (_, _) => { vm.BoardChanged -= Redraw; vm.Battle?.StopPlayback(); };
            Redraw();
        }

        private void Redraw()
        {
            if (_canvas == null || VM == null) return;
            // The first three children are the board picture, its ring and the drag ghost.
            for (int i = _canvas.Children.Count - 1; i >= 3; i--) _canvas.Children.RemoveAt(i);
            foreach (var placed in VM.Placed)
            {
                var image = new Image
                {
                    Source = placed.Sticker, Width = 32 * Zoom, Height = 32 * Zoom, Tag = placed.Slot,
                    Stretch = global::Avalonia.Media.Stretch.Fill, Cursor = new Cursor(StandardCursorType.SizeAll),
                };
                global::Avalonia.Media.RenderOptions.SetBitmapInterpolationMode(image, global::Avalonia.Media.Imaging.BitmapInterpolationMode.None);
                ToolTip.SetTip(image, placed.Seal.Name);
                Canvas.SetLeft(image, ToCanvasX(placed.X - 16));
                Canvas.SetTop(image, ToCanvasY(placed.Y - 16));
                image.PointerPressed += Sticker_PointerPressed;
                _canvas.Children.Add(image);
            }
        }

        private void Sticker_PointerPressed(object sender, PointerPressedEventArgs e)
        {
            if (sender is not Image { Tag: int slot }) return;
            if (e.GetCurrentPoint(_canvas).Properties.IsRightButtonPressed) { VM?.Remove(slot); e.Handled = true; return; }
            _dragging = slot;
            e.Pointer.Capture(_canvas);
            _canvas.PointerMoved += Canvas_PointerMoved;
            _canvas.PointerReleased += Canvas_PointerReleased;
            e.Handled = true;
        }

        private void Canvas_PointerMoved(object sender, PointerEventArgs e)
        {
            if (_dragging < 0) return;
            Point p = e.GetPosition(_canvas);
            VM?.Move(_dragging, ToPlacedX(p.X), ToPlacedY(p.Y));
        }

        private void Canvas_PointerReleased(object sender, PointerReleasedEventArgs e)
        {
            _dragging = -1;
            e.Pointer.Capture(null);
            _canvas.PointerMoved -= Canvas_PointerMoved;
            _canvas.PointerReleased -= Canvas_PointerReleased;
        }

        private void SealList_PointerPressed(object sender, PointerPressedEventArgs e)
        {
            if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
            _carrying = (e.Source as Control)?.DataContext as SealChoice;
            _carryFrom = e.GetPosition(this);
            _carryMoved = false;
        }

        private void Window_PointerMoved(object sender, PointerEventArgs e)
        {
            if (_carrying == null || _ghost == null) return;
            Point p = e.GetPosition(this);
            if (!_carryMoved && Math.Abs(p.X - _carryFrom.X) + Math.Abs(p.Y - _carryFrom.Y) < 6) return;
            _carryMoved = true;
            Point on = e.GetPosition(_canvas);
            _ghost.Source = _carrying.Sticker;
            Canvas.SetLeft(_ghost, on.X - 16 * Zoom);
            Canvas.SetTop(_ghost, on.Y - 16 * Zoom);
            _ghost.IsVisible = true;
        }

        private void Window_PointerReleased(object sender, PointerReleasedEventArgs e)
        {
            if (_carrying == null) return;
            var seal = _carrying;
            _carrying = null;
            if (_ghost != null) _ghost.IsVisible = false;
            if (!_carryMoved || VM == null) return;
            Point at = e.GetPosition(_canvas);
            if (at.X < 0 || at.Y < 0 || at.X >= _canvas.Bounds.Width || at.Y >= _canvas.Bounds.Height) return;
            VM.PlaceAt(seal.Seal, ToPlacedX(at.X), ToPlacedY(at.Y));
        }

        private SealChoice Chosen => this.FindControl<ListBox>("SealList")?.SelectedItem as SealChoice;

        private void Place_Click(object sender, RoutedEventArgs e) => VM?.Place(Chosen?.Seal);
        private void SealList_DoubleTapped(object sender, TappedEventArgs e) => VM?.Place(Chosen?.Seal);
        private void Clear_Click(object sender, RoutedEventArgs e) => VM?.Clear();
        private void Save_Click(object sender, RoutedEventArgs e) => VM?.SaveChanges();
        private void Play_Click(object sender, RoutedEventArgs e) => VM?.Play();

        private void EditSticker_Click(object sender, RoutedEventArgs e)
        {
            if (Chosen?.Seal != null) AvaloniaEditorLauncher.OpenGraphicAt(DirNames.sealGraphics, Chosen.Seal.Sprite, preferAssembled: true);
        }

        private void EditParticles_Click(object sender, RoutedEventArgs e)
        {
            if (Chosen?.Seal == null || VM == null) return;
            AvaloniaEditorLauncher.OpenParticleEditor(DirNames.ballParticles, Chosen.Seal.Particle, Chosen.Seal.Name, VM.ParticlesChanged,
                                                      orthographic: true);
        }
    }
}
