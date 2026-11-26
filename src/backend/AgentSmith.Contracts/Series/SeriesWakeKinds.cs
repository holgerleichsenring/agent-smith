namespace AgentSmith.Contracts.Series;

/// <summary>2026-11-25-9992g: what woke a series master — a CI result on a pull request's head, another change of a pull
/// request (its state, a conflict, its target), a person's comment on the ticket or its pull request, a ticket run's end.</summary>
public static class SeriesWakeKinds
{
    public const string Ci = "ci";
    public const string PullRequest = "pr";
    public const string Comment = "comment";
    public const string Run = "run";

    /// <summary>2026-11-25-96aec: a person's new approval or change request on a ticket's pull request — never the master's own.</summary>
    public const string Review = "review";

    /// <summary>2026-11-25-96aea: a ticket a member requires landed or (on_green_push) went green — whether it starts is the master's.</summary>
    public const string Prerequisite = "prerequisite";
}
