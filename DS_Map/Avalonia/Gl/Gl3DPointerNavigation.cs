using System;
using global::Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Input;

namespace DSPRE.Avalonia.Gl
{
    /// <summary>
    /// Shared mouse-drag camera + pick/gizmo wiring for every 3D viewport (Map/Event/Building/
    /// Headbutt editors), so the button mapping and click-to-focus behavior stay consistent
    /// instead of drifting per view.
    ///
    /// Button roles match PDSMS's convention, not DSPRE's old default: right-drag orbits (tilts)
    /// the camera, left-drag pans, middle-drag also pans. Left is still the "action" button
    /// first, falling back to pan only when paint/edit mode didn't claim the press.
    /// </summary>
    public sealed class Gl3DPointerNavigation
    {
        private readonly Border _host;
        private readonly NsbmdGlControl _view;
        private Point? _lastPointer;
        private bool _orbiting;
        private bool _panning;
        private bool _painting;
        private int _dragAxis = -1;   // gizmo axis being dragged (0=X,1=Y,2=Z), -1 = none

        /// <summary>True while a tile-paint tool is active: the camera is fully locked (any button) and left-drag paints instead.</summary>
        public Func<bool> IsPaintModeActive;
        public Action<Point> PaintAt;

        /// <summary>True while 3D move-gizmo editing is active: left-press hit-tests the gizmo handle before falling back to picking.</summary>
        public Func<bool> IsEditModeActive;
        public Action BeginGizmoDrag;
        /// <summary>Gizmo axis (0=X,1=Y,2=Z) and the raw normalized-space drag delta along it (via <see cref="NsbmdGlControl.ScreenDragToAxis"/>); the caller applies its own model-scale division.</summary>
        public Action<int, float> NudgeAxis;
        public Action<Point> Pick;

        public Gl3DPointerNavigation(Border host, NsbmdGlControl view)
        {
            _host = host ?? throw new ArgumentNullException(nameof(host));
            _view = view ?? throw new ArgumentNullException(nameof(view));
            _host.Focusable = true;

            _host.PointerPressed += OnPressed;
            _host.PointerMoved += OnMoved;
            _host.PointerReleased += OnReleased;
            _host.PointerWheelChanged += (s, e) => _view.ZoomByWheel((float)e.Delta.Y);
        }

        private void OnPressed(object sender, PointerPressedEventArgs e)
        {
            var pt = e.GetCurrentPoint(_host);
            _dragAxis = -1; _painting = false; _orbiting = false; _panning = false;

            if (IsPaintModeActive?.Invoke() == true)
            {
                if (pt.Properties.IsLeftButtonPressed) { _painting = true; PaintAt?.Invoke(pt.Position); }
                // any other button stays locked (no camera movement) while the paint tool is active
            }
            else if (pt.Properties.IsRightButtonPressed)
            {
                _orbiting = true;
            }
            else if (pt.Properties.IsMiddleButtonPressed)
            {
                _panning = true;
            }
            else if (pt.Properties.IsLeftButtonPressed)
            {
                if (IsEditModeActive?.Invoke() == true)
                {
                    int axis = _view.HitTestGizmoAxis((float)pt.Position.X, (float)pt.Position.Y);
                    if (axis >= 0) { _dragAxis = axis; BeginGizmoDrag?.Invoke(); }
                    else { Pick?.Invoke(pt.Position); _panning = true; }
                }
                else
                {
                    _panning = true;
                }
            }

            _lastPointer = pt.Position;
            e.Pointer.Capture(_host);
            _host.Focus();
        }

        private void OnMoved(object sender, PointerEventArgs e)
        {
            if (_lastPointer is not Point last) return;
            var p = e.GetPosition(_host);

            if (_painting) { PaintAt?.Invoke(p); _lastPointer = p; return; }
            if (IsPaintModeActive?.Invoke() == true) { _lastPointer = p; return; }   // camera locked while the paint tool is active

            if (_dragAxis >= 0)
            {
                float normDelta = _view.ScreenDragToAxis(_dragAxis, (float)(p.X - last.X), (float)(p.Y - last.Y));
                NudgeAxis?.Invoke(_dragAxis, normDelta);
            }
            else if (_orbiting) _view.OrbitByDrag((float)(p.X - last.X), (float)(p.Y - last.Y));
            else if (_panning) _view.PanByDrag((float)(p.X - last.X), (float)(p.Y - last.Y));

            _lastPointer = p;
        }

        private void OnReleased(object sender, PointerReleasedEventArgs e)
        {
            _lastPointer = null; _orbiting = false; _panning = false; _painting = false; _dragAxis = -1;
            e.Pointer.Capture(null);
        }
    }
}
