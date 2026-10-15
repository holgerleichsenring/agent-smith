using System.Security.Claims;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Server.Hubs;
using AgentSmith.Server.Security;
using AgentSmith.Server.Services.Events;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using static AgentSmith.Tests.Server.Auth.ProjectBindingTests;

namespace AgentSmith.Tests.Server.Auth;

/// <summary>
/// 2026-10-15-4a4dc: the hub filter scopes a project-bound caller by the run or dialog an invocation
/// names. A foreign run answers what an unknown run answers, a foreign dialog what another person's
/// dialog answers, and every caller without a binding meets today's filter with no lookup.
/// </summary>
public sealed class HubFilterScopeTests : IDisposable
{
    private const string UnknownRun = "is visible to this caller";
    private const string Owner = "belongs to another principal";

    private readonly HubProjectStore _store = new();

    public void Dispose() => _store.Dispose();

    [Fact]
    public async Task HubFilter_EnforceOff_ProceedsWithoutResolvingAProject()
    {
        var outcome = await InvokeAsync(Caller("alpha-team"), nameof(JobsHub.SubscribeRun), ["r-beta"], enforce: false);

        outcome.Should().BeNull();
        _store.Reads.Should().Be(0);
    }

    [Fact]
    public async Task HubFilter_GlobalReader_ProceedsWithoutResolvingAProject()
    {
        var outcome = await InvokeAsync(Caller("everyone"), nameof(JobsHub.SubscribeRun), ["r-beta"]);

        outcome.Should().BeNull();
        _store.Reads.Should().Be(0);
    }

    [Fact]
    public async Task HubFilter_UnboundCallerLackingWatch_ExpandSandbox_NamesTheMissingWatchWithoutResolvingAProject()
    {
        var outcome = await InvokeAsync(Caller("global-run-readers"), nameof(JobsHub.ExpandSandbox), ["r-none", "repo"]);

        outcome.Should().Contain(Permissions.RunsWatch);
        _store.Reads.Should().Be(0);
    }

    [Fact]
    public async Task HubFilter_ProjectReader_SubscribeRunInTheirProject_Proceeds()
    {
        await _store.SeedAsync(db => ProjectScopeRows.RunAsync(db, "r-alpha", "alpha"));

        (await InvokeAsync(Caller("alpha-team"), nameof(JobsHub.SubscribeRun), ["r-alpha"])).Should().BeNull();
    }

    [Fact]
    public async Task HubFilter_ProjectReader_SubscribeRunInAnotherProject_IsRefusedWithTheUnknownRunSentence()
    {
        await _store.SeedAsync(db => ProjectScopeRows.RunAsync(db, "r-beta", "beta"));

        (await InvokeAsync(Caller("alpha-team"), nameof(JobsHub.SubscribeRun), ["r-beta"]))
            .Should().Be("No run 'r-beta' is visible to this caller.");
    }

    [Fact]
    public async Task HubFilter_ProjectReader_UnknownRun_IsRefusedWithTheSameSentence() =>
        (await InvokeAsync(Caller("alpha-team"), nameof(JobsHub.GetTrail), ["r-nowhere"]))
            .Should().Be("No run 'r-nowhere' is visible to this caller.");

    [Fact]
    public async Task HubFilter_ProjectReader_RunOfAProjectNoLongerConfigured_IsRefused()
    {
        await _store.SeedAsync(db => ProjectScopeRows.RunAsync(db, "r-gone", "gone"));

        (await InvokeAsync(Caller("alpha-team"), nameof(JobsHub.GetResultMarkdown), ["r-gone"]))
            .Should().Contain(UnknownRun, "a binding to a project the configuration no longer names reaches nothing");
    }

    [Fact]
    public async Task HubFilter_ProjectReaderWithoutWatch_ExpandSandboxInTheirProject_NamesTheMissingWatch()
    {
        await _store.SeedAsync(db => ProjectScopeRows.RunAsync(db, "r-alpha-2", "alpha"));

        (await InvokeAsync(Caller("alpha-readers", "alpha-watchers-beta"), nameof(JobsHub.ExpandSandbox), ["r-alpha-2", "repo"]))
            .Should().Be($"'ExpandSandbox' needs {Permissions.RunsWatch}, which this caller does not hold.");
    }

    [Fact]
    public async Task HubFilter_ProjectWatcher_ExpandSandboxInAnotherProject_IsRefusedAndTheRefcountIsUnchanged()
    {
        await _store.SeedAsync(db => ProjectScopeRows.RunAsync(db, "r-beta-2", "beta"));
        var registry = new SandboxExpansionRegistry();

        var outcome = await InvokeAsync(
            Caller("alpha-team"), nameof(JobsHub.ExpandSandbox), ["r-beta-2", "repo"],
            next: () => registry.Expand("r-beta-2", "repo"));

        outcome.Should().Contain(UnknownRun);
        registry.IsExpanded("r-beta-2", "repo").Should().BeFalse();
    }

