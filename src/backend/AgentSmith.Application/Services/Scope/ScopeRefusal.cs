namespace AgentSmith.Application.Services.Scope;

/// <summary>
/// The scope call's judgement that the ticket demands something that must not be
/// done. The model is free in the judgement; what follows is not its to soften — the
/// run parks with these two lines on the ticket and no later step runs.
/// </summary>
/// <param name="Quote">The ticket sentence the model objects to, verbatim, so the
/// person reading the ticket sees WHAT was refused rather than a paraphrase.</param>
/// <param name="Reason">One line on why.</param>
public sealed record ScopeRefusal(string Quote, string Reason);
