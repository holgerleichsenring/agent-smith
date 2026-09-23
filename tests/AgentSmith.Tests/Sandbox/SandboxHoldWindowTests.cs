using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Server.Services.Sandbox;
using FluentAssertions;

namespace AgentSmith.Tests.Sandbox;

/// <summary>
/// 2026-09-22-2d11a: how long a conversation's sandboxes are held is ONE operator
/// setting, resolved per project and once per scan. The loader re-assembles the whole
/// catalog from the document store on every call, so a per-container read would be a
/// self-inflicted load — and a read that failed must not turn an operator's deliberate
/// zero into the built-in default.
/// </summary>
[Collection(TestSupport.EnvVarCollection.Name)]
public sealed class SandboxHoldWindowTests
{
    private const string Conversation = "a1b2c3d4";
    private const string Project = "project-a";
    private static readonly DateTimeOffset Now = SandboxHoldRailDoubles.Now;

    [Fact]
    public void HoldWindow_Unconfigured_IsThreeMinutes()
    {
        var resolved = SandboxHoldRailDoubles.Resolver(Loader(new AgentSmithConfig())).ResolveForScan();

        resolved!.ProcessWide.Should().Be(TimeSpan.FromMinutes(3));
        resolved.For(Project).Should().Be(TimeSpan.FromMinutes(3));
    }

    [Fact]
    public void HoldWindow_AProjectThatSetsItsOwn_OverridesTheProcessWideValue()
    {
        var resolved = SandboxHoldRailDoubles
            .Resolver(Loader(Catalog(processWide: 60, projectOverride: 600)))
            .ResolveForScan();

        resolved!.ProcessWide.Should().Be(TimeSpan.FromSeconds(60));
        resolved.For(Project).Should().Be(TimeSpan.FromSeconds(600));
        resolved.For("another-project").Should().Be(TimeSpan.FromSeconds(60));
    }

    [Fact]
    public async Task HoldWindow_SetToZero_MakesEveryLabelledSandboxACorpseAtOnce()
    {
        var held = await SandboxHoldRailDoubles
            .Reader(Catalog(processWide: 0, projectOverride: null), JustSpoke())
            .ReadAsync(Candidates(), CancellationToken.None);

        held.IsHeld(Conversation).Should().BeFalse("zero means hold nothing — today's behaviour exactly");
    }

    [Fact]
    public void HoldWindow_AConfigurationReadThatFails_KeepsThePreviousValueAndReapsNothingNew()
    {
        var loader = Loader(Catalog(processWide: 0, projectOverride: null));
        var resolver = SandboxHoldRailDoubles.Resolver(loader);
        resolver.ResolveForScan()!.ProcessWide.Should().Be(TimeSpan.Zero);

        loader.Fails = true;

        resolver.ResolveForScan()!.ProcessWide.Should().Be(TimeSpan.Zero,
            "a failed read must not turn a configured zero into the built-in default");
    }

    [Fact]
    public async Task HoldWindow_AConfigurationReadThatNeverSucceeded_SparesEveryLabelledSandbox()
    {
        var loader = Loader(new AgentSmithConfig());
        loader.Fails = true;
        var reader = new HeldConversationReader(
            SandboxHoldRailDoubles.Resolver(loader),
            new SandboxHoldRailDoubles.StubConversations(JustSpoke()), SandboxHoldRailDoubles.Clock,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<HeldConversationReader>.Instance);

        var held = await reader.ReadAsync(Candidates(), CancellationToken.None);

        held.IsHeld(Conversation).Should().BeTrue("a scan that could not resolve reaps nothing new");
    }

    [Fact]
    public async Task HoldWindow_ResolvedOncePerScan_NotOncePerContainer()
    {
        var loader = Loader(new AgentSmithConfig());
        var reader = new HeldConversationReader(
            SandboxHoldRailDoubles.Resolver(loader),
            new SandboxHoldRailDoubles.StubConversations(JustSpoke()), SandboxHoldRailDoubles.Clock,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<HeldConversationReader>.Instance);

        await reader.ReadAsync(ManyCandidates(count: 5), CancellationToken.None);

        loader.Reads.Should().Be(1, "five labelled sandboxes are one scan, not five catalog reads");
    }

    private static SandboxHoldRailDoubles.CountingConfigLoader Loader(AgentSmithConfig config) => new(config);

    private static ConversationLiveness JustSpoke() =>
        new(Conversation, Project, IsOpen: true, Now - TimeSpan.FromSeconds(1));

    private static AgentSmithConfig Catalog(int processWide, int? projectOverride) => new()
    {
        Sandbox = new SandboxGlobalConfig { HoldSeconds = processWide },
        Projects = new Dictionary<string, ResolvedProject>(StringComparer.OrdinalIgnoreCase)
        {
            [Project] = new()
            {
                Name = Project,
                Sandbox = projectOverride is null ? null : new SandboxConfig { HoldSeconds = projectOverride }
            }
        }
    };

    private static IReadOnlyList<SandboxReapCandidate> Candidates() =>
        [new SandboxReapCandidate("held-1", "job-1", "run-1", Conversation, TimeSpan.FromHours(1))];

    private static IReadOnlyList<SandboxReapCandidate> ManyCandidates(int count) =>
        [.. Enumerable.Range(0, count).Select(i => new SandboxReapCandidate(
            $"held-{i}", $"job-{i}", $"run-{i}", $"conversation-{i}", TimeSpan.FromHours(1)))];
}
