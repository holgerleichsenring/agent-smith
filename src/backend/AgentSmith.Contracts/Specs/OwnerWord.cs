namespace AgentSmith.Contracts.Specs;

/// <summary>
/// 2026-11-15-53d0: one message the operator wrote in the design conversation an approval was given in, verbatim.
/// </summary>
/// <param name="Author">Who wrote it: the turn's author, else the conversation's user.</param>
/// <param name="At">When it was written.</param>
/// <param name="Text">The message as written.</param>
public sealed record OwnerWord(string Author, DateTimeOffset At, string Text);
