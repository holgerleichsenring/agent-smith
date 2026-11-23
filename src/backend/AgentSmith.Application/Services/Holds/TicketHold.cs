using System.Text.Json.Serialization;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Holds;

/// <summary>
/// 2026-11-23-31dcg: what keeps a ticket from moving and who moves it — ONE addressee (<see cref="TicketHoldAddressees"/>),
/// the case that names the sentence (<see cref="TicketHoldCases"/>), at most one act, and since when it holds. Read once on
/// the server by <see cref="TicketHoldReader"/>; every surface renders this record and decides nothing of its own.
/// </summary>
/// <param name="Attempt">The attempt the sentence names, numbered from 1; null when it names none.</param>
/// <param name="Names">The tickets the sentence names — the merges it waits for, the requirements still running.</param>
/// <param name="Detail">What a stop recorded about its cause (2026-11-16-f3fba), quoted by the sentence; null otherwise.</param>
public sealed record TicketHold(string Addressee, string Case, TicketHoldAct? Act, DateTimeOffset? Since)
{
    public int? Attempt { get; init; }

    public IReadOnlyList<string> Names { get; init; } = [];

    public EndDetail? Detail { get; init; }

    /// <summary>A person's move: merge, plan or decide. An admin hold is the admin's, nobody's is no one's.</summary>
    [JsonIgnore]
    public bool IsPersons => Addressee is TicketHoldAddressees.Merge or TicketHoldAddressees.Plan or TicketHoldAddressees.Decide;
}

/// <summary>2026-11-23-31dcg: the one act a hold offers and what it acts on — a run id, a series id, a path or a URL.</summary>
public sealed record TicketHoldAct(string Kind, string? Target = null);

/// <summary>2026-11-23-31dcg: who moves a held ticket, in the order a hold is chosen when several apply.</summary>
public static class TicketHoldAddressees
{
    /// <summary>Configuration, credentials, images or capacity — only an admin can lift it.</summary>
    public const string Admin = "admin";

    /// <summary>A person decides how the ticket goes on: an answer, a reply, a retry or a rework.</summary>
    public const string Decide = "decide";

    /// <summary>The plan changed under the ticket: a person settles it in the conversation.</summary>
    public const string Plan = "plan";

    /// <summary>A pull request is to be merged before anything else moves.</summary>
    public const string Merge = "merge";

    /// <summary>Nothing is needed from anyone; the ticket moves on by itself.</summary>
    public const string Nobody = "nobody";
}

/// <summary>2026-11-23-31dcg: each case a hold is read from — the sentence's key on every surface.</summary>
public static class TicketHoldCases
{
    public const string ImageUntrusted = "image_untrusted";
    public const string BootstrapMissing = "bootstrap_missing";
    public const string Preflight = "preflight";
    public const string Question = "question";
    public const string Handback = "handback";
    public const string ReadinessUnknown = "readiness_unknown";
    public const string Infrastructure = "infrastructure";
    public const string Interrupted = "interrupted";
    public const string CriteriaUnmet = "criteria_unmet";
    public const string Failed = "failed";
    public const string Shortfall = "shortfall";
    public const string TextEdited = "text_edited";
    public const string MergeNext = "merge_next";
    public const string StartsOnMerge = "starts_on_merge";
    public const string Working = "working";
    public const string Queued = "queued";
    public const string Resuming = "resuming";
    public const string Restarting = "restarting";
    public const string MergeAfter = "merge_after";
    public const string Retarget = "retarget";
    public const string Prerequisite = "prerequisite";
}

/// <summary>2026-11-23-31dcg: what a hold's act does; its target says on what.</summary>
public static class TicketHoldActs
{
    /// <summary>Answer the parked question in the conversation.</summary>
    public const string Answer = "answer";

    /// <summary>Talk it through in the ticket's conversation: a reply to a hand-back, a rework, a changed text.</summary>
    public const string Conversation = "conversation";

    /// <summary>Retry the attempt unchanged; the target is its run id.</summary>
    public const string Retry = "retry";

    /// <summary>Merge the ticket's pull requests through the series' merge (2026-11-23-31dce); the target is the series id.</summary>
    public const string Merge = "merge";

    /// <summary>Open the series' Merge next; the target is the series id.</summary>
    public const string MergeNext = "merge_next";


    /// <summary>Open the setting that lifts the hold; the target is its dashboard path.</summary>
    public const string Settings = "settings";

    /// <summary>Initialize the project's repositories; the target is the project.</summary>
    public const string Init = "init";
}