    [Fact]
    public async Task HubFilter_ProjectReader_SubscribeSystem_IsRefused() =>
        (await InvokeAsync(Caller("alpha-team"), nameof(JobsHub.SubscribeSystem), []))
            .Should().Contain(Permissions.DiagnosticsRead);

    [Fact]
    public async Task HubFilter_ProjectDialogHolder_FreshDialogId_Proceeds() =>
        (await InvokeAsync(Caller("alpha-team"), nameof(JobsHub.SubscribeSpecDialog), ["d-fresh"])).Should().BeNull();

    [Fact]
    public async Task HubFilter_ProjectDialogHolder_OwnSessionWithNoProjectYet_Proceeds()
    {
        await _store.SeedAsync(db => ProjectScopeRows.SessionAsync(db, "s-new", "d-new", "", owner: "alpha-team-person"));

        (await InvokeAsync(Caller("alpha-team"), nameof(JobsHub.SubscribeSpecDialog), ["d-new"])).Should().BeNull();
    }

    [Fact]
    public async Task HubFilter_ProjectDialogHolder_SessionOfAnotherProject_IsRefusedWithTheOwnerSentence()
    {
        await _store.SeedAsync(db => ProjectScopeRows.SessionAsync(db, "s-beta", "d-beta", "beta", ticketKey: "B-1"));

        (await InvokeAsync(Caller("alpha-team"), nameof(JobsHub.SubscribeSpecDialog), ["d-beta"]))
            .Should().Be("Spec dialog 'd-beta' belongs to another principal.");
    }

    [Fact]
    public async Task HubFilter_ProjectDialogHolder_WatchFiledWorkWithoutRunsReadInThatProject_IsRefused()
    {
        await _store.SeedAsync(db => ProjectScopeRows.SessionAsync(db, "s-designer", "d-designer", "alpha", ticketKey: "A-1"));

        (await InvokeAsync(Caller("alpha-designers", "beta-readers"), nameof(JobsHub.WatchFiledWork), ["d-designer"]))
            .Should().Contain(Owner);
    }

    private async Task<string?> InvokeAsync(
        ClaimsPrincipal caller, string method, object?[] arguments, bool enforce = true, Action? next = null)
    {
        var auth = new TokenAuthorityConfig { Enforce = enforce, NameClaim = "preferred_username" };
        var access = ProjectScopeUnderTest.Access(
            auth, ResolverUnderTest.Over(auth, new StoredMappingStub(Mapping())), "alpha", "beta");
        var filter = new HubPermissionFilter(
            auth, ResolverUnderTest.Over(auth, new StoredMappingStub(Mapping())), new HubProjectScope(access, _store.Index()));
        await using var scope = _store.Services.CreateAsyncScope();
        var invocation = HubScopeUnderTest.Invocation(caller, scope.ServiceProvider, method, arguments);
        var refusal = await Record.ExceptionAsync(() => filter.InvokeMethodAsync(
            invocation, _ => { next?.Invoke(); return ValueTask.FromResult<object?>(null); }).AsTask());
        return refusal is null ? null : refusal.Should().BeOfType<HubException>().Which.Message;
    }

    private static ClaimsPrincipal Caller(params string[] groups) => ResolverUnderTest.Caller(
        new TokenAuthorityConfig { NameClaim = "preferred_username" },
        [.. groups.Select(group => ("groups", group)), ("sub", $"{groups[0]}-person")]);

    private static RoleMappingConfig Mapping()
    {
        var mapping = ProjectBindingTests.Mapping(
            Binding("alpha", groups: ["alpha-team"], roles: [BuiltInRoles.Operator]),
            Binding("gone", groups: ["alpha-team"], roles: [BuiltInRoles.Operator]),
            Binding("alpha", groups: ["alpha-readers"], roles: ["run-reader"]),
            Binding("beta", groups: ["alpha-watchers-beta"], roles: [BuiltInRoles.Reader]),
            Binding("alpha", groups: ["alpha-designers"], roles: ["designer"]),
            Binding("beta", groups: ["beta-readers"], roles: ["run-reader"]));
        mapping.Roles["run-reader"] = [Permissions.RunsRead];
        mapping.Roles["designer"] = [Permissions.DialogWrite];
        mapping.GroupRoles["everyone"] = [BuiltInRoles.Reader];
        mapping.GroupRoles["global-run-readers"] = ["run-reader"];
        return mapping;
    }
}
