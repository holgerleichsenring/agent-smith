using System.ComponentModel;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Sandbox.Wire;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-09-07-b7e2: the account's branch search, offered to the derivation — the same
/// argv grep under /work, read-only by construction, with its path contained.
/// </summary>
public sealed class RepositorySearchTool(DerivationLook look, ILogger logger)
{
    public const string Name = "search_repository";

    /// <summary>2026-09-15-ffa7: per look, because the id it spells is the holder's.</summary>
    public string Description =>
        "Searches one repository as checked out now and returns the matching lines with file "
        + "and line number. Use it to settle whether something is present or absent before you "
        + $"state it. Read-only, and NOT exhaustive: it skips {GrepScope.Summary}, so what it "
        + "reports absent is absent outside those. The result starts with an evidence id such "
        + $"as [{look.Terms.EvidencePrefix}3]; a fact that rests on this search cites that id.";

    public async Task<string> SearchRepository(
        [Description("The repository to search. Use one of the names listed as in scope.")]
        string repository,
        [Description("A regular expression, e.g. 'PackageReference|<Version>'. Alternation, "
            + "character classes and quantifiers are available; lookaround and backreferences "
            + "are not, on either engine that may serve the search.")]
        string pattern,
        [Description("Optional path within the repository to search under. Omit to search all of it.")]
        string? path = null,
        CancellationToken ct = default)
    {
        if (!ContainedPath.TryRelative(path, out var under)) return ContainedPath.Refusal;
        if (string.IsNullOrWhiteSpace(pattern)) return "A search needs a pattern.";
        if (!look.TryOpen(repository, out var sandbox, out var refusal)) return refusal;

        // 2026-09-17-042ed: a read-only source scope refuses a Run step with exit 1, which a
        // Run grep's convention reads as "found nothing". It is opened first and searched with
        // a Grep step instead; no Run step is ever sent to one. The two paths run DIFFERENT
        // engines under different filters, so the evidence line names the one that actually ran
        // — a line that said 'grep -E' for a ripgrep search would be a fact about nothing.
        var opened = sandbox is ISourceScopeSandbox scope ? new SourceScopeLook(scope) : null;
        var what = opened is null
            ? $"grep -E '{pattern}' {under}"
            : $"rg --hidden --no-ignore '{pattern}' {under}";
        if (opened is not null && await opened.TryOpenAsync(ct) is { } failure)
            return NothingRan(repository, what, failure);
        var result = opened is null
            ? await sandbox.RunStepAsync(SearchCommands.OverTree(pattern, under), progress: null, ct)
            : await opened.SearchAsync(pattern, under, ct);
        var id = look.Evidence.Remember(
            repository, what, result.ExitCode, ran: result.ExitCode is 0 or 1);
        logger.LogInformation(
            "The {Actor} searched {Repo} for {Pattern} under {Path} — exit {Exit} as {Id}",
            look.Terms.Actor, repository, pattern, under, result.ExitCode, id);
        return $"[{id}] " + SearchOutcome.Report(
            result, repository, under == "." ? null : under, pattern);
    }

    /// <summary>A look that never happened: its id is minted, and its line says it proves
    /// nothing, so a statement citing it is not admitted as a fact.</summary>
    private string NothingRan(string repository, string what, string why)
    {
        var id = look.Evidence.Remember(repository, what, SourceScopeLook.NotRunExit, ran: false);
        logger.LogInformation(
            "The {Actor} could not open {Repo} — {Why} as {Id}", look.Terms.Actor, repository, why, id);
        return $"[{id}] {repository} could not be opened ({why}), so this search proves nothing.";
    }
}
