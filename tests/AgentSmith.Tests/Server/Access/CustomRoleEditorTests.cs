using System.Text.Json;
using AgentSmith.Contracts.Models.Access;
using AgentSmith.Contracts.Models.ConfigStudio;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Exceptions;
using AgentSmith.Infrastructure.Core.Services.Configuration.Studio;
using AgentSmith.Server.Services.Access;
using FluentAssertions;

namespace AgentSmith.Tests.Server.Access;

/// <summary>
/// 2026-09-14-91ad: a role composed out of the permission catalog, and the two refusals that
/// replaced the one that used to say no to all of them.
/// </summary>
public sealed class CustomRoleEditorTests
{
    [Fact]
    public async Task Access_CustomRoleNamed_ADMIN_IsRefusedAtTheSave()
    {
        using var h = new AccessTestHarness();

        var save = async () => await h.Writer.SaveAsync(Doc(Roles(("ADMIN", ["runs.read"]))), Actor, default);

        // Role names fold case everywhere in this server, so the collision is real.
        await save.Should().ThrowAsync<ConfigurationException>().WithMessage("*built-in role*");
    }

    [Fact]
    public async Task Access_CustomRoleNamingAnUnknownPermission_IsRefusedRatherThanSilentlyDropped()
    {
        using var h = new AccessTestHarness();

        var save = async () =>
            await h.Writer.SaveAsync(Doc(Roles(("auditor", ["runs.read", "runs.invent"]))), Actor, default);

        await save.Should().ThrowAsync<ConfigurationException>().WithMessage("*runs.invent*");
    }

    [Fact]
    public async Task Access_CustomRoleNamingAPermissionInTheWrongCase_IsRefusedLikeAnUnknownOne()
    {
        using var h = new AccessTestHarness();

        var save = async () =>
            await h.Writer.SaveAsync(Doc(Roles(("auditor", ["Runs.Read"]))), Actor, default);

        // The catalog compares permissions ordinally, so accepting this would hand it a name
        // it then drops — the silent drop this phase exists to end, through the door meant
        // to stop it.
        await save.Should().ThrowAsync<ConfigurationException>().WithMessage("*case-sensitive*");
    }

    [Fact]
    public async Task Access_SaveCarryingAnUntouchedLegacyRole_IsNotRefused()
    {
        using var h = new AccessTestHarness();
        // A bundle the catalog does not fully know, of the kind that predates the catalog.
        h.RawDocStore.Save(Write(Roles(("auditor", ["config.read", "runs.invent"]))));
        h.Store.Load();

        await h.Writer.SaveAsync(
            Doc(Held(Roles(("auditor", ["config.read", "runs.invent"])), Grant("sub", "ada", "reader"))),
            Actor, default);

        h.Mapping.Current().Mapping.PersonGrants.Should().ContainSingle(
            "a person grant must not be refused because of a role nobody touched");
    }

    [Fact]
    public async Task Access_CustomRoleEdited_ChangesWhatItsHoldersHoldOnTheNextRequest()
    {
        using var h = new AccessTestHarness();
        await h.Writer.SaveAsync(Doc(Roles(("auditor", ["config.read"]))), Actor, default);

        await h.Writer.SaveAsync(Doc(Roles(("auditor", ["config.read", "runs.read"]))), Actor, default);

        h.Mapping.Current().Catalog.Permissions(["auditor"]).Should().Contain("runs.read");
    }

    [Fact]
    public async Task Access_EditRemovingAPermissionFromAHeldRole_IsAllowedAndTakesEffect()
    {
        // Narrowing a role is the ordinary way one is corrected; refusing it would leave an
        // installation unable to tighten anything. Removing the ROLE is the different act —
        // it leaves its holders with nothing and no surface saying why.
        using var h = new AccessTestHarness();
        await h.Writer.SaveAsync(
            Doc(Held(Roles(("auditor", ["config.read", "runs.delete"])), Grant("sub", "ada", "auditor"))),
            Actor, default);

        await h.Writer.SaveAsync(
            Doc(Held(Roles(("auditor", ["config.read"])), Grant("sub", "ada", "auditor"))),
            Actor, default);

        h.Mapping.Current().Catalog.Permissions(["auditor"]).Should().NotContain("runs.delete");
    }

    [Fact]
    public async Task Access_RemovingARoleNobodyHolds_Succeeds()
    {
        using var h = new AccessTestHarness();
        await h.Writer.SaveAsync(Doc(Roles(("auditor", ["config.read"]))), Actor, default);

        await h.Writer.SaveAsync(Doc(new RoleMappingConfig { RoleClaim = "roles" }), Actor, default);

        h.Mapping.Current().Mapping.Roles.Should().NotContainKey("auditor");
    }

