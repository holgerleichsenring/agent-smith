using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.AI;

namespace AgentSmith.Application.Services.Loop;

/// <summary>
/// Decorator around an <see cref="AIFunction"/> that records each tool invocation
/// on the active <see cref="LoopTraceCollector"/>. When the wrapped function is
/// a filesystem read (name matches "read_file" / "ReadFile" / "readFile" — see
/// <see cref="IsReadFileTool"/>), the cited path is added to the collector's
/// ReadSet — the foundation for p0151b's source-anchored observation validator.
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
        if (!IsReadFileTool(inner.Name)) return;
        if (!arguments.TryGetValue("path", out var raw) || raw is null) return;
        var path = raw.ToString();
        if (!string.IsNullOrWhiteSpace(path)) trace.AppendReadPath(path);
    }

    // AIFunctionFactory.Create(method) defaults the tool name to the C# method name
    // (PascalCase "ReadFile"), while many LLM-side conventions use snake_case
    // ("read_file"). Normalise both to a punctuation-free, lowercase token.
    private static bool IsReadFileTool(string name) =>
        Normalize(name) == "readfile";

    private static string Normalize(string name) =>
        new string(name.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();

    private string SerializeArgs(AIFunctionArguments arguments)
    {
        try { return JsonSerializer.Serialize(arguments, JsonSerializerOptions); }
        catch { return "{}"; }
    }
}
