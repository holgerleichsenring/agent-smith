namespace AgentSmith.Server.Models;

/// <summary>
/// Who posted the message a dialog turn answers, and whether that person may start runs (runs.control).
/// A turn acts on the ticket only as its poster could, so the ports a bound turn is seeded with are built
/// from this, never from the conversation's owner.
/// </summary>
/// <param name="Id">The poster's principal — the dashboard caller, or the chat user id.</param>
/// <param name="MayStartRuns">Whether the poster holds runs.control; a chat message carries none.</param>
public sealed record TurnPoster(string Id, bool MayStartRuns);
