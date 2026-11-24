using System.Net;
using System.Text.Json;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Pocs;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Tests.Pocs.Variables;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using static AgentSmith.Tests.Pocs.PocsAuthorityFixture;

namespace AgentSmith.Tests.Pocs.Lifetime;

/// <summary>
/// 2026-11-23-c81ee, over HTTP on an enforcing server: the owner's seen reminder is recorded only inside the window (an
/// admin's never, the owner's after the expiry never); the list and the detail answer carry the lifetime fields; an owner
/// extends twice, the third is refused naming the key, and an admin's passes.
/// </summary>
[Trait("TestProcess", "env-1")]
[Collection(EnvVarCollection.Name)]
public sealed class PocLifetimeEndpointTests(PocVariablesFixture fixture) : IClassFixture<PocVariablesFixture>
{
    private readonly PocImportClient _client = new(fixture);

    [Fact]
    public async Task Seen_TheOwnerInsideTheWindowRecords_AnAdminNothing_TheOwnerAfterExpiryNothing()
    {
        var expiring = await SeedAsync("seen-app", DateTimeOffset.UtcNow.AddDays(5));
        var expired = await SeedAsync("seen-late", DateTimeOffset.UtcNow.AddDays(-1));

        var admin = await _client.CallAsync(Admins, HttpMethod.Post, $"/api/pocs/{expiring.Id}/reminders/seen");
        var adminSeen = (await RowAsync(expiring.Id)).ReminderSeenAtTicks;
        var owner = await _client.CallAsync(Ann, HttpMethod.Post, $"/api/pocs/{expiring.Id}/reminders/seen");
        var late = await _client.CallAsync(Ann, HttpMethod.Post, $"/api/pocs/{expired.Id}/reminders/seen");

        (admin.StatusCode, owner.StatusCode, late.StatusCode).Should().Be((HttpStatusCode.NoContent, HttpStatusCode.NoContent, HttpStatusCode.NoContent));
        adminSeen.Should().BeNull("an admin's view never counts as the owner's");
        (await RowAsync(expiring.Id)).ReminderSeenAtTicks.Should().NotBeNull();
        (await RowAsync(expired.Id)).ReminderSeenAtTicks.Should().BeNull("a view after the expiry never counts");
    }

    [Fact]
    public async Task LifetimeFields_OnTheListAndTheDetail_MayExtendFalseForTheOwnerAtTwo_TrueForAnAdmin()
    {
        var expires = DateTimeOffset.UtcNow.AddDays(5);
        var poc = await SeedAsync("fields-app", expires, extensions: 2);

        var detail = (await JsonAsync(Ann, $"/api/pocs/{poc.Id}")).GetProperty("lifetime");
        var item = (await JsonAsync(Ann, "/api/pocs")).GetProperty("pocs").EnumerateArray().Single(p => p.GetProperty("id").GetInt64() == poc.Id)
            .GetProperty("lifetime");
        var admin = (await JsonAsync(Admins, $"/api/pocs/{poc.Id}")).GetProperty("lifetime");

        foreach (var lifetime in new[] { detail, item })
        {
            lifetime.GetProperty("expiresAt").GetDateTimeOffset().UtcTicks.Should().Be(new DateTimeOffset(expires.UtcTicks, TimeSpan.Zero).UtcTicks);
            (lifetime.GetProperty("daysLeft").GetInt32(), lifetime.GetProperty("windowOpen").GetBoolean(), lifetime.GetProperty("expired").GetBoolean())
                .Should().Be((5, true, false));
            lifetime.GetProperty("deletesAt").ValueKind.Should().Be(JsonValueKind.Null);
            (lifetime.GetProperty("extensions").GetInt32(), lifetime.GetProperty("ownerAllowance").GetInt32(), lifetime.GetProperty("mayExtend").GetBoolean())
                .Should().Be((2, 2, false));
        }
        admin.GetProperty("mayExtend").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task Detail_OfAnExpiredStoppedPoc_RefusesStart_AndNamesWhenItIsDeleted()
    {
        var poc = await SeedAsync("expired-app", DateTimeOffset.UtcNow.AddDays(-3), state: PocStates.Stopped);
        var stop = DateTimeOffset.UtcNow.AddDays(-2);
        await _client.ReadAsync(async db => { db.Add(PocRows.Entry(poc.Id, PocActs.Expired, "expiry-run", stop.UtcTicks)); return await db.SaveChangesAsync(); });

        var detail = await JsonAsync(Ann, $"/api/pocs/{poc.Id}");
        var start = await _client.CallAsync(Ann, HttpMethod.Post, $"/api/pocs/{poc.Id}/acts/start", new { });

        detail.GetProperty("acts").GetProperty("start").GetBoolean().Should().BeFalse();
        detail.GetProperty("lifetime").GetProperty("deletesAt").GetDateTimeOffset().UtcTicks.Should().Be(stop.AddDays(30).UtcTicks);
        start.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await start.Content.ReadAsStringAsync()).Should().Contain("expired on").And.Contain("extend it first");
    }

