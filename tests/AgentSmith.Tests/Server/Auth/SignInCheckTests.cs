using AgentSmith.Application.Services.Preflight;
using AgentSmith.Contracts.Models.Access;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Preflight;
using AgentSmith.Contracts.Services;
using AgentSmith.Server.Security;
using AgentSmith.Server.Services.Preflight;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Tests.Server.Auth;

/// <summary>
/// 2026-09-14-3f5b: the two questions an operator does not know to ask until enforcement is
/// already on — has a token ever got through here, and is there an administrator this server
/// can see. Both are answered from facts the server already holds.
/// </summary>
public sealed class SignInCheckTests
{
    private const string Authority = "https://login.example/realm";

    [Fact]
    public async Task SignIn_NoAuthorityConfigured_SkipsWithTheReason()
    {
        var result = await Check(new TokenAuthorityConfig()).RunAsync(default);

        result.Status.Should().Be(PreflightStatus.Skip);
        result.Message.Should().Contain("no authority is configured");
    }

    [Fact]
    public async Task SignIn_EnforcementOffAndNothingSeen_PassesAndSaysWhatIsStillUnproven()
    {
        var result = await Check(Auth(enforce: false)).RunAsync(default);

        result.Status.Should().Be(PreflightStatus.Pass, "with nothing refused, nothing is urgent");
        result.Message.Should().Contain("enforcement OFF");
        result.Message.Should().Contain("no caller accepted");
        result.Message.Should().Contain("no administrator visible");
    }

    [Fact]
    public async Task SignIn_EnforcementOnAndTheStoreIsEmptyButReachable_FailsAndNamesTheRetentionWindow()
    {
        var mapping = Mapping(g => g.PersonGrants.Add(AdminOf("someone")));

        var result = await Check(Auth(), mapping).RunAsync(default);

        result.Status.Should().Be(PreflightStatus.Fail);
        result.Message.Should().Contain($"last {RoleMappingConfig.DefaultObservationRetentionDays} days",
            "an installation nobody signed into for the retention window has lost its own "
            + "evidence, and 'never' and 'not lately' are different situations");
        result.FixHint.Should().NotBeNullOrWhiteSpace("a red check nobody can act on is worthless");
    }

    [Fact]
    public async Task SignIn_EnforcementOnAndTheStoreCannotBeRead_SaysSoRatherThanReportingNobodySignedIn()
    {
        var mapping = Mapping(g => g.PersonGrants.Add(AdminOf("someone")));

        var result = await Check(Auth(), mapping, new UnreadableStore()).RunAsync(default);

        result.Status.Should().Be(PreflightStatus.Pass,
            "a database that could not be read says nothing about who has signed in");
        result.Message.Should().Contain("could not be read");
        result.Message.Should().NotContain("no caller accepted");
    }

    [Fact]
    public async Task SignIn_EnforcementOnAndACallerWasSeenInTheWindow_PassesNamingWhenItWasSeen()
    {
        var seen = new DateTimeOffset(2026, 9, 12, 8, 0, 0, TimeSpan.Zero);
        var mapping = Mapping(g => g.PersonGrants.Add(AdminOf("someone")));

        var result = await Check(Auth(), mapping, Store(Caller("alice", seen: seen))).RunAsync(default);

        result.Status.Should().Be(PreflightStatus.Pass);
        result.Message.Should().Contain("2026-09-12");
    }

    [Fact]
    public async Task SignIn_EnforcementOnAndNoVisibleAdmin_FailsNamingTheFourRoutes()
    {
        var result = await Check(Auth(), new RoleMappingConfig(), Store(Caller("alice"))).RunAsync(default);

        result.Status.Should().Be(PreflightStatus.Fail);
        result.Message.Should().Contain("no administrator can be seen from here",
            "the role-claim route is real and invisible, so this is never 'nobody is an administrator'");
        result.FixHint.Should().Contain(AdminGrant.EnvVar);
    }

