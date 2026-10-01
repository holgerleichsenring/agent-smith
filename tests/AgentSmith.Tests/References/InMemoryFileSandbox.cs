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
                ? Ok(step, Encoding.UTF8.GetString(bytes)) : Fail(step, "file not found"),
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
        foreach (var encoded in Files.Keys.Where(p => p.EndsWith(".b64", StringComparison.Ordinal)).ToList())
        {
            Files[encoded[..^4]] = Convert.FromBase64String(Encoding.UTF8.GetString(Files[encoded]));
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

    private static string Resolve(string path) =>
        path.StartsWith('/') ? path : Work + (path is "." ? string.Empty : path);

    private static StepResult Ok(Step step, string output) =>
        new(StepResult.CurrentSchemaVersion, step.StepId, 0, false, 0.01, null, output);

    private static StepResult Fail(Step step, string error) =>
        new(StepResult.CurrentSchemaVersion, step.StepId, 1, false, 0.01, error, null);

    public Task ForceRemoveAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
