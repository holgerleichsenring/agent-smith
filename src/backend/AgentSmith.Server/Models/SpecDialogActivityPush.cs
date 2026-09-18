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
/// <param name="Seq">2026-09-18-2f8b: the step's place in its turn, from 1. The same step is
/// served by the conversation read and pushed over the hub, and a page merges the two by this
/// number: a moment cannot tell two identical tool calls apart, and dropping one of them would
/// corrupt the step count a reader watches for signs of life.</param>
/// <param name="TurnStartedAt">Which turn the number belongs to. The sequence restarts every
/// turn, so without this a page whose reply push never arrived would read the next turn's
/// steps as duplicates of the dead turn's and discard every one of them.</param>
public sealed record SpecDialogActivityPush(
    string DialogId,
    string Kind,
    string? Name,
    string? Detail,
    DateTimeOffset At,
    int Seq,
    DateTimeOffset TurnStartedAt);
