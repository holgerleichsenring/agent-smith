namespace AgentSmith.Server.Models;

/// <summary>
/// 2026-10-15-114fa: what a Close or a Reopen answers — 200 with its body, or a refusal status with the sentence
/// saying what to do instead.
/// </summary>
public sealed record TicketCloseResult(int Status, string? Refusal, object? Body)
{
    public static TicketCloseResult Ok(object body) => new(StatusCodes.Status200OK, null, body);

    public static TicketCloseResult Refuse(int status, string sentence) => new(status, sentence, null);
}
