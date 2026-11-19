using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Events;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.SpecExecution;
using AgentSmith.Application.Services.Persistence;
using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Application.Services.Validation;
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

namespace AgentSmith.Tests.Specs;

/// <summary>
/// 2026-11-19-6b0ba: a ticket with no spec to build gets stand-ins — the whole ticket per repository in scope — and
/// no model cuts it; its coding master writes the real spec. A series out of step is still handed back first.
/// </summary>
public sealed class DeriveSpecHandlerTests
{
    private const string Ticket = """
        Migrate the client.

        Call sites MUST be named `<Domain>ServiceClient`.

        Ping me if unclear.
        """;

    [Fact]
    public async Task DeriveSpec_NoSet_PublishesTheWholeTicketAsAStandIn()
    {
        var published = new CapturingPublisher();
        var context = Context();

        var result = await Handler(published).ExecuteAsync(context, default);

        result.IsSuccess.Should().BeTrue();
        var set = published.Set!;
        set.Specs.Should().ContainSingle("the coding master writes the real spec; nothing cuts the ticket here");
        set.TicketPinnedWhole.Should().BeTrue();
        set.StandIns.Should().Equal(set.Specs[0].SpecId);
        set.Specs[0].Markdown.Should().Contain("`<Domain>ServiceClient`", "the whole ticket is carried, verbatim");
        set.Specs[0].Draft.Repos.Should().Equal("primary");
    }

    [Fact]
    public async Task DeriveSpec_NoSetOverSeveralRepositories_WritesOneStandInPerRepository()
    {
        var published = new CapturingPublisher();

        await Handler(published).ExecuteAsync(Context("BackgroundWorker", "Server"), default);

        var set = published.Set!;
        set.IsHandedBack.Should().BeFalse("a ticket over several repositories is no longer handed back for want of a cut");
        set.Specs.Select(p => p.Draft.Repos.Single()).Should().Equal("BackgroundWorker", "Server");
        set.StandIns.Should().HaveCount(2);
        set.Specs.Select(p => p.SpecId).Should().OnlyHaveUniqueItems();
    }

