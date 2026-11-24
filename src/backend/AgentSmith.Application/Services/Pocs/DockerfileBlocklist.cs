using System.Text.Json;

namespace AgentSmith.Application.Services.Pocs;

/// <summary>
/// 2026-11-23-933db: what a PoC's Dockerfile may not ask of the builder, each refused naming its line — a '# syntax='
/// directive (it picks the frontend that parses the file, which can be any image), RUN --security=insecure, RUN
/// --network=host, RUN --mount=type=ssh|secret (each reaches past the build's sandbox), and ADD from a URL or a git
/// remote (the build would fetch content nobody uploaded or checked).
/// </summary>
public sealed class DockerfileBlocklist
{
    private static readonly string[] RemoteSources = ["http://", "https://", "git@", "git://", "ssh://"];

    internal PocRuleRefusal? RefusalOf(DockerfileDocument document)
    {
        if (document.Directives.FirstOrDefault(d => d.Keyword == "SYNTAX") is { } syntax)
            return Refused(syntax, $"'# syntax={syntax.Arguments}' is refused, because it picks the image that parses "
                + "the Dockerfile and a PoC builds with the default one");
        foreach (var instruction in document.Instructions)
            if (Why(instruction) is { } why) return Refused(instruction, why);
        return null;
    }

    private static string? Why(DockerfileInstruction instruction) => instruction.Keyword switch
    {
        "RUN" => instruction.Flags.Select(RunFlagWhy).FirstOrDefault(w => w is not null),
        "ADD" => Sources(instruction).Where(IsRemote).Select(s => $"ADD from '{s}' is refused, because the build would "
            + "fetch content nobody uploaded or checked; put the file into the archive").FirstOrDefault(),
        _ => null,
    };

    private static string? RunFlagWhy(string flag)
    {
        var lower = flag.ToLowerInvariant();
        if (lower == "--security=insecure")
            return $"RUN {flag} is refused, because an insecure build step runs with the host's privileges";
        if (lower == "--network=host")
            return $"RUN {flag} is refused, because a build step on the host network reaches services a PoC must not";
        return lower.StartsWith("--mount=", StringComparison.Ordinal)
            && lower["--mount=".Length..].Split(',').Any(o => o is "type=ssh" or "type=secret")
            ? $"RUN {flag} is refused, because it hands the build an SSH agent or a secret a PoC's build has no business with"
            : null;
    }

    // ADD's sources: every operand but the destination, from the shell or the JSON form.
    private static IReadOnlyList<string> Sources(DockerfileInstruction instruction)
    {
        var operands = instruction.Operands;
        var rest = string.Join(' ', operands);
        if (rest.StartsWith('['))
        {
            try { operands = JsonSerializer.Deserialize<string[]>(rest) ?? []; }
            catch (JsonException) { return operands; }
        }
        return [.. operands.Take(Math.Max(0, operands.Count - 1))];
    }

    private static bool IsRemote(string source) =>
        RemoteSources.Any(r => source.StartsWith(r, StringComparison.OrdinalIgnoreCase));

    private static PocRuleRefusal Refused(DockerfileInstruction at, string why) =>
        new(PocArchiveChecks.Dockerfile, $"Dockerfile line {at.Line}: {why}.");
}
