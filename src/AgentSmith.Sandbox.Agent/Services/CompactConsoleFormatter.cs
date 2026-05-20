using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Console;

namespace AgentSmith.Sandbox.Agent.Services;

/// <summary>
/// Single-line console formatter for the sandbox-agent container, mirroring
/// AgentSmith.Server.Services.Logging.CompactConsoleFormatter so operator-side
/// `docker logs` reads consistently across server + agent containers.
///
/// Duplication is deliberate: AgentSmith.Sandbox.Agent is a standalone Exe with
/// minimal dependencies (no reference to AgentSmith.Server). When the format
/// changes, change both copies in lockstep — covered by a flatness test in
/// AgentSmith.Sandbox.Agent.Tests if added later.
/// </summary>
internal sealed class CompactConsoleFormatter() : ConsoleFormatter(FormatterName)
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
