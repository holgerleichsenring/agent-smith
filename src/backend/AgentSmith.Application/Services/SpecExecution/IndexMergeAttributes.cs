using AgentSmith.Contracts.Services;
using AgentSmith.Sandbox.Wire;

namespace AgentSmith.Application.Services.SpecExecution;

/// <summary>
/// 2026-11-26-462aa: the git attributes that let two branches' index lines merge without a conflict — git's
/// built-in union driver for every context's done.md and for the memory index. The record step writes them into the
/// repository's .gitattributes; the sandbox base merge writes them into the clone's info/attributes too, because git
/// reads attributes from the work branch's tree and a branch cut before the record would not carry them.
/// </summary>
public static class IndexMergeAttributes
{
    public static IReadOnlyList<string> Lines { get; } =
    [
        $"{ProjectMetaPaths.Contexts}/*/{ProjectMetaPaths.ContextDoneFile} merge=union",
        $"{ProjectMetaPaths.MemoryIndex} merge=union",
    ];

    /// <summary>The step that appends the lines the clone's <c>$GIT_DIR/info/attributes</c> lacks.</summary>
    public static Step CloneStep(int timeoutSeconds)
    {
        const string script = "f=$(git rev-parse --git-path info/attributes) && mkdir -p \"$(dirname \"$f\")\" && touch \"$f\" "
            + "&& { [ ! -s \"$f\" ] || [ -z \"$(tail -c1 \"$f\")\" ] || echo >> \"$f\"; } "
            + "&& for l in \"$@\"; do grep -qxF -e \"$l\" \"$f\" || printf '%s\\n' \"$l\" >> \"$f\" || exit 1; done";
        return new Step(Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.Run,
            Command: "sh", Args: ["-c", script, "sh", .. Lines], TimeoutSeconds: timeoutSeconds);
    }

    /// <summary>The file with the lines it lacks appended, or null when it already has them all.</summary>
    public static string? Ensure(string? existing)
    {
        var text = (existing ?? string.Empty).Replace("\r\n", "\n");
        var have = text.Split('\n').Select(l => l.Trim()).ToHashSet(StringComparer.Ordinal);
        var missing = Lines.Where(l => !have.Contains(l)).ToList();
        if (missing.Count == 0) return null;
        var head = text.Length == 0 || text.EndsWith('\n') ? text : text + "\n";
        return head + string.Join('\n', missing) + "\n";
    }
}
