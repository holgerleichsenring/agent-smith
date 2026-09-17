using AgentSmith.Sandbox.Wire;

namespace AgentSmith.Application.Services.Sandbox;

/// <summary>
/// The failed step result a read-only source scope answers with, so a refusal reaches the
/// model as a tool result it can read rather than as an exception that ends its turn.
/// </summary>
public static class SourceScopeRefusal
{
    /// <summary>The refusal for a step that is not a file read; null when it is one.</summary>
    public static StepResult? UnlessRead(Step step) => step.Kind is
        StepKind.ReadFile or StepKind.ListFiles or StepKind.Grep or StepKind.DirectoryTree
        ? null
        : Because(step,
            $"Step kind '{step.Kind}' is not available on the read-only source sandbox "
            + "for spec-dialog grounding — only file reads (read_file, grep, "
            + "list_directory, directory_tree) are served.");

    public static StepResult Because(Step step, string reason) => new(
        StepResult.CurrentSchemaVersion, step.StepId, ExitCode: 1,
        TimedOut: false, DurationSeconds: 0, ErrorMessage: reason, OutputContent: null);
}
