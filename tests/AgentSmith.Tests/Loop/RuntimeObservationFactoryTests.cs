using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Loop;
using AgentSmith.Contracts.Models;
using FluentAssertions;

namespace AgentSmith.Tests.Loop;

/// <summary>
/// p0147b: maps Incomplete / FailedRuntime outcomes into typed execution-limit
/// observations so silent skill drops become pipeline-visible.
/// </summary>
public sealed class RuntimeObservationFactoryTests
{
    private readonly RuntimeObservationFactory _sut = new();

    [Fact]
    public void Build_OkOutcome_ReturnsNull()
    {
        _sut.Build(SkillCallOutcome.Ok, "skill", hitLimitLabel: null, exception: null, failureReason: null)
            .Should().BeNull();
    }

    [Fact]
    public void Build_FailedParse_ReturnsNull()
    {
        _sut.Build(SkillCallOutcome.FailedParse, "skill", null, null, "parse failed")
            .Should().BeNull("parse failures carry their own diagnostics via FailureReason");
    }

    [Fact]
    public void Build_FailedValidation_ReturnsNull()
    {
        _sut.Build(SkillCallOutcome.FailedValidation, "skill", null, null, "schema mismatch")
            .Should().BeNull("validation failures carry their own diagnostics via FailureReason");
    }

    [Fact]
    public void Build_IncompleteWithTokenLimit_EmitsExecutionLimitTokens()
    {
        var obs = _sut.Build(SkillCallOutcome.Incomplete, "verifier", "tokens", null, null);

        obs.Should().NotBeNull();
        obs!.Category.Should().Be(ExecutionLimitCategories.ExecutionLimitTokens);
        obs.Severity.Should().Be(ObservationSeverity.Info);
        obs.Blocking.Should().BeFalse();
        obs.Description.Should().Contain("verifier").And.Contain("token budget");
    }

    [Fact]
    public void Build_IncompleteWithWallClockLimit_EmitsExecutionLimitWallClock()
    {
        var obs = _sut.Build(SkillCallOutcome.Incomplete, "scanner", "wall-clock", null, null);

        obs.Should().NotBeNull();
        obs!.Category.Should().Be(ExecutionLimitCategories.ExecutionLimitWallClock);
        obs.Severity.Should().Be(ObservationSeverity.Info);
        obs.Description.Should().Contain("wall-clock");
    }

    [Fact]
    public void Build_IncompleteWithoutEnforcerLabel_EmitsExecutionLimitToolCalls()
    {
        // ME.AI FunctionInvokingChatClient's MaximumIterationsPerRequest fires
        // inside the chat client — the enforcer doesn't see it, so HitLimit
        // stays null. The runtime maps this case to the tool-call category.
        var obs = _sut.Build(SkillCallOutcome.Incomplete, "investigator", hitLimitLabel: null, null, null);

        obs.Should().NotBeNull();
        obs!.Category.Should().Be(ExecutionLimitCategories.ExecutionLimitToolCalls);
        obs.Severity.Should().Be(ObservationSeverity.Info);
        obs.Description.Should().Contain("tool-call budget");
    }

    [Fact]
    public void Build_FailedRuntimeWithException_EmitsExecutionError()
    {
        var obs = _sut.Build(
            SkillCallOutcome.FailedRuntime, "skill",
            hitLimitLabel: null,
            exception: new InvalidOperationException("network down"),
            failureReason: null);

        obs.Should().NotBeNull();
        obs!.Category.Should().Be(ExecutionLimitCategories.ExecutionError);
        obs.Severity.Should().Be(ObservationSeverity.Info);
        obs.Description.Should().Contain("network down");
    }

    [Fact]
    public void Build_FailedRuntimeFromWallClockCap_EmitsExecutionLimitWallClock()
    {
        // Wall-clock cap with no response → FailedRuntime, but the cap label
        // is still present, so we route to the correct execution-limit-* bucket.
        var obs = _sut.Build(
            SkillCallOutcome.FailedRuntime, "scanner",
            hitLimitLabel: "wall-clock",
            exception: null,
            failureReason: null);

        obs.Should().NotBeNull();
        obs!.Category.Should().Be(ExecutionLimitCategories.ExecutionLimitWallClock);
    }

    [Fact]
    public void Build_AlwaysReturnsInfoSeverityAndNonBlocking()
    {
        var obs = _sut.Build(SkillCallOutcome.Incomplete, "skill", "tokens", null, null);

        obs!.Severity.Should().Be(
            ObservationSeverity.Info,
            "limit-hits are operational events, not security findings");
        obs.Blocking.Should().BeFalse("execution-limit observations must not poison gate decisions");
    }
}
