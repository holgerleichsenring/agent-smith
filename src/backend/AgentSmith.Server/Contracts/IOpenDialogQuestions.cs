namespace AgentSmith.Server.Contracts;

/// <summary>
/// 2026-10-26-0f19b: which question a spec-dialog session is waiting on with buttons, readable by every
/// replica. The replica that asks keeps its own wait in process; a click the chat platform delivers to
/// another replica is answered only when this record still names the clicked question, so a click on
/// an expired or replaced question answers nothing.
/// </summary>
public interface IOpenDialogQuestions
{
    Task OpenAsync(string sessionId, string questionId, TimeSpan waitsFor, CancellationToken cancellationToken);

    Task CloseAsync(string sessionId, string questionId, CancellationToken cancellationToken);

    Task<bool> IsOpenAsync(string sessionId, string questionId, CancellationToken cancellationToken);
}
