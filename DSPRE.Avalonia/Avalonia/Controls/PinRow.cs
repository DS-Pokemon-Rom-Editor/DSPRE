using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;

namespace DSPRE.Avalonia.Controls
{
    /// <summary>
    /// A row of buttons that shows as many as fit and moves the rest behind <see cref="OverflowButton"/>, so the row
    /// never runs under what sits beside it.
    /// </summary>
    public class PinRow : Panel
    {
        private Control _overflow;

        /// <summary>Shown at the end of the row while some children do not fit; it is the last child.</summary>
        public Control OverflowButton
        {
            get => _overflow;
            set
            {
                if (_overflow != null) Children.Remove(_overflow);
                _overflow = value;
                if (value != null) Children.Add(value);
            }
        }

        /// <summary>The children that did not fit in the last arrange, in order.</summary>
        public IReadOnlyList<Control> Hidden { get; private set; } = Array.Empty<Control>();

        public event EventHandler HiddenChanged;

        private IEnumerable<Control> Pins => Children.Where(c => !ReferenceEquals(c, _overflow) && c.IsVisible);

        protected override Size MeasureOverride(Size availableSize)
        {
            double width = 0, height = 0;
            foreach (Control c in Children)
            {
                c.Measure(Size.Infinity);
                if (ReferenceEquals(c, _overflow) || !c.IsVisible) continue;
                width += c.DesiredSize.Width;
                height = Math.Max(height, c.DesiredSize.Height);
            }
            if (_overflow != null) height = Math.Max(height, _overflow.DesiredSize.Height);
            return new Size(Math.Min(width, availableSize.Width), height);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            List<Control> pins = Pins.ToList();
            double total = pins.Sum(c => c.DesiredSize.Width);
            bool overflow = total > finalSize.Width + 0.5 && _overflow != null;
            double room = overflow ? finalSize.Width - _overflow.DesiredSize.Width : finalSize.Width;
            List<Control> hidden = new List<Control>();
            double x = 0;
            foreach (Control c in pins)
            {
                double w = c.DesiredSize.Width;
                if (hidden.Count == 0 && x + w <= room + 0.5)
                {
                    c.Arrange(new Rect(x, 0, w, finalSize.Height));
                    x += w;
                }
                else
                {
                    hidden.Add(c);
                    c.Arrange(new Rect(0, 0, 0, 0));
                }
            }
            if (_overflow != null)
                _overflow.Arrange(overflow ? new Rect(x, 0, _overflow.DesiredSize.Width, finalSize.Height) : new Rect(0, 0, 0, 0));
            if (!hidden.SequenceEqual(Hidden))
            {
                Hidden = hidden;
                HiddenChanged?.Invoke(this, EventArgs.Empty);
            }
            return finalSize;
        }
    }
}
