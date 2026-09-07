using AgentSmith.Contracts.Specs;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// A question the previous run handed the ticket back with, which no person answered:
/// the hand-back as it was persisted on the branch, and the reading the run proceeds
/// on — named from THAT question, never from what the model says this time.
/// </summary>
/// <param name="Question">The persisted question hand-back — case, readings, taken index.</param>
/// <param name="TakenLabel">The label the reading was listed under on the ticket: (a), (b), …</param>
/// <param name="TakenReading">The reading itself, verbatim from the first question.</param>
public sealed record UnansweredQuestion(SpecHandback Question, string TakenLabel, string TakenReading);
