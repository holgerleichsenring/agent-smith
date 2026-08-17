using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Contracts.Runs;

/// <summary>
/// p0423: reads the one question "does this run record its conversation?" from the two
/// places a deployment can answer it — the configuration file, and the environment.
/// <para>
/// The environment wins. A k8s Job and a compose service are configured by env, their
/// config file is a read-only mount, and a switch that cannot be flipped where the failure
/// happens is not a switch.
/// </para>
/// </summary>
public sealed class TraceSwitch(AgentSmithConfig config)
{
    public const string EnvironmentVariable = "AGENTSMITH_TRACE";

    public bool IsOn => Environment.GetEnvironmentVariable(EnvironmentVariable) is { } value
        ? IsTruthy(value)
        : config.Trace.Enabled;

    private static bool IsTruthy(string value) =>
        value.Equals("1", StringComparison.OrdinalIgnoreCase)
        || value.Equals("true", StringComparison.OrdinalIgnoreCase)
        || value.Equals("yes", StringComparison.OrdinalIgnoreCase)
        || value.Equals("on", StringComparison.OrdinalIgnoreCase);
}
