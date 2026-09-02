using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Infrastructure.Services.Factories.ChatClientBuilders;
using AgentSmith.Infrastructure.Services.Workers;

namespace AgentSmith.PipelineHarness.Evals;

/// <summary>
/// 2026-08-28-cc40: whether an agent CLI answers on this machine, and which one.
/// <para>
/// The eval tier's other clients are configured by a credential in the environment, and
/// their absence is a missing variable. A worker-driven eval has no credential to look
/// for — the subscription lives inside the CLI — so its precondition is the BINARY, and
/// the probe answers exactly the question the loud skip asks.
/// </para>
/// </summary>
internal static class AgentCliProbe
{
    internal const string ModelEnv = "AGENTSMITH_WORKER_MODEL";
    internal const string DefaultModel = "sonnet";

    /// <summary>The binary the run would invoke — the operator's override, then the
    /// bridge's own default. The same order <see cref="ExternalWorkerCliOptionsFactory"/>
    /// resolves in, because a probe of a different binary proves nothing.</summary>
    internal static string Binary =>
        Environment.GetEnvironmentVariable(ExternalWorkerCliOptionsFactory.BinaryEnv)
            is { Length: > 0 } configured
            ? configured
            : ExternalWorkerCliOptionsFactory.DefaultBinary;

    internal static string Model =>
        Environment.GetEnvironmentVariable(ModelEnv) is { Length: > 0 } model
            ? model
            : DefaultModel;

    /// <summary>The agent an eval run is driven by: worker type, the probed model, and a
    /// call budget generous enough for a scan master's turn.</summary>
    internal static AgentConfig Agent() => new()
    {
        Type = ExternalWorkerChatClientBuilder.TypeName,
        Model = Model,
        NetworkTimeoutSeconds = 900,
    };

    /// <summary>The resolved path of the CLI, or null when nothing answers to that name.
    /// Named explicitly so a test can ask about a binary that is definitely absent without
    /// mutating a process-wide environment two parallel collections share.</summary>
    internal static string? Resolve(string binary)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(binary);
        if (binary.Contains(Path.DirectorySeparatorChar) || binary.Contains('/'))
            return File.Exists(binary) ? binary : null;
        return SearchPath(binary);
    }

    internal static string? Resolve() => Resolve(Binary);

    internal static bool IsAvailable(string binary) => Resolve(binary) is not null;

    internal static bool IsAvailable() => IsAvailable(Binary);

    /// <summary>The reason a suite skips, phrased so the absence is unmistakable in a log
    /// that otherwise shows a passing test.</summary>
    internal static string SkipReason(string binary) =>
        $"SKIP: no agent CLI — '{binary}' is not on PATH. This tier drives the real scan "
        + $"through an agent CLI; set {ExternalWorkerCliOptionsFactory.BinaryEnv} to one. "
        + "NOTHING WAS MEASURED.";

    internal static string SkipReason() => SkipReason(Binary);

    private static string? SearchPath(string binary)
    {
        var path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        foreach (var directory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = Path.Combine(directory.Trim(), binary);
            if (File.Exists(candidate)) return candidate;
        }
        return null;
    }
}
