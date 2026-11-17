using AgentSmith.Application.Services.Filing;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Specs;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.PipelineHarness.Composition;
using AgentSmith.Server.Extensions;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.SpecDialog;
using AgentSmith.Server.Services.SpecDialog.Continuation;
using AgentSmith.Server.Services.Tickets;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// 2026-11-17-a0b6c: through the REAL server composition, a filed ticket's spec text is read off its branch by
/// every reader — the ticket page's series, a continuation's basis and the conversation's approved-set pane — and
/// none of them reads the approval record's copy: after the stored record is overwritten with other text, all
/// three still answer the branch's. The tracker and the source remote are the harness boundaries; the remote keeps
/// what filing wrote to the branch and answers reads from it.
/// </summary>
[Trait("Category", "PipelineHarness")]
public sealed class SpecTextFromBranchCompositionTests
{
    private const string Project = "fixture-file";
    private const string Tracker = "github-filing";
    private const string Key = "github-1";

    [Fact]
    public async Task FiledTicket_RecordOverwritten_PageBasisAndPaneReadTheBranch()
    {
        var (tracker, branches) = (new FilePipelineCompositionTests.FilingTracker(), new RecordingBranchSources());
        await using var harness = RealCompositionHarness.Build(FixturePaths.For(FixturePaths.Filing), services =>
        {
            services.RemoveAll<ITicketProviderFactory>()
                .AddSingleton<ITicketProviderFactory>(new FilePipelineCompositionTests.FilingTrackerFactory(tracker));
            services.RemoveAll<ISourceProviderFactory>().AddSingleton<ISourceProviderFactory>(branches);
            services.AddTicketReaders().AddTicketDefaults(); // GET /api/tickets/.../series, as the dashboard API registers it
        });
        await using var scope = harness.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var filed = await services.GetRequiredService<IFilingRun>().RunAsync(
            FilePipelineCompositionTests.State(), new TicketGraphOutcome([FilePipelineCompositionTests.Ticket("api", [])]), false, "prop-1");
        filed.Error.Should().BeNull();
        branches.Writes.Should().NotBeEmpty("filing writes the approved set to the ticket branch");

        await OverwriteTheRecordAsync(services);
        await BindAConversationAsync(services);

        var series = await services.GetRequiredService<TicketSeriesReader>().ReadAsync(Project, "1", null, default);
        series!.Source.Should().Be(TicketSeriesSources.Branch);
        series.Specs.Single().Goal.Should().Be("Goal of api", "the ticket page reads the branch");

        var basis = await services.GetRequiredService<TicketContinuationStateReader>().ReadAsync(Project, "1", false, default);
        basis.Failure.Should().BeNull();
        basis.State!.Basis.Single().Goal.Should().Be("Goal of api", "rework continues from the branch");

        var pane = await services.GetRequiredService<ApprovedSetForConversation>().ForAsync("d-bound", default);
        pane!.Specs.Single().Goal.Should().Be("Goal of api", "the conversation's pane reads the branch");
    }

    private static async Task OverwriteTheRecordAsync(IServiceProvider services)
    {
        var store = services.GetRequiredService<ISpecApprovalStore>();
        var record = (await store.GetAsync(Tracker, Key, default))!;
        var specs = record.Set.Specs.Select(s => s with { Draft = s.Draft with { Goal = "A goal only the record holds" } }).ToList();
        await store.SaveAsync(record with { Set = record.Set with { Specs = specs } }, default);
        (await store.GetAsync(Tracker, Key, default))!.Set.Specs.Single().Draft.Goal.Should().Be("A goal only the record holds");
    }

    private static Task BindAConversationAsync(IServiceProvider services) =>
        services.GetRequiredService<SpecDialogSessionRepository>().AddAsync(new SpecDialogSession
        {
            SessionId = "s-bound", Platform = "dashboard", ChannelId = "d-bound", ThreadId = "d-bound", UserId = "sample.user",
            Project = Project, Tracker = Tracker, TicketKey = Key, LastActivityAt = DateTimeOffset.UtcNow,
        }, default);
}
