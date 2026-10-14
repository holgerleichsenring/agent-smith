using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Services.SpecDialog;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Tests.TestSupport;

/// <summary>The filing stamp reading the transcript through <paramref name="repository"/>, for tests whose
/// subject is the conversation rather than the stamp's fresh scope.</summary>
internal static class RatifiedMockStamps
{
    public static RatifiedMockStamp Over(SpecDialogSessionRepository repository) =>
        new(new ServiceCollection().AddSingleton(repository).BuildServiceProvider()
            .GetRequiredService<IServiceScopeFactory>());
}
