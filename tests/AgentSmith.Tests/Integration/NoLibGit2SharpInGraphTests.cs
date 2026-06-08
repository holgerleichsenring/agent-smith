using FluentAssertions;

namespace AgentSmith.Tests.Integration;

/// <summary>
/// Regression guard for p0117b: LibGit2Sharp must not return to the project graph.
/// Walks every loaded assembly + every type the assemblies expose, asserts no
/// AssemblyName / Type.Namespace mentions LibGit2Sharp. Catches re-introduction
/// via transitive PackageReference too — csproj-level grep would miss those.
/// </summary>
public sealed class NoLibGit2SharpInGraphTests
{
    [Fact]
    public void RuntimeClosure_DoesNotReferenceLibGit2Sharp()
    {
        // Force-load Application + Infrastructure + Server so transitive deps are resolved.
        _ = typeof(AgentSmith.Application.ServiceCollectionExtensions).Assembly;
        _ = typeof(AgentSmith.Infrastructure.Services.Security.StaticPatternScanner).Assembly;
        _ = typeof(AgentSmith.Infrastructure.Core.Services.SkillMdParser).Assembly;
        _ = typeof(AgentSmith.Domain.Entities.Repository).Assembly;
        _ = typeof(AgentSmith.Sandbox.Wire.Step).Assembly;

        var assemblies = AppDomain.CurrentDomain.GetAssemblies();

        var libgitAssemblies = assemblies
            .Where(a => a.GetName().Name?.StartsWith("LibGit2Sharp", StringComparison.Ordinal) == true)
            .Select(a => a.GetName().Name)
            .ToList();

        libgitAssemblies.Should().BeEmpty(
            "LibGit2Sharp must not appear in the runtime closure after p0117b — git operations route through the sandbox via Step{{Kind=Run}}");

        var libgitTypes = assemblies
            .SelectMany(LibGitTypeNamesIn)
            .Distinct()
            .ToList();

        libgitTypes.Should().BeEmpty(
            "LibGit2Sharp namespace types must not leak into the project graph — "
            + "GitIgnoreResolver uses the Ignore NuGet, GitHistoryScanner shells out to git via Step{{Kind=Run}}");
    }

    // p0254/p0257: return the LibGit2Sharp-namespaced type names in one assembly,
    // swallowing ALL load errors per-assembly. Under the parallel full-suite run a
    // sibling test loads assemblies while we reflect, so BOTH GetTypes() AND the
    // later t.Namespace / t.FullName access can throw (ReflectionTypeLoadException,
    // FileNotFoundException, TypeLoadException on a half-loaded type). The earlier
    // fix only guarded GetTypes(); the property access still raced. Guarding the
    // whole per-assembly walk makes the check deterministic — LibGit2Sharp would
    // also surface via its assembly NAME (the reliable, type-free check above).
    private static IReadOnlyList<string> LibGitTypeNamesIn(System.Reflection.Assembly assembly)
    {
        Type?[] types;
        try { types = assembly.GetTypes(); }
        catch (System.Reflection.ReflectionTypeLoadException ex) { types = ex.Types; }
        catch { return Array.Empty<string>(); }

        var hits = new List<string>();
        foreach (var t in types)
        {
            if (t is null) continue;
            try
            {
                if (t.Namespace?.StartsWith("LibGit2Sharp", StringComparison.Ordinal) == true)
                    hits.Add(t.FullName ?? t.Name);
            }
            catch { /* half-loaded type under parallel load — skip */ }
        }
        return hits;
    }
}