    [Fact]
    public async Task Access_RemovingARoleSomebodyHolds_IsRefusedAndNamesTheHolders()
    {
        using var h = new AccessTestHarness();
        await h.Writer.SaveAsync(
            Doc(Held(Roles(("auditor", ["config.read"])), Grant("sub", "ada", "auditor"))),
            Actor, default);

        var save = async () => await h.Writer.SaveAsync(
            Doc(new RoleMappingConfig
            {
                RoleClaim = "roles",
                PersonGrants = [Grant("sub", "ada", "auditor")],
            }),
            Actor, default);

        var refusal = await save.Should().ThrowAsync<ConfigurationException>();
        refusal.WithMessage("*ada*");
        refusal.WithMessage("*can SEE*");
    }

    [Fact]
    public async Task Access_RemovingARoleAndWithdrawingItTogether_IsOneDeliberateAct()
    {
        using var h = new AccessTestHarness();
        await h.Writer.SaveAsync(
            Doc(Held(Roles(("auditor", ["config.read"])), Grant("sub", "ada", "auditor"))),
            Actor, default);

        // Judged against the document that is about to exist, so the withdrawal and the
        // removal in one save are not two refused halves.
        await h.Writer.SaveAsync(Doc(new RoleMappingConfig { RoleClaim = "roles" }), Actor, default);

        h.Mapping.Current().Mapping.Roles.Should().NotContainKey("auditor");
    }

    [Fact]
    public async Task Access_RemovingARoleWhileTheObservationStoreIsUnreadable_IsRefusedRatherThanAllowed()
    {
        var guard = new RoleRemovalGuard(new UnreadableStore());

        var check = async () => await guard.AgainstAsync(
            Roles(("auditor", ["config.read"])), new RoleMappingConfig(), default);

        // The tempting shortcut fails open: AccessSurfaceReader catches and returns an empty
        // list, which would permit the destructive act exactly when the holders are invisible.
        await check.Should().ThrowAsync<ConfigurationException>().WithMessage("*refused rather than*");
    }

    [Fact]
    public async Task Access_BuiltInRole_CannotBeRemovedByOmission()
    {
        using var h = new AccessTestHarness();

        await h.Writer.SaveAsync(Doc(new RoleMappingConfig { RoleClaim = "roles" }), Actor, default);

        h.Mapping.Current().Catalog.Names.Should().Contain(["admin", "operator", "reader"],
            "the built-in bundles are not in the document, which is what makes a custom role additive");
    }

    private static ChangeAttribution Actor => new("tester");

    private static RoleMappingConfig Roles(params (string Name, string[] Bundle)[] roles)
    {
        var mapping = new RoleMappingConfig { RoleClaim = "roles" };
        foreach (var (name, bundle) in roles) mapping.Roles[name] = [.. bundle];
        return mapping;
    }

    /// <summary>The same mapping with grants against it — RoleMappingConfig is a class, not a record.</summary>
    private static RoleMappingConfig Held(RoleMappingConfig mapping, params PersonGrant[] grants)
    {
        mapping.PersonGrants = [.. grants];
        return mapping;
    }

    private static PersonGrant Grant(string claim, string value, params string[] roles) =>
        new() { Claim = claim, Value = value, Roles = [.. roles] };

    private static JsonElement Doc(RoleMappingConfig mapping) =>
        JsonSerializer.SerializeToElement(mapping, new ConfigDocJson().Options);

    private static ConfigDocWrite Write(RoleMappingConfig mapping) => new(
        ConfigDocTypes.RoleMapping, ConfigDocTypes.SingletonId,
        JsonSerializer.Serialize(mapping, new ConfigDocJson().Options),
        ExpectedVersion: null, Edges: [], "tester");

    private sealed class UnreadableStore : IObservedCallerStore
    {
        public Task UpsertAsync(IReadOnlyList<ObservedCaller> c, CancellationToken ct) => Task.CompletedTask;
        public Task<IReadOnlyList<ObservedCaller>> AllAsync(CancellationToken ct) =>
            throw new InvalidOperationException("the database could not be reached");
        public Task<bool> RemoveAsync(string subject, CancellationToken ct) => Task.FromResult(false);
        public Task<int> RemoveSeenBeforeAsync(DateTimeOffset cut, CancellationToken ct) => Task.FromResult(0);
    }
}
