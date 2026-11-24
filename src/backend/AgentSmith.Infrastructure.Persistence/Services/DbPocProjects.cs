using AgentSmith.Contracts.Pocs;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Infrastructure.Persistence.Services;

/// <summary>2026-11-23-933db: a project is a server-provisioned PoC project when its key starts with 'poc-' and a Poc row
/// names it — only the provisioner's projects ever get rows. A scope per question, for the run prologue and the sweep.</summary>
public sealed class DbPocProjects(IServiceScopeFactory scopeFactory) : IPocProjects
{
    public async Task<bool> IsAsync(string project, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(project);
        if (!project.StartsWith(IPocProjects.Prefix, StringComparison.OrdinalIgnoreCase)) return false;
        using var scope = scopeFactory.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().Set<Poc>().AnyAsync(p => p.Project == project, ct);
    }
}
