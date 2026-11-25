using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Events;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Sandbox.Wire;
using FluentAssertions;
using Moq;

namespace AgentSmith.Tests.Services;

/// <summary>
/// 2026-11-25-098f: inside a run the pusher records the head it is about to push, before the push, so a moved pull-request head
/// is known as agent-smith's own; outside a run it records nothing and asks git nothing extra.
/// </summary>
public sealed class GitBranchPusherHeadTests
{
    private readonly Mock<ISandbox> _sandbox = new();
    private readonly List<string> _calls = [];
    private readonly AgentSmith.Tests.Events.RecordingEventPublisher _events = new();
    private readonly AsyncLocalRunContextAccessor _run = new();

    public GitBranchPusherHeadTests()
    {
        _sandbox.Setup(s => s.RunStepAsync(It.IsAny<Step>(), It.IsAny<IProgress<StepEvent>?>(), It.IsAny<CancellationToken>()))
            .Returns<Step, IProgress<StepEvent>?, CancellationToken>((step, _, _) =>
            {
                var call = string.Join(' ', step.Args ?? []);
                _calls.Add(call);
                if (call.Contains("push")) _calls.Add($"published:{_events.Events.Count}");
                return Task.FromResult(new StepResult(StepResult.CurrentSchemaVersion, step.StepId, 0, false, 0.1, null,
                    call == "rev-parse HEAD" ? "abc123\n" : null));
            });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InsideARun_TheHeadIsPublished_BeforeThePush(bool plain)
    {
        using (_run.BeginScope("run-1"))
            await PushAsync(plain);

        var pushed = _events.Events.OfType<BranchPushedEvent>().Should().ContainSingle().Subject;
        (pushed.RunId, pushed.Branch, pushed.Sha).Should().Be(("run-1", "agent-smith/7", "abc123"));
        _calls.Should().ContainInOrder("rev-parse HEAD", "published:1");
    }

    [Fact]
    public async Task OutsideARun_NothingIsRecorded()
    {
        await PushAsync(plain: false);

        _events.Events.Should().BeEmpty();
        _calls.Should().NotContain("rev-parse HEAD");
    }

    private Task PushAsync(bool plain)
    {
        var pusher = new GitBranchPusher(_events, _run);
        return plain
            ? pusher.PushPlainAsync(_sandbox.Object, "agent-smith/7", "helper", GitCredential.None, CancellationToken.None)
            : pusher.PushAsync(_sandbox.Object, "agent-smith/7", "helper", GitCredential.None, CancellationToken.None);
    }
}
