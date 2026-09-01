using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Infrastructure.Services;

/// <summary>
/// 2026-09-01-379a: turns the deserialised <c>probe:</c> block into the contract probe the
/// discovery carries.
/// <para>
/// A block without a target or without a command is not a probe, so it is dropped here
/// rather than travelling as a half-record. Dropping is the right answer for exactly one
/// reason: the run reports a probe it does not have as NOT DECLARED, which is a sentence
/// the operator can act on — where a half-record would reach the runner as a command with
/// no target to name in its own failure.
/// </para>
/// </summary>
internal static class ContextYamlProbeReader
{
    public static ContextYamlTargetProbe? Read(ContextYamlReadShape.ProbeBlock? block)
    {
        if (block is null) return null;
        if (string.IsNullOrWhiteSpace(block.Target) || string.IsNullOrWhiteSpace(block.Command))
            return null;
        return new ContextYamlTargetProbe(block.Target!.Trim(), block.Command!.Trim());
    }
}
