using AgentSmith.Contracts.Dialogue;

namespace AgentSmith.Infrastructure.Services.Dialogue;

/// <summary>
/// Simple in-memory implementation that accumulates dialogue entries during a pipeline run.
/// Thread-safe via locking.
/// </summary>
public sealed class InMemoryDialogueTrail : IDialogueTrail
{
    private readonly List<DialogTrailEntry> _entries = [];
    private readonly object _lock = new();

    public Task RecordAsync(DialogQuestion question, DialogAnswer answer)
    {
        lock (_lock)
        {
            _entries.Add(new DialogTrailEntry(question, answer));
        }

        return Task.CompletedTask;
    }

    public IReadOnlyList<DialogTrailEntry> GetAll()
    {
        lock (_lock)
        {
            return _entries.ToList().AsReadOnly();
        }
    }
}