    [Fact]
    public async Task SignIn_AdminOnlyReachableThroughAnObservedRoleClaim_IsNotReportedAsALockout()
    {
        // Nothing is granted here and nothing is mapped: the directory put the role in the
        // claim, and the only reason this server knows is that somebody arrived carrying it.
        var store = Store(Caller("alice", roles: ["admin"]));

        var result = await Check(Auth(), new RoleMappingConfig(), store).RunAsync(default);

        result.Status.Should().Be(PreflightStatus.Pass);
        result.Message.Should().Contain("observed role claim");
    }

    [Fact]
    public async Task SignIn_EnvironmentGrantNamesSomebody_CountsAsAnAdminRoute()
    {
        var result = await Check(
            Auth(), new RoleMappingConfig(), Store(Caller("alice")), grant: "sub:0a1b2c").RunAsync(default);

        result.Status.Should().Be(PreflightStatus.Pass);
        result.Message.Should().Contain(AdminGrant.EnvVar);
    }

    [Fact]
    public void Preflight_TheSharedRegistration_DoesNotCarryTheSignInCheck()
    {
        // Two of its four facts are internal to the server assembly and a third resolves to
        // nothing in the CLI's graph, so a shared registration would be a doctor that throws.
        var services = new ServiceCollection();
        services.AddPreflight();

        services.Where(d => d.ServiceType == typeof(IPreflightCheck))
            .Select(d => d.ImplementationType)
            .Should().NotContain(typeof(SignInCheck));
    }

    private static SignInCheck Check(
        TokenAuthorityConfig auth,
        RoleMappingConfig? mapping = null,
        IObservedCallerStore? observed = null,
        string? grant = null)
    {
        var source = new RoleMappingSource(new StoredMappingStub(mapping ?? new RoleMappingConfig()), auth);
        source.AdoptStore();
        return new SignInCheck(auth, source, ResolverUnderTest.Grant(grant), observed ?? Store());
    }

    private static TokenAuthorityConfig Auth(bool enforce = true) =>
        new() { Authority = Authority, Audience = "agent-smith", Enforce = enforce };

    private static RoleMappingConfig Mapping(Action<RoleMappingConfig> build)
    {
        var mapping = new RoleMappingConfig();
        build(mapping);
        return mapping;
    }

    private static PersonGrant AdminOf(string value) =>
        new() { Claim = "sub", Value = value, Roles = [BuiltInRoles.Admin] };

    private static ObservedCaller Caller(
        string subject, DateTimeOffset? seen = null, IReadOnlyList<string>? roles = null)
    {
        var at = seen ?? DateTimeOffset.UtcNow;
        return new ObservedCaller(subject, "sub", subject, roles ?? [], [], false, at, at);
    }

    private static IObservedCallerStore Store(params ObservedCaller[] callers) => new StoreStub(callers);

    private sealed class StoreStub(IReadOnlyList<ObservedCaller> callers) : IObservedCallerStore
    {
        public Task UpsertAsync(IReadOnlyList<ObservedCaller> c, CancellationToken ct) => Task.CompletedTask;
        public Task<IReadOnlyList<ObservedCaller>> AllAsync(CancellationToken ct) => Task.FromResult(callers);
        public Task<bool> RemoveAsync(string subject, CancellationToken ct) => Task.FromResult(false);
        public Task<int> RemoveSeenBeforeAsync(DateTimeOffset cut, CancellationToken ct) => Task.FromResult(0);
    }

    private sealed class UnreadableStore : IObservedCallerStore
    {
        public Task UpsertAsync(IReadOnlyList<ObservedCaller> c, CancellationToken ct) => Task.CompletedTask;
        public Task<IReadOnlyList<ObservedCaller>> AllAsync(CancellationToken ct) =>
            throw new InvalidOperationException("the database could not be reached");
        public Task<bool> RemoveAsync(string subject, CancellationToken ct) => Task.FromResult(false);
        public Task<int> RemoveSeenBeforeAsync(DateTimeOffset cut, CancellationToken ct) => Task.FromResult(0);
    }
}
