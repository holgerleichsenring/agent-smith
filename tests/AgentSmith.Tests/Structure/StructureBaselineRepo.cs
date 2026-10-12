using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Structure;
using AgentSmith.Contracts.Models.Structure;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Tests.Structure;

/// <summary>2026-10-06-8565c: one context 'api' at src/Api, its principles and its sources.</summary>
internal static class StructureBaselineRepo
{
    public const string Principles = ".agentsmith/contexts/api/principles.md";
    public const string Baseline = ".agentsmith/contexts/api/structure-baseline.tsv";

    public static string Limits(int functionLines) => $"## Limits\n\n```yaml\nfunction_lines: {functionLines}\n```\n";

    /// <summary>2026-10-13-2d0c: the limits a baseline header records.</summary>
    public static StructureLimitSet LimitSet(int functionLines, int tolerance = 0) =>
        new(new Dictionary<string, int> { [StructureMetric.FunctionLines] = functionLines }, tolerance);

    /// <summary>A method of <paramref name="bodyLines"/> statements measures bodyLines + 3 lines.</summary>
    public static string Method(string name, int bodyLines) =>
        $"    void {name}()\n    {{\n{string.Concat(Enumerable.Repeat("        x();\n", bodyLines))}    }}\n";

    public static string Class(params string[] methods) => $"class A\n{{\n{string.Concat(methods)}}}\n";

    public static RepoSandbox Repo(string source, string? principles = null)
    {
        var repo = new RepoSandbox();
        repo.Files[Principles] = principles ?? Limits(4);
        repo.Files["src/Api/A.cs"] = source;
        return repo;
    }

    public static Task<StructureBaselineResult> Apply(RepoSandbox repo, PrinciplesMode mode = PrinciplesMode.Transferred,
        string workdir = "src/Api", IStructureEngine? engine = null, params string[] siblings) =>
        StructureBaselineStubs.Real(engine).ApplyAsync(
            repo, new StructureBaselineScope("api", workdir, siblings, mode), CancellationToken.None);
}
