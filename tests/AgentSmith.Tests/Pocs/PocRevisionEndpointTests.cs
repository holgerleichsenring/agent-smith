using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Pocs;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using static AgentSmith.Tests.Pocs.PocsAuthorityFixture;

namespace AgentSmith.Tests.Pocs;

/// <summary>
/// 2026-11-23-933dc over HTTP on an enforcing server: a revision's page shows its verdict and explanation with
/// may_overrule and why_not; an overrule needs pocs.overrule AND runs.read in the PoC's project, refuses a blank or
/// over-long reason, a secret block, an older revision, a purged upload and a live run, and otherwise queues poc-deploy
/// seeded with the reason and the blocking ids.
/// </summary>
[Trait("TestProcess", "env-1")]
[Collection(EnvVarCollection.Name)]
public sealed class PocRevisionEndpointTests(PocImportFixture fixture) : IClassFixture<PocImportFixture>
{
    [Fact]
    public async Task Read_TheOwner_SeesVerdictAndExplanation_ButMayNotOverrule()
    {
        var poc = await SeedAsync(Vulnerability());

        var answer = await ReadAsync(Ann, poc.Id, 1);

        answer.Verdict!.Blocks.Should().ContainSingle().Which.Id.Should().Be("osv:CVE-1|django|3.2.0");
        answer.Explanation.Should().Be("agent text");
        (answer.MayOverrule, answer.WhyNot).Should().Be((false, "a holder of pocs.overrule who can read this PoC's runs can lift this block"));
    }

