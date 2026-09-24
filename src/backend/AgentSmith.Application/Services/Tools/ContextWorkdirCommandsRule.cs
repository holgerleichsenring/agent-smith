using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// 2026-09-23-72c7: a context whose every declared command begins by entering one
/// directory does not also claim its source is the whole repository. Returns the
/// contradiction, or null when the document does not carry it.
/// <para>
/// 2026-09-03-7bac: build, test, prerequisites and probe all run at the REPOSITORY ROOT
/// and no declared path places one, so a command carrying its own <c>cd</c> is how a
/// sub-tree component expresses itself; meta.workdir says where that component's SOURCE
/// lives. A source at the root and commands that must all leave the root cannot both be
/// true — and a live round wrote both into one document.
/// </para>
/// <para>
/// UNANIMITY IS THE BAR, and the reading of a <c>cd</c> is deliberately narrow. One
/// command entering a directory while another does not is a repository with something at
/// its root, and a check that fired there would refuse a shape that works; anything this
/// cannot read with certainty — an expansion, a subshell, a path leaving the tree —
/// counts as NOT entering, so doubt costs a refusal that never happens rather than one
/// that should never have.
/// </para>
/// <para>
/// It refuses rather than corrects. The commands prove "." is wrong; they do not prove
/// their directory is right, because a build script may enter a sub-directory of a
/// component that spans more. The round is told what it contradicted and answers.
/// </para>
/// </summary>
public sealed class ContextWorkdirCommandsRule
{
    public string? Defect(ContextYamlDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (!IsRepositoryRoot(document.Meta?.Workdir)) return null;
        var commands = Commands(document);
        if (commands.Count == 0) return null;

        var entered = commands.ConvertAll(Entered);
        var first = entered[0];
        return first is not null
               && entered.TrueForAll(directory => string.Equals(directory, first, StringComparison.Ordinal))
            ? Contradiction(first)
            : null;
    }

    private static string Contradiction(string directory) =>
        $"/meta/workdir: the document contradicts itself. meta.workdir says this context's "
        + $"SOURCE is the whole repository (\".\"), while EVERY declared command begins by "
        + $"entering '{directory}' — and build, test, prerequisites and probe all run at the "
        + "repository root, so a command that carries its own cd is saying the root is not "
        + $"where this context lives. Decide which half is wrong: if the source really sits "
        + $"under '{directory}', set meta.workdir to the sub-tree it occupies (read off the "
        + "tree, not copied from the cd — a command may enter a sub-directory of a component "
        + "that spans more) and declare the commands as they run from the repository root; if "
        + "the source is the whole repository, the commands that leave it are wrong.";

    // "." only where the source is the whole repository (ContextYamlMeta), so that is the
    // only workdir a command leaving the root contradicts. A workdir already naming a
    // sub-tree is not contested by a command entering one.
    private static bool IsRepositoryRoot(string? workdir)
    {
        var value = (workdir ?? string.Empty).Trim().TrimEnd('/');
        return value.Length == 0 || value == ".";
    }

    // Every command the document DECLARES — the ones ContextYamlMeta names as running at
    // the repository root. A blank one declares nothing and is not counted.
    private static List<string> Commands(ContextYamlDocument document)
    {
        var all = new List<string?>();
        if (document.Verify is { } stages) all.AddRange(stages.Select(stage => stage.Command));
        all.Add(document.Prerequisites);
        all.Add(document.Probe?.Command);
        return [.. all.Where(command => !string.IsNullOrWhiteSpace(command)).Select(command => command!)];
    }

    // Entering a directory, read narrowly: the command STARTS with `cd`, its operand is a
    // plain relative path inside the tree, and the rest of the line follows an `&&`. A
    // subshell, a `pushd`, an expansion, a path going up or out, and a `cd` whose result
    // nothing depends on all read as not entering — this decides a refusal.
    private static string? Entered(string command)
    {
        var text = command.TrimStart();
        if (!text.StartsWith("cd", StringComparison.Ordinal)) return null;
        if (text.Length < 3 || !char.IsWhiteSpace(text[2])) return null;
        var rest = text[2..].TrimStart();
        var separator = rest.IndexOf("&&", StringComparison.Ordinal);
        return separator <= 0 ? null : Directory(rest[..separator].Trim());
    }

    private static string? Directory(string operand)
    {
        var path = Unquoted(operand).TrimEnd('/');
        if (path.Length == 0 || path[0] == '/' || !path.All(IsPathChar)) return null;

        var kept = new List<string>();
        foreach (var segment in path.Split('/'))
        {
            if (segment == "." && kept.Count == 0) continue; // a leading "./" names the same place
            if (segment.Length == 0 || segment == "." || segment == "..") return null;
            kept.Add(segment);
        }
        return kept.Count == 0 ? null : string.Join('/', kept);
    }

    private static string Unquoted(string operand) =>
        operand.Length >= 2 && (operand[0] == '"' || operand[0] == '\'') && operand[^1] == operand[0]
            ? operand[1..^1]
            : operand;

    // Nothing a shell would expand, split or glob — a character outside this set means the
    // directory the command enters cannot be read off the text with certainty.
    private static bool IsPathChar(char c) =>
        char.IsLetterOrDigit(c) || c is '.' or '_' or '-' or '/';
}
