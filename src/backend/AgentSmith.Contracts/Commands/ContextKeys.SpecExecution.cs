namespace AgentSmith.Contracts.Commands;

public static partial class ContextKeys
{
    /// <summary>p0315d: the validated spec (SpecDraft) extracted from the
    /// spec ticket by SpecGate — the requirement record the master
    /// executes and the artifact WriteSpecRecord commits to specs/done/.</summary>
    public const string CurrentSpec = "CurrentSpec";

    /// <summary>p0315d: questions the master raised mid-run via ask_human on a
    /// ticket-triggered run (IReadOnlyList&lt;PlanOpenQuestion&gt;) — captured by
    /// TicketClarificationToolHost, posted + parked by MasterOpenQuestions.</summary>
    public const string MasterOpenQuestions = "MasterOpenQuestions";

    /// <summary>2026-10-13-a347a: the declared formatters that failed at a spec commit
    /// (IReadOnlyList&lt;SpecFormatFailure&gt;), each carrying its repository — accumulated over the
    /// run and read by that repository's code PR body.</summary>
    public const string SpecFormatFailures = "SpecFormatFailures";

    /// <summary>2026-10-13-8762n: the entered spec's citations whose lines changed and whose text was found
    /// nowhere (IReadOnlyList&lt;string&gt;, one line each) — replaced at every spec entry, read by the master prompt.</summary>
    public const string StaleCitations = "StaleCitations";
}
