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
                block.Label!.Trim(), block.Command!.Trim(), Trimmed(block.WhenPresent),
                Globs(block.WhenChanged)))
            .ToList();
        return stages.Count > 0 ? stages : null;
    }

    /// <summary>
    /// 2026-09-01-e14d: the derivation record. A block naming no file records no
    /// derivation, so it is dropped here rather than travelling as an empty claim the
    /// drift check would have to re-check.
    /// </summary>
    public static ContextYamlVerifyDerivation? ReadDerivation(
        ContextYamlReadShape.DerivedFromBlock? block)
    {
        var files = block?.Files?
            .Where(file => !string.IsNullOrWhiteSpace(file))
            .Select(file => file.Trim())
            .ToList();
        return files is { Count: > 0 }
            ? new ContextYamlVerifyDerivation(files, Trimmed(block!.Hash))
            : null;
    }

    // Trimmed, blank entries dropped; nothing left is no condition.
    private static IReadOnlyList<string>? Globs(IReadOnlyList<string>? globs)
    {
        var kept = globs?.Where(glob => !string.IsNullOrWhiteSpace(glob)).Select(glob => glob.Trim()).ToList();
        return kept is { Count: > 0 } ? kept : null;
    }

    private static string? Trimmed(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
