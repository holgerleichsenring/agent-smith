using System.Net;
using System.Text.Json;
using AgentSmith.Application.Services;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using static AgentSmith.Tests.Server.Auth.ProjectScopeAuthorityFixture;

namespace AgentSmith.Tests.Server.Auth;

/// <summary>
/// 2026-10-25-0099b: GET /api/delivery for a caller bound to alpha holds alpha's tickets alone — no beta ticket,
/// count, cost or project row, and no beta chip; asking for beta answers what an unknown project answers. The
/// seeds are minted at the system clock's now, because the window is rolling; every ticket id is unique, the
/// server being shared by the class.
/// </summary>
[Trait("TestProcess", "env-1")]
[Collection(EnvVarCollection.Name)]
public sealed class DeliveryScopeTests(ProjectScopeAuthorityFixture fixture) : IClassFixture<ProjectScopeAuthorityFixture>
{
    [Fact]
    public async Task Delivery_BootedScopedCaller_SeesNoTicketCountOrProjectOfAnotherProject()
    {
        await SeedAsync(("alpha", "D-1", -1), ("beta", "D-2", -2), ("beta", "D-3", -3));

        var root = await ReadAsync(AlphaTeam, "/api/delivery?period=week");

        Tickets(root).Should().Contain(("alpha", "D-1")).And.NotContain(t => t.Project == "beta");
        Strings(root, "projects").Should().Equal("alpha", "team/alpha");
        root.GetProperty("byProject").EnumerateArray().Select(p => p.GetProperty("project").GetString())
            .Should().NotContain("beta");
        root.GetProperty("current").GetProperty("ticketsWorked").GetInt32()
            .Should().Be(Tickets(root).Count, "the figures count the rows the caller sees");
    }

    [Fact]
    public async Task Delivery_BootedScopedCallerAskingForAnotherProject_AnswersAsAnUnknownProject()
    {
        await SeedAsync(("beta", "D-4", -1));

        var beta = await ReadAsync(AlphaTeam, "/api/delivery?period=week&project=beta");
        var nowhere = await ReadAsync(AlphaTeam, "/api/delivery?period=week&project=nowhere");

        Tickets(beta).Should().BeEmpty();
        beta.GetProperty("current").GetRawText().Should().Be(nowhere.GetProperty("current").GetRawText());
    }

    [Fact]
    public async Task Delivery_BootedGlobalHolder_SeesEveryProject()
    {
        await SeedAsync(("alpha", "D-5", -1), ("beta", "D-6", -1));

        var root = await ReadAsync(Admins, "/api/delivery?period=day");

        Tickets(root).Should().Contain([("alpha", "D-5"), ("beta", "D-6")]);
        Strings(root, "projects").Should().Equal("alpha", "beta", "team/alpha");
    }

    [Fact]
    public async Task DeliveryReader_PreviousPeriodAndSlots_AreComputedApart()
    {
        await SeedAsync(("alpha", "D-7", -1), ("alpha", "D-8", -30));

        var root = await ReadAsync(AlphaTeam, "/api/delivery?period=day&project=alpha");

        Tickets(root).Should().Contain(("alpha", "D-7")).And.NotContain(("alpha", "D-8"));
        root.GetProperty("previous").GetProperty("ticketsWorked").GetInt32().Should().BeGreaterThanOrEqualTo(1);
        root.GetProperty("slots").GetArrayLength().Should().Be(24);
        root.GetProperty("slots").EnumerateArray().Sum(s => s.GetProperty("ticketsWorked").GetInt32())
            .Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task DeliveryTicketComposer_Bucket_IsTheTicketListsBucket()
    {
        await SeedAsync(("alpha", "D-9", -1));

        var delivery = await ReadAsync(AlphaTeam, "/api/delivery?period=day");
        var list = await ReadAsync(AlphaTeam, "/api/tickets?project=alpha");

        var bucket = delivery.GetProperty("tickets").EnumerateArray()
            .Single(t => t.GetProperty("ticketId").GetString() == "D-9").GetProperty("bucket").GetString();
        var listed = list.GetProperty("tickets").EnumerateArray()
            .Single(t => t.GetProperty("ticketId").GetString() == "D-9").GetProperty("bucket").GetString();
        bucket.Should().NotBeNull().And.Be(listed);
    }

    [Fact]
    public async Task Delivery_UnknownPeriod_Is400()
    {
        var response = await fixture.Server.Client.SendAsync(fixture.As(Admins, HttpMethod.Get, "/api/delivery?period=year"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private Task SeedAsync(params (string Project, string Ticket, int HoursAgo)[] runs) =>
        fixture.SeedAsync(async db =>
        {
            foreach (var (project, ticket, hoursAgo) in runs) Add(db, project, ticket, DateTimeOffset.UtcNow.AddHours(hoursAgo));
            await db.SaveChangesAsync();
        });

    private static void Add(AgentSmithDbContext db, string project, string ticket, DateTimeOffset at) => db.Add(new Run
    {
        Id = RunIdGenerator.Generate(at), Project = project, Pipeline = "code", TicketId = ticket, Status = "success",
        StartedAt = at, FinishedAt = at.AddMinutes(10), DurationSeconds = 600, CostTotalUsd = 1.5m,
    });

    private async Task<JsonElement> ReadAsync(string group, string path)
    {
        var response = await fixture.Server.Client.SendAsync(fixture.As(group, HttpMethod.Get, path));
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
    }

    private static List<(string Project, string TicketId)> Tickets(JsonElement root) =>
        [.. root.GetProperty("tickets").EnumerateArray()
            .Select(t => (t.GetProperty("project").GetString()!, t.GetProperty("ticketId").GetString()!))];

    private static List<string> Strings(JsonElement root, string name) =>
        [.. root.GetProperty(name).EnumerateArray().Select(n => n.GetString()!)];
}
