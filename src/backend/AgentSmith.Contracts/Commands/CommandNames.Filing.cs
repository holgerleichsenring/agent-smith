namespace AgentSmith.Contracts.Commands;

/// <summary>2026-11-17-5701b: the steps of the 'file' run, which files an approved proposal as tracker tickets.</summary>
public static partial class CommandNames
{
    /// <summary>Turns the approved proposal into an ordered filing plan, refusing the whole plan before anything exists.</summary>
    public const string PlanFiling = "PlanFilingCommand";

    /// <summary>Files the plan in order — create, requires edges, approval record, branch write — then settles every
    /// created ticket's start; once the first ticket exists it never fails.</summary>
    public const string FileTickets = "FileTicketsCommand";
}
