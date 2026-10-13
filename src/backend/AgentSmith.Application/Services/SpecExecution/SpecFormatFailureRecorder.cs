using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Runs;

namespace AgentSmith.Application.Services.SpecExecution;

/// <summary>
/// 2026-10-13-a347a: keeps a failed formatter run for the code PR of its repository. Failures
/// accumulate over the run; a later failure of the same repository, context and declared command
/// replaces the earlier one, and a later success clears nothing — the command is wrong until the
/// operator corrects it. The output is masked before it is cut, so no secret is left half shown.
/// </summary>
public sealed class SpecFormatFailureRecorder(SecretMasker masker)
{
    public const int OutputTailChars = 800;

    public void Record(PipelineContext pipeline, string repository, SpecFormatInvocation invocation, VerifyOutcome outcome)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        ArgumentNullException.ThrowIfNull(invocation);
        ArgumentNullException.ThrowIfNull(outcome);
        var failure = new SpecFormatFailure(
            repository, invocation.ContextName, invocation.DeclaredCommand, outcome.ExitCode,
            VerifyCommandRunner.Tail(masker.Apply(outcome.Output), OutputTailChars));
        pipeline.TryGet<IReadOnlyList<SpecFormatFailure>>(ContextKeys.SpecFormatFailures, out var earlier);
        var failures = (earlier ?? []).Where(f => !SameRun(f, failure)).ToList();
        failures.Add(failure);
        pipeline.Set<IReadOnlyList<SpecFormatFailure>>(ContextKeys.SpecFormatFailures, failures);
    }

    private static bool SameRun(SpecFormatFailure a, SpecFormatFailure b) =>
        a.Repository == b.Repository && a.ContextName == b.ContextName && a.DeclaredCommand == b.DeclaredCommand;
}
