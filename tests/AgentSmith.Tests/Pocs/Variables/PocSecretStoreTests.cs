using AgentSmith.Contracts.Models;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Infrastructure.Persistence.Services.Secrets;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Tests.Pocs.Variables;

/// <summary>2026-11-23-933dg: one store, two scopes — a PoC variable keeps its case, lives beside an installation secret
/// of the same name, is deleted and listed within its scope only, survives a re-wrap, and never reaches the load.</summary>
public sealed class PocSecretStoreTests : IDisposable
{
    private readonly SecretStoreRig _rig = new();
    private readonly CancellationToken _ct = CancellationToken.None;

    [Fact]
    public async Task Set_APocRow_KeepsTheNamesCase_AndLivesBesideTheInstallationRowOfThatName()
    {
        await _rig.SetAsync(SecretScope.Poc(7), "OPENAI_API_KEY", "poc-value-123456");
        await _rig.SetAsync(SecretScope.Installation, "OPENAI_API_KEY", "installation-value-1");

        var rows = await _rig.Db().Set<StoredSecret>().AsNoTracking().OrderBy(s => s.Scope).ToListAsync(_ct);
        rows.Select(r => (r.Scope, r.Name)).Should().Equal(("installation", "openai_api_key"), ("poc:7", "OPENAI_API_KEY"));
    }

    [Fact]
    public async Task Delete_InThePocScope_LeavesTheInstallationRow_AndStatesListOneScope()
    {
        await _rig.SetAsync(SecretScope.Installation, "api_key", "installation-value-1");
        await _rig.SetAsync(SecretScope.Poc(7), "API_KEY", "poc-value-123456");

        (await _rig.Vault().DeleteAsync(SecretScope.Poc(7), "API_KEY", _ct)).Should().BeTrue();

        (await _rig.Vault().StatesAsync(SecretScope.Installation, _ct)).Select(s => s.Name).Should().Equal("api_key");
        (await _rig.Vault().StatesAsync(SecretScope.Poc(7), _ct)).Should().BeEmpty();
    }

    [Fact]
    public async Task StatesAsync_Installation_ListsNoPocRow()
    {
        await _rig.SetAsync(SecretScope.Poc(7), "LOG_TOKEN", "poc-value-123456");

        (await _rig.Vault().StatesAsync(SecretScope.Installation, _ct)).Should().BeEmpty();
        (await _rig.Vault().StatesAsync(SecretScope.Poc(7), _ct)).Single().Name.Should().Be("LOG_TOKEN");
    }

    [Fact]
    public async Task ExistingRow_WithoutAScope_ReadsAsInstallation()
    {
        await using (var db = _rig.Db())
            await db.Database.ExecuteSqlRawAsync("INSERT INTO StoredSecrets (Name, Version, Ciphertext, Nonce, WrappedKey, WrapNonce, KeyId, SetBy, SetAt, CreatedAt, UpdatedAt) "
                + "VALUES ('legacy', 1, x'00', x'00', x'00', x'00', 'k', 'ada', '2026-11-01 00:00:00+00:00', '2026-11-01 00:00:00+00:00', '2026-11-01 00:00:00+00:00')", _ct);

        (await _rig.Vault().StatesAsync(SecretScope.Installation, _ct)).Single().Name.Should().Be("legacy");
    }

    [Fact]
    public async Task Rewrap_OpensPocRowsAfterwards()
    {
        using var old = new SecretStoreRig();
        var poc = await _rig.PocAsync("todo", "poc-ann");
        await _rig.SetAsync(SecretScope.Poc(poc.Id), "OPENAI_API_KEY", "poc-value-123456");
        var next = new MasterKeyRing(name => name switch
        {
            MasterKeyRing.Variable => old.Key, "AGENTSMITH_SECRET_MASTER_KEY_PREVIOUS" => _rig.Key, _ => null,
        });

        (await new StoredSecretRepository(_rig.Db(), next, new SecretEnvelope(), TimeProvider.System).RewrapAsync(_ct)).Should().Be(1);

        var values = await _rig.PocValues(next).ReadAsync(poc.Id, _ct);
        values.Values["OPENAI_API_KEY"].Reveal().Should().Be("poc-value-123456");
    }

    [Fact]
    public async Task Load_APocRowNamedLikeACatalogSecret_DoesNotResolve_AndAnUnopenablePocRowRecordsNothing()
    {
        await _rig.SetAsync(SecretScope.Poc(7), "github_token", "poc-value-123456");
        await using (var db = _rig.Db())
            await db.Database.ExecuteSqlRawAsync("UPDATE StoredSecrets SET KeyId = 'gone'", _ct);
        await _rig.SetAsync(SecretScope.Poc(8), "github_token", "poc-value-654321");

        var load = _rig.Load().Load();

        load.Values.Should().BeEmpty();
        load.Unreadable.Should().BeEmpty();
        _rig.Findings.All.Should().BeEmpty();
    }

    [Fact]
    public async Task PocValues_ForProject_OpensEveryPocOfThatProjectOnly()
    {
        var ann = await _rig.PocAsync("todo", "poc-ann");
        var bob = await _rig.PocAsync("shop", "poc-bob");
        await _rig.SetAsync(SecretScope.Poc(ann.Id), "OPENAI_API_KEY", "ann-value-123456");
        await _rig.SetAsync(SecretScope.Poc(bob.Id), "OPENAI_API_KEY", "bob-value-123456");

        (await _rig.PocValues().ForProjectAsync("poc-ann", _ct)).Should().Equal("ann-value-123456");
        (await _rig.PocValues().ForProjectAsync("payments", _ct)).Should().BeEmpty();
    }

    public void Dispose() => _rig.Dispose();
}
