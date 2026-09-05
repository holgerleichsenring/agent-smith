using System.ComponentModel;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Sandbox.Wire;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// p0483: a read-only search of the branch, for the delivery account to run ITSELF.
/// <para>
/// Five phases fixed the FORM of a fact copied into a budgeted list and back out; p0483
/// changed the posture instead — look. 2026-08-25-0eae extends the reach to the BASE.
/// </para>
/// <para>
/// A search is STRONGER evidence than the agent could offer: the agent's commands ran at
/// some point during the work, while this runs against what the tree carries now — already
/// the tie-break the account's prompt states.
/// </para>
/// <para>
/// This opens no channel to the agent that did the work: the account stays a fresh
/// instance. Asking the author to argue is persuasion; looking is not.</para>
/// </summary>
public sealed class BranchSearch(
    IReadOnlyDictionary<string, ISandbox> sandboxes, ILogger logger,
    IReadOnlyDictionary<string, string?>? baseRefs = null,
    // Who is looking: two callers now, and a log naming the wrong one misdirects its reader.
    string searcher = "The delivery account")
{

    /// <summary>The repositories a search may name, for the prompt to list.</summary>
    public IReadOnlyList<string> Repositories => [.. sandboxes.Keys];

    /// <summary>
    /// 2026-08-25-0eae: the repositories whose BASE can be searched — those whose delivery
    /// diff resolved a real ref. A repository whose ladder fell through to HEAD has no base:
    /// searching it would search the branch under another name, which is worse than not
    /// searching at all.
    /// </summary>
    public IReadOnlyList<string> BaseSearchable =>
        [.. sandboxes.Keys.Where(key => BaseRefOf(key) is not null)];

    private string? BaseRefOf(string repository) =>
        baseRefs is not null && baseRefs.TryGetValue(repository, out var reference)
            ? reference
            : null;

    /// <summary>2026-08-25-6f12: the allowance, which is per PASS and not per account. A pass
    /// opens its own before it asks, so a windowed pass that spent everything cannot leave a
    /// later question answering "No search left" over a branch nobody looked at.</summary>
    public AccountSearchBudget Budget { get; } = new();

    private readonly SearchEvidence _evidence = new();

    /// <summary>p0484: what the account searched, in the same grammar an agent command is
    /// reported in, so the citation check needs no new reading. 2026-08-25-9749:
    /// <see cref="BaseAbsences"/> is the subset a not-applicable disposition may rest on — a
    /// search of a real BASE that ran and matched nothing.</summary>
    public IReadOnlyList<string> Evidence => _evidence.Lines;
    public IReadOnlyList<string> BaseAbsences => _evidence.BaseAbsences;

    [Description("Searches the branch as it stands now, in one repository, and returns the "
                 + "matching lines with their file and line number. This is how you settle a "
                 + "criterion about something being ABSENT: search for it and see. No output "
                 + "means the branch does not contain it. Read-only.")]
    public async Task<string> SearchBranch(
        [Description("The repository to search. Use one of the names listed under COMMANDS.")]
        string repository,
        [Description("An extended regular expression, e.g. 'MassTransit|IMessageBus\\.InvokeAsync'.")]
        string pattern,
        [Description("Optional path within the repository to search under. Omit to search all of it.")]
        string? path = null,
        CancellationToken ct = default)
    {
        if (!Budget.TryTake()) return AccountSearchBudget.Exhausted;
        if (!sandboxes.TryGetValue(repository, out var sandbox))
            return $"No repository named '{repository}'. The branch carries: {string.Join(", ", sandboxes.Keys)}.";
        if (string.IsNullOrWhiteSpace(pattern))
            return "A search needs a pattern.";

        var result = await sandbox.RunStepAsync(
            SearchCommands.OverTree(pattern, path), progress: null, ct);
        _evidence.Remember(repository, pattern, result.ExitCode);
        logger.LogInformation(
            "{Searcher} searched {Repo} for {Pattern} under {Path} — exit {Exit}", searcher,
            repository, pattern, path ?? ".", result.ExitCode);
        return SearchOutcome.Report(result, repository, path, pattern);
    }

    [Description("Searches the BASE the branch will merge into — the code as it stood BEFORE "
                 + "this delivery — in one repository. This is how you settle whether something "
                 + "was there to begin with: a criterion that applies only where something was "
                 + "PREVIOUSLY configured is answered here, not by the diff. No output means the "
                 + "base did not contain it. Read-only.")]
    public async Task<string> SearchBase(
        [Description("The repository to search. Use one of the names listed as base-searchable.")]
        string repository,
        [Description("An extended regular expression, e.g. 'ServiceBus|UseAzureServiceBus'.")]
        string pattern,
        [Description("Optional path within the repository to search under. Omit to search all of it.")]
        string? path = null,
        CancellationToken ct = default)
    {
        if (!Budget.TryTake()) return AccountSearchBudget.Exhausted;
        if (!sandboxes.TryGetValue(repository, out var sandbox))
            return $"No repository named '{repository}'. The branch carries: {string.Join(", ", sandboxes.Keys)}.";
        if (string.IsNullOrWhiteSpace(pattern))
            return "A search needs a pattern.";
        if (BaseRefOf(repository) is not { } baseRef)
            return $"'{repository}' has no base to search — its delivery was compared against the "
                   + "branch itself, so nothing here can say what was there before. This proves nothing.";

        var result = await sandbox.RunStepAsync(
            SearchCommands.OverRef(baseRef, pattern, path), progress: null, ct);
        _evidence.Remember(repository, pattern, result.ExitCode, baseRef);
        logger.LogInformation(
            "{Searcher} searched {Repo}@{Ref} for {Pattern} under {Path} — exit {Exit}", searcher,
            repository, baseRef, pattern, path ?? ".", result.ExitCode);
        return SearchOutcome.Report(result, $"{repository}@{baseRef}", path, pattern);
    }
}
