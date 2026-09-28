namespace AgentSmith.Infrastructure.Persistence.Repositories;

/// <summary>
/// An operator's judgement of one criterion, reduced to what the Criteria met figure needs:
/// which criterion (the <c>CriterionKey</c> digest), the status it answered, and the status
/// the operator says was true.
/// </summary>
public sealed record CriterionOverrule(string CriterionKey, string MachineStatus, string HumanStatus);
