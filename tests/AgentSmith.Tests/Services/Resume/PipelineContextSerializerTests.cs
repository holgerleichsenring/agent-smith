using AgentSmith.Application.Services.Resume;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Services.Resume;

/// <summary>
/// p0327: the checkpoint serializer round-trips DATA entries and excludes the
/// live infrastructure objects that resume re-establishes (sandbox handles,
/// the borrowed coordinator, pricing/catalog bindings).
/// </summary>
public sealed class PipelineContextSerializerTests
{
    private readonly PipelineContextSerializer _sut = new(
        NullLogger<PipelineContextSerializer>.Instance);

    [Fact]
    public void PipelineContextSerializer_RoundTrip_PreservesContext()
    {
        var source = new PipelineContext();
        source.Set(ContextKeys.RunId, "2026-07-13T10-00-00-abcd");
        source.Set(ContextKeys.Headless, false);
        source.Set(ContextKeys.RunCommandTimeoutSeconds, 120);
        source.Set(ContextKeys.RunStartedAt, DateTimeOffset.Parse("2026-07-10T09:00:00Z"));
        source.Set(ContextKeys.CheckoutBranch, "agentsmith/42-fix");
        source.Set(ContextKeys.TicketId, new TicketId("42"));
        source.Set(ContextKeys.Ticket, new Ticket(new TicketId("42"), "Title", "Body", null, "Active", "github"));
        // The ScopeRepos-narrowed repo list is run state — it must round-trip
        // and win over the standard all-repos seeding on resume.
        source.Set<IReadOnlyList<RepoConnection>>(ContextKeys.Repos,
            new[] { new RepoConnection { Name = "api", Type = RepoType.Local, Path = "/tmp/api" } });
        source.Set(ContextKeys.DialogueQuestion, new DialogQuestion(
            "q1", QuestionType.Approval, "Approve?", "plan", null, "reject", TimeSpan.FromDays(3)));
        source.TrackCommand("FetchTicketCommand", true, "ok", TimeSpan.FromSeconds(1), null);

        var target = new PipelineContext();
        _sut.Restore(_sut.Serialize(source), target);

        target.Get<string>(ContextKeys.RunId).Should().Be("2026-07-13T10-00-00-abcd");
        target.Get<bool>(ContextKeys.Headless).Should().BeFalse();
        target.Get<int>(ContextKeys.RunCommandTimeoutSeconds).Should().Be(120);
        target.Get<DateTimeOffset>(ContextKeys.RunStartedAt)
            .Should().Be(DateTimeOffset.Parse("2026-07-10T09:00:00Z"));
        target.Get<string>(ContextKeys.CheckoutBranch).Should().Be("agentsmith/42-fix");
        target.Get<TicketId>(ContextKeys.TicketId).Value.Should().Be("42");
        target.Get<Ticket>(ContextKeys.Ticket).Title.Should().Be("Title");
        target.TryGet<IReadOnlyList<RepoConnection>>(ContextKeys.Repos, out var repos).Should().BeTrue();
        repos!.Should().ContainSingle().Which.Name.Should().Be("api");
        var question = target.Get<DialogQuestion>(ContextKeys.DialogueQuestion);
        question.QuestionId.Should().Be("q1");
        question.Timeout.Should().Be(TimeSpan.FromDays(3));
        target.TryGet<List<ExecutionTrailEntry>>(ContextKeys.ExecutionTrail, out var trail).Should().BeTrue();
        trail!.Should().ContainSingle().Which.CommandName.Should().Be("FetchTicketCommand");
    }

    [Fact]
    public void Serialize_LiveObjects_AreExcluded()
    {
        var source = new PipelineContext();
        source.Set(ContextKeys.RunId, "run-1");
        source.Set<IReadOnlyDictionary<string, ISandbox>>(ContextKeys.Sandboxes,
            new Dictionary<string, ISandbox> { ["default"] = Mock.Of<ISandbox>() });
        source.Set(ContextKeys.Sandbox, Mock.Of<ISandbox>());
        source.Set(ContextKeys.SandboxCoordinator, Mock.Of<IPipelineSandboxCoordinator>());
        source.Set("ModelPricingResolver", new object());
        source.Set(ContextKeys.ConfigDir, "/pod-local/config");

        var target = new PipelineContext();
        _sut.Restore(_sut.Serialize(source), target);

        target.Get<string>(ContextKeys.RunId).Should().Be("run-1");
        target.Has(ContextKeys.Sandboxes).Should().BeFalse("live sandbox handles are re-provisioned on resume");
        target.Has(ContextKeys.Sandbox).Should().BeFalse();
        target.Has(ContextKeys.SandboxCoordinator).Should().BeFalse("the coordinator is owned by the executor, only borrowed by the context");
        target.Has("ModelPricingResolver").Should().BeFalse();
        target.Has(ContextKeys.ConfigDir).Should().BeFalse("the config dir is pod-local and re-seeded per launch");
    }

    [Fact]
    public void Serialize_UnserializableEntry_IsSkippedNotFatal()
    {
        var source = new PipelineContext();
        source.Set(ContextKeys.RunId, "run-1");
        source.Set("SomethingLive", new CancellationTokenSource()); // not JSON-serializable

        var target = new PipelineContext();
        _sut.Restore(_sut.Serialize(source), target);

        target.Get<string>(ContextKeys.RunId).Should().Be("run-1");
        target.Has("SomethingLive").Should().BeFalse();
    }

    [Fact]
    public void CheckpointCommand_RoundTrip_PreservesSplicedParameters()
    {
        var spliced = PipelineCommand.SkillRound(
            "BootstrapRoundCommand", "bootstrap-csharp", 1,
            repoName: "api", contextName: "server", workdir: "server/");

        var restored = AgentSmith.Application.Models.CheckpointCommand.From(spliced).ToPipelineCommand();

        restored.Name.Should().Be("BootstrapRoundCommand");
        restored.SkillName.Should().Be("bootstrap-csharp");
        restored.Round.Should().Be(1);
        restored.RepoName.Should().Be("api");
        restored.ContextName.Should().Be("server");
        restored.Workdir.Should().Be("server/");
    }
}
