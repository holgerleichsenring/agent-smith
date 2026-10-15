using System.Net.Http.Headers;
using System.Security.Claims;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Server.Contracts;
using AgentSmith.Server.Security;
using AgentSmith.Server.Services.SpecDialog;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AgentSmith.Tests.Server.Auth;

/// <summary>
/// 2026-10-15-4a4db: an enforcing authority over a migrated store and a configuration naming alpha, beta
/// and team/alpha. One group is bound to operator in alpha and team/alpha only; another holds admin.
/// </summary>
public class ProjectScopeAuthorityFixture : AuthorityFixture
{
    public const string AlphaTeam = "alpha-team";
    public const string Admins = "admins";

    internal StoredMappingStub Stored { get; } = new(new RoleMappingConfig
    {
        RoleClaim = "roles", GroupClaim = "groups",
        GroupRoles = new() { [Admins] = [BuiltInRoles.Admin] },
        ProjectBindings =
        [
            new() { Project = "alpha", Groups = [AlphaTeam], Roles = [BuiltInRoles.Operator] },
            new() { Project = "team/alpha", Groups = [AlphaTeam], Roles = [BuiltInRoles.Operator] },
        ],
    });

    /// <summary>How many design turns the message route handed to a background task.</summary>
    internal int Dispatched => _dispatched.Count;

    private readonly CountingWork _dispatched = new();

    protected virtual bool Enforce => true;

    protected override bool MigratesStore => true;

    protected override Action<IServiceCollection>? Services => services =>
    {
        services.RemoveAll<IStoredRoleMapping>();
        services.AddSingleton<IStoredRoleMapping>(Stored);
        services.RemoveAll<IConfigurationLoader>();
        services.AddSingleton<IConfigurationLoader>(
            new ProjectScopeUnderTest.FixedLoader(ProjectScopeUnderTest.Configured("alpha", "beta", "team/alpha")));
        services.AddSingleton<IDispatchedWork>(_dispatched);
    };

    protected override void Booted() => Server.Services.GetRequiredService<RoleMappingSource>().AdoptStore();

    protected override string AuthYaml(string authority) => $"""
        auth:
          authority: {authority}
          audience: {Audience}
          enforce: {(Enforce ? "true" : "false")}
          role_claim: roles
          group_claim: groups
          name_claim: sub
          group_roles:
            {Admins}:
              - admin
        """;

    /// <summary>A request carrying a token for a caller in this group.</summary>
    internal HttpRequestMessage As(string group, HttpMethod method, string path, HttpContent? body = null)
    {
        var request = new HttpRequestMessage(method, path) { Content = body };
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer", Issuer.Token(Audience, extra: [new Claim("groups", group)], subject: $"{group}-person"));
        return request;
    }

    internal async Task SeedAsync(Func<AgentSmithDbContext, Task> seed)
    {
        await using var scope = Server.Services.CreateAsyncScope();
        await seed(scope.ServiceProvider.GetRequiredService<AgentSmithDbContext>());
    }

    private sealed class CountingWork : IDispatchedWork
    {
        public int Count { get; private set; }

        public void Started(Task work) => Count++;
    }
}
