using AgentSmith.Contracts.Events;
using AgentSmith.Infrastructure.Persistence.Contracts;

namespace AgentSmith.Infrastructure.Persistence.Services;

/// <summary>
/// 2026-10-13-9a59d: the sandbox family — a sandbox's lifetime and (p0369) the command facts folded
/// onto the run's metrics summary. 2026-10-13-9a59i: the run's end names its sandboxes' release.
/// </summary>
public sealed class RunSandboxFamily(RunSandboxProjection sandboxes, SandboxEndFold endFold) : IRunEventFamily
{
    public IReadOnlyCollection<Type> Events { get; } =
    [
        typeof(SandboxResultEvent), typeof(SandboxCreatedEvent), typeof(SandboxDisposedEvent), typeof(SandboxVanishedEvent),
        typeof(RunFinishedEvent),
    ];

    public Task ApplyAsync(IUnitOfWork uow, RunEvent runEvent, long? eventSeq, CancellationToken ct) => runEvent switch
    {
        SandboxResultEvent e => sandboxes.FoldResultAsync(uow, e, eventSeq, ct),
        SandboxCreatedEvent e => sandboxes.CreateAsync(uow, e, eventSeq, ct),
        SandboxDisposedEvent e => sandboxes.DisposeAsync(uow, e, eventSeq, ct),
        SandboxVanishedEvent e => sandboxes.MarkVanishedAsync(uow, e, eventSeq, ct),
        RunFinishedEvent e => endFold.FoldAsync(uow, e, eventSeq, ct),
        _ => Task.CompletedTask,
    };
}
