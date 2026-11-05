using AgentSmith.Sandbox.Wire;

namespace AgentSmith.Application.Services.Sandbox;

/// <summary>
/// 2026-11-04-96a0c: the paths read_file opened on one upload in this turn. Every read of an upload passes its
/// reference sandbox, the children's included, and the master and its children share that sandbox, so the log is
/// thread-safe. Only a ReadFile step that came back with exit code 0 counts: a grep hit or a command's output shows a
/// fragment, not the file. Paths are kept relative to the set.
/// </summary>
public sealed class ReferenceReadLog
{
    private const string WorkRoot = "/work/";
    private readonly object _lock = new();
    private readonly HashSet<string> _paths = new(StringComparer.Ordinal);

    public IReadOnlyCollection<string> Paths
    {
        get
        {
            lock (_lock) return [.. _paths.Order(StringComparer.Ordinal)];
        }
    }

    /// <summary>Records <paramref name="step"/> when it is a successful read_file, and hands its result on.</summary>
    public StepResult Record(Step step, StepResult result)
    {
        ArgumentNullException.ThrowIfNull(step);
        ArgumentNullException.ThrowIfNull(result);
        if (step.Kind != StepKind.ReadFile || result.ExitCode != 0 || string.IsNullOrWhiteSpace(step.Path)) return result;
        var path = Normalised(step.Path);
        if (path.Length > 0)
            lock (_lock) _paths.Add(path);
        return result;
    }

    private static string Normalised(string path)
    {
        var relative = path.Trim();
        if (relative.StartsWith(WorkRoot, StringComparison.Ordinal)) relative = relative[WorkRoot.Length..];
        while (relative.StartsWith("./", StringComparison.Ordinal)) relative = relative[2..];
        return relative.TrimStart('/');
    }
}
