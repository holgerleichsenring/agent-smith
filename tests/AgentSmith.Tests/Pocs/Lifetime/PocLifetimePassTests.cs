using System.Net;
using System.Net.Http.Json;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Pocs;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Services.Acts;
using AgentSmith.Server.Services.Pocs.Lifetime;
using AgentSmith.Tests.Pocs.Variables;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using static AgentSmith.Tests.Pocs.PocsAuthorityFixture;

namespace AgentSmith.Tests.Pocs.Lifetime;

/// <summary>
/// 2026-11-23-c81ee, on a SQLite-backed server: a lifetime pass computes every expiry in memory, stops an expired PoC whose
/// owner saw no reminder, starts nothing for it while its expiry stop is younger than pocs.lifetime.unseen_delete_after_days
/// (read from config) and deletes it after; with pocs.enabled off it starts nothing. A real purge run removes a blocked
/// upload older than pocs.blocked_retention_days, the overrule is then refused saying so, and the next pass removes the
/// blocked PoC that never deployed.
/// </summary>
[Trait("TestProcess", "env-1")]
[Collection(EnvVarCollection.Name)]
public sealed class PocLifetimePassTests(PocVariablesFixture fixture) : IClassFixture<PocVariablesFixture>
{
    private readonly PocImportClient _client = new(fixture);

    [Fact]
    public async Task Pass_StopsAnExpiredUnseenPoc_ThenNothingUntilTheUnseenDays_ThenDeletes()
    {
        fixture.Pocs.Lifetime.UnseenDeleteAfterDays = 10;
        var poc = await SeedAsync("unseen-app", DateTimeOffset.UtcNow.AddDays(-20));

        await PassAsync();
        var stop = Queued(poc.Id).Should().ContainSingle().Subject;
        await FinishAsync(stop.RunId!, PocStates.Stopped);
        await _client.ReadAsync(async db => { db.Add(PocRows.Entry(poc.Id, PocActs.Expired, stop.RunId, DateTimeOffset.UtcNow.UtcTicks)); return await db.SaveChangesAsync(); });
        await PassAsync();
        var afterStop = Queued(poc.Id).Count;
        var elevenDaysAgo = DateTimeOffset.UtcNow.AddDays(-11).UtcTicks;
        await _client.ReadAsync(db => db.Set<PocAuditEntry>().Where(e => e.RunId == stop.RunId)
            .ExecuteUpdateAsync(set => set.SetProperty(e => e.AtTicks, elevenDaysAgo)));
        await PassAsync();

        (stop.PipelineName, stop.Context![ContextKeys.PocLifetimeCase].ToString(), stop.Context[ContextKeys.PocActor].ToString())
            .Should().Be((PipelinePresets.PocStopName, PocLifetimeCases.ExpiryStop, PocLifetimePass.Actor));
        afterStop.Should().Be(1, "the expiry stop is younger than unseen_delete_after_days");
        var delete = Queued(poc.Id).Last();
        (delete.PipelineName, delete.Context![ContextKeys.PocLifetimeCase].ToString()).Should().Be((PipelinePresets.PocDeleteName, PocLifetimeCases.UnseenDelete));
    }

    [Fact]
    public async Task Pass_WithPocsOff_StartsNothing()
    {
        var poc = await SeedAsync("off-app", DateTimeOffset.UtcNow.AddDays(-20));
        var service = new PocLifetimeHostedService(fixture.Server.Services, new ServerContext("c.yml"), Mock.Of<IConfigurationLoader>(),
            NullLogger<PocLifetimeHostedService>.Instance);

        await service.PassAsync(new AgentSmithConfig { Pocs = new PocsConfig { Enabled = false, Targets = fixture.Pocs.Targets } }, default);

        Queued(poc.Id).Should().BeEmpty();
    }

