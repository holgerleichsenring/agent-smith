using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Application.Services.TicketActs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.TicketActs;

/// <summary>2026-11-17-dd05b: the 'ticket-body' step answers through its slot; nothing on the branch rewrites nothing, and
/// an unchanged fingerprint stamps nothing.</summary>
public sealed class RenderTicketBodyHandlerTests
{
    private static readonly ResolvedProject Project = new()
    {
        Name = "alpha", Tracker = new TrackerConnection { Name = "gh", Type = TrackerType.GitHub },
    };

    private readonly Mock<ITicketProviderFactory> _trackers = new();
    private readonly RecordingBranchSources _branches = new();

    private RenderTicketBodyHandler Handler() => new(new TicketBodyRender(
        SeriesDoubles.BranchSeries(_branches), new SeriesTicketRegion(new SpecTicketRenderer()),
        new TicketDescriptionRewrite(_trackers.Object, NullLogger<TicketDescriptionRewrite>.Instance), _trackers.Object, Stamp()));

    private SeriesFingerprintStamp Stamp() => new(_branches, new SeriesFiles(new SeriesManifest()),
        new SeriesRepos(), new AgentSmith.Application.Services.Persistence.InMemorySpecSetPointerStore(),
        new SpecSetPointerRecorder(new AgentSmith.Application.Services.Persistence.InMemorySpecSetPointerStore(), NullLogger<SpecSetPointerRecorder>.Instance));

    [Fact]
    public async Task RenderTicketBody_NoSeeds_FailsNamingTheKeys()
    {
        var result = await Handler().ExecuteAsync(new RenderTicketBodyContext(new PipelineContext(), Project), default);

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain(nameof(ContextKeys.TicketBodyRequest)).And.Contain(nameof(ContextKeys.ActOutcome));
    }

    [Fact]
    public async Task RenderTicketBody_NoSpecsOnTheBranch_RewritesNothing()
    {
        var (pipeline, slot) = (new PipelineContext(), new ActSlot<TicketBodyOutcome>());
        pipeline.Set(ContextKeys.TicketBodyRequest, new TicketBodyRequest("42", "sample.user"));
        pipeline.Set(ContextKeys.ActOutcome, slot);

        var result = await Handler().ExecuteAsync(new RenderTicketBodyContext(pipeline, Project), default);

        result.IsSuccess.Should().BeFalse();
        slot.Value!.Rewritten.Should().BeFalse();
        slot.Value.Message.Should().Contain("no specs on its branch");
        _trackers.Verify(t => t.CreateRewriter(It.IsAny<TrackerConnection>()), Times.Never);
    }

    [Fact]
    public async Task SeriesFingerprintStamp_FingerprintUnchanged_WritesNothing()
    {
        var ticket = new Ticket(new TicketId("42"), "t", "body", null, "open", "github", []);
        var set = new SpecSet("github:42", [], SpecAccounting.Empty, [], SpecSource.BranchArtifact, TicketFingerprint: TicketTextFingerprint.Of(ticket), Series: "2026-11-17-aaaa");

        (await Stamp().StampAsync(Project, set, ticket, default)).Should().BeNull();
        _branches.Writes.Should().BeEmpty();
    }
}
