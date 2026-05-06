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
        _ = typeof(AgentSmith.Infrastructure.Core.Services.ProjectDetector).Assembly;
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
            .SelectMany(SafeGetTypes)
            .Where(t => t.Namespace?.StartsWith("LibGit2Sharp", StringComparison.Ordinal) == true)
            .Select(t => t.FullName)
            .Distinct()
            .ToList();

        libgitTypes.Should().BeEmpty(
            "LibGit2Sharp namespace types must not leak into the project graph — "
            + "GitIgnoreResolver uses the Ignore NuGet, GitHistoryScanner shells out to git via Step{{Kind=Run}}");
    }

    private static IEnumerable<Type> SafeGetTypes(System.Reflection.Assembly assembly)
    {
        try { return assembly.GetTypes(); }
        catch (System.Reflection.ReflectionTypeLoadException ex) { return ex.Types.Where(t => t is not null)!; }
    }
}
