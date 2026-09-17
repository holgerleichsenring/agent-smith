namespace AgentSmith.Contracts.Turns;

/// <summary>
/// One step a design turn took, as its owner is shown it.
/// <para>
/// NO TOOL RESULT, NO PROMPT, NO ARGUMENT BLOB. <paramref name="Name"/> is a tool or model
/// name the framework chose, and <paramref name="Detail"/> is a whitelisted argument summary
/// or the model's own one-sentence intent.
/// </para>
/// <para>
/// BUT A SEARCH PATTERN IS MODEL-AUTHORED TEXT, and a model writes a pattern out of what it
/// has just read — a literal it found in a config among it. Shown deliberately: a search
/// nobody can read is not evidence of what the turn looked for. It is why a step goes to the
/// dialog's OWN group, whose membership is owner-checked, and never to the run group or the
/// run's event stream: the one person who reads the reply is the one person who sees this.
/// </para>
/// </summary>
/// <param name="Kind">What the turn was doing.</param>
/// <param name="Name">The tool or model it was doing it with; none for a state of the turn.</param>
/// <param name="Detail">The argument summary or the intent sentence, where there is one.</param>
public sealed record TurnActivity(TurnActivityKind Kind, string? Name = null, string? Detail = null);
