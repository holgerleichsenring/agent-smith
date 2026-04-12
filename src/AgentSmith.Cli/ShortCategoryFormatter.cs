using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Options;

namespace AgentSmith.Cli;

public sealed class ShortCategoryFormatterOptions : ConsoleFormatterOptions
{
}

/// <summary>
/// Single-line log formatter that shows only the class name, not the full namespace.
/// Output: "08:46:46 info: PipelineExecutor[0] Starting pipeline with 7 commands"
/// </summary>
public sealed class ShortCategoryFormatter : ConsoleFormatter, IDisposable
{
    private readonly IDisposable? _optionsReloadToken;

    public ShortCategoryFormatter(IOptionsMonitor<ShortCategoryFormatterOptions> options)
        : base("short")
    {
        _optionsReloadToken = options.OnChange(_ => { });
    }

    public override void Write<TState>(
        in LogEntry<TState> logEntry,
        IExternalScopeProvider? scopeProvider,
        TextWriter textWriter)
    {
        var message = logEntry.Formatter(logEntry.State, logEntry.Exception);
        if (message is null) return;

        var timestamp = DateTimeOffset.Now.ToString("HH:mm:ss");
        var level = logEntry.LogLevel switch
        {
            LogLevel.Trace => "trce",
            LogLevel.Debug => "dbug",
            LogLevel.Information => "info",
            LogLevel.Warning => "warn",
            LogLevel.Error => "fail",
            LogLevel.Critical => "crit",
            _ => "????"
        };

        var category = ShortenCategory(logEntry.Category);

        textWriter.Write(timestamp);
        textWriter.Write(' ');
        textWriter.Write(level);
        textWriter.Write(": ");
        textWriter.Write(category);
        textWriter.Write(' ');
        textWriter.WriteLine(message);

        if (logEntry.Exception is not null)
            textWriter.WriteLine(logEntry.Exception);
    }

    private static string ShortenCategory(string? category)
    {
        if (string.IsNullOrEmpty(category)) return "";

        var lastDot = category.LastIndexOf('.');
        return lastDot >= 0 ? category[(lastDot + 1)..] : category;
    }

    public void Dispose() => _optionsReloadToken?.Dispose();
}
