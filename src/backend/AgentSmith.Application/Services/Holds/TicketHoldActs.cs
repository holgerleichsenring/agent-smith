namespace AgentSmith.Application.Services.Holds;

/// <summary>2026-11-23-31dcg: what a hold's act does; its target says on what.</summary>
public static class TicketHoldActs
{
    /// <summary>Answer the parked question in the conversation.</summary>
    public const string Answer = "answer";

    /// <summary>Talk it through in the ticket's conversation: a reply to a hand-back, a rework, a changed text. 2026-11-23-31dch:
    /// with a target, the series' conversation — the target is the series id.</summary>
    public const string Conversation = "conversation";

    /// <summary>Retry the attempt unchanged; the target is its run id.</summary>
    public const string Retry = "retry";

    /// <summary>Merge the ticket's pull requests through the series' merge (2026-11-23-31dce); the target is the series id.</summary>
    public const string Merge = "merge";

    /// <summary>2026-11-23-849e: open the series' page — a ticket that starts on merges waits for its merge order there;
    /// the target is the series id. Replaces the former merge_next act, which offered a merge the person could not make.</summary>
    public const string Series = "series";

    /// <summary>2026-11-23-d60d: open the pull request on its host; the target is its URL, null when several are open.</summary>
    public const string PullRequest = "pull_request";

    /// <summary>Open the setting that lifts the hold; the target is its dashboard path.</summary>
    public const string Settings = "settings";

    /// <summary>Initialize the project's repositories; the target is the project.</summary>
    public const string Init = "init";
}
