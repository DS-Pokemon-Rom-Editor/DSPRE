using System;
using Avalonia.Logging;

namespace DSPRE.Avalonia
{
    /// <summary>Sends Avalonia's own warnings (rendering backend, OpenGL context failures) to the application log.</summary>
    internal sealed class AvaloniaLogSink : ILogSink
    {
        private readonly ILogSink _inner;

        public AvaloniaLogSink(ILogSink inner) => _inner = inner;

        public bool IsEnabled(LogEventLevel level, string area) => level >= LogEventLevel.Warning || (_inner?.IsEnabled(level, area) ?? false);

        public void Log(LogEventLevel level, string area, object source, string messageTemplate)
            => Log(level, area, source, messageTemplate, Array.Empty<object>());

        public void Log(LogEventLevel level, string area, object source, string messageTemplate, params object[] propertyValues)
        {
            if (_inner?.IsEnabled(level, area) == true) _inner.Log(level, area, source, messageTemplate, propertyValues);
            if (level < LogEventLevel.Warning) return;
            string text = messageTemplate;
            // Templates name their values in braces: {Name}. Fill them in order.
            foreach (var v in propertyValues ?? Array.Empty<object>())
            {
                int a = text.IndexOf('{'), b = a < 0 ? -1 : text.IndexOf('}', a);
                if (b < 0) break;
                text = text.Substring(0, a) + v + text.Substring(b + 1);
            }
            string line = $"Avalonia [{area}] {text}";
            if (level >= LogEventLevel.Error) AppLogger.Error(line); else AppLogger.Warn(line);
        }
    }
}
