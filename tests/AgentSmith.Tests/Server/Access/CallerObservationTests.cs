using AgentSmith.Contracts.Models.Access;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Server.Contracts;
using AgentSmith.Server.Security;
using AgentSmith.Server.Services.Hosting;
using AgentSmith.Tests.Server.Auth;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using System.Security.Claims;
using Xunit;

namespace AgentSmith.Tests.Server.Access;

/// <summary>
/// 2026-08-26-7a51: a caller is NOTED, not logged in. There is no sign-in event to hook, so
/// the observation is coalesced in memory, written off the request path, and never a reason
/// to refuse anybody.
/// </summary>
public sealed class CallerObservationTests
{
    private static readonly TokenAuthorityConfig Auth = new();

    [Fact]
    public async Task Observation_ManyRequestsFromOneCaller_WritesOnceNotPerRequest()
    {
        var clock = new SteppableClock();
        var buffer = new CallerObservationBuffer(clock);
        var store = new CountingObservedCallerStore();
        var resolver = ResolverUnderTest.Resolver(Source(), ResolverUnderTest.Grant(null), buffer);
        var caller = ResolverUnderTest.Caller(Auth, ("sub", "ada-0001"));

        for (var i = 0; i < 50; i++) resolver.Resolve(caller);
        await Flush(store, buffer).FlushAsync(CancellationToken.None);

        store.Batches.Should().ContainSingle().Which.Should().ContainSingle()
            .Which.Subject.Should().Be("ada-0001");
    }

    [Fact]
    public void Observation_StoreUnavailable_TheCallerIsStillServed()
    {
        var resolver = ResolverUnderTest.Resolver(
            Source(), ResolverUnderTest.Grant("sub:ada-0001"), new ThrowingObservations());

        var identity = resolver.Resolve(ResolverUnderTest.Caller(Auth, ("sub", "ada-0001")));

        identity.Authenticated.Should().BeTrue();
        identity.Roles.Should().Contain(BuiltInRoles.Admin);
    }

    [Fact]
    public void Observation_Refused_RecordsNothing()
    {
        var buffer = new CallerObservationBuffer(new SteppableClock());
        var resolver = ResolverUnderTest.Resolver(Source(), ResolverUnderTest.Grant(null), buffer);

        // A caller who presented nothing and one whose token the server refused arrive here
        // as the same anonymous principal — and neither of them was ever here.
        resolver.Resolve(new ClaimsPrincipal(new ClaimsIdentity()));

        buffer.Drain().Should().BeEmpty();
    }

    [Fact]
    public void Observation_GroupOverage_IsMarkedRatherThanRecordedAsNoGroups()
    {
        var buffer = new CallerObservationBuffer(new SteppableClock());
        var resolver = ResolverUnderTest.Resolver(Source(), ResolverUnderTest.Grant(null), buffer);

        resolver.Resolve(ResolverUnderTest.Caller(Auth, ("sub", "ada-0001"), ("_claim_names", "{}")));

        var noted = buffer.Drain().Should().ContainSingle().Subject;
        noted.GroupValues.Should().BeEmpty();
        noted.GroupsOmitted.Should().BeTrue(
            "the directory left the claim out; recording that as 'carries no groups' is a fact that is not true");
    }

    [Fact]
    public async Task Observation_BeyondTheRetentionWindow_IsRemoved()
    {
        using var h = new AccessTestHarness();
        var clock = new SteppableClock();
        await h.Observed.UpsertAsync(
            [Seen("ancient", clock.GetUtcNow().AddDays(-120)), Seen("recent", clock.GetUtcNow().AddDays(-3))],
            CancellationToken.None);

        await new ObservedCallerRetentionHostedService(
                h.Observed, h.Mapping, clock, NullLogger<ObservedCallerRetentionHostedService>.Instance)
            .SweepAsync(CancellationToken.None);

        (await h.Observed.AllAsync(CancellationToken.None))
            .Select(c => c.Subject).Should().Equal("recent");
    }

    private static ObservedCaller Seen(string subject, DateTimeOffset at) =>
        new(subject, "sub", subject, [], [], GroupsOmitted: false, at, at);

    private static RoleMappingSource Source() =>
        new(new StoredMappingStub(new RoleMappingConfig()), Auth);

    private static CallerObservationFlushHostedService Flush(
        IObservedCallerStore store, CallerObservationBuffer buffer) =>
        new(store, buffer, NullLogger<CallerObservationFlushHostedService>.Instance);

    private sealed class SteppableClock : TimeProvider
    {
        private DateTimeOffset _now = new(2026, 8, 26, 8, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan by) => _now = _now.Add(by);
    }

    private sealed class ThrowingObservations : ICallerObservations
    {
        public void Observe(ObservedCaller caller) => throw new InvalidOperationException("the store is down");
    }

    private sealed class CountingObservedCallerStore : IObservedCallerStore
    {
        public List<IReadOnlyList<ObservedCaller>> Batches { get; } = [];

        public Task UpsertAsync(IReadOnlyList<ObservedCaller> callers, CancellationToken ct)
        {
            Batches.Add(callers);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<ObservedCaller>> AllAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<ObservedCaller>>([.. Batches.SelectMany(b => b)]);

        public Task<bool> RemoveAsync(string subject, CancellationToken ct) => Task.FromResult(false);

        public Task<int> RemoveSeenBeforeAsync(DateTimeOffset cut, CancellationToken ct) => Task.FromResult(0);
    }
}
