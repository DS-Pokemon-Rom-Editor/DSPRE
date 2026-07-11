using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using AvaloniaEdit.Editing;
using AvaloniaEdit.Rendering;

namespace DSPRE.Avalonia
{
    public sealed class CachedLineNumberMargin : LineNumberMargin
    {
        private readonly Dictionary<int, FormattedText> _lineNumbers = new();
        private FormattedText _measureText;
        private int _measureDigits;
        private bool _typefaceValid;
        private IBrush _cachedForeground;

        protected override Size MeasureOverride(Size availableSize)
        {
            EnsureTypeface();
            if (_measureText == null || _measureDigits != MaxLineNumberLength)
            {
                _measureDigits = MaxLineNumberLength;
                _measureText = CreateFormattedText(
                    new string('9', _measureDigits),
                    GetValue(TextBlock.ForegroundProperty));
            }

            return new Size(_measureText.Width, 0);
        }

        public override void Render(DrawingContext drawingContext)
        {
            var textView = TextView;
            if (textView is not { VisualLinesValid: true }) return;

            EnsureTypeface();
            var foreground = GetValue(TextBlock.ForegroundProperty);
            if (!Equals(_cachedForeground, foreground))
            {
                _cachedForeground = foreground;
                _lineNumbers.Clear();
                _measureText = null;
            }

            double width = Bounds.Width;
            foreach (var line in textView.VisualLines)
            {
                int lineNumber = line.FirstDocumentLine.LineNumber;
                if (!_lineNumbers.TryGetValue(lineNumber, out var text))
                {
                    text = CreateFormattedText(lineNumber.ToString(CultureInfo.CurrentCulture), foreground);
                    _lineNumbers[lineNumber] = text;
                }

                double y = line.GetTextLineVisualYPosition(line.TextLines[0], VisualYPosition.TextTop);
                drawingContext.DrawText(text, new Point(width - text.Width, y - textView.VerticalOffset));
            }
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);
            if (change.Property == TextBlock.FontFamilyProperty
                || change.Property == TextBlock.FontSizeProperty
                || change.Property == TextBlock.FontStyleProperty
                || change.Property == TextBlock.FontWeightProperty
                || change.Property == TextBlock.FontStretchProperty)
            {
                _typefaceValid = false;
                _lineNumbers?.Clear();
                _measureText = null;
            }
            else if (change.Property == TextBlock.ForegroundProperty)
            {
                _lineNumbers?.Clear();
                _measureText = null;
            }
        }

        private void EnsureTypeface()
        {
            if (_typefaceValid) return;
            Typeface = new Typeface(
                GetValue(TextBlock.FontFamilyProperty),
                GetValue(TextBlock.FontStyleProperty),
                GetValue(TextBlock.FontWeightProperty),
                GetValue(TextBlock.FontStretchProperty));
            EmSize = GetValue(TextBlock.FontSizeProperty);
            _typefaceValid = true;
            _lineNumbers.Clear();
            _measureText = null;
        }

        private FormattedText CreateFormattedText(string text, IBrush foreground)
            => new FormattedText(
                text,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                Typeface,
                EmSize,
                foreground);
    }
}
