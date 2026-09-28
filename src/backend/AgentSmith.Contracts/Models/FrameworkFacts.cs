namespace AgentSmith.Contracts.Models;

/// <summary>
/// 2026-09-28-1da5e: the concrete strings THIS framework would use for THIS ticket.
/// <para>
/// A convention the framework enforces reaches the model as a rendered fact about the case in
/// hand, never as prose about the rule. The work-branch convention lives in five places in the
/// source and in no skill text at all — correct for acting, since the framework names branches and
/// the model does not, and wrong for READING THE WORLD BACK: asked whether a ticket's branches
/// were finished, a conversation had no way to know what to look for.
/// </para>
/// <para>
/// Restating the rule in a skill would be a second statement of something the code decides, and
/// the two would drift. A rendered instance cannot: it IS the code. It is also smaller — one
/// string beats a paragraph explaining how the string is built, and it answers the question the
/// model actually has.
/// </para>
/// </summary>
/// <param name="WorkBranch">Where this framework's work on this ticket would live.</param>
/// <param name="EnqueuedLabel">What this installation calls a ticket it has taken up.</param>
/// <param name="InProgressLabel">What it calls one being worked.</param>
/// <param name="ApprovedSetStamp">What it marks a ticket with when it filed it from an approved
/// specification — the words an installation renamed, not the defaults.</param>
public sealed record FrameworkFacts(
    string WorkBranch, string EnqueuedLabel, string InProgressLabel, string ApprovedSetStamp);