    [Fact]
    public async Task Purge_RemovesAnEightDayOldBlockedUpload_TheOverruleIsRefusedSayingSo_AndTheNextPassRemovesThePoc()
    {
        var poc = await BlockedAsync("blocked-app", daysOld: 8);
        var purge = await RunPurgeAsync();
        purge.IsSuccess.Should().BeTrue(purge.Message);
        (await _client.ReadAsync(db => db.Set<ReferenceFile>().CountAsync(f => f.SessionId == "poc-upload:lifetime"))).Should().Be(0);

        var overrule = await _client.CallAsync(Admins, HttpMethod.Post, $"/api/pocs/{poc.Id}/revisions/1/overrule", new { reason = "accepted" });
        await PassAsync();

        overrule.StatusCode.Should().Be(HttpStatusCode.Gone);
        (await overrule.Content.ReadAsStringAsync()).Should().Contain("revision 1").And.Contain("after 7 days").And.Contain(PocsConfig.BlockedRetentionDaysKey);
        var removal = Queued(poc.Id).Should().ContainSingle().Subject;
        (removal.PipelineName, removal.Context![ContextKeys.PocLifetimeCase].ToString()).Should().Be((PipelinePresets.PocDeleteName, PocLifetimeCases.Removal));
    }

    private async Task<AgentSmith.Domain.Models.CommandResult> RunPurgeAsync()
    {
        await using var scope = fixture.Server.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IActRun>()
            .RunAsync(InstallationScope.Name, PipelinePresets.PocUploadPurgeName, new Dictionary<string, object>());
    }

    private async Task PassAsync()
    {
        await using var scope = fixture.Server.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<PocLifetimePass>().RunAsync(fixture.Pocs, default);
    }

    private List<AgentSmith.Contracts.Models.PipelineRequest> Queued(long pocId)
    {
        lock (fixture.Queued)
            return [.. fixture.Queued.Where(q => q.Context is { } c && c.TryGetValue(ContextKeys.PocId, out var id) && id.ToString() == pocId.ToString())];
    }

    // The queued run's projection would finish its row and the follow would settle the PoC; this host runs neither.
    private Task FinishAsync(string runId, string state) => _client.ReadAsync(async db =>
    {
        var run = await db.Set<Run>().SingleAsync(r => r.Id == runId);
        (run.FinishedAt, run.Status) = (DateTimeOffset.UtcNow, RunStatuses.Success);
        await db.Set<Poc>().Where(p => p.LastRunId == runId).ExecuteUpdateAsync(set => set.SetProperty(p => p.State, state).SetProperty(p => p.Replicas, 0));
        return await db.SaveChangesAsync();
    });

    private Task<Poc> SeedAsync(string name, DateTimeOffset expires) => _client.ReadAsync(async db =>
    {
        var poc = PocRows.Poc(name, "poc-ann");
        (poc.Owner, poc.ExpiresAtTicks) = ("ann-person", expires.UtcTicks);
        return await PocRows.AddAsync(db, poc);
    });

    private Task<Poc> BlockedAsync(string name, int daysOld) => _client.ReadAsync(async db =>
    {
        var poc = PocRows.Poc(name, "poc-ann");
        (poc.Owner, poc.State, poc.Revision, poc.ImageDigest) = ("ann-person", PocStates.Blocked, null, null);
        await PocRows.AddAsync(db, poc);
        var set = (await new ReferenceSetRepository(db).AddAsync("poc-upload:lifetime",
            [new ReferenceFile { RelativePath = "a.zip", MediaType = "application/zip", Content = [1] }], default)).SetId;
        var created = DateTimeOffset.UtcNow.AddDays(-daysOld);
        await db.Set<ReferenceFile>().Where(f => f.SetId == set).ExecuteUpdateAsync(s => s.SetProperty(f => f.CreatedAt, created));
        var verdict = new PocScanVerdict([new PocScanFinding("osv:CVE-1|django|3.2.0", PocScanFindingKinds.Vulnerability, "CVE-1",
            "requirements.txt", null, "SQLi", "CVSS 9.8", null, "Upgrade", [])], []);
        db.Add(new PocRevision
        {
            PocId = poc.Id, Number = 1, UploadSession = "poc-upload:lifetime", UploadId = set, Actor = "ann-person", State = PocRevisionStates.Blocked,
            VerdictJson = verdict.ToJson(), BlockedStage = PocScanStages.Code,
        });
        await db.SaveChangesAsync();
        return poc;
    });
}
