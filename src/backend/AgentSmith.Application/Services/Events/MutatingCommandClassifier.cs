using System.Text.RegularExpressions;
using AgentSmith.Sandbox.Wire;

namespace AgentSmith.Application.Services.Events;

/// <summary>
/// p0357: classifies a sandbox step as tree-mutating so the dashboard's write
/// counter stays honest when the master edits via shell scripts (perl -i,
/// cat &gt; file &lt;&lt;EOF, git apply) instead of write_file. Conservative by
/// design: false negatives are acceptable, false positives are not — when in
/// doubt, not a write. Pure classification, no side effects.
/// </summary>
public sealed partial class MutatingCommandClassifier
{
    public bool IsMutating(Step step) => step.Kind switch
    {
        StepKind.WriteFile => true,
        StepKind.Run => IsMutatingShellText(ShellText(step)),
        _ => false,
    };

    // Run steps arrive as /bin/sh -c <text>: the text is the last arg.
    private static string ShellText(Step step) =>
        step.Args is { Count: > 0 } args ? args[^1] : step.Command ?? string.Empty;

    private static bool IsMutatingShellText(string text) =>
        !string.IsNullOrEmpty(text) && (Redirection().IsMatch(text) || MutatingTool().IsMatch(text));

    // Output redirection into a file (>, >>, tee) — heredoc writes (cat > f <<EOF)
    // land here too. `2>` / `&>` stderr-to-file redirects count: they create files.
    [GeneratedRegex(@"(^|[^<>])>{1,2}\s*[\w/.]|\btee\s+(-a\s+)?[\w/.]")]
    private static partial Regex Redirection();

    // In-place editors and patch appliers: sed -i, perl -i / -pi / -pi.bak,
    // git apply, patch, and plain file movers/removers.
    [GeneratedRegex(@"\bsed\s+(-\w*\s+)*-i|\bperl\s+(-\w*\s+)*-p?i\b|\bgit\s+apply\b|\bpatch\s+|\b(mv|cp|rm|mkdir|touch|ln)\s+")]
    private static partial Regex MutatingTool();
}
