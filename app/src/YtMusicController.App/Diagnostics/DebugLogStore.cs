using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting.Display;

namespace YtMusicController.App.Diagnostics;

public sealed class DebugLogStore : ILogEventSink
{
    public const int Capacity = 10000;
    private static readonly MessageTemplateTextFormatter Formatter = new(
        "[{Timestamp:HH:mm:ss.fff} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}",
        CultureInfo.InvariantCulture);
    private static readonly Regex BearerTokenPattern = new(
        @"(?i)(Bearer\s+)[A-Za-z0-9._~+/=-]{8,}",
        RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(100));

    private readonly object _gate = new();
    private readonly Queue<string> _entries = new();

    public static string LogDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "YtMusicController",
        "Logs");

    public static string RollingLogPath => Path.Combine(LogDirectory, "ytmusic-.log");

    public event EventHandler<string>? EntryAdded;
    public int Count
    {
        get
        {
            lock (_gate)
                return _entries.Count;
        }
    }

    public event EventHandler? Cleared;

    public void Emit(LogEvent logEvent)
    {
        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        Formatter.Format(logEvent, writer);
        var entry = Redact(writer.ToString()).TrimEnd();

        lock (_gate)
        {
            _entries.Enqueue(entry);
            while (_entries.Count > Capacity)
                _entries.Dequeue();
        }

        EntryAdded?.Invoke(this, entry);
    }

    public string GetSnapshotText()
    {
        lock (_gate)
            return string.Join(Environment.NewLine, _entries);
    }

    public void Clear()
    {
        lock (_gate)
            _entries.Clear();
        Cleared?.Invoke(this, EventArgs.Empty);
    }

    private static string Redact(string value) =>
        BearerTokenPattern.Replace(value, "$1[REDACTED]");
}
