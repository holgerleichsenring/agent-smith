using System.Security.Claims;
using AgentSmith.Contracts.Deployment;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Extensions;
using AgentSmith.Server.Models;
using AgentSmith.Server.Security;
using AgentSmith.Tests.Server.Auth;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using static AgentSmith.Tests.Deployment.DeploymentsPageSeed;
using static AgentSmith.Tests.Server.Auth.ProjectBindingTests;

namespace AgentSmith.Tests.Deployment;

/// <summary>2026-11-23-8df6a: GET /api/deployments answers the caller's projects only — a project-bound reader sees their
/// project, a global reader without admin.assistant none of the installation — and refuses a window it cannot form.</summary>
public sealed class DeploymentsPageEndpointTests : IDisposable
{
    private static readonly TokenAuthorityConfig Auth = new() { Enforce = true, NameClaim = "preferred_username" };
    private readonly Microsoft.Data.Sqlite.SqliteConnection _connection = MigratedStoreTemplate.OpenCopy();
    private readonly DeploymentsPageSeed _seed;
    private readonly CallerProjectScope _scopes;

    public DeploymentsPageEndpointTests()
    {
        _seed = new(_connection);
        var mapping = Mapping(Binding("alpha", groups: ["alpha-team"], roles: [BuiltInRoles.Reader]));
        mapping.GroupRoles["readers"] = [BuiltInRoles.Reader];
        _scopes = new CallerProjectScope(ProjectScopeUnderTest.Access(
            Auth, ResolverUnderTest.Over(Auth, new StoredMappingStub(mapping)), "alpha", "beta"));
    }

    public void Dispose() => _connection.Dispose();

    [Fact]
    public async Task Get_ProjectBoundCaller_SeesOnlyTheirProjects()
    {
        await _seed.MergedAsync("alpha", "r-a", Now.AddDays(-1));
        await _seed.MergedAsync("beta", "r-b", Now.AddDays(-1));
        await _seed.DeployedAsync("alpha", "d-a", Now.AddDays(-1));
        await _seed.DeployedAsync("beta", "d-b", Now.AddDays(-1));

        var page = Page(await GetAsync(null, "alpha-team"));

        page.Merged.Select(m => (m.Project, m.MergeSha)).Should().Equal(("alpha", "m1"));
        page.Deploys.Select(d => d.RunId).Should().Equal("d-a");
    }

    [Fact]
    public async Task Get_GlobalHolderWithHiddenProject_SeesNoneOfIt()
    {
        await _seed.MergedAsync("alpha", "r-a", Now.AddDays(-1));
        await _seed.MergedAsync(InstallationScope.Name, "r-h", Now.AddDays(-1));
        await _seed.DeployedAsync(InstallationScope.Name, "d-h", Now.AddDays(-1));

        var page = Page(await GetAsync(30, "readers"));

        page.Merged.Select(m => m.Project).Should().Equal("alpha");
        page.Deploys.Should().BeEmpty();
    }

    [Fact]
    public async Task Get_DaysBelowOne_Is400()
    {
        Status(await GetAsync(0, "readers")).Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task Get_DaysPastTheCalendar_Is400()
    {
        var answer = await GetAsync(int.MaxValue, "readers");

        Status(answer).Should().Be(StatusCodes.Status400BadRequest);
        answer.Should().BeOfType<BadRequest<string>>().Which.Value.Should().Contain("days");
    }

    private async Task<IResult> GetAsync(int? days, string group)
    {
        await using var ctx = MigratedStoreTemplate.Context(_connection);
        return await DeploymentsPageEndpoints.GetAsync(days, Caller(group), _scopes, new PrReviewRecordRepository(ctx),
            new DeployRecordRepository(ctx), new SettableClock { Now = Now }, default);
    }

    private static DeploymentsPageView Page(IResult result) => result.Should().BeOfType<Ok<DeploymentsPageView>>().Which.Value!;

    private static int? Status(IResult result) => (result as IStatusCodeHttpResult)?.StatusCode;

    private static ClaimsPrincipal Caller(string group) => ResolverUnderTest.Caller(Auth, ("groups", group));
}
