namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// A run's summary or step message bounded for a tool answer: the dialog loop caps no tool result,
/// so text over <see cref="Cap"/> characters is cut and says so, with the length it had.
/// </summary>
public static class TicketRunText
{
    public const int Cap = 2000;

    public static string? Cut(string? text) =>
        text is null || text.Length <= Cap ? text : $"{text[..Cap]} … [cut at {Cap} of {text.Length} characters]";
}
