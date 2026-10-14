using AgentSmith.Infrastructure.Persistence.Entities;

namespace AgentSmith.Server.Services.Tickets;

/// <summary>
/// What a set of attempts recorded without a cut: their spec rows, the spec records they stored, the
/// runs themselves (status and run-end acceptance), the operator judgements, and each attempt's number.
/// </summary>
public sealed record TicketRunRecord(
    IReadOnlyList<RunSpec> Rows,
    IReadOnlyList<RunArtifact> SpecRecords,
    IReadOnlyDictionary<string, Run> Runs,
    IReadOnlyList<RunCriterionJudgement> Judgements,
    IReadOnlyDictionary<string, int> Numbers);
