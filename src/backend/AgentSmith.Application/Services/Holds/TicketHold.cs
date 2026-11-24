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

    /// <summary>2026-11-23-31dci: a nobody hold whose wait outlasted the configured minutes and is the admin's now; null
    /// for every other hold. The case stays the wait's, so the sentence still says what it waits for.</summary>
    public TicketHoldEscalation? Escalation { get; init; }

    /// <summary>2026-11-23-849e: the series' next merge after this one (a merge_next hold), or the merge to come first (a
    /// starts_on_merge hold); null when none.</summary>
    public string? Next { get; init; }

    /// <summary>2026-11-23-849e: how many merges a starts_on_merge hold waits for; null for every other hold.</summary>
    public int? Merges { get; init; }

    /// <summary>2026-11-23-849e: the merge act approves the pull request where its host requires a review (the project
    /// respects branch protection), as the repo's identity or merge.approval_auth.</summary>
    public bool Approves { get; init; }

    /// <summary>A person's move: merge, plan or decide. An admin hold is the admin's, nobody's is no one's.</summary>
    [JsonIgnore]
    public bool IsPersons => Addressee is TicketHoldAddressees.Merge or TicketHoldAddressees.Plan or TicketHoldAddressees.Decide;
}

/// <summary>2026-11-23-31dci: why a wait is the admin's — it lasted longer than <paramref name="AfterMinutes"/>, the value
/// of the configuration key <paramref name="Key"/>.</summary>
public sealed record TicketHoldEscalation(int AfterMinutes, string Key);

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

    /// <summary>2026-11-23-31dch: a parked question about the plan, settled in the series conversation.</summary>
    public const string QuestionPlan = "question_plan";

    /// <summary>2026-11-23-31dch: a parked question about the environment, reported to the admin.</summary>
    public const string QuestionEnvironment = "question_environment";
    public const string Handback = "handback";
    public const string ReadinessUnknown = "readiness_unknown";
    public const string Infrastructure = "infrastructure";
    public const string Interrupted = "interrupted";
    public const string CriteriaUnmet = "criteria_unmet";
    public const string Failed = "failed";
    public const string Shortfall = "shortfall";
    public const string TextEdited = "text_edited";
    public const string MergeNext = "merge_next";

    /// <summary>2026-11-23-d60d: a pull request outside any series' merge order can merge now — CI green or none, nothing
    /// it requires unlanded.</summary>
    public const string MergeReady = "merge_ready";

    /// <summary>2026-11-24-6811a: the host says an open pull request conflicts with its base; nothing merges it.</summary>
    public const string PrConflict = "pr_conflict";

    /// <summary>2026-11-23-d60d: CI failed on an open pull request; the names are the failed checks.</summary>
    public const string CiFailed = "ci_failed";

    /// <summary>2026-11-23-d60d: CI still runs on an open pull request.</summary>
    public const string CiRunning = "ci_running";

    /// <summary>2026-11-23-0219: a required build has not started on an open pull request's head and will not by itself.</summary>
    public const string CiNotStarted = "ci_not_started";
    public const string StartsOnMerge = "starts_on_merge";
    public const string Working = "working";

    /// <summary>2026-11-23-31dci: the attempt runs but waits for its model place, which another run holds.</summary>
    public const string ModelWait = "model_wait";
    public const string Queued = "queued";
    public const string Resuming = "resuming";
    public const string Restarting = "restarting";
    public const string MergeAfter = "merge_after";
    public const string Retarget = "retarget";
    public const string Prerequisite = "prerequisite";
}
