using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Preflight;
using AgentSmith.Contracts.Services;
using AgentSmith.Server.Extensions;
using AgentSmith.Contracts.Models.Access;
using AgentSmith.Server.Contracts;
using AgentSmith.Server.Security;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Tests.Server.Auth;

/// <summary>
/// 2026-09-23-2c60: the sign-in check is built with the auth block as a CONSTRUCTOR ARGUMENT,
/// and an installation that declares no <c>auth:</c> key has no block to pass. The check's own
/// first branch reports exactly that state — but ActivatorUtilities matches a supplied argument
/// to a parameter by its runtime type, and null has none, so the constructor was rejected and
/// the host died at startup instead. The check's behaviour was covered; the path that builds it
/// was not.
/// </summary>
public sealed class SignInCheckRegistrationTests
{
    [Fact]
    public void Preflight_NoAuthBlockDeclared_StillBuildsTheSignInCheck()
    {
        using var provider = Provider(auth: null);

        var checks = provider.GetServices<IPreflightCheck>();

        checks.Should().Contain(c => c.Name == "sign-in",
            "an installation with no auth: key is the state the check exists to report");
    }

    [Fact]
    public async Task Preflight_NoAuthBlockDeclared_SignInSkipsWithTheReason()
    {
        using var provider = Provider(auth: null);

        var signIn = provider.GetServices<IPreflightCheck>().Single(c => c.Name == "sign-in");
        var result = await signIn.RunAsync(default);

        result.Status.Should().Be(PreflightStatus.Skip);
        result.Message.Should().Contain("no authority is configured");
    }

    private static ServiceProvider Provider(TokenAuthorityConfig? auth)
    {
        var services = new ServiceCollection();
        services.AddSingleton(_ => new AdminGrant(_ => null));
        services.AddSingleton<IStoredRoleMapping>(new NoStoredRoleMapping());
        services.AddSingleton(sp => new RoleMappingSource(
            sp.GetRequiredService<IStoredRoleMapping>(), auth ?? new TokenAuthorityConfig()));
        services.AddSingleton<IObservedCallerStore>(new NobodySeen());
        services.AddSignInCheck(auth);
        return services.BuildServiceProvider();
    }

    private sealed class NoStoredRoleMapping : IStoredRoleMapping
    {
        public RoleMappingConfig? Read() => null;
    }

    private sealed class NobodySeen : IObservedCallerStore
    {
        public Task UpsertAsync(IReadOnlyList<ObservedCaller> c, CancellationToken ct) => Task.CompletedTask;

        public Task<IReadOnlyList<ObservedCaller>> AllAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<ObservedCaller>>([]);

        public Task<bool> RemoveAsync(string subject, CancellationToken ct) => Task.FromResult(false);

        public Task<int> RemoveSeenBeforeAsync(DateTimeOffset cutoff, CancellationToken ct) =>
            Task.FromResult(0);
    }
}
