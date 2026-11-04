using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Services.References;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.TestHelpers;

/// <summary>
/// 2026-11-04-96a0c: the coverage reader and the read recorder over one unit of work. Both open a scope of their own
/// per call, so the scope here hands out repositories over the store the test holds; a test whose turn opened no
/// readable file passes a mocked unit of work, which nothing then touches.
/// </summary>
internal static class ReferenceReads
{
    internal static ReferenceCoverageReader Coverage(IUnitOfWork store) => new(Scopes(store));

    internal static ReferenceReadRecorder Recorder(IUnitOfWork store) =>
        new(Scopes(store), NullLogger<ReferenceReadRecorder>.Instance);

    internal static IServiceScopeFactory Scopes(IUnitOfWork store) =>
        new ServiceCollection()
            .AddScoped(_ => new ReferenceSetRepository(store))
            .AddScoped(_ => new ReferenceReadRepository(store))
            .BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
}
