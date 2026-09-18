namespace AgentSmith.Server.Models;

/// <summary>
/// One step a running design turn took — the payload of the hub's "SpecDialogActivity" push.
/// <paramref name="Kind"/> is "tool", "model", "reviewing" or "revising"; <paramref name="Name"/>
/// is the tool or model it used and <paramref name="Detail"/> the whitelisted argument summary
/// or the model's own intent sentence, either of which may be absent.
/// <para>
/// A plain payload into the dialog's OWN group, as every other dialog push is. A run event
/// would reach the runs list and the unchecked run subscription; this line is addressed to the
/// one person holding the conversation.
/// </para>
/// </summary>
public sealed record SpecDialogActivityPush(
    string DialogId, string Kind, string? Name, string? Detail, DateTimeOffset At);
