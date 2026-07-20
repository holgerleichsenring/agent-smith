using AgentSmith.Application.Services;
using FluentAssertions;

namespace AgentSmith.Tests.Services;

// p0355: the classifier decides which DONE ledger steps the keystone exempts
// from the target-in-diff cross-check — inspection verbs only, decided by the
// LEADING word so "verify and fix" style compounds still key off intent.
public sealed class ReadOnlyStepClassifierTests
{
    [Theory]
    [InlineData("Audit .agentsmith/contexts/default/context.yaml for stale entries")]
    [InlineData("verify the build stays green")]
    [InlineData("Analyze: current retry behaviour")]
    [InlineData("  Review the error handling in the poller")]
    [InlineData("identify the writer of the label")]
    public void IsReadOnly_InspectionLeadingVerb_True(string activity) =>
        ReadOnlyStepClassifier.IsReadOnly(activity).Should().BeTrue();

    [Theory]
    [InlineData("Refactor src/Di.cs to Mediator")]
    [InlineData("swap DI container")]
    [InlineData("Fix the audit trail writer")]
    [InlineData("")]
    public void IsReadOnly_MutatingOrEmptyActivity_False(string activity) =>
        ReadOnlyStepClassifier.IsReadOnly(activity).Should().BeFalse();
}
