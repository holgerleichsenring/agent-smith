using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Server.Security;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using static AgentSmith.Tests.Server.Auth.ProjectBindingTests;

namespace AgentSmith.Tests.Server.Auth;

/// <summary>
/// 2026-10-15-4a4db: whether a message's turn may start runs is runs.control in the conversation's
/// projects — a designer in alpha who operates beta does not start runs from alpha's conversation.
/// </summary>
public sealed class DialogMessageScopeTests : IDisposable
{
    private static readonly TokenAuthorityConfig Auth = new() { Enforce = true, NameClaim = "preferred_username" };
    private readonly SqliteConnection _connection = MigratedStoreTemplate.OpenCopy();
    private readonly AgentSmithDbContext _db;
    private readonly DialogMessageScope _scope;

    public DialogMessageScopeTests()
    {
        _db = MigratedStoreTemplate.Context(_connection);
        var mapping = Mapping(
            Binding("alpha", groups: ["makers"], roles: ["designer"]),
            Binding("beta", groups: ["makers"], roles: [BuiltInRoles.Operator]));
        mapping.Roles["designer"] = [Permissions.DialogWrite];
        var resolver = ResolverUnderTest.Over(Auth, new StoredMappingStub(mapping));
        _scope = ProjectScopeUnderTest.Scope(ProjectScopeUnderTest.Access(Auth, resolver, "alpha", "beta"), _db);
    }

    [Fact]
    public async Task SpecDialogMessage_RunsControlOnlyInAnotherProject_DoesNotStartRuns()
    {
        await ProjectScopeRows.SessionAsync(_db, "s-alpha", "d-alpha", "alpha");
        var caller = ResolverUnderTest.Caller(Auth, ("groups", "makers"));

        var alpha = await _scope.AdmitAsync(caller, "d-alpha", null, "approve it", CancellationToken.None);
        var beta = await _scope.AdmitAsync(caller, "d-beta-new", "beta", "a widget", CancellationToken.None);

        alpha.Should().Be(new DialogAdmission(true, false), "runs.control is held in beta, not in alpha");
        beta.Should().Be(new DialogAdmission(true, true));
    }

    [Fact]
    public async Task SpecDialogMessage_NotEnforcing_AdmitsAndMayStartRuns()
    {
        var scope = ProjectScopeUnderTest.Scope(ProjectScopeUnderTest.Access(new TokenAuthorityConfig()));

        var admission = await scope.AdmitAsync(
            ResolverUnderTest.Caller(Auth), "d-any", "beta", "/spec", CancellationToken.None);

        admission.Should().Be(new DialogAdmission(true, true), "an installation that enforces nothing refuses nothing");
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }
}