    [Theory]
    [InlineData(Bob)]
    [InlineData(Overrulers)]
    public async Task Read_AForeignPocOrNoRunsRead_IsAnEmpty404(string group)
    {
        var poc = await SeedAsync(Vulnerability());

        var response = await SendAsync(group, HttpMethod.Get, $"/api/pocs/{poc.Id}/revisions/1");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await response.Content.ReadAsStringAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task Read_AnOverrulerWithGlobalReader_MayOverrule_ButNotASecret()
    {
        var vulnerable = await SeedAsync(Vulnerability());
        var secret = await SeedAsync(Secret());

        (await ReadAsync(ReadingOverrulers, vulnerable.Id, 1)).MayOverrule.Should().BeTrue();
        (await ReadAsync(ReadingOverrulers, secret.Id, 1)).WhyNot.Should().Be("a secret cannot be overruled: remove it and upload again");
    }

    [Fact]
    public async Task Overrule_WithoutPocsOverrule_Is403_WithoutRunsRead_IsAnEmpty404()
    {
        var poc = await SeedAsync(Vulnerability());

        (await OverruleAsync(Ann, poc.Id, 1, "why")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var hidden = await OverruleAsync(Overrulers, poc.Id, 1, "why");
        hidden.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await hidden.Content.ReadAsStringAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task Overrule_ABadReasonASecretAnOlderRevisionOrAPurgedUpload_IsRefusedNamingWhy()
    {
        var poc = await SeedAsync(Vulnerability());
        var secret = await SeedAsync(Secret());
        var older = await SeedAsync(Vulnerability(), revisions: 2);
        var purged = await SeedAsync(Vulnerability(), upload: false);

        (await TextAsync(OverruleAsync(Admins, poc.Id, 1, "  "))).Should().Contain("needs a reason");
        (await TextAsync(OverruleAsync(Admins, poc.Id, 1, new string('r', 501)))).Should().Contain("PocAuditEntries.Reason").And.Contain("500");
        (await TextAsync(OverruleAsync(Admins, secret.Id, 1, "why"))).Should().Contain("A secret cannot be overruled");
        (await TextAsync(OverruleAsync(Admins, older.Id, 1, "why"))).Should().Contain("older than r2");
        var gone = await OverruleAsync(Admins, purged.Id, 1, "why");
        gone.StatusCode.Should().Be(HttpStatusCode.Gone);
        (await gone.Content.ReadAsStringAsync()).Should().Contain("after 7 days").And.Contain("pocs.blocked_retention_days");
    }

    [Fact]
    public async Task Overrule_AVulnerabilityBlock_QueuesPocDeployWithReasonAndIds_ASecondOneWhileItRunsIs409()
    {
        var poc = await SeedAsync(Vulnerability());

        var started = await OverruleAsync(Admins, poc.Id, 1, "accepted for the demo");
        var again = await OverruleAsync(Admins, poc.Id, 1, "again");

        started.StatusCode.Should().Be(HttpStatusCode.Created);
        var runId = (await started.Content.ReadFromJsonAsync<Started>())!.RunId;
        var queued = fixture.Queued.Single(q => q.RunId == runId);
        queued.PipelineName.Should().Be(PipelinePresets.PocDeployName);
        queued.Context![ContextKeys.PocOverrule].ToString().Should().Be("true");
        queued.Context[ContextKeys.PocOverruleReason].ToString().Should().Be("accepted for the demo");
        queued.Context[ContextKeys.PocOverruleIds].ToString().Should().Be("""["osv:CVE-1|django|3.2.0"]""");
        again.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Overrule_AnImageStageBlock_ReadsTheImageVerdict_AndQueuesItsIds_ASecretInTheImageIsRefused()
    {
        var image = new PocScanVerdict([new PocScanFinding("trivy:CVE-9|openssl|1.0", PocScanFindingKinds.Vulnerability, "CVE-9", "debian 12",
            null, "t", "CVSS 9.8", null, null, [])], []);
        var poc = await SeedAsync(new PocScanVerdict([], []), image: image);
        var secret = await SeedAsync(new PocScanVerdict([], []), image: Secret());

        var answer = await ReadAsync(ReadingOverrulers, poc.Id, 1);
        var started = await OverruleAsync(Admins, poc.Id, 1, "base image fix pending");

        (answer.BlockedStage, answer.MayOverrule).Should().Be((PocScanStages.Image, true));
        answer.ImageVerdict!.Blocks.Should().ContainSingle().Which.Id.Should().Be("trivy:CVE-9|openssl|1.0");
        var runId = (await started.Content.ReadFromJsonAsync<Started>())!.RunId;
        fixture.Queued.Single(q => q.RunId == runId).Context![ContextKeys.PocOverruleIds].ToString().Should().Be("""["trivy:CVE-9|openssl|1.0"]""");
        (await TextAsync(OverruleAsync(Admins, secret.Id, 1, "why"))).Should().Contain("A secret cannot be overruled");
    }

    [Fact]
    public async Task Install_NeedsPocsAdmin_AndAnUnknownTargetIsAnEmpty404()
    {
        (await SendAsync(Ann, HttpMethod.Post, "/api/pocs/targets/main/install")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var unknown = await SendAsync(Admins, HttpMethod.Post, "/api/pocs/targets/nowhere/install");
        unknown.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private static PocScanVerdict Vulnerability() => new(
        [new PocScanFinding("osv:CVE-1|django|3.2.0", PocScanFindingKinds.Vulnerability, "CVE-1", "requirements.txt", null, "SQLi", "CVSS 9.8", null, "Upgrade", ["GHSA-1"])], []);

    private static PocScanVerdict Secret() => new(
        [new PocScanFinding("sec:1", PocScanFindingKinds.Secret, "aws-access-key", "a.py", 1, "AWS", "key", "<secret at a.py:1>", "Remove", [])], []);

    private async Task<Poc> SeedAsync(PocScanVerdict verdict, int revisions = 1, bool upload = true, PocScanVerdict? image = null)
    {
        Poc poc = null!;
        await fixture.SeedAsync(async db =>
        {
            poc = await PocRows.AddAsync(db, PocRows.Poc("p" + Guid.NewGuid().ToString("N")[..8], "poc-ann"));
            var set = upload
                ? (await new ReferenceSetRepository(db).AddAsync("poc-upload:test", [new ReferenceFile { RelativePath = "a.zip", MediaType = "application/zip", Content = [1] }], default)).SetId
                : "gone";
            for (var n = 1; n <= revisions; n++)
                db.Add(new PocRevision
                {
                    PocId = poc.Id, Number = n, UploadSession = "poc-upload:test", UploadId = set, Actor = "ann-sub",
                    State = PocRevisionStates.Blocked, VerdictJson = verdict.ToJson(), Explanation = "agent text",
                    ImageVerdictJson = image?.ToJson(), BlockedStage = image is null ? PocScanStages.Code : PocScanStages.Image, // 933dd
                });
            await db.SaveChangesAsync();
        });
        return poc;
    }

    private async Task<Answer> ReadAsync(string group, long pocId, int n)
    {
        var response = await SendAsync(group, HttpMethod.Get, $"/api/pocs/{pocId}/revisions/{n}");
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<Answer>())!;
    }

    private Task<HttpResponseMessage> OverruleAsync(string group, long pocId, int n, string reason) =>
        SendAsync(group, HttpMethod.Post, $"/api/pocs/{pocId}/revisions/{n}/overrule", JsonContent.Create(new { reason }));

    private static async Task<string> TextAsync(Task<HttpResponseMessage> response) => await (await response).Content.ReadAsStringAsync();

    private Task<HttpResponseMessage> SendAsync(string group, HttpMethod method, string path, HttpContent? content = null)
    {
        var request = new HttpRequestMessage(method, path) { Content = content };
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer", fixture.Issuer.Token(PocsAuthorityFixture.Audience, extra: [new Claim("groups", group)], subject: $"{group}-person"));
        return fixture.Server.Client.SendAsync(request);
    }

    private sealed record Answer(long PocId, int Revision, string State, PocScanVerdict? Verdict, string? Explanation, bool MayOverrule, string? WhyNot,
        string? BlockedStage = null, PocScanVerdict? ImageVerdict = null);

    private sealed record Started(long PocId, int Revision, string RunId);
}
