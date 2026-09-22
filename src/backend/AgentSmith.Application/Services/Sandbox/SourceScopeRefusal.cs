using AgentSmith.Sandbox.Wire;

namespace AgentSmith.Application.Services.Sandbox;

/// <summary>
/// The failed step result a read-only source scope answers with, so a refusal reaches the
/// model as a tool result it can read rather than as an exception that ends its turn.
/// <para>
/// 2026-09-22-46ef: the rule was the step's KIND — four reads served, everything else
/// refused — and that refused a category instead of a consequence. A process already runs
/// in this container (the clone writes the whole tree a moment before the guard wraps it),
/// so what the kind rule actually refused was the model's OWN process, at the price of the
/// three tools that need a server-built one.
/// </para>
/// <para>
/// The rule now has two halves. A Run step is served when its program is one the server
/// itself sends to a scope (<see cref="SourceScopeProgramAllowance"/>) — a model-authored
/// command travels as a shell with a command string and is not in that set, which closes
/// the redirection hole by construction, because a redirection needs a shell. The other
/// half is that every operand a served step carries is a server constant or a value the
/// server has shaped, enforced in the tools where the model's value enters; bounding the
/// program alone would still let <c>find</c> read a leading dash as its expression. A write
/// is decided by <see cref="SourceScopeWritePolicy"/>, on its canonical path.
/// </para>
/// </summary>
public static class SourceScopeRefusal
{
    /// <summary>
    /// The refusal for a step this scope will not serve; null when it serves it. The four
    /// content reads are unconditional; a process is judged by its program; a write by its
    /// path.
    /// </summary>
    public static StepResult? Unless(Step step, SourceScopeWritePolicy writes)
    {
        ArgumentNullException.ThrowIfNull(step);
        ArgumentNullException.ThrowIfNull(writes);
        return step.Kind switch
        {
            StepKind.ReadFile or StepKind.ListFiles or StepKind.Grep or StepKind.DirectoryTree
                => null,
            StepKind.Run => UnlessServerBuilt(step),
            StepKind.WriteFile => writes.Refuse(step),
            _ => Because(step,
                $"Step kind '{step.Kind}' is not available on the read-only source sandbox "
                + "for spec-dialog grounding — it serves file reads (read_file, grep, "
                + "list_directory, directory_tree), the processes the server itself builds, "
                + "and writes under a declared prefix."),
        };
    }

    private static StepResult? UnlessServerBuilt(Step step) =>
        SourceScopeProgramAllowance.Allows(step.Command)
            ? null
            : Because(step,
                $"'{step.Command ?? "(no program)"}' is not a program the read-only source "
                + $"sandbox serves — it serves {SourceScopeProgramAllowance.Listed}, the "
                + "processes the server itself builds. A command of your own runs through a "
                + "shell, and a shell is refused here; use the file reads and the tools "
                + "offered instead.");

    public static StepResult Because(Step step, string reason) => new(
        StepResult.CurrentSchemaVersion, step.StepId, ExitCode: 1,
        TimedOut: false, DurationSeconds: 0, ErrorMessage: reason, OutputContent: null);
}
