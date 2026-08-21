namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// p0495: what a run_command's wall-time comes out as — the one place the configured
/// default and the operator's step cap are read together.
/// <para>
/// A command that asks for nothing gets the default (p0230: per-project override ??
/// global sandbox.run_command_timeout_seconds); a command that asks may ask for up to the
/// cap, so RAISING the cap raises what a command may ask for. The ceiling used to be a
/// private const 600 widened only by a higher DEFAULT — a bound computed from something
/// other than the bounding thing, which killed a test command at 600.5s on a project whose
/// cap read 900.
/// </para>
/// <para>
/// Both values are optional because a call site that resolves neither exists (the toolchain
/// probe, the bootstrap hosts). There the request stands and the sandbox backend's
/// SandboxStepCap lowers it — the same cap, one layer down. No constant bounds it here.
/// </para>
/// </summary>
internal sealed class RunCommandTimeout(int? configuredDefaultSeconds, int? stepTimeoutCapSeconds)
{
    /// <summary>The conservative floor used when no configured default was threaded.</summary>
    private const int FallbackDefaultSeconds = 60;

    private readonly int _defaultSeconds =
        configuredDefaultSeconds is > 0 ? configuredDefaultSeconds.Value : FallbackDefaultSeconds;

    private readonly int? _capSeconds = stepTimeoutCapSeconds is > 0 ? stepTimeoutCapSeconds : null;

    public int For(int? requestedSeconds) =>
        requestedSeconds is not { } requested
            ? _defaultSeconds
            : Math.Max(1, _capSeconds is { } cap ? Math.Min(requested, cap) : requested);
}
