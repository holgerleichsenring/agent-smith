using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Sandbox.Wire;

namespace AgentSmith.Tests.References;

/// <summary>
/// 2026-10-01-283dc: a container with a file system and nothing else — writes land at their path
/// under /work, reads and greps see them, and the one python3 step decodes every .b64 file the way
/// the materialiser's script does. It counts what reached it, which is what the tests assert on.
/// </summary>
internal sealed class InMemoryFileSandbox : IHoldableSandbox
{
    private const string Work = "/work/";

    public Dictionary<string, byte[]> Files { get; } = new(StringComparer.Ordinal);

    public int Writes { get; private set; }

    public int PythonRuns { get; private set; }

    public string JobId { get; } = "mem-" + Guid.NewGuid().ToString("N")[..8];

    public Task<StepResult> RunStepAsync(Step step, IProgress<StepEvent>? progress, CancellationToken cancellationToken) =>
        Task.FromResult(step.Kind switch
        {
            StepKind.WriteFile => Write(step),
            StepKind.ReadFile => Files.TryGetValue(Resolve(step.Path!), out var bytes)
                ? Read(step, bytes) : Fail(step, "file not found"),
            StepKind.ListFiles => Ok(step, List(step)),
            StepKind.Grep => Ok(step, Grep(step)),
            StepKind.Run when step.Command == "python3" => Decode(step),
            _ => Ok(step, string.Empty),
        });

    private StepResult Write(Step step)
    {
        Writes++;
        Files[Resolve(step.Path!)] = Encoding.UTF8.GetBytes(step.Content ?? string.Empty);
        return Ok(step, string.Empty);
    }

    private StepResult Decode(Step step)
    {
        PythonRuns++;
        var suffix = AgentSmith.Application.Services.Sandbox.ReferenceSetMaterialiser.EncodedSuffix;
        foreach (var encoded in Files.Keys.Where(p => p.EndsWith(suffix, StringComparison.Ordinal)).ToList())
        {
            Files[encoded[..^suffix.Length]] = Convert.FromBase64String(Encoding.UTF8.GetString(Files[encoded]));
            Files.Remove(encoded);
        }
        return Ok(step, string.Empty);
    }

    private string Grep(Step step)
    {
        var root = Resolve(step.Path ?? ".").TrimEnd('/', '.');
        var rows = Files.Where(f => f.Key.StartsWith(root, StringComparison.Ordinal))
            .SelectMany(f => Encoding.UTF8.GetString(f.Value).Split('\n')
                .Select((text, i) => (Path: f.Key[Work.Length..], Line: i + 1, Text: text)))
            .Where(l => Regex.IsMatch(l.Text, step.Pattern!))
            .Select(l => new { path = l.Path, line = l.Line, text = l.Text, kind = "match" });
        return JsonSerializer.Serialize(rows);
    }

    // 2026-10-01-283dh: like the agent — UTF-8 or refused, never a lossy decode.
    private static StepResult Read(Step step, byte[] bytes)
    {
        try { return Ok(step, StrictUtf8.GetString(bytes)); }
        catch (DecoderFallbackException) { return Fail(step, "binary or non-UTF-8 content not supported"); }
    }

    // 2026-10-01-283dh: every file under the path, at any depth, in the agent's wire shape.
    private string List(Step step)
    {
        var root = Resolve(step.Path ?? ".").TrimEnd('/') + "/";
        return JsonSerializer.Serialize(Files.Where(f => f.Key.StartsWith(root, StringComparison.Ordinal))
            .Select(f => new Dictionary<string, object> { ["path"] = f.Key, ["size_bytes"] = f.Value.LongLength, ["is_directory"] = false }));
    }

    private static readonly UTF8Encoding StrictUtf8 = new(false, throwOnInvalidBytes: true);

    private static string Resolve(string path) =>
        path.StartsWith('/') ? path : Work + (path is "." ? string.Empty : path);

    private static StepResult Ok(Step step, string output) =>
        new(StepResult.CurrentSchemaVersion, step.StepId, 0, false, 0.01, null, output);

    private static StepResult Fail(Step step, string error) =>
        new(StepResult.CurrentSchemaVersion, step.StepId, 1, false, 0.01, error, null);

    public Task ForceRemoveAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
