namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// 2026-09-28-1da5b: a way to observe the work a request STARTS BUT DOES NOT AWAIT.
/// <para>
/// The spec-dialog ingest route answers before its turn has run — it must, because a turn runs a
/// master whose approval gate waits up to fifteen minutes. So it starts a task and drops the
/// handle, and from that moment nothing in the process can tell whether the work finished, failed
/// or is still going: not a caller, not a diagnostic, and not a test.
/// </para>
/// <para>
/// Nothing is registered for this in production, so the route behaves exactly as it did. What it
/// buys is that the work becomes OBSERVABLE to anything that chooses to look — which the tests of
/// this route need, because the only signal they had was a message written for a person, and a
/// message is not a completion.
/// </para>
/// </summary>
public interface IDispatchedWork
{
    /// <summary>Called as the work starts, with the task nobody is awaiting.</summary>
    void Started(Task work);
}
