using AgentSmith.Contracts.Events;
using AgentSmith.Infrastructure.Persistence.Contracts;

namespace AgentSmith.Infrastructure.Persistence.Services;

/// <summary>
/// 2026-10-13-9a59d: the sandbox family — a sandbox's lifetime and (p0369) the command facts folded
/// onto the run's metrics summary.
/// </summary>
public sealed class RunSandboxFamily(RunSandboxProjection sandboxes) : IRunEventFamily
{
    public IReadOnlyCollection<Type> Events { get; } =
    [
        typeof(SandboxResultEvent), typeof(SandboxCreatedEvent), typeof(SandboxDisposedEvent), typeof(SandboxVanishedEvent),
    ];

    public Task ApplyAsync(IUnitOfWork uow, RunEvent runEvent, long? eventSeq, CancellationToken ct) => runEvent switch
    {
        SandboxResultEvent e => sandboxes.FoldResultAsync(uow, e, ct),
        SandboxCreatedEvent e => sandboxes.CreateAsync(uow, e, ct),
        SandboxDisposedEvent e => sandboxes.DisposeAsync(uow, e, ct),
        SandboxVanishedEvent e => sandboxes.MarkVanishedAsync(uow, e, ct),
        _ => Task.CompletedTask,
    };
}
