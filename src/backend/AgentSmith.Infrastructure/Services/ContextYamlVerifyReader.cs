using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Infrastructure.Services;

/// <summary>
/// 2026-08-31-26d4: turns the deserialised <c>verify:</c> block into the contract stages
/// the discovery carries.
/// <para>
/// A stage without a label or without a command is not a stage, so it is dropped here
/// rather than travelling as a half-record the resolver would have to re-check. Order is
/// the declaration's own — the file states the sequence the run executes.
/// </para>
/// </summary>
internal static class ContextYamlVerifyReader
{
    public static IReadOnlyList<ContextYamlVerifyStage>? Read(
        IReadOnlyList<ContextYamlReadShape.VerifyBlock>? blocks)
    {
        if (blocks is null) return null;
        var stages = blocks
            .Where(block => !string.IsNullOrWhiteSpace(block.Label)
                            && !string.IsNullOrWhiteSpace(block.Command))
            .Select(block => new ContextYamlVerifyStage(
                block.Label!.Trim(), block.Command!.Trim(), Trimmed(block.WhenPresent)))
            .ToList();
        return stages.Count > 0 ? stages : null;
    }

    private static string? Trimmed(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