    // 2026-10-12-f5f7f: a series whose repositories disagree is not derived from or written over —
    // it is handed back as its own case naming them, before any model call, and nothing is written.
    [Fact]
    public async Task DeriveSpecHandler_LaggingRead_HandsBackOutOfStep_NoWrite()
    {
        var published = new CapturingPublisher();
        var pointers = new InMemorySpecSetPointerStore();
        var read = new SpecSetReadResult(BranchSet(), "sha-Server")
        {
            Lagging = ["Client"],
        };
        var reader = new Mock<ISpecSetReader>();
        reader.Setup(r => r.ReadAsync(
                It.IsAny<PipelineContext>(), It.IsAny<RepoConnection>(), It.IsAny<IReadOnlyList<RepoConnection>>(),
                It.IsAny<TicketKey>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SpecSetOnBranch.Answered(read));

        var context = Context("Server", "Client");
        var result = await Handler(published, reader.Object, pointers)
            .ExecuteAsync(context, default);

        result.IsSuccess.Should().BeTrue("the run parks; it does not fail");
        result.Message.Should().StartWith("awaiting_user_input").And.Contain("Client").And.Contain("out of step");
        context.Pipeline.Get<SpecHandback>(ContextKeys.SpecHandback).Case.Should().Be(SpecHandbackCase.SeriesOutOfStep);
        published.Set.Should().BeNull("nothing is written");
        (await pointers.GetAsync(string.Empty, "azdo-19106", default)).Should().BeNull("the pointer is untouched");
    }

    // 2026-09-22-6ad7: the reader answers "nothing at the path", never null — a default-valued
    // mock would hand the handler a null branch answer and the split would read as a crash.
    private static ISpecSetReader EmptyBranch()
    {
        var reader = new Mock<ISpecSetReader>();
        reader.Setup(r => r.ReadAsync(
                It.IsAny<PipelineContext>(), It.IsAny<RepoConnection>(), It.IsAny<IReadOnlyList<RepoConnection>>(),
                It.IsAny<TicketKey>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(SpecSetOnBranch.Nothing);
        return reader.Object;
    }

    private static DeriveSpecHandler Handler(
        CapturingPublisher publisher, ISpecSetReader? reader = null, ISpecSetPointerStore? pointers = null)
    {
        var validator = new SpecDraftValidator(new SpecSchemaProvider());
        var draftReader = new SpecDraftReader();
        return new DeriveSpecHandler(
            SeriesDoubles.BranchRead(reader ?? EmptyBranch()),
            publisher,
            pointers ?? new InMemorySpecSetPointerStore(),
            new ApprovedSpecSetResolver(
                new InMemorySpecApprovalStore(), NullLogger<ApprovedSpecSetResolver>.Instance),
            new SpecSourceResolver(
                new ApprovedSetHandoff(NullLogger<ApprovedSetHandoff>.Instance), AgentSmith.Tests.TestSupport.ApprovedSetDoubles.Continuation(),
                new FiledTicketSpecGate(NullLogger<FiledTicketSpecGate>.Instance),
                AgentSmith.Tests.TestSupport.SeriesSettingsDoubles.Reader(), NullLogger<SpecSourceResolver>.Instance),
            new SpecStandIns(validator, draftReader, new DerivedSpecYamlRenderer()),
            new SpecSetTicketCommenter(
                Mock.Of<ITicketProviderFactory>(), Moq.Mock.Of<AgentSmith.Contracts.Reviews.IFullSetPrNotice>(), AgentSmith.Tests.TestSupport.SeriesSettingsDoubles.Reader(), NullLogger<SpecSetTicketCommenter>.Instance),
            ApprovedSetDoubles.KeptNotice(),
            new UnansweredQuestionPin(NullLogger<UnansweredQuestionPin>.Instance),
            new UnansweredQuestionNotice(
                Mock.Of<ITicketProviderFactory>(), NullLogger<UnansweredQuestionNotice>.Instance),
            new SeriesResolver(new SeriesIdFactory(TimeProvider.System)),
            AgentSmith.Tests.TestSupport.HandbackTestDoubles.Probe(),
            new SpecSetUnchanged(new SeriesFiles(new SeriesManifest())));
    }

    private static DeriveSpecContext Context(params string[] repos)
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.RunId, "run-1");
        return new DeriveSpecContext(
            new Ticket(new TicketId("19106"), "Migrate the client", Ticket, null, "open", "azdo", []),
            null,
            repos.Length == 0
                ? [new RepoConnection { Name = "primary" }]
                : [.. repos.Select(name => new RepoConnection { Name = name })],
            new AgentConfig(),
            pipeline);
    }

    // A set on the branch, read back.
    private static SpecSet BranchSet()
    {
        var spec = new DerivedSpec(
            new Contracts.Models.SpecDraft("p19106a", "Rename the call sites", "spec: p19106a", [])
            {
                Done = ["Every call site is renamed."],
            },
            "rename-the-call-sites", string.Empty, [1, 2]);
        return new SpecSet(
            "azdo-19106", [spec], SpecAccounting.Empty,
            [new SpecRevision(1, SpecRevisionCause.Initial, DateTimeOffset.UtcNow)], SpecSource.BranchArtifact);
    }

    private sealed class CapturingPublisher : ISpecSetPublisher
    {
        public SpecSet? Set { get; private set; }

        public Task<CommandResult> PublishAsync(
            PipelineContext pipeline, string project, SeriesScope scope, SpecSet set,
            IReadOnlyList<IgnoredInstruction> ignoredInstructions, CancellationToken ct)
        {
            Set = set;
            return Task.FromResult(CommandResult.Ok("published"));
        }

        public bool Kept { get; private set; }

        public Task<CommandResult> KeepAsync(
            PipelineContext pipeline, SeriesScope scope, SpecSet set, string revisionSha, CancellationToken ct)
        {
            (Set, Kept) = (set, true);
            return Task.FromResult(CommandResult.Ok("kept"));
        }
    }
}
