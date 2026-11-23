using AgentSmith.Application.Services.Pocs;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Infrastructure.Persistence.Entities;

namespace AgentSmith.Server.Services.Pocs.Import;

/// <summary>2026-11-23-933db: the sentences an import is refused with — each names the value, the reason and, for a
/// limit, its key.</summary>
internal static class PocImportRefusals
{
    public static PocRefusal InProgress(Poc poc, string? runId) =>
        PocRefusal.Conflict($"A run of '{poc.Name}' is in progress (run {runId}); start again once it has ended.");

    public static PocRefusal BadName(string name) =>
        PocRefusal.BadRequest($"'{name}' is not a PoC name: use lower-case letters, digits and '-', at most "
            + $"{PocNameProposal.MaxNameChars} characters, starting and ending with a letter or digit.");

    public static PocRefusal NameTaken(string name, string target) =>
        PocRefusal.Conflict($"The name '{name}' is taken on target '{target}'; choose another.");

    public static PocRefusal OverLimit(PocTargetConfig target, int live) =>
        PocRefusal.Conflict($"You hold {live} live PoCs on target '{target.Name}', the most pocs.targets[].per_person_limit "
            + $"allows ({target.PerPersonLimit}), because each person's PoCs share one cluster. Delete one, or ask an admin.");

    public static PocRefusal DifferentApp(Poc poc, PocMatch match) =>
        PocRefusal.Conflict($"Is this really '{poc.Name}'? {match.Added + match.Changed + match.Removed} files differ "
            + $"({match.RatioPercent} %){(match.PortChanged ? ", the port changed" : "")}"
            + $"{(match.EntrypointChanged ? ", the entrypoint changed" : "")}. Replace it anyway (confirm), import it as a new PoC, "
            + "or choose another ZIP.");

    public static PocRefusal NotLive(Poc poc) =>
        PocRefusal.Conflict($"'{poc.Name}' is {poc.State}; import the ZIP as a new PoC instead.");

    public static PocRefusal Purged(int revision, int days) =>
        new(StatusCodes.Status410Gone, $"The upload of revision {revision} was purged after {days} days "
            + $"({PocsConfig.BlockedRetentionDaysKey}); import the ZIP again.");

    public static PocRefusal BadChoice { get; } = PocRefusal.BadRequest("An import's choice is 'new' or 'replace'.");
}
