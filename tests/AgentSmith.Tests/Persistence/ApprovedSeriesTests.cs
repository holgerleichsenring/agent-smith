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
        view!.Phases.Should().ContainSingle().Which.PhaseId.Should().Be($"{Series}a");
    }

    private Task<SpecApprovalRecord> FileAsync() =>
        ApprovedSetDoubles.Recorder(Store()).RecordAsync(
            State(), Project(), TicketId,
            new FiledSeries(Series, [new PhaseDraft($"{Series}a", "Do it", $"spec: {Series}a", [])]),
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
