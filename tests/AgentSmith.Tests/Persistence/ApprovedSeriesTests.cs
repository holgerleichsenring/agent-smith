using System.Text.Json.Nodes;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Specs;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Infrastructure.Persistence.Services;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.SpecDialog;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Tests.Persistence;

/// <summary>
/// 2026-10-06-03c7f: an approval is stored as the SERIES it approved, over the shipped migrations —
/// keyed by tracker connection and ticket key, the series id, the tracker's own ticket id, the
/// repositories and the goal kept — and a conversation bound to the filed ticket reads it back.
/// </summary>
public sealed class ApprovedSeriesTests : IDisposable
{
    private const string Tracker = "sample-tracker";
    private const string TicketId = "DPG-7";
    private const string Series = "2026-10-06-0a0a";
    private const string Dialog = "d-1";

    private readonly SqliteConnection _connection = MigratedStoreTemplate.OpenCopy();
    private readonly AgentSmithDbContext _context;

    public ApprovedSeriesTests() => _context = MigratedStoreTemplate.Context(_connection);

    [Fact]
    public async Task ApprovedSeries_ByTicketKey_RoundTrips()
    {
        var record = await FileAsync();

        var row = await _context.Set<ApprovedSeries>().AsNoTracking().SingleAsync();
        row.TicketKey.Should().Be(TicketKey.For("azuredevops", TicketId).Value);
        row.SeriesId.Should().Be(Series);
        row.TicketId.Should().Be(TicketId, "discovery names the tracker's own spelling");
        row.Repositories.Should().Be("sample-api");
        row.CarryingRepo.Should().Be("sample-api");
        row.ApprovedBy.Should().Be("sample.user");
        var back = await new ApprovedSeriesRepository(_context).GetAsync(Tracker, row.TicketKey, default);
        back!.Set.Goal.Should().Be("The epic's goal", "the content keeps the series' goal");
        back.Set.Series.Should().Be(Series);
        back.Approval.Should().Be(record.Approval);
    }

    [Fact]
    public async Task ApprovedSetPane_BoundConversationAfterFiling_ShowsContent()
    {
        await FileAsync();
        var binding = TicketBinding.For(Tracker, "azuredevops", TicketId, "Widget");
        await new SpecDialogSessionRepository(_context).AddAsync(new SpecDialogSession
        {
            SessionId = "s-2", Platform = "dashboard", ChannelId = Dialog, ThreadId = Dialog,
            UserId = "someone", Project = "sample", Tracker = binding.Tracker, TicketKey = binding.Key,
            LastActivityAt = DateTimeOffset.UtcNow,
        }, default);

        var view = await new ApprovedSetForConversation(
            new SpecDialogSessionRepository(_context), Store()).ForAsync(Dialog, default);

        view.Should().NotBeNull("the binding computes the key the filing stored under");
        view!.Specs.Should().ContainSingle().Which.SpecId.Should().Be($"{Series}a");
    }

    [Fact]
    public async Task ApprovedSpecSetRecorder_Record_CarriesTheProjectName()
    {
        var record = await FileAsync();

        record.Project.Should().Be("sample", "dbcef: the series records the project it was filed into");
    }

    [Fact]
    public async Task ApprovedSeries_NewApproval_ProjectsTheFilingProject()
    {
        await FileAsync();

        var row = await _context.Set<ApprovedSeries>().AsNoTracking().SingleAsync();
        row.Project.Should().Be("sample");
        (await new ApprovedSeriesRepository(_context).GetAsync(Tracker, row.TicketKey, default))!
            .Project.Should().Be("sample", "the content of record carries it too");
    }

    [Fact]
    public async Task ApprovedSeries_RowWrittenBeforeThisSpec_ReadsAnEmptyProject()
    {
        var key = TicketKey.For("azuredevops", TicketId).Value;
        var json = JsonNode.Parse(SpecApprovalJson.Write(ApprovedSets.Record(key, ApprovedSets.Noon, tracker: Tracker)))!.AsObject();
        json.Remove("project").Should().BeTrue("the record writes its project");
        _context.Add(new ApprovedSeries { Tracker = Tracker, TicketKey = key, ContentJson = json.ToJsonString() });
        await _context.SaveChangesAsync();

        var back = await new ApprovedSeriesRepository(_context).GetAsync(Tracker, key, default);

        back.Should().NotBeNull("an older row still reads");
        back!.Project.Should().BeEmpty();
        back.Approval!.At.Should().Be(ApprovedSets.Noon);
        (await _context.Set<ApprovedSeries>().AsNoTracking().SingleAsync()).Project.Should().BeEmpty();
    }

    [Fact]
    public void SpecApprovalJson_RoundTrip_KeepsTheProject()
    {
        var record = ApprovedSets.Record("azdo-7", ApprovedSets.Noon) with { Project = "sample" };

        SpecApprovalJson.Read(SpecApprovalJson.Write(record))!.Project.Should().Be("sample");
    }

    private Task<SpecApprovalRecord> FileAsync() =>
        ApprovedSetDoubles.Recorder(Store()).RecordAsync(
            State(), Project(), TicketId,
            new FiledSeries(Series, [new SpecDraft($"{Series}a", "Do it", $"spec: {Series}a", [])]),
            "The epic's goal", default);

    private DbSpecApprovalStore Store()
    {
        var services = new ServiceCollection();
        services.AddScoped(_ => new ApprovedSeriesRepository(_context));
        return new DbSpecApprovalStore(
            services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>());
    }

    private static ConversationState State() => new()
    {
        JobId = "session-1", ChannelId = "channel", UserId = "sample.user", Platform = "dashboard",
        Project = "sample", TicketId = string.Empty, StartedAt = ApprovedSets.Noon,
        Scope = new ActiveScope { Project = "sample", Repos = ["sample-api"] },
    };

    private static ResolvedProject Project() => new()
    {
        Name = "sample",
        Tracker = new TrackerConnection { Name = Tracker, Type = TrackerType.AzureDevOps },
        Repos = [new RepoConnection { Name = "sample-api" }],
    };

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }
}
