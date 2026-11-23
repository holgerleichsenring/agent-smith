using System.Net;
using System.Net.Http.Json;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Pocs;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using static AgentSmith.Tests.Pocs.PocsAuthorityFixture;

namespace AgentSmith.Tests.Pocs;

/// <summary>
/// 2026-11-23-933db: upload and Start over HTTP on an enforcing server — a refused archive names the file or line and
/// starts nothing; an admitted one is stored under the caller's key; Start writes the Poc row and its revision, claims
/// the run gate and queues poc-deploy; a foreign upload or PoC is an empty 404; the different-app question, a taken
/// name, the per-person limit and a running run answer 409; a retried Start reuses its rows; a purged upload is 410.
/// </summary>
[Trait("TestProcess", "env-1")]
[Collection(EnvVarCollection.Name)]
public sealed class PocImportEndpointTests(PocImportFixture fixture) : IClassFixture<PocImportFixture>
{
    private readonly PocImportClient _client = new(fixture);

    [Fact]
    public async Task Upload_AnEnvFile_IsRefusedNamingIt_AndNothingStarts()
    {
        var response = await _client.UploadAsync(Ann, PocZips.App(("app/.env", PocZips.Text("A=1"))));

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await response.Content.ReadAsStringAsync()).Should().Contain(".env");
    }

    [Fact]
    public async Task Upload_RunNetworkHost_IsRefusedNamingTheLine()
    {
        var zip = PocZips.Zip(("Dockerfile", PocZips.Text("FROM python:3.12\nRUN --network=host pip install x\nEXPOSE 8000\n")));

        var response = await _client.UploadAsync(Ann, zip);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await response.Content.ReadAsStringAsync()).Should().Contain("--network=host");
    }

    [Fact]
    public async Task Upload_AnAdmittedZip_AnswersChecksPortAndProposal()
    {
        var response = await _client.UploadAsync(Ann, PocZips.App(), "My Cool App (2).zip");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var answer = (await response.Content.ReadFromJsonAsync<PocImportClient.UploadAnswer>())!;
        (answer.Port, answer.EntryCount, answer.ProposedName).Should().Be((8000, 2, "my-cool-app-2"));
        answer.Checks.Should().Equal("paths", "kinds", "bounds", "forbidden", "dockerfile", "expose");
        answer.Match.Should().BeNull();
    }

    [Fact]
    public async Task Start_New_ProvisionsTheProject_WritesRowAndRevision_AndQueuesPocDeploy()
    {
        var upload = await _client.UploadIdAsync(Ann, PocZips.App());

        var response = await _client.StartAsync(Ann, upload, new { choice = "new", name = "first-app" });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var started = (await response.Content.ReadFromJsonAsync<PocImportClient.StartAnswer>())!;
        var poc = await _client.ReadAsync(db => db.Set<Poc>().AsNoTracking().SingleAsync(p => p.Id == started.PocId));
        (poc.State, poc.LastRunId, poc.Owner).Should().Be((PocStates.Importing, started.RunId, "ann-person"));
        poc.Project.Should().StartWith("poc-u");
        fixture.Projects.Should().Contain(p => p.Id == poc.Project && p.Repos.Contains("gh/poc-src"));
        var revision = await _client.ReadAsync(db => db.Set<PocRevision>().AsNoTracking().SingleAsync(r => r.UploadId == upload));
        (revision.PocId, revision.Number, revision.RunId).Should().Be((poc.Id, 1, started.RunId));
        var queued = fixture.Queued.Should().ContainSingle(q => q.RunId == started.RunId).Subject;
        queued.PipelineName.Should().Be(PipelinePresets.PocDeployName);
        queued.Context![ContextKeys.PocId].ToString().Should().Be(poc.Id.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public async Task Start_Retried_ReusesItsRows_AndAPurgedUploadIs410WritingNothing()
    {
        var upload = await _client.UploadIdAsync(Ann, PocZips.App());
        var first = await (await _client.StartAsync(Ann, upload, new { choice = "new", name = "retry-app" }))
            .Content.ReadFromJsonAsync<PocImportClient.StartAnswer>();

        var again = await _client.StartAsync(Ann, upload, new { choice = "new", name = "retry-app" });

        (await again.Content.ReadFromJsonAsync<PocImportClient.StartAnswer>()).Should().Be(first);
        await _client.ReadAsync(db => db.Set<ReferenceFile>().Where(f => f.SetId == upload).ExecuteDeleteAsync());
        var counts = await CountsAsync();
        var purged = await _client.StartAsync(Ann, upload, new { choice = "new", name = "retry-app" });
        purged.StatusCode.Should().Be(HttpStatusCode.Gone);
        (await purged.Content.ReadAsStringAsync()).Should().Contain("revision 1").And.Contain("pocs.blocked_retention_days");
        (await CountsAsync()).Should().Be(counts);
    }

    [Fact]
    public async Task Start_AnotherPersonsUpload_IsAnEmpty404()
    {
        var upload = await _client.UploadIdAsync(Bob, PocZips.App());

        var response = await _client.StartAsync(Ann, upload, new { choice = "new", name = "stolen" });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await response.Content.ReadAsStringAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task Upload_ASecondOne_DeletesTheFirstUnimported()
    {
        var first = await _client.UploadIdAsync(Ann, PocZips.App());
        await _client.UploadIdAsync(Ann, PocZips.App());

        (await _client.StartAsync(Ann, first, new { choice = "new", name = "gone-app" })).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Start_Replace_ADifferentApp_AsksUnlessConfirmed_AnotherPersonsPocIs404()
    {
        var poc = await SeedValidatedAsync("shop", "poc-ann", "ann-person", port: 9000);
        var bobs = await SeedValidatedAsync("bobs", "poc-bob", "bob-person", port: 8000);
        var upload = await _client.UploadIdAsync(Ann, PocZips.App());

        var asked = await _client.StartAsync(Ann, upload, new { choice = "replace", pocId = poc.Id });
        var foreign = await _client.StartAsync(Ann, upload, new { choice = "replace", pocId = bobs.Id });
        var confirmed = await _client.StartAsync(Ann, upload, new { choice = "replace", pocId = poc.Id, confirmed = true });

        asked.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await asked.Content.ReadAsStringAsync()).Should().Contain("Is this really 'shop'?");
        foreign.StatusCode.Should().Be(HttpStatusCode.NotFound);
        confirmed.StatusCode.Should().Be(HttpStatusCode.Created);
        (await confirmed.Content.ReadFromJsonAsync<PocImportClient.StartAnswer>())!.Revision.Should().Be(2);
    }

    [Fact]
    public async Task Start_ASecondImportWhileTheFirstRuns_Answers409NamingTheRun()
    {
        // In poc-ann, which the fixture's mapping binds ann to — a provisioned project would need the live store.
        var poc = await SeedAsync("busy-app", "poc-ann", "ann-person", PocStates.Importing);
        var first = await _client.UploadIdAsync(Ann, PocZips.App());
        var started = await (await _client.StartAsync(Ann, first, new { choice = "replace", pocId = poc.Id }))
            .Content.ReadFromJsonAsync<PocImportClient.StartAnswer>();
        var second = await _client.UploadIdAsync(Ann, PocZips.App());

        var response = await _client.StartAsync(Ann, second, new { choice = "replace", pocId = started!.PocId });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await response.Content.ReadAsStringAsync()).Should().Contain(started.RunId);
        (await _client.ReadAsync(db => db.Set<PocRevision>().CountAsync(r => r.PocId == started.PocId))).Should().Be(1);
    }

    [Fact]
    public async Task Start_New_ATakenName_IsRefused_TheOwnersRefusedNameIsReused()
    {
        await SeedAsync("taken-app", "poc-bob", "bob-person", PocStates.Ready);
        var refused = await SeedAsync("again-app", "poc-ann", "ann-person", PocStates.Refused);
        var upload = await _client.UploadIdAsync(Ann, PocZips.App());

        var taken = await _client.StartAsync(Ann, upload, new { choice = "new", name = "taken-app" });
        var reused = await _client.StartAsync(Ann, upload, new { choice = "new", name = "again-app" });

        taken.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await taken.Content.ReadAsStringAsync()).Should().Contain("'taken-app' is taken");
        reused.StatusCode.Should().Be(HttpStatusCode.Created);
        (await reused.Content.ReadFromJsonAsync<PocImportClient.StartAnswer>())!.PocId.Should().Be(refused.Id);
        (await _client.ReadAsync(db => db.Set<Poc>().AsNoTracking().SingleAsync(p => p.Id == refused.Id))).State.Should().Be(PocStates.Importing);
    }

    [Fact]
    public async Task Start_New_TwoPeopleOneNameAtOnce_OneWinsOneIsRefusedNamingIt()
    {
        var ann = await _client.UploadIdAsync(Ann, PocZips.App());
        var bob = await _client.UploadIdAsync(Bob, PocZips.App());

        var answers = await Task.WhenAll(_client.StartAsync(Ann, ann, new { choice = "new", name = "race-app" }),
            _client.StartAsync(Bob, bob, new { choice = "new", name = "race-app" }));

        answers.Select(a => a.StatusCode).Should().BeEquivalentTo([HttpStatusCode.Created, HttpStatusCode.Conflict]);
        (await answers.Single(a => a.StatusCode == HttpStatusCode.Conflict).Content.ReadAsStringAsync()).Should().Contain("race-app");
    }

    [Fact]
    public async Task Start_New_AtThePerPersonLimit_IsRefusedNamingTheKey_AnAdminPasses()
    {
        await _client.ReadAsync(db => db.Set<Poc>().Where(p => p.Owner == "bob-person").ExecuteDeleteAsync());
        fixture.Pocs.Targets[0].PerPersonLimit = 3;
        for (var i = 0; i < 3; i++) await SeedAsync($"full-{i}", "poc-bob", "bob-person", PocStates.Ready);
        var bob = await _client.UploadIdAsync(Bob, PocZips.App());
        var admin = await _client.UploadIdAsync(Admins, PocZips.App());

        var refused = await _client.StartAsync(Bob, bob, new { choice = "new", name = "fourth" });
        var passed = await _client.StartAsync(Admins, admin, new { choice = "new", name = "admin-app" });

        refused.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await refused.Content.ReadAsStringAsync()).Should().Contain("pocs.targets[].per_person_limit").And.Contain("share one cluster");
        passed.StatusCode.Should().Be(HttpStatusCode.Created);
        fixture.Pocs.Targets[0].PerPersonLimit = PocImportFixture.RoomyLimit;
    }

    [Fact]
    public async Task Start_OneUploadStartedTwiceAtOnce_LeavesOnePoc()
    {
        var upload = await _client.UploadIdAsync(Ann, PocZips.App());

        var answers = await Task.WhenAll(_client.StartAsync(Ann, upload, new { choice = "new", name = "twice-a" }),
            _client.StartAsync(Ann, upload, new { choice = "new", name = "twice-b" }));

        answers.Should().OnlyContain(a => a.StatusCode == HttpStatusCode.Created);
        (await _client.ReadAsync(db => db.Set<PocRevision>().CountAsync(r => r.UploadId == upload))).Should().Be(1);
    }

    [Fact]
    public async Task Start_NoTargetWithTwoConfigured_Is400NamingTheKey()
    {
        var upload = await _client.UploadIdAsync(Ann, PocZips.App());
        fixture.Pocs.Targets.Add(new AgentSmith.Contracts.Models.Configuration.PocTargetConfig { Name = "second", SourceRepo = "gh/two" });
        try
        {
            var response = await _client.StartAsync(Ann, upload, new { choice = "new", name = "no-target" });

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await response.Content.ReadAsStringAsync()).Should().Contain("pocs.targets");
        }
        finally { fixture.Pocs.Targets.RemoveAt(1); }
    }

    private Task<(int, int, int)> CountsAsync() => _client.ReadAsync(async db =>
        (await db.Set<Poc>().CountAsync(), await db.Set<PocRevision>().CountAsync(), await db.Runs.CountAsync()));

    private Task<Poc> SeedAsync(string name, string project, string owner, string state) => _client.ReadAsync(async db =>
    {
        var poc = PocRows.Poc(name, project);
        (poc.Owner, poc.State) = (owner, state);
        return await PocRows.AddAsync(db, poc);
    });

    private async Task<Poc> SeedValidatedAsync(string name, string project, string owner, int port)
    {
        var poc = await SeedAsync(name, project, owner, PocStates.Ready);
        await _client.ReadAsync(async db =>
        {
            db.Add(new PocRevision
            {
                PocId = poc.Id, Number = 1, UploadSession = "s", UploadId = $"seed-{name}", State = PocRevisionStates.Validated,
                Actor = owner, ManifestJson = "{}", Port = port,
            });
            return await db.SaveChangesAsync();
        });
        return poc;
    }
}
