using AgentSmith.Server.Services.SpecDialog;

namespace AgentSmith.Tests.TestHelpers;

/// <summary>
/// 2026-09-28-1da5b: the work the spec-dialog ingest route starts and does not await, kept so a
/// test can wait for it to finish.
/// <para>
/// These tests used to synchronise on the message the route pushed when a conversation opened —
/// which is a line written for a person, not a completion. When the dashboard stopped announcing
/// its openings the class became flaky: the same twenty-four tests, two of them failing, and a
/// different two each run. A handle is the honest signal.
/// </para>
/// </summary>
internal sealed class RecordingDispatchedWork : IDispatchedWork
{
    private readonly List<Task> _work = [];

    public void Started(Task work)
    {
        lock (_work) _work.Add(work);
    }

    /// <summary>Waits for everything dispatched so far — including work started by that work.</summary>
    public async Task SettleAsync()
    {
        while (true)
        {
            Task[] pending;
            lock (_work) pending = [.. _work];
            if (pending.Length == 0) return;
            await Task.WhenAll(pending);
            lock (_work)
            {
                if (_work.Count == pending.Length)
                {
                    _work.Clear();
                    return;
                }
            }
        }
    }
}
