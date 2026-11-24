using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Admin;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Pocs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Pocs;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Tests.Pocs.Variables;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Pocs.Lifetime;

/// <summary>2026-11-23-c81ee, on SQLite: the purge run deletes the stored PoC uploads older than pocs.blocked_retention_days
/// (ages compared in memory) and keeps younger ones and a conversation's references; a deleted PoC's secret values go
/// through SecretWrite in its scope with its plain variables; a second run does nothing.</summary>
public sealed class PurgePocUploadsHandlerTests : IDisposable
{
    private readonly SecretStoreRig _rig = new();
    private readonly DateTimeOffset _now = DateTimeOffset.UtcNow;

    private sealed class RepositoryStore(PocUploadPurgeRepository repository) : IPocUploadPurgeStore
    {
        public Task<int> PurgeUploadsAsync(DateTimeOffset olderThan, CancellationToken ct) => repository.PurgeUploadsAsync(olderThan, ct);

        public Task<IReadOnlyList<long>> DeletedWithVariablesAsync(CancellationToken ct) => repository.DeletedWithVariablesAsync(ct);

        public Task ClearPlainVariablesAsync(long pocId, CancellationToken ct) => repository.ClearPlainVariablesAsync(pocId, ct);
    }

    private PurgePocUploadsHandler Handler()
    {
        var config = new AgentSmithConfig { Pocs = PocsConfigTests.Sound() };
        var source = new PocsConfigSource(Mock.Of<IConfigurationLoader>(l => l.LoadConfig(It.IsAny<string>()) == config), new ServerContext("c.yml"));
        var vault = _rig.Vault();
        return new PurgePocUploadsHandler(new RepositoryStore(new PocUploadPurgeRepository(_rig.Db())), vault,
            new SecretWrite(vault, Mock.Of<IConfigStore>()), source, new PocClock(_now), NullLogger<PurgePocUploadsHandler>.Instance);
    }

    private async Task FileAsync(string session, string set, int daysOld)
    {
        await using var db = _rig.Db();
        var file = new ReferenceFile { SessionId = session, SetId = set, Kind = "file", RelativePath = "a.zip", MediaType = "application/zip", Content = [1] };
        db.Add(file);
        await db.SaveChangesAsync();
        await db.Set<ReferenceFile>().Where(f => f.Id == file.Id).ExecuteUpdateAsync(s => s.SetProperty(f => f.CreatedAt, _now.AddDays(-daysOld)));
    }

    private async Task<IReadOnlyList<string>> SetsAsync()
    {
        await using var db = _rig.Db();
        return await db.Set<ReferenceFile>().AsNoTracking().Select(f => f.SetId).OrderBy(s => s).ToListAsync();
    }

    private Task<AgentSmith.Domain.Models.CommandResult> RunAsync()
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.RunId, "purge-1");
        return Handler().ExecuteAsync(new PurgePocUploadsContext(pipeline), default);
    }

    [Fact]
    public async Task Purge_DeletesAnEightDayOldUpload_KeepsASixDayOldOne_AndAConversationsReference_ThenDoesNothing()
    {
        await FileAsync("poc-upload:0123456789abcdef", "old", 8);
        await FileAsync("poc-upload:0123456789abcdef", "young", 6);
        await FileAsync("design-session-1", "reference", 30);

        var first = await RunAsync();
        var second = await RunAsync();

        first.IsSuccess.Should().BeTrue(first.Message);
        first.Message.Should().StartWith("1 stored PoC upload file(s) older than 7 days (pocs.blocked_retention_days) deleted");
        (await SetsAsync()).Should().Equal("reference", "young");
        second.Message.Should().StartWith("0 stored PoC upload file(s)").And.Contain("of 0 deleted PoC(s)");
    }

    [Fact]
    public async Task Purge_DeletesADeletedPocsSecretValuesAndPlainVariables_NotALiveOnes()
    {
        var gone = await _rig.PocAsync("gone", "poc-ann");
        var live = await _rig.PocAsync("live", "poc-ann");
        await using (var db = _rig.Db())
            await db.Set<Poc>().Where(p => p.Id == gone.Id).ExecuteUpdateAsync(s => s.SetProperty(p => p.State, PocStates.Deleted)
                .SetProperty(p => p.LiveName, (string?)null).SetProperty(p => p.PlainVariablesJson, "[]"));
        await _rig.SetAsync(SecretScope.Poc(gone.Id), "API_KEY", "secret-value-1");
        await _rig.SetAsync(SecretScope.Poc(live.Id), "API_KEY", "secret-value-2");

        var first = await RunAsync();
        var second = await RunAsync();

        first.Message.Should().Contain("1 secret value(s) and the plain variables of 1 deleted PoC(s) removed");
        (await _rig.Vault().StatesAsync(SecretScope.Poc(gone.Id), default)).Should().BeEmpty();
        (await _rig.Vault().StatesAsync(SecretScope.Poc(live.Id), default)).Should().ContainSingle();
        await using (var db = _rig.Db())
            (await db.Set<Poc>().AsNoTracking().SingleAsync(p => p.Id == gone.Id)).PlainVariablesJson.Should().BeNull();
        second.Message.Should().Contain("0 secret value(s) and the plain variables of 0 deleted PoC(s) removed");
    }

    public void Dispose() => _rig.Dispose();
}
