using System.Reflection;
using AgentSmith.Contracts.Sandbox;
using FluentAssertions;

namespace AgentSmith.Tests.Architecture;

/// <summary>
/// 2026-11-25-9992c: <see cref="SandboxWorkDirectoryExtensions.WorkDirectory"/> answers the first
/// root a sandbox's wrapper chain declares, and the workspace root when none does. That fallback
/// is meant for test doubles only: a production sandbox that declares nothing would silently serve
/// /work as a run repository's root while the repository lives at /work/&lt;repo&gt;. So every
/// concrete production type implementing <see cref="ISandbox"/> either declares its root
/// (<see cref="ISandboxWorkDirectory"/>) or wraps one inner sandbox (<see cref="ISandboxWrapper"/>).
/// </summary>
public sealed class SandboxRootDeclarationRuleTests
{
    private static readonly string[] ProductionAssemblies =
    [
        "AgentSmith.Contracts",
        "AgentSmith.Application",
        "AgentSmith.Infrastructure",
        "AgentSmith.Infrastructure.Core",
        "AgentSmith.Server",
        "AgentSmith.Cli",
    ];

    [Fact]
    public void EveryProductionSandbox_DeclaresItsRootOrWrapsOne()
    {
        var undeclared = Assemblies()
            .SelectMany(Types)
            .Where(t => t is { IsClass: true, IsAbstract: false } && typeof(ISandbox).IsAssignableFrom(t))
            .Where(t => !typeof(ISandboxWorkDirectory).IsAssignableFrom(t) && !typeof(ISandboxWrapper).IsAssignableFrom(t))
            .Select(t => t.FullName)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

        undeclared.Should().BeEmpty(
            "a production sandbox names its repository root (ISandboxWorkDirectory) or wraps one sandbox "
            + "(ISandboxWrapper); otherwise WorkDirectory() serves it the test-double fallback");
    }

    [Fact]
    public void TheRule_SeesTheBackends() =>
        Assemblies().SelectMany(Types).Select(t => t.Name)
            .Should().Contain(["KubernetesSandbox", "DockerSandbox", "InProcessSandbox", "RepoSandbox"],
                "a rule that loads none of the sandboxes passes by seeing nothing");

    private static IEnumerable<Type> Types(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.OfType<Type>();
        }
    }

    private static IReadOnlyList<Assembly> Assemblies() =>
        [.. ProductionAssemblies
            .Select(name => AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == name) ?? Assembly.Load(name))];
}
