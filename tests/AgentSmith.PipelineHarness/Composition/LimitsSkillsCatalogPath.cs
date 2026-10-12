using AgentSmith.Contracts.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AgentSmith.PipelineHarness.Composition;

/// <summary>
/// 2026-10-06-8565c: the checked-in fixture catalog's skills plus a principles core and a C#
/// delta that declares Limits (Fixtures/LimitsCatalog), merged into a temp directory — a
/// dedicated catalog, so the other init tests keep composing without principles as before.
/// </summary>
internal sealed class LimitsSkillsCatalogPath : ISkillsCatalogPath
{
    public LimitsSkillsCatalogPath()
    {
        var fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures");
        Root = Path.Combine(Path.GetTempPath(), $"agentsmith-harness-limits-{Guid.NewGuid():N}");
        Copy(Path.Combine(fixtures, "SkillsCatalog"), Root);
        Copy(Path.Combine(fixtures, "LimitsCatalog"), Root);
    }

    public string Root { get; }

    public string Origin => $"limits fixture at {Root}";

    public static void Register(IServiceCollection services)
    {
        services.RemoveAll<ISkillsCatalogPath>();
        services.AddSingleton<ISkillsCatalogPath, LimitsSkillsCatalogPath>();
    }

    private static void Copy(string from, string to)
    {
        foreach (var file in Directory.EnumerateFiles(from, "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(to, Path.GetRelativePath(from, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite: true);
        }
    }
}
