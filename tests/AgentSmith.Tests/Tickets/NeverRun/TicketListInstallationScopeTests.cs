using System.Security.Claims;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Server.Extensions;
using AgentSmith.Server.Models;
using AgentSmith.Server.Security;
using AgentSmith.Tests.Server.Auth;
using FluentAssertions;
using Microsoft.AspNetCore.Http.HttpResults;
using static AgentSmith.Tests.Server.Auth.ProjectBindingTests;
using static AgentSmith.Tests.Tickets.NeverRun.NeverRunFixtures;
using static AgentSmith.Tests.Tickets.TicketStoreHarness;

namespace AgentSmith.Tests.Tickets.NeverRun;

/// <summary>
/// 2026-11-23-47b6: a global reader without admin.assistant reads none of the installation's rows through the
/// ticket list — its pull-request records and its filings are hidden as its runs are — while an admin reads them.
/// </summary>
public sealed class TicketListInstallationScopeTests : IDisposable
{
    private static readonly TokenAuthorityConfig Auth = new() { Enforce = true, NameClaim = "preferred_username" };
    private readonly TicketStoreHarness _h = new();
    private readonly CallerProjectScope _scopes;

    public TicketListInstallationScopeTests()
    {
        var mapping = Mapping();
        mapping.GroupRoles["admins"] = [BuiltInRoles.Admin];
        mapping.GroupRoles["readers"] = [BuiltInRoles.Reader];
        _scopes = new CallerProjectScope(ProjectScopeUnderTest.Access(
            Auth, ResolverUnderTest.Over(Auth, new StoredMappingStub(mapping)), "alpha"));
    }

    public void Dispose() => _h.Dispose();

    [Fact]
    public async Task TicketListRead_GlobalReader_HoldsNoInstallationPrRecord_AnAdminDoes()
    {
        await _h.AddAsync(Record("alpha", "42"), Record(InstallationScope.Name, "7"));

        var reader = await _h.List.AllAsync(_scopes.For(Caller("readers"), Permissions.RunsRead), default);
        var admin = await _h.List.AllAsync(_scopes.For(Caller("admins"), Permissions.RunsRead), default);

        reader.PrRecords.Select(g => g.Key.Project).Should().Equal("alpha");
        admin.PrRecords.Select(g => g.Key.Project).Should().BeEquivalentTo(["alpha", InstallationScope.Name]);
    }

    [Fact]
    public async Task TicketListRoute_GlobalReader_ListsNoInstallationFiling_AnAdminDoes()
    {
        await _h.AddAsync(Filing("59"), Filing("80", project: InstallationScope.Name));

        (await ListedAsync("readers")).Should().Equal("alpha");
        (await ListedAsync("admins")).Should().BeEquivalentTo(["alpha", InstallationScope.Name]);
    }

    private async Task<IEnumerable<string>> ListedAsync(string group)
    {
        var result = await TicketQueryEndpoints.ListAsync(null, null, null, null, _h.List, Caller(group), _scopes, default);
        return result.Should().BeOfType<Ok<TicketListView>>().Which.Value!.Tickets.Select(t => t.Project);
    }

    private static PrReviewRecord Record(string project, string ticket)
    {
        var url = $"https://github.com/o/{project.TrimStart('@')}/pull/{ticket}";
        return new PrReviewRecord
        {
            PrKey = PrKey.Of(url), Url = url, Repo = "api", Number = ticket, Project = project, TicketId = ticket,
            RunId = "run-1", State = PrReviewStates.Open, CheckedAtTicks = T.UtcTicks,
        };
    }

    private static ClaimsPrincipal Caller(string group) => ResolverUnderTest.Caller(Auth, ("groups", group));
}
