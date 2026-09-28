using System.Reflection;
using AgentSmith.Application.Services.Dialogue;
using AgentSmith.Application.Services.Spawning;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Server.Contracts;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services;
using AgentSmith.Server.Services.ChatLaunch;
using AgentSmith.Server.Services.Handlers;
using AgentSmith.Tests.Spawning;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Server;

/// <summary>
/// A ticket's run asked for in chat goes through the REAL spawn funnel — admission, queue and
/// claim request as a polled ticket gets them — with the claim service as the double, because
/// the claim is where the run leaves this process.
/// </summary>
public sealed class ChatTicketRunLaunchTests
{
    private const string Project = "sample";
    private readonly List<string> _said = [];
    private ClaimRequest? _claimed;

    [Fact]
    public async Task FixTicketIntent_GoesThroughTheSpawnFunnel_SpawnsNothing()
    {
        await Handler().HandleAsync(Fix(pipeline: null), CancellationToken.None);

        _claimed.Should().NotBeNull("the funnel claimed the ticket");
        _claimed!.PipelineName.Should().Be(PipelinePresets.CodeName, "a chat fix that names no pipeline runs code");
        _claimed.TicketId.Value.Should().Be("42");
        _claimed.ExistingRunId.Should().NotBeNullOrEmpty();
        _said.Should().ContainSingle().Which.Should().Contain(_claimed.ExistingRunId!);

        var chatTypes = typeof(ChatTicketRunLauncher).Assembly.GetTypes().Where(t =>
            t.Namespace is "AgentSmith.Server.Services.ChatLaunch" or "AgentSmith.Server.Services.Handlers"
            || t == typeof(AgentSmith.Server.Services.Adapters.SlackMessageDispatcher));
        chatTypes.SelectMany(t => t.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            .SelectMany(c => c.GetParameters())
            .Select(p => p.ParameterType.Name)
            .Should().NotContain("IJobSpawner", "no chat intent starts a run in a spawned job");
    }

    [Fact]
    public async Task ChatFixTicket_ResolvesStatusesFromTheProjectsTrigger()
    {
        await Handler().HandleAsync(Fix(pipeline: "mad-discussion"), CancellationToken.None);

        _claimed!.Platform.Should().Be("azuredevops", "the envelope speaks for the project's tracker");
        _claimed.PipelineName.Should().Be("mad-discussion");
        var context = _claimed.InitialContext!;
        context[ContextKeys.DoneStatus].Should().Be("Resolved");
        context[ContextKeys.FailedStatus].Should().Be("Blocked");
        context.IsRequestedByName().Should().BeTrue();
    }

    [Fact]
    public async Task ChatFixTicket_UnknownProject_IsRefusedWithoutAClaim()
    {
        await Handler().HandleAsync(Fix(pipeline: null) with { Project = "nope" }, CancellationToken.None);

        _claimed.Should().BeNull();
        _said.Should().ContainSingle().Which.Should().Contain("not configured");
    }

    private FixTicketIntentHandler Handler()
    {
        var loader = new Mock<IConfigurationLoader>();
        loader.Setup(l => l.LoadConfig(It.IsAny<string>())).Returns(Config());
        var launcher = new ChatTicketRunLauncher(
            loader.Object, new ServerContext("agentsmith.yml"), Funnel(),
            NullLogger<ChatTicketRunLauncher>.Instance);
        var adapter = new Mock<IPlatformAdapter>();
        adapter.Setup(a => a.SendMessageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, CancellationToken>((_, text, _) => _said.Add(text))
            .Returns(Task.CompletedTask);
        return new FixTicketIntentHandler(
            launcher, new ChatLaunchAnnouncer(adapter.Object, new RunAnswerLink(new AgentSmithConfig())));
    }

    private SpawnPipelineRunsUseCase Funnel()
    {
        var claims = new Mock<ITicketClaimService>();
        claims.Setup(c => c.ClaimAsync(It.IsAny<ClaimRequest>(), It.IsAny<AgentSmithConfig>(), It.IsAny<CancellationToken>()))
            .Callback<ClaimRequest, AgentSmithConfig, CancellationToken>((r, _, _) => _claimed = r)
            .ReturnsAsync(ClaimResult.Claimed());
        return new SpawnPipelineRunsUseCase(
            claims.Object, CapacityTestDoubles.StubCalculator(), CapacityTestDoubles.AlwaysReserve(),
            CapacityTestDoubles.EmptyQueue(), CapacityTestDoubles.NoCorpses(), CapacityTestDoubles.NoHolds(),
            CapacityTestDoubles.AlwaysAdmit(), TestSupport.ApprovedSetDoubles.Carrier(),
            CapacityTestDoubles.NoNudge(), CapacityTestDoubles.NoStandingRefusal(),
            NullLogger<SpawnPipelineRunsUseCase>.Instance);
    }

    private static AgentSmithConfig Config() => new()
    {
        Projects = new Dictionary<string, ResolvedProject>
        {
            [Project] = new()
            {
                Name = Project,
                Repos = [new RepoConnection { Name = "sample-server" }],
                Tracker = new TrackerConnection { Name = "boards", Type = TrackerType.AzureDevOps },
                AzuredevopsTrigger = new WebhookTriggerConfig
                {
                    TriggerStatuses = ["Approved"], DoneStatus = "Resolved", FailedStatus = "Blocked",
                },
            },
        },
    };

    private static FixTicketIntent Fix(string? pipeline) => new()
    {
        RawText = "fix #42", UserId = "U1", ChannelId = "C1", Platform = "slack",
        TicketId = "42", Project = Project, PipelineOverride = pipeline,
    };
}
