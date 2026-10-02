namespace AgentSmith.Application.Services.Lifecycle;

/// <summary>
/// 2026-10-02-5ab2b: a popped request this consumer may start. When it won its row's claim,
/// the row is beaten from the pop until the request's run ends — the semaphore wait and the
/// prologue included — so "claimed, no fresh beat" means the consumer is really gone.
/// Disposing stops the beat.
/// </summary>
public sealed class ClaimedRunStart(CancellationTokenSource? beat, Task? beating) : IAsyncDisposable
{
    public bool IsBeating => beat is not null;

    public async ValueTask DisposeAsync()
    {
        if (beat is null) return;
        await beat.CancelAsync();
        try { await beating!; }
        catch (OperationCanceledException) { /* the beat ends with its token */ }
        beat.Dispose();
    }
}
