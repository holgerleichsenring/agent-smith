namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// p0469: one command the agent ran, in the form the account is shown and the checkpoint
/// stores. It is a public record because a parked run must restore what it recorded: the
/// log used to hold its entries privately, so it serialised to <c>{}</c> and came back
/// present-but-empty — which TryGet reports as a success and the reader reads as a phase
/// that ran nothing.
/// </summary>
/// <param name="Repo">The repository the command ran in, empty when there is only one.</param>
/// <param name="Command">The command itself, as the agent wrote it.</param>
/// <param name="Tail">The end of its output — where a search's matches and a build's
/// verdict are.</param>
/// <param name="ExitCode">Its exit status, or null when the output carried none.</param>
/// <param name="OutputTrimmed">p0470: true when the budget took this entry's output away to
/// keep the command itself. It is not the same as a command that printed nothing, and the
/// account is shown the difference — otherwise the trimming would recreate, one level down,
/// the silence it exists to end.</param>
public sealed record PhaseCommandEntry(
    string Repo, string Command, string Tail, int? ExitCode, bool OutputTrimmed = false);
