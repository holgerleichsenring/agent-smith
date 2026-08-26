using System.Text.Json;
using AgentSmith.Contracts.Models.Access;
using AgentSmith.Contracts.Models.ConfigStudio;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Exceptions;
using AgentSmith.Server.Models;
using FluentAssertions;
using Xunit;

namespace AgentSmith.Tests.Server.Access;

/// <summary>
/// 2026-08-26-7a51: the four panes over one document — what a save preserves, what it
/// refuses, and what removing a person takes with it.
/// </summary>
public sealed class AccessSurfaceTests
{
    private static readonly TokenAuthorityConfig Named = new() { NameClaim = "preferred_username" };

    [Fact]
    public void Save_FromThePeoplePane_LeavesTheClaimNamesAsTheyWere()
    {
        using var h = new AccessTestHarness();
        h.Writer.Save(Doc(new RoleMappingConfig { RoleClaim = "app_roles", GroupClaim = "memberOf" }), Actor);

        // The pane edits people and sends the WHOLE document back, because a settings write
        // binds onto a fresh model and every omitted field would revert to its default.
        var edited = h.Reader.ViewAsync(CancellationToken.None).Result;
        h.Writer.Save(Doc(new RoleMappingConfig
        {
            RoleClaim = edited.RoleClaim,
            GroupClaim = edited.GroupClaim,
            PersonGrants = [Grant("sub", "ada", "operator")],
        }), Actor);

        var mapping = h.Mapping.Current().Mapping;
        mapping.RoleClaim.Should().Be("app_roles");
        mapping.GroupClaim.Should().Be("memberOf");
        mapping.PersonGrants.Should().ContainSingle();
    }

    [Fact]
    public void CustomRole_AlreadyConfigured_SurvivesAnUnrelatedSave()
    {
        using var h = new AccessTestHarness();
        h.RawDocStore.Save(Write(WithCustomRole()));
        h.Store.Load();

        h.Writer.Save(Doc(new RoleMappingConfig
        {
            RoleClaim = "roles",
            Roles = { ["auditor"] = ["config.read"] },
            PersonGrants = [Grant("sub", "ada", "auditor")],
        }), Actor);

        h.Mapping.Current().Mapping.Roles.Should().ContainKey("auditor");
        var view = h.Reader.ViewAsync(CancellationToken.None).Result;
        view.Roles.Should().Contain(role => role.Name == "auditor" && !role.BuiltIn);
    }

    [Fact]
    public void CustomRole_New_IsRefused()
    {
        using var h = new AccessTestHarness();

        var save = () => h.Writer.Save(
            Doc(new RoleMappingConfig { RoleClaim = "roles", Roles = { ["auditor"] = ["config.read"] } }),
            Actor);

        save.Should().Throw<ConfigurationException>().WithMessage("*new custom role cannot be added*");
    }

    [Fact]
    public async Task RemovePerson_RemovesTheGrantAndTheRecordTogether()
    {
        using var h = new AccessTestHarness(auth: Named);
        await h.Observed.UpsertAsync([Seen("ada-0001", "ada@example.com")], CancellationToken.None);
        h.Writer.Save(Doc(new RoleMappingConfig
        {
            RoleClaim = "roles",
            PersonGrants = [Grant("preferred_username", "ada@example.com", "admin")],
        }), Actor);

        (await h.Remover.RemoveAsync("ada-0001", Actor, CancellationToken.None)).Should().BeTrue();

        h.Mapping.Current().Mapping.PersonGrants.Should().BeEmpty();
        (await h.Observed.AllAsync(CancellationToken.None)).Should().BeEmpty();
    }

    [Fact]
    public async Task View_SomeoneAddedByHand_HasNoLastSeen()
    {
        using var h = new AccessTestHarness(auth: Named);
        await h.Observed.UpsertAsync([Seen("ada-0001", "ada@example.com")], CancellationToken.None);
        h.Writer.Save(Doc(new RoleMappingConfig
        {
            RoleClaim = "roles",
            PersonGrants = [Grant("preferred_username", "newcomer@example.com", "reader")],
        }), Actor);

        var people = (await h.Reader.ViewAsync(CancellationToken.None)).People;

        Person(people, "ada@example.com").LastSeen.Should().NotBeNull();
        Person(people, "newcomer@example.com").LastSeen.Should().BeNull(
            "'not signed in yet' and 'signed in and holds nothing' are different situations");
    }

    [Fact]
    public async Task View_NameClaimIsNotSub_SaysTheValuesAreSelfAsserted()
    {
        using var named = new AccessTestHarness(auth: Named);
        using var opaque = new AccessTestHarness();

        (await named.Reader.ViewAsync(CancellationToken.None)).NameClaimIsSelfAsserted.Should().BeTrue();
        (await opaque.Reader.ViewAsync(CancellationToken.None)).NameClaimIsSelfAsserted.Should().BeFalse();
    }

    [Fact]
    public async Task View_AGroupNobodyMapped_IsStillListedWithItsCarriers()
    {
        using var h = new AccessTestHarness();
        await h.Observed.UpsertAsync(
            [Seen("ada-0001", "ada", "/platform-admins"), Seen("bob-0002", "bob", "platform-admins")],
            CancellationToken.None);

        var groups = (await h.Reader.ViewAsync(CancellationToken.None)).Groups;

        groups.Should().ContainSingle().Which.Should().BeEquivalentTo(
            new AccessGroupView("platform-admins", [], 2),
            "one leading slash is normalised away, and only that");
    }

    private static ChangeAttribution Actor => new("tester");

    private static AccessPersonView Person(IReadOnlyList<AccessPersonView> people, string nameValue) =>
        people.Single(p => p.NameValue == nameValue);

    private static ObservedCaller Seen(string subject, string nameValue, params string[] groups) =>
        new(subject, Named.NameClaim, nameValue, [], [.. groups], false,
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);

    private static PersonGrant Grant(string claim, string value, params string[] roles) =>
        new() { Claim = claim, Value = value, Roles = [.. roles] };

    private static RoleMappingConfig WithCustomRole() => new()
    {
        RoleClaim = "roles", Roles = { ["auditor"] = ["config.read"] },
    };

    private static JsonElement Doc(RoleMappingConfig mapping) =>
        JsonSerializer.SerializeToElement(mapping, new AgentSmith.Infrastructure.Core
            .Services.Configuration.Studio.ConfigDocJson().Options);

    private static ConfigDocWrite Write(RoleMappingConfig mapping) => new(
        AgentSmith.Infrastructure.Core.Services.Configuration.Studio.ConfigDocTypes.RoleMapping,
        AgentSmith.Infrastructure.Core.Services.Configuration.Studio.ConfigDocTypes.SingletonId,
        JsonSerializer.Serialize(mapping, new AgentSmith.Infrastructure.Core
            .Services.Configuration.Studio.ConfigDocJson().Options),
        ExpectedVersion: null, Edges: [], "tester");
}
