using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Extensions;
using AgentSmith.Server.Security;
using AgentSmith.Server.Services;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Server.Auth;

/// <summary>2026-10-15-4a4db: the server composition resolves what the project scope is handed.</summary>
[Trait("TestProcess", "env-1")]
[Collection(TestSupport.EnvVarCollection.Name)]
public sealed class ProjectScopeCompositionTests
{
    [Fact]
    public void ProjectScope_ServerComposition_ResolvesAccessReaderResolverAndMessageScope()
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddProvider(NullLoggerProvider.Instance));
        ServerCompositionBuilder.ConfigureServices(services, "agentsmith.yml");
        services.AddDashboardApi();

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        using var scope = provider.CreateScope();
        var sp = scope.ServiceProvider;
        sp.GetRequiredService<ProjectAccess>().Should().NotBeNull();
        sp.GetRequiredService<ResourceProjectReader>().Should().NotBeNull();
        sp.GetRequiredService<ProjectSourceResolver>().Should().NotBeNull();
        sp.GetRequiredService<DialogMessageScope>().Should().NotBeNull();
    }

    [Fact]
    public void ProjectScope_ServerComposition_ResolvesCallerProjectScopeAndTheListServices()
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddProvider(NullLoggerProvider.Instance));
        ServerCompositionBuilder.ConfigureServices(services, "agentsmith.yml");
        services.AddDashboardApi();

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        using var scope = provider.CreateScope();
        var sp = scope.ServiceProvider;
        sp.GetRequiredService<CallerProjectScope>().Should().NotBeNull();
        sp.GetRequiredService<AgentSmith.Server.Services.SpecDialog.ProjectScopedCatalog>().Should().NotBeNull();
        sp.GetRequiredService<AgentSmith.Server.Services.Tickets.TicketProjects>().Should().NotBeNull();
    }
}