    [Fact]
    public async Task Extend_TwiceByTheOwner_TheThirdRefusedNamingTheKey_AnAdminsPasses()
    {
        var poc = await SeedAsync("extend-app", DateTimeOffset.UtcNow.AddDays(5));

        var first = await ExtendAsync(Ann, poc.Id);
        var second = await ExtendAsync(Ann, poc.Id);
        var third = await ExtendAsync(Ann, poc.Id);
        var admin = await ExtendAsync(Admins, poc.Id);
        var foreign = await _client.CallAsync(Bob, HttpMethod.Post, $"/api/pocs/{poc.Id}/acts/extend");

        (first.StatusCode, second.StatusCode).Should().Be((HttpStatusCode.OK, HttpStatusCode.OK), await second.Content.ReadAsStringAsync());
        third.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await third.Content.ReadAsStringAsync()).Should().Contain(PocLifetimeConfig.MaxOwnerExtensionsKey);
        admin.StatusCode.Should().Be(HttpStatusCode.OK, await admin.Content.ReadAsStringAsync());
        var answer = JsonDocument.Parse(await admin.Content.ReadAsStringAsync()).RootElement;
        answer.GetProperty("extensions").GetInt32().Should().Be(3);
        answer.GetProperty("expiresAt").GetDateTimeOffset().Should().BeCloseTo(DateTimeOffset.UtcNow.AddDays(5 + 3 * 90), TimeSpan.FromMinutes(5));
        foreign.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // An extension's run is awaited; its row is finished by the run event projection, which this host does not run — the
    // test finishes it the way the projection would before the next click.
    private async Task<HttpResponseMessage> ExtendAsync(string group, long pocId)
    {
        var response = await _client.CallAsync(group, HttpMethod.Post, $"/api/pocs/{pocId}/acts/extend");
        var runId = (await RowAsync(pocId)).LastRunId;
        await _client.ReadAsync(async db =>
        {
            var run = await db.Set<Run>().SingleAsync(r => r.Id == runId);
            (run.FinishedAt, run.Status) = (DateTimeOffset.UtcNow, AgentSmith.Contracts.Runs.RunStatuses.Success);
            return await db.SaveChangesAsync();
        });
        return response;
    }

    private async Task<JsonElement> JsonAsync(string group, string path)
    {
        var response = await _client.CallAsync(group, HttpMethod.Get, path);
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
    }

    private Task<Poc> RowAsync(long pocId) => _client.ReadAsync(db => db.Set<Poc>().AsNoTracking().SingleAsync(p => p.Id == pocId));

    private Task<Poc> SeedAsync(string name, DateTimeOffset expires, int extensions = 0, string state = PocStates.Ready) => _client.ReadAsync(async db =>
    {
        var poc = PocRows.Poc(name, "poc-ann");
        (poc.Owner, poc.State, poc.ExpiresAtTicks, poc.Extensions) = ("ann-person", state, expires.UtcTicks, extensions);
        if (state == PocStates.Stopped) poc.Replicas = 0;
        return await PocRows.AddAsync(db, poc);
    });
}
