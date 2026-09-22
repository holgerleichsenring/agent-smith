using AgentSmith.Contracts.Sandbox;
using AgentSmith.Server.Services.Sandbox;
using Docker.DotNet.Models;
using FluentAssertions;
using k8s.Models;

namespace AgentSmith.Tests.Sandbox;

/// <summary>
/// 2026-09-22-2d11a: a sandbox labelled with a live design conversation is not a
/// corpse. The rail is a LABEL plus a session ROW — never process state — because the
/// owner identity a reaper judges under is the liveness store's and the cluster's
/// corpse sweep is leader-elected, so the replica that judges a held pod is usually
/// not the one holding it.
/// <para>
/// Nothing holds anything until 2026-09-22-2d11b, so the rail is driven here directly:
/// a synthetic labelled container, a synthetic labelled pod, and synthetic rows.
/// </para>
/// </summary>
public sealed class SandboxHoldRailTests
{
    private const string Conversation = "a1b2c3d4";
    private static readonly DateTimeOffset Now = SandboxHoldRailDoubles.Now;
    private static readonly TimeSpan PastTheAgeRail = SandboxOrphanReaper.MinContainerAge * 2;

    [Fact]
    public async Task Reaper_AContainerLabelledWithAnOpenActiveConversation_IsNotACorpse()
    {
        var verdict = await JudgeContainerAsync(Conversation, Open(TimeSpan.FromSeconds(30)));

        verdict.Outcome.Should().Be(SandboxReapOutcome.ConversationIsHeld);
    }

    [Fact]
    public async Task Reaper_AContainerLabelledWithAClosedConversation_IsACorpse()
    {
        var verdict = await JudgeContainerAsync(
            Conversation, Open(TimeSpan.FromSeconds(30)) with { IsOpen = false });

        verdict.Outcome.Should().Be(SandboxReapOutcome.Orphan);
    }

    [Fact]
    public async Task Reaper_AContainerLabelledWithAConversationQuietPastTheWindow_IsACorpse()
    {
        var verdict = await JudgeContainerAsync(
            Conversation, Open(SandboxHoldWindowResolver.DefaultWindow + TimeSpan.FromSeconds(1)));

        verdict.Outcome.Should().Be(SandboxReapOutcome.Orphan);
    }

    [Fact]
    public async Task Reaper_AContainerWithNoConversationLabel_IsJudgedExactlyAsItIsToday()
    {
        var held = await SandboxHoldRailDoubles
            .Reader(Open(TimeSpan.FromSeconds(30)))
            .ReadAsync(Candidates(conversationId: null), CancellationToken.None);

        held.Should().BeSameAs(HeldConversations.None,
            "a scan with nothing labelled reads neither the catalog nor the session store");
        Judge(Candidates(conversationId: null), held).Outcome.Should().Be(SandboxReapOutcome.Orphan);
    }

    [Fact]
    public async Task Reaper_ASessionReadThatFails_SparesWhatItCouldNotJudge()
    {
        var conversations = new SandboxHoldRailDoubles.StubConversations(Open(TimeSpan.FromSeconds(30)))
        {
            Fails = true
        };

        var held = await SandboxHoldRailDoubles.Reader(conversations)
            .ReadAsync(Candidates(Conversation), CancellationToken.None);

        Judge(Candidates(Conversation), held).Outcome.Should().Be(SandboxReapOutcome.ConversationIsHeld);
        Judge(Candidates(conversationId: null), held).Outcome.Should().Be(SandboxReapOutcome.Orphan,
            "an unlabelled sandbox is judged on the rails it always was");
    }

    [Fact]
    public async Task CorpseReaper_APodLabelledWithAnOpenActiveConversation_IsNotACorpse()
    {
        var candidates = SandboxPodCandidates.From([Pod(Conversation)], Now);
        var held = await SandboxHoldRailDoubles
            .Reader(Open(TimeSpan.FromSeconds(30)))
            .ReadAsync(candidates, CancellationToken.None);

        SandboxReapJudge.Judge(candidates, NoLiveRuns(), held, SandboxOrphanReaper.MinContainerAge)
            .Single().Outcome.Should().Be(SandboxReapOutcome.ConversationIsHeld);
    }

    [Fact]
    public void CorpseReaper_TheSelection_CarriesTheConversationLabelItMustJudge()
    {
        var candidate = SandboxPodCandidates.From([Pod(Conversation)], Now).Single();

        candidate.ConversationId.Should().Be(Conversation,
            "the pod selection returned a pod name and a run id and read no other label");
        candidate.JobId.Should().Be("job-held");
        candidate.RunId.Should().Be("run-that-spawned-it");
    }

    private static async Task<SandboxReapVerdict> JudgeContainerAsync(
        string conversationId, ConversationLiveness row)
    {
        var candidates = Candidates(conversationId);
        var held = await SandboxHoldRailDoubles.Reader(row).ReadAsync(candidates, CancellationToken.None);
        return Judge(candidates, held);
    }

    private static SandboxReapVerdict Judge(
        IReadOnlyList<SandboxReapCandidate> candidates, HeldConversations held) =>
        SandboxReapJudge.Judge(candidates, NoLiveRuns(), held, SandboxOrphanReaper.MinContainerAge).Single();

    private static IReadOnlyList<SandboxReapCandidate> Candidates(string? conversationId) =>
        SandboxContainerCandidates.From([Container(conversationId)], Now);

    private static ISet<string> NoLiveRuns() => new HashSet<string>(StringComparer.Ordinal);

    private static ConversationLiveness Open(TimeSpan quietFor) =>
        new(Conversation, "project-a", IsOpen: true, Now - quietFor);

    private static ContainerListResponse Container(string? conversationId)
    {
        var labels = new Dictionary<string, string>
        {
            [DockerContainerSpecBuilder.JobIdLabel] = "job-held",
            [DockerContainerSpecBuilder.RunIdLabel] = "run-that-spawned-it",
        };
        if (conversationId is not null)
            labels[DockerContainerSpecBuilder.ConversationIdLabel] = conversationId;
        return new ContainerListResponse
        {
            ID = "held-1", Created = (Now - PastTheAgeRail).UtcDateTime, Labels = labels
        };
    }

    private static V1Pod Pod(string conversationId) => new()
    {
        Metadata = new V1ObjectMeta
        {
            Name = "agentsmith-sandbox-held",
            CreationTimestamp = (Now - PastTheAgeRail).UtcDateTime,
            Labels = new Dictionary<string, string>
            {
                ["app"] = SandboxPodLabels.AppLabel,
                [SandboxPodLabels.PipelineIdLabel] = "job-held",
                [SandboxPodLabels.RunIdLabel] = "run-that-spawned-it",
                [SandboxPodLabels.ConversationIdLabel] = conversationId,
            }
        }
    };
}
