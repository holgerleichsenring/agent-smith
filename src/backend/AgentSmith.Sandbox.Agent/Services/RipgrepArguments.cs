using AgentSmith.Sandbox.Wire;

namespace AgentSmith.Sandbox.Agent.Services;

/// <summary>
/// The argument vector a Grep step hands ripgrep.
/// <para>
/// 2026-09-17-042ed: what is not searched — <see cref="GrepScope"/>'s directory list and size
/// ceiling — is applied here exactly as the managed fallback applies it, so an "absent" means
/// the same thing whichever path served the step. <see cref="Step.SearchHidden"/> adds dotfiles
/// and ignored paths for the caller that asks for them; every other grep keeps the repository's
/// own ignore rules, which is what its readers see.
/// </para>
/// </summary>
internal static class RipgrepArguments
{
    public static List<string> For(Step step, int headLimit)
    {
        var args = new List<string> { "--max-filesize", GrepScope.MaxFileSizeBytes.ToString() };
        if (step.SearchHidden) args.AddRange(["--hidden", "--no-ignore"]);
        switch (step.OutputMode)
        {
            case GrepOutputMode.FilesWithMatches:
                args.Add("--files-with-matches");
                break;
            case GrepOutputMode.Count:
                args.Add("--count");
                break;
            default:
                args.AddRange(["--json", "--max-count", headLimit.ToString()]);
                if (step.ContextBefore is > 0) args.AddRange(["-B", step.ContextBefore.Value.ToString()]);
                if (step.ContextAfter is > 0) args.AddRange(["-A", step.ContextAfter.Value.ToString()]);
                break;
        }
        if (!string.IsNullOrEmpty(step.Glob)) args.AddRange(["--glob", step.Glob]);
        // After the caller's glob, so an exclusion wins over a glob that would include it — but
        // never over a path the caller NAMED: the managed fallback reads a named file wherever it
        // sits (EnumerateFiles), and a grep_in_file on vendor/x/y.go must not depend on the engine.
        if (!File.Exists(step.Path!))
            foreach (var dir in GrepScope.ExcludedDirs) args.AddRange(["--glob", $"!**/{dir}/**"]);
        args.Add("-e");
        args.Add(step.Pattern!);
        args.Add(step.Path!);
        return args;
    }
}
