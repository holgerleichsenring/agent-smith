using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.AI;

namespace AgentSmith.Application.Services.Loop;

/// <summary>
/// Decorator around an <see cref="AIFunction"/> that records each tool invocation
/// on the active <see cref="LoopTraceCollector"/>. When the wrapped function is
/// a filesystem read (name == "read_file"), the cited path is added to the
/// collector's ReadSet — the foundation for p0151b's source-anchored
/// observation validator.
/// </summary>
public sealed class TracingAIFunction(AIFunction inner, LoopTraceCollector trace) : AIFunction
{
    public override string Name => inner.Name;
    public override string Description => inner.Description;
    public override JsonElement JsonSchema => inner.JsonSchema;
    public override JsonSerializerOptions JsonSerializerOptions => inner.JsonSerializerOptions;

    protected override async ValueTask<object?> InvokeCoreAsync(
        AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        var argsJson = SerializeArgs(arguments);
        var sw = Stopwatch.StartNew();
        var success = false;
        string? errorMessage = null;
        try
        {
            var result = await inner.InvokeAsync(arguments, cancellationToken);
            success = true;
            return result;
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            throw;
        }
        finally
        {
            sw.Stop();
            trace.AppendToolCall(inner.Name, argsJson, sw.ElapsedMilliseconds, success, errorMessage);
            CapturePathIfRead(arguments);
        }
    }

    private void CapturePathIfRead(AIFunctionArguments arguments)
    {
        if (!inner.Name.Equals("read_file", StringComparison.OrdinalIgnoreCase)) return;
        if (!arguments.TryGetValue("path", out var raw) || raw is null) return;
        var path = raw.ToString();
        if (!string.IsNullOrWhiteSpace(path)) trace.AppendReadPath(path);
    }

    private string SerializeArgs(AIFunctionArguments arguments)
    {
        try { return JsonSerializer.Serialize(arguments, JsonSerializerOptions); }
        catch { return "{}"; }
    }
}
