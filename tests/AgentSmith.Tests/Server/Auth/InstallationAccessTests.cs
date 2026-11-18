using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Server.Security;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using static AgentSmith.Tests.Server.Auth.ProjectBindingTests;

namespace AgentSmith.Tests.Server.Auth;

/// <summary>
/// 2026-11-17-f3a8a: the installation's own conversation and runs exist only for a caller holding admin.assistant —
/// an operator or reader holding runs.read and dialog.write globally is refused, and told why when they post.
/// </summary>
public sealed class InstallationAccessTests : IDisposable
{
    private static readonly TokenAuthorityConfig Auth = new() { Enforce = true, NameClaim = "preferred_username" };
    private readonly SqliteConnection _connection = MigratedStoreTemplate.OpenCopy();
    private readonly AgentSmithDbContext _db;
    private readonly ProjectAccess _access;

    public InstallationAccessTests()
    {
        _db = MigratedStoreTemplate.Context(_connection);
        var mapping = Mapping();
        mapping.GroupRoles["admins"] = [BuiltInRoles.Admin];
        mapping.GroupRoles["operators"] = [BuiltInRoles.Operator];
        mapping.GroupRoles["readers"] = [BuiltInRoles.Reader];
        _access = ProjectScopeUnderTest.Access(Auth, ResolverUnderTest.Over(Auth, new StoredMappingStub(mapping)), "alpha");
    }

    [Fact]
    public void HoldsIn_Installation_OnlyWithTheAdminAssistantGrant()
    {
        _access.HoldsIn(Caller("admins"), [Permissions.RunsRead], InstallationScope.Name).Should().BeTrue();
        _access.HoldsIn(Caller("operators"), [Permissions.RunsRead], InstallationScope.Name).Should().BeFalse();
        _access.HoldsIn(Caller("readers"), [Permissions.RunsRead], InstallationScope.Name).Should().BeFalse();
        _access.HoldsIn(Caller("readers"), [Permissions.RunsRead], "alpha").Should().BeTrue("projects are unchanged");
    }

    [Fact]
    public void CallerScope_GlobalReader_ListsEveryProjectButTheInstallation()
    {
        var scopes = new CallerProjectScope(_access);

        var reader = scopes.For(Caller("readers"), Permissions.RunsRead);
        var admin = scopes.For(Caller("admins"), Permissions.RunsRead);

        reader.Admits(InstallationScope.Name).Should().BeFalse();
        reader.Admits("alpha").Should().BeTrue();
        reader.Narrowed(InstallationScope.Name).Only.Should().BeEmpty("asking for the installation by name reads as unknown");
        admin.Admits(InstallationScope.Name).Should().BeTrue();
    }

    [Fact]
    public async Task AdmitAsync_OperatorNamingTheInstallation_IsRefusedWithAReason()
    {
        var scope = ProjectScopeUnderTest.Scope(_access, _db);

        var operatorAdmission = await scope.AdmitAsync(Caller("operators"), "d-1", InstallationScope.Name, "why", CancellationToken.None);
        var adminAdmission = await scope.AdmitAsync(Caller("admins"), "d-2", InstallationScope.Name, "why", CancellationToken.None);

        operatorAdmission.Admitted.Should().BeFalse();
        operatorAdmission.Refusal.Should().Contain("admin.assistant");
        adminAdmission.Admitted.Should().BeTrue();
    }

    [Fact]
    public async Task MayEnterAsync_AnInstallationConversation_RefusesAnOperator()
    {
        await ProjectScopeRows.SessionAsync(_db, "s-admin", "d-admin", InstallationScope.Name);
        var scope = ProjectScopeUnderTest.Scope(_access, _db);

        (await scope.MayEnterAsync(Caller("operators"), "d-admin", CancellationToken.None)).Should().BeFalse();
        (await scope.MayEnterAsync(Caller("admins"), "d-admin", CancellationToken.None)).Should().BeTrue();
    }

    [Fact]
    public void Permissions_AdminAssistant_HeldByTheAdminRoleOnly()
    {
        BuiltInRoles.All[BuiltInRoles.Admin].Should().Contain(Permissions.AdminAssistant);
        BuiltInRoles.All[BuiltInRoles.Operator].Should().NotContain(Permissions.AdminAssistant);
        Permissions.ProjectScopable.Should().NotContain(Permissions.AdminAssistant);
    }

    private static System.Security.Claims.ClaimsPrincipal Caller(string group) =>
        ResolverUnderTest.Caller(Auth, ("groups", group));

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }
}
