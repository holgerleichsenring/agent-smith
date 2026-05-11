using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Console;

namespace AgentSmith.Server.Services.Logging;

/// <summary>
/// Single-line console formatter: timestamp · level · short-category · message,
/// with exception types and messages flattened inline using `|N: …` markers
/// for nested inner exceptions. Avoids multi-line stack traces that interleave
/// with concurrent log output and break readability in tail/grep workflows.
/// </summary>
public sealed class CompactConsoleFormatter() : ConsoleFormatter(FormatterName)
{
    public const string FormatterName = "compact";

    public override void Write<TState>(
        in LogEntry<TState> logEntry,
        IExternalScopeProvider? scopeProvider,
        TextWriter textWriter)
    {
        var timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff");
        var level = LevelLabel(logEntry.LogLevel);
        var category = ShortCategory(logEntry.Category);
        var message = logEntry.Formatter(logEntry.State, logEntry.Exception);
        var scope = ScopeText(scopeProvider);

        var output = new StringBuilder($"{timestamp} {level} {scope}[{category}] {message}");
        if (logEntry.Exception is not null)
            AppendExceptionChain(output, logEntry.Exception);

        textWriter.WriteLine(output.ToString());
    }

    private static string LevelLabel(LogLevel level) => level switch
    {
        LogLevel.Trace => "trce",
        LogLevel.Debug => "dbug",
        LogLevel.Information => "info",
        LogLevel.Warning => "warn",
        LogLevel.Error => "FAIL",
        LogLevel.Critical => "CRIT",
        _ => level.ToString()
    };

    private static string ShortCategory(string category)
    {
        var parts = category.Split('.');
        return parts.Length >= 2 ? $"{parts[^2]}.{parts[^1]}" : parts[^1];
    }

    // Framework-scope markers that aren't useful for operator-facing log reading.
    // ASP.NET Core Hosting / Kestrel / Activity tracking stamp these on every
    // request-scoped log line; we keep our own `run=...` / `ticket=...` scopes
    // (produced by BeginScope("name={X}", value)) and drop the rest.
    private static readonly string[] FrameworkScopeMarkers =
    [
        "SpanId:", "TraceId:", "ParentId:",
        "ConnectionId:",
        "RequestPath:", "RequestId:",
        "HTTP GET ", "HTTP POST ", "HTTP PATCH ", "HTTP PUT ", "HTTP DELETE "
    ];

    private static string ScopeText(IExternalScopeProvider? scopeProvider)
    {
        if (scopeProvider is null) return string.Empty;
        var sb = new StringBuilder();
        scopeProvider.ForEachScope((scope, builder) =>
        {
            var text = scope?.ToString();
            if (string.IsNullOrEmpty(text)) return;
            if (IsFrameworkScope(text)) return;
            builder.Append($"[{text}] ");
        }, sb);
        return sb.ToString();
    }

    private static bool IsFrameworkScope(string scopeText)
    {
        foreach (var marker in FrameworkScopeMarkers)
            if (scopeText.Contains(marker, StringComparison.Ordinal)) return true;
        return false;
    }

    private static void AppendExceptionChain(StringBuilder output, Exception ex)
    {
        var current = ex;
        var depth = 0;
        while (current is not null)
        {
            output.Append($" |{depth}: {current.GetType().Name}: {current.Message}");
            if (!string.IsNullOrEmpty(current.StackTrace))
                output.Append($" @ {Compact(current.StackTrace)}");
            current = current.InnerException;
            depth++;
        }
    }

    private static string Compact(string stackTrace) =>
        stackTrace.Replace('\n', ' ').Replace('\r', ' ').Replace("   at ", " ← ").Trim();
}
