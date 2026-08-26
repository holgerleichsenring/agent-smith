using System.Text.Json;
using AgentSmith.Contracts.Models.ConfigStudio;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Exceptions;
using AgentSmith.Infrastructure.Core.Services.Configuration.Studio;
using AgentSmith.Server.Security;
using FluentAssertions;
using Xunit;

namespace AgentSmith.Tests.Server.Access;

/// <summary>
/// 2026-08-26-7a51: no write to the role mapping may leave the installation with no route
/// to admin — and the check sits on the document store, because three of the four writes
/// that reach a mapping never touch the settings route.
/// </summary>
public sealed class AdminInvariantTests
{
    [Fact]
    public void Invariant_SaveRemovingTheLastAdmin_IsRefusedWithTheReason()
    {
        using var h = new AccessTestHarness();

        var save = () => h.Writer.Save(NoRoute(), Actor);

        save.Should().Throw<ConfigurationException>()
            .WithMessage("*no way to reach the admin role*")
            .WithMessage($"*{AdminGrant.EnvVar}*");
    }

    [Fact]
    public void Invariant_ImportWithNoRouteToAdmin_IsRefused()
    {
        using var h = new AccessTestHarness();

        var import = () => h.DocStore.Import([Write(NoRoute())], force: true);

        import.Should().Throw<ConfigurationException>().WithMessage("*no way to reach the admin role*");
        h.DocStore.LoadAll().Should().NotContain(row => row.Type == ConfigDocTypes.RoleMapping);
    }

    [Fact]
    public void Invariant_RevertToADocumentWithNoRouteToAdmin_IsRefused()
    {
        using var h = new AccessTestHarness();
        // The routeless document is how an installation that upgraded into this rule looks:
        // it was written before the invariant existed, so only a revert can reinstate it.
        h.RawDocStore.Save(Write(NoRoute()));
        h.Store.Load();
        h.Writer.Save(Routed(), Actor);
        var change = h.Store.GetChanges().First(c => c.EntityId == ConfigDocTypes.RoleMapping);

        var revert = () => h.Store.Revert(change.Id, Actor);

        revert.Should().Throw<ConfigurationException>().WithMessage("*no way to reach the admin role*");
    }

    [Fact]
    public void Invariant_WithAParsableEnvironmentAdmin_AllowsTheRemoval()
    {
        using var h = new AccessTestHarness(adminGrant: "sub:rescue-0001");

        h.Writer.Save(NoRoute(), Actor);

        h.Mapping.Current().Mapping.RoleClaim.Should().BeEmpty(
            "the way back in is genuinely a way back in, so the mapping may empty");
    }

    [Fact]
    public void Invariant_WithAnUnparsableEnvironmentGrant_StillRefuses()
    {
        // Set, and it names nobody: an unprefixed entry parses to nothing and grants no one,
        // so "the variable is not null" is the wrong question to ask of it.
        using var h = new AccessTestHarness(adminGrant: "rescue-0001");

        var save = () => h.Writer.Save(NoRoute(), Actor);

        save.Should().Throw<ConfigurationException>().WithMessage("*no way to reach the admin role*");
    }

    [Fact]
    public void Invariant_TwoAdminsSavingConcurrently_LeavesARouteToAdmin()
    {
        using var h = new AccessTestHarness();
        h.Writer.Save(Routed(Grant("sub", "ada", "admin")), Actor);

        // One administrator clears the role claim, keeping the grant; the other, working
        // from the view before that, drops the grant and keeps the claim. Each document is
        // judged whole, so whichever lands last still carries a route.
        h.Writer.Save(new RoleMappingConfig { RoleClaim = "", PersonGrants = [Grant("sub", "ada", "admin")] }, Actor);
        h.Writer.Save(Routed(), Actor);

        new AdminRoute(NoEnvironmentGrant).ExistsIn(h.Mapping.Current().Mapping).Should().BeTrue();
        var third = () => h.Writer.Save(NoRoute(), Actor);
        third.Should().Throw<ConfigurationException>("a document that removes both is one document, and it is refused");
    }

    private static ChangeAttribution Actor => new("tester");

    private static AdminGrant NoEnvironmentGrant => Auth.ResolverUnderTest.Grant(null);

    private static PersonGrant Grant(string claim, string value, params string[] roles) =>
        new() { Claim = claim, Value = value, Roles = [.. roles] };

    private static RoleMappingConfig NoRoute() => new() { RoleClaim = "", GroupClaim = "groups" };

    private static RoleMappingConfig Routed(params PersonGrant[] grants) =>
        new() { RoleClaim = "roles", GroupClaim = "groups", PersonGrants = [.. grants] };

    private static ConfigDocWrite Write(RoleMappingConfig mapping) => new(
        ConfigDocTypes.RoleMapping, ConfigDocTypes.SingletonId,
        JsonSerializer.Serialize(mapping, new ConfigDocJson().Options),
        ExpectedVersion: null, Edges: [], "tester");
}
