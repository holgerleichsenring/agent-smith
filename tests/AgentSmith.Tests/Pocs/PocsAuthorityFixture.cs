using System.Net.Http.Headers;
using System.Security.Claims;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Server.Contracts;
using AgentSmith.Server.Security;
using AgentSmith.Tests.Server.Auth;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AgentSmith.Tests.Pocs;

/// <summary>
/// 2026-11-23-933da: a booted server over a migrated store whose configuration names poc-ann and poc-bob and switches
/// PoCs on with one target. ann and bob hold the poc role and the reader role in their own poc- project; operators,
/// readers and admins hold their built-in role globally. <see cref="Pocs"/> is the live block, so a test may flip it.
/// </summary>
public class PocsAuthorityFixture : AuthorityFixture
{
    public const string Ann = "ann";
    public const string Bob = "bob";
    public const string Operators = "operators";
    public const string Readers = "readers";
    public const string Admins = "admins";

    /// <summary>2026-11-23-933dc: hold pocs.overrule (and pocs.read), and runs.read only in poc-bob.</summary>
    public const string Overrulers = "overrulers";

    /// <summary>2026-11-23-933dc: hold pocs.overrule and the global reader role.</summary>
    public const string ReadingOverrulers = "reading-overrulers";

    internal PocsConfig Pocs { get; } = new()
    {
        Enabled = true, ProjectAgent = "claude", ProjectTracker = "issues",
        Targets =
        [
            new PocTargetConfig
            {
                Name = "main", SourceRepo = "gh/poc-src", DomainPattern = "{app}.poc.example.internal",
                Registry = PocsConfigTests.SoundRegistry(),
            },
        ],
        Scan = new PocScanConfig { OsvImage = PocsConfigTests.PinnedOsvImage, TrivyImage = PocsConfigTests.PinnedTrivyImage },
    };

    protected virtual bool Enforce => true;

    protected override bool MigratesStore => true;

    protected override Action<IServiceCollection>? Services => services =>
    {
        var config = ProjectScopeUnderTest.Configured("poc-ann", "poc-bob");
        services.RemoveAll<IStoredRoleMapping>();
        services.AddSingleton<IStoredRoleMapping>(new StoredMappingStub(Mapping()));
        services.RemoveAll<IConfigurationLoader>();
        services.AddSingleton<IConfigurationLoader>(new ProjectScopeUnderTest.FixedLoader(new AgentSmithConfig
        {
            Projects = config.Projects, Pocs = Pocs,
        }));
    };

    protected override string AuthYaml(string authority) => $"""
        auth:
          authority: {authority}
          audience: {Audience}
          enforce: {(Enforce ? "true" : "false")}
          role_claim: roles
          group_claim: groups
          name_claim: sub
        """;

    /// <summary>A request carrying a token for a caller in this group, or none.</summary>
    internal HttpRequestMessage As(string? group, string path)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        if (group is not null)
            request.Headers.Authorization = new AuthenticationHeaderValue(
                "Bearer", Issuer.Token(Audience, extra: [new Claim("groups", group)], subject: $"{group}-person"));
        return request;
    }

    internal async Task SeedAsync(Func<AgentSmithDbContext, Task> seed)
    {
        await using var scope = Server.Services.CreateAsyncScope();
        await seed(scope.ServiceProvider.GetRequiredService<AgentSmithDbContext>());
    }

    private static RoleMappingConfig Mapping() => new()
    {
        RoleClaim = "roles", GroupClaim = "groups",
        GroupRoles = new()
        {
            [Admins] = [BuiltInRoles.Admin], [Operators] = [BuiltInRoles.Operator], [Readers] = [BuiltInRoles.Reader],
            [Ann] = [BuiltInRoles.Poc], [Bob] = [BuiltInRoles.Poc],
            [Overrulers] = ["overruler"], [ReadingOverrulers] = ["overruler", BuiltInRoles.Reader],
        },
        Roles = new() { ["overruler"] = [Permissions.PocsOverrule, Permissions.PocsRead] },
        ProjectBindings =
        [
            new() { Project = "poc-ann", Groups = [Ann], Roles = [BuiltInRoles.Reader] },
            new() { Project = "poc-bob", Groups = [Bob], Roles = [BuiltInRoles.Reader] },
            new() { Project = "poc-bob", Groups = [Overrulers], Roles = [BuiltInRoles.Reader] },
        ],
    };
}
