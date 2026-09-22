using AgentSmith.Application.Services.Builders;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Server.Services.Sandbox;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Sandbox;

/// <summary>
/// 2026-09-22-2d11a: a source-scope sandbox is stamped with the conversation it
/// belongs to, beside the job, run and owner labels — on the container and on the pod,
/// so one rail judges both backends. Nothing passes a conversation until
/// 2026-09-22-2d11b holds one, and a spec that names none carries no label at all,
/// which is what keeps every reaper verdict exactly what it is today.
/// </summary>
public sealed class SandboxConversationLabelTests
{
    private const string Conversation = "a1b2c3d4";

    [Fact]
    public void DockerLabels_ASpecThatNamesAConversation_CarriesTheLabel()
    {
        var labels = Toolchain(Conversation).Labels;

        labels[DockerContainerSpecBuilder.ConversationIdLabel].Should().Be(Conversation);
    }

    [Fact]
    public void DockerLabels_ASpecThatNamesNone_CarriesNoConversationLabelAtAll()
    {
        Toolchain(conversationId: null).Labels
            .Should().NotContainKey(DockerContainerSpecBuilder.ConversationIdLabel);
    }

    [Fact]
    public void PodLabels_AConversationIsStampedBesideTheJobAndTheRun()
    {
        var labels = new SandboxPodLabels(new SandboxOwnerIdentity("store-test"))
            .Build("job-1", "run-1", Conversation);

        labels[SandboxPodLabels.ConversationIdLabel].Should().Be(Conversation);
        labels[SandboxPodLabels.PipelineIdLabel].Should().Be("job-1");
        labels[SandboxPodLabels.RunIdLabel].Should().Be("run-1");
    }

    [Fact]
    public void PodLabels_NoConversation_StampsNothingExtra()
    {
        new SandboxPodLabels(new SandboxOwnerIdentity("store-test")).Build("job-1", "run-1")
            .Should().NotContainKey(SandboxPodLabels.ConversationIdLabel);
    }

    [Fact]
    public async Task SourceScope_OpenedForAConversation_SpawnsASandboxStampedWithIt()
    {
        var factory = new StubSandboxFactory();

        await Scopes(factory).Create(Project, RepoWithUrl(), revision: null, Conversation)
            .MaterializeAsync(CancellationToken.None);

        factory.Spawned.Should().ContainSingle()
            .Which.Spec.ConversationId.Should().Be(Conversation);
    }

    [Fact]
    public async Task SourceScope_OpenedWithNoConversation_SpawnsAnUnlabelledSandbox()
    {
        var factory = new StubSandboxFactory();

        await Scopes(factory).Create(Project, RepoWithUrl()).MaterializeAsync(CancellationToken.None);

        factory.Spawned.Should().ContainSingle().Which.Spec.ConversationId.Should().BeNull(
            "nothing holds a conversation yet, so no sandbox carries the label");
    }

    private static ISourceScopeSandboxFactory Scopes(StubSandboxFactory factory)
    {
        var specBuilder = new SandboxSpecBuilder(
            new StubSandboxResourceResolver(),
            Mock.Of<IAgentImageResolver>(r => r.Resolve(It.IsAny<ResolvedProject>()) == "agent:test"));
        var runContext = new Mock<IRunContextAccessor>();
        runContext.SetupGet(r => r.CurrentRunId).Returns("run-1");
        return new SourceScopeSandboxFactory(
            new SourceScopeOpener(
                new SourceScopeMaterialiser(), factory, specBuilder, runContext.Object),
            new AsyncLocalSourceScopeObserverAccessor(), NullLogger<SourceScopeSandbox>.Instance);
    }

    private static Docker.DotNet.Models.CreateContainerParameters Toolchain(string? conversationId) =>
        new DockerContainerSpecBuilder(new SandboxOwnerIdentity("store-test")).BuildToolchain(
            "container-1", "shared", "work", "job-1", "redis:6379",
            new SandboxSpec("node:20", ResourceLimits.Default, "agent:1")
            {
                RunId = "run-1", ConversationId = conversationId
            });

    private static ResolvedProject Project => new() { Name = "p1" };

    private static RepoConnection RepoWithUrl() => new()
    {
        Name = "repo-a", Type = RepoType.GitHub, Url = "https://stub.test/repo-a",
    };
}
