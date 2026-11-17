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

    /// <summary>A grep that could not run, a path filter that does not exist among its causes; the
    /// source scope's search answers in the same convention.</summary>
    private const int CouldNotRunExit = 2; // not a limit: grep's exit code for a search that could not run

    /// <summary>2026-09-15-ffa7: per look, because the id it spells is the holder's.</summary>
    public string Description =>
        "Searches one repository as checked out now and returns the matching lines with file "
        + "and line number. It proves a string is PRESENT; it does not locate a file — a pattern "
        + "containing a filename matches every file that merely MENTIONS that name. Read-only, "
        + "and NOT exhaustive, differently per engine: a repository served as a read-only source "
        + $"scope is searched with ripgrep, which skips {GrepScope.Summary}; otherwise grep runs "
        + "and skips only .git, node_modules, bin and obj, with no size ceiling. What it reports "
        + "absent is absent outside whichever of those applied. A long result is CUT and says so "
        + "where it ends — read no absence out of a tail that was never shown. The result starts "
        + $"with an evidence id such as [{look.Terms.EvidencePrefix}3]; a fact that rests on this "
        + "search cites that id.";

    public async Task<string> SearchRepository(
        [Description("The repository to search. Use one of the names listed under 'Repositories you may look into'.")]
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
        if (opened is not null && await opened.TryOpenAsync(ct) is { } failure)
            return NothingRan(repository, What(opened, pattern, under), failure);
        var result = await SearchAt(sandbox, opened, pattern, under, ct);
        // 2026-11-17-5701f: a path filter written '<repository>/<path>' in a run keyed 'default' does not exist
        // (exit 2 on both engines). Only then is it searched below the repository's name, on the sandbox already
        // opened; a search that ran and matched nothing stands as it is.
        if (result.ExitCode == CouldNotRunExit && look.WithoutRepositoryName(repository, under) is { } below
            && await SearchAt(sandbox, opened, pattern, below, ct) is { ExitCode: 0 or 1 } found)
            (result, under) = (found, below);
        var what = What(opened, pattern, under);
        var id = look.Evidence.Remember(new EvidenceRecord(
            repository, EvidenceRecord.Search, what, result.ExitCode,
            Ran: result.ExitCode is 0 or 1, under, Pattern: pattern));
        logger.LogInformation(
            "The {Actor} searched {Repo} for {Pattern} under {Path} — exit {Exit} as {Id}",
            look.Terms.Actor, repository, pattern, under, result.ExitCode, id);
        return $"[{id}] " + SearchOutcome.Report(
            result, repository, under == "." ? null : under, pattern);
    }

    private static string What(SourceScopeLook? opened, string pattern, string under) =>
        opened is null ? $"grep -E '{pattern}' {under}" : $"rg --hidden --no-ignore '{pattern}' {under}";

    private static async Task<StepResult> SearchAt(
        ISandbox sandbox, SourceScopeLook? opened, string pattern, string under, CancellationToken ct) =>
        opened is null
            ? await sandbox.RunStepAsync(SearchCommands.OverTree(pattern, under), progress: null, ct)
            : await opened.SearchAsync(pattern, under, ct);

    /// <summary>A look that never happened: its id is minted, and its line says it proves
    /// nothing, so a statement citing it is not admitted as a fact.</summary>
    private string NothingRan(string repository, string what, string why)
    {
        var id = look.Evidence.Remember(new EvidenceRecord(
            repository, EvidenceRecord.Search, what, SourceScopeLook.NotRunExit, Ran: false));
        logger.LogInformation(
            "The {Actor} could not open {Repo} — {Why} as {Id}", look.Terms.Actor, repository, why, id);
        return $"[{id}] {repository} could not be opened ({why}), so this search proves nothing.";
    }
}
