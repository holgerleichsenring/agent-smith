using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Persistence.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Infrastructure.Persistence.Services;

/// <summary>
/// The IActiveRunLease facade for the singleton claim path. The claim path is NOT
/// a web request, so it opens a SCOPE per operation and delegates to the scoped
/// <see cref="ActiveRunRepository"/> (which uses the scoped unit of work). This is
/// how a background/singleton entry point gets a scoped DbContext — no
/// IDbContextFactory, the DbContext stays abstracted behind IUnitOfWork.
/// </summary>
public sealed class DbActiveRunLease(IServiceScopeFactory scopeFactory) : IActiveRunLease
{
    public Task<LeaseClaimOutcome> TryClaimAsync(string project, TicketId ticketId, CancellationToken ct)
        => InScope(r => r.TryClaimAsync(project, ticketId, ct));

    public Task ReleaseAsync(string project, TicketId ticketId, CancellationToken ct)
        => InScope(r => r.ReleaseAsync(project, ticketId, ct));

    public Task AttachRunAsync(string project, TicketId ticketId, string runId, string? jobId, CancellationToken ct)
        => InScope(r => r.AttachRunAsync(project, ticketId, runId, jobId, ct));

    public Task RenewHeartbeatAsync(string project, TicketId ticketId, CancellationToken ct)
        => InScope(r => r.RenewHeartbeatAsync(project, ticketId, ct));

    public Task<IReadOnlyList<StaleLease>> FindStaleAsync(TimeSpan olderThan, CancellationToken ct)
        => InScope(r => r.FindStaleAsync(olderThan, ct));

    public Task<StaleLease?> GetByTicketAsync(string project, TicketId ticketId, CancellationToken ct)
        => InScope(r => r.GetByTicketAsync(project, ticketId, ct));

    public Task<IReadOnlyCollection<string>> GetActiveRunIdsAsync(TimeSpan freshFor, CancellationToken ct)
        => InScope(r => r.GetActiveRunIdsAsync(freshFor, ct));

    private async Task<T> InScope<T>(Func<ActiveRunRepository, Task<T>> op)
    {
        using var scope = scopeFactory.CreateScope();
        return await op(scope.ServiceProvider.GetRequiredService<ActiveRunRepository>());
    }

    private async Task InScope(Func<ActiveRunRepository, Task> op)
    {
        using var scope = scopeFactory.CreateScope();
        await op(scope.ServiceProvider.GetRequiredService<ActiveRunRepository>());
    }
}
