using System.ComponentModel;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-09-07-b7e2: the account's branch search, offered to the derivation — the same
/// argv grep under /work, read-only by construction, with its path contained.
/// </summary>
public sealed class RepositorySearchTool(DerivationLook look, ILogger logger)
{
    public const string Name = "search_repository";

    [Description("Searches one repository as checked out now and returns the matching lines "
                 + "with file and line number. Use it to settle whether something is present "
                 + "or absent before you state it. Read-only. The result starts with an evidence "
                 + "id such as [L3]; a fact that rests on this search cites that id.")]
    public async Task<string> SearchRepository(
        [Description("The repository to search. Use one of the names listed as in scope.")]
        string repository,
        [Description("An extended regular expression, e.g. 'PackageReference|<Version>'.")]
        string pattern,
        [Description("Optional path within the repository to search under. Omit to search all of it.")]
        string? path = null,
        CancellationToken ct = default)
    {
        if (!ContainedPath.TryRelative(path, out var under)) return ContainedPath.Refusal;
        if (string.IsNullOrWhiteSpace(pattern)) return "A search needs a pattern.";
        if (!look.TryOpen(repository, out var sandbox, out var refusal)) return refusal;

        var step = SearchCommands.OverTree(pattern, under);
        var result = await sandbox.RunStepAsync(step, progress: null, ct);
        var id = look.Evidence.Remember(
            repository, $"grep -E '{pattern}' {under}", result.ExitCode,
            ran: result.ExitCode is 0 or 1);
        logger.LogInformation(
            "The derivation searched {Repo} for {Pattern} under {Path} — exit {Exit} as {Id}",
            repository, pattern, under, result.ExitCode, id);
        return $"[{id}] " + SearchOutcome.Report(
            result, repository, under == "." ? null : under, pattern);
    }
}
