using System.Net;
using System.Text.Json;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Pocs;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Server.Services.Pocs;
using AgentSmith.Tests.Pocs.Variables;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static AgentSmith.Tests.Pocs.PocsAuthorityFixture;

namespace AgentSmith.Tests.Pocs.Acts;

/// <summary>
/// 2026-11-23-c81ed, over HTTP on an enforcing server: the owner's act queues its preset in the PoC's project with string
/// seeds; another person's PoC is an empty 404; a live run answers 409 naming it; a state the act is not allowed from and
/// a wrong delete confirmation are refused; the detail answer names a pending delete and the acts allowed; a variable
/// change on a deployed PoC queues poc-apply by the gate's handover, on a deleted one nothing.
/// </summary>
[Trait("TestProcess", "env-1")]
[Collection(EnvVarCollection.Name)]
public sealed class PocActEndpointTests(PocVariablesFixture fixture) : IClassFixture<PocVariablesFixture>
{
    private readonly PocImportClient _client = new(fixture);

    [Fact]
    public async Task Stop_ByTheOwner_QueuesPocStopInThePocsProject()
    {
        var poc = await SeedAsync("stop-app");

        var response = await _client.CallAsync(Ann, HttpMethod.Post, $"/api/pocs/{poc.Id}/acts/stop", new { });

        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        var runId = (await Json(response)).GetProperty("runId").GetString();
        var queued = fixture.Queued.Should().ContainSingle(q => q.RunId == runId).Subject;
        (queued.PipelineName, queued.ProjectName, queued.Context![ContextKeys.PocId].ToString())
            .Should().Be((PipelinePresets.PocStopName, "poc-ann", poc.Id.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        (await _client.ReadAsync(db => db.Set<Poc>().AsNoTracking().SingleAsync(p => p.Id == poc.Id))).LastRunId.Should().Be(runId);
    }

    [Fact]
    public async Task Act_OnAnotherPersonsPoc_IsAnEmpty404()
    {
        var poc = await SeedAsync("anns-act");

        var response = await _client.CallAsync(Bob, HttpMethod.Post, $"/api/pocs/{poc.Id}/acts/stop", new { });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await response.Content.ReadAsStringAsync()).Should().BeEmpty();
        (await _client.CallAsync(Bob, HttpMethod.Get, $"/api/pocs/{poc.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Act_WhileARunIsLive_Answers409NamingIt()
    {
        var poc = await SeedAsync("busy-act");
        string? live;
        await using (var scope = fixture.Server.Services.CreateAsyncScope())
            live = (await scope.ServiceProvider.GetRequiredService<PocRunGate>().ClaimAsync(poc, PipelinePresets.PocDeployName, "deploying", default)).RunId;

        var response = await _client.CallAsync(Ann, HttpMethod.Post, $"/api/pocs/{poc.Id}/acts/stop", new { });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await response.Content.ReadAsStringAsync()).Should().Contain($"run {live}");
    }

    [Fact]
    public async Task Start_FromReady_IsRefusedNamingTheState_ARollbackWithoutRevision_IsABadRequest()
    {
        var poc = await SeedAsync("ready-act");

        var start = await _client.CallAsync(Ann, HttpMethod.Post, $"/api/pocs/{poc.Id}/acts/start", new { });
        var rollback = await _client.CallAsync(Ann, HttpMethod.Post, $"/api/pocs/{poc.Id}/acts/rollback", new { });

        start.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await start.Content.ReadAsStringAsync()).Should().Contain("'ready-act' is ready; start needs it stopped");
        rollback.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Delete_WithAWrongName_IsRefused_WithTheName_QueuesPocDelete()
    {
        var poc = await SeedAsync("del-app");

        var wrong = await _client.CallAsync(Ann, HttpMethod.Post, $"/api/pocs/{poc.Id}/acts/delete", new { confirmName = "del" });
        var right = await _client.CallAsync(Ann, HttpMethod.Post, $"/api/pocs/{poc.Id}/acts/delete", new { confirmName = "del-app" });

        wrong.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await wrong.Content.ReadAsStringAsync()).Should().Contain("'del-app'");
        right.StatusCode.Should().Be(HttpStatusCode.Created);
        fixture.Queued.Should().Contain(q => q.PipelineName == PipelinePresets.PocDeleteName && q.Context![ContextKeys.PocId].ToString() == poc.Id.ToString());
    }

    [Fact]
    public async Task Detail_NamesAPendingDelete_TheAllowedActs_AndTheAuditTrail()
    {
        var poc = await SeedAsync("pending-detail");
        await _client.ReadAsync(async db =>
        {
            await db.Set<Poc>().Where(p => p.Id == poc.Id).ExecuteUpdateAsync(set => set.SetProperty(p => p.Revision, (int?)null)
                .SetProperty(p => p.ImageDigest, (string?)null));
            db.Add(PocRows.Entry(poc.Id, PocActs.Deleted, "run-del"));
            return await db.SaveChangesAsync();
        });

        var detail = await Json(await _client.CallAsync(Ann, HttpMethod.Get, $"/api/pocs/{poc.Id}"));

        detail.GetProperty("deletePending").GetBoolean().Should().BeTrue();
        detail.GetProperty("acts").GetProperty("delete").GetBoolean().Should().BeTrue();
        detail.GetProperty("acts").GetProperty("stop").GetBoolean().Should().BeFalse();
        detail.GetProperty("audit")[0].GetProperty("act").GetString().Should().Be(PocActs.Deleted);
        detail.GetProperty("readinessPathMaxChars").GetInt32().Should().Be(256);
    }

    [Fact]
    public async Task Revisions_MarkTheHealthyOlderOneAsRollBackable()
    {
        var poc = await SeedAsync("rev-app");
        await _client.ReadAsync(async db =>
        {
            foreach (var (n, digest) in new[] { (1, "sha256:" + new string('0', 64)), (2, PocRows.Digest) })
                db.Add(new PocRevision
                {
                    PocId = poc.Id, Number = n, UploadSession = "s", UploadId = $"rev-{n}", State = PocRevisionStates.Built, Actor = "ann-person",
                    Digest = digest, RolloutState = PocRolloutStates.Healthy, RunId = $"run-{n}",
                });
            await db.Set<Poc>().Where(p => p.Id == poc.Id).ExecuteUpdateAsync(set => set.SetProperty(p => p.Revision, 2));
            return await db.SaveChangesAsync();
        });

        var rows = (await Json(await _client.CallAsync(Ann, HttpMethod.Get, $"/api/pocs/{poc.Id}/revisions"))).GetProperty("revisions");

        rows.EnumerateArray().Select(r => (r.GetProperty("number").GetInt32(), r.GetProperty("mayRollBack").GetBoolean(), r.GetProperty("current").GetBoolean()))
            .Should().Equal((2, false, true), (1, true, false));
    }

    [Fact]
    public async Task Variables_OnADeployedPoc_QueuePocApplyByHandover_OnADeletedOneNothing()
    {
        var deployed = await SeedAsync("apply-vars");
        var deleted = await SeedAsync("gone-vars");
        await _client.ReadAsync(db => db.Set<Poc>().Where(p => p.Id == deleted.Id)
            .ExecuteUpdateAsync(set => set.SetProperty(p => p.State, PocStates.Deleted).SetProperty(p => p.LiveName, (string?)null)));
        var change = new { set = new[] { new { name = "LOG_LEVEL", secret = false, value = "debug" } } };

        var applied = await Json(await _client.CallAsync(Ann, HttpMethod.Put, $"/api/pocs/{deployed.Id}/variables", change));
        var none = await Json(await _client.CallAsync(Ann, HttpMethod.Put, $"/api/pocs/{deleted.Id}/variables", change));

        var applyRun = applied.GetProperty("applyRunId").GetString();
        fixture.Queued.Should().ContainSingle(q => q.RunId == applyRun).Which.PipelineName.Should().Be(PipelinePresets.PocApplyName);
        (await _client.ReadAsync(db => db.Set<Poc>().AsNoTracking().SingleAsync(p => p.Id == deployed.Id))).LastRunId.Should().Be(applyRun);
        none.GetProperty("applyRunId").ValueKind.Should().Be(JsonValueKind.Null);
        fixture.Queued.Should().NotContain(q => q.PipelineName == PipelinePresets.PocApplyName
            && q.Context![ContextKeys.PocId].ToString() == deleted.Id.ToString());
    }

    private static async Task<JsonElement> Json(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

    private Task<Poc> SeedAsync(string name) => _client.ReadAsync(async db =>
    {
        var poc = PocRows.Poc(name, "poc-ann");
        (poc.Owner, poc.State) = ("ann-person", PocStates.Ready);
        return await PocRows.AddAsync(db, poc);
    });
}
