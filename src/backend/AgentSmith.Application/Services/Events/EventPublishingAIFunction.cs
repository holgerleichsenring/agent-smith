using System.Text.Json;
using AgentSmith.Contracts.Events;
using Microsoft.Extensions.AI;

namespace AgentSmith.Application.Services.Events;

/// <summary>
/// Wraps an <see cref="AIFunction"/> so each tool invocation emits ToolCall +
/// ToolResult events. Payloads are METADATA ONLY — the args / result blobs
/// stay out of the event stream (same security class as prompts; the first
/// 200 chars of a write_file are often exactly the sensitive part). The
/// event carries tool name + arg length + ok/fail + result length, nothing
/// more.
/// </summary>
public sealed class EventPublishingAIFunction(
    AIFunction inner,
    IEventPublisher eventPublisher,
    IRunContextAccessor runContext) : AIFunction
{
    public override string Name => inner.Name;
    public override string Description => inner.Description;
    public override JsonElement JsonSchema => inner.JsonSchema;
    public override JsonSerializerOptions JsonSerializerOptions => inner.JsonSerializerOptions;

    protected override async ValueTask<object?> InvokeCoreAsync(
        AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        var runId = runContext.CurrentRunId;
        var argsLength = EstimateArgsLength(arguments);
        var summary = ExtractSummary(arguments);

        if (!string.IsNullOrEmpty(runId))
        {
            await eventPublisher.PublishAsync(
                new ToolCallEvent(runId!, inner.Name, argsLength, DateTimeOffset.UtcNow, summary),
                cancellationToken);
        }

        var ok = false;
        object? result = null;
        string? errorMessage = null;
        try
        {
            result = await inner.InvokeAsync(arguments, cancellationToken);
            ok = true;
            return result;
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            throw;
        }
        finally
        {
            if (!string.IsNullOrEmpty(runId))
            {
                var resultLength = EstimateResultLength(result);
                await eventPublisher.PublishAsync(
                    new ToolResultEvent(runId!, inner.Name, ok, resultLength, DateTimeOffset.UtcNow, errorMessage),
                    CancellationToken.None);
            }
        }
    }

    private int EstimateArgsLength(AIFunctionArguments arguments)
    {
        try { return JsonSerializer.Serialize(arguments, JsonSerializerOptions).Length; }
        catch { return 0; }
    }

    // p0175-fix: pull operator-visible identifiers out of the args so the
    // activity row reads "read_file src/Foo.cs" instead of "read_file (47B)".
    // Whitelist-only — never serialise the full arg dict. Capped at 120
    // chars to stay inside one row.
    private const int SummaryCap = 120;
    private static readonly string[] SummaryKeys =
        ["path", "paths", "file", "files", "url", "target", "dir", "directory", "pattern"];

    private static string? ExtractSummary(AIFunctionArguments arguments)
    {
        foreach (var key in SummaryKeys)
        {
            if (!arguments.TryGetValue(key, out var raw) || raw is null) continue;
            var rendered = RenderValue(raw);
            if (string.IsNullOrWhiteSpace(rendered)) continue;
            return rendered.Length > SummaryCap ? rendered[..SummaryCap] : rendered;
        }
        return null;
    }

    private static string? RenderValue(object value) => value switch
    {
        string s => s,
        System.Collections.IEnumerable e when value is not string =>
            string.Join(", ", e.Cast<object?>().Where(x => x is not null).Select(x => x!.ToString())),
        _ => value.ToString(),
    };

    private int EstimateResultLength(object? result)
    {
        if (result is null) return 0;
        if (result is string s) return s.Length;
        try { return JsonSerializer.Serialize(result, JsonSerializerOptions).Length; }
        catch { return 0; }
    }
}
