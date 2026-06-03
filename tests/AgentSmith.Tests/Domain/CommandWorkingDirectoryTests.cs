using AgentSmith.Domain.Models;
using FluentAssertions;

namespace AgentSmith.Tests.Domain;

/// <summary>
/// p0212: CommandWorkingDirectory derives the repo-relative directory in which
/// a context's prerequisites / test command runs. meta.workdir overrides; else
/// the longest common directory prefix of the analyzer's module paths; else ".".
/// </summary>
public sealed class CommandWorkingDirectoryTests
{
    [Fact]
    public void Resolve_NestedModulesShareRoot_ReturnsCommonPrefix()
    {
        var map = MapWith("Sample.Client", "Sample.Client/src");

        CommandWorkingDirectory.Resolve(map, ".").Should().Be("Sample.Client");
    }

    [Fact]
    public void Resolve_SingleModule_ReturnsThatModuleDirectory()
    {
        var map = MapWith("Sample.Client");

        CommandWorkingDirectory.Resolve(map, ".").Should().Be("Sample.Client");
    }

    [Fact]
    public void Resolve_ModuleAtRepoRoot_ReturnsDot()
    {
        // The .NET fixture convention: a single module at "." (test_command
        // embeds the test-project path) must derive "." so the command runs at /work.
        var map = MapWith(".");

        CommandWorkingDirectory.Resolve(map, ".").Should().Be(".");
    }

    [Fact]
    public void Resolve_ModulesWithoutSharedRoot_ReturnsDot()
    {
        var map = MapWith("api", "client");

        CommandWorkingDirectory.Resolve(map, ".").Should().Be(".");
    }

    [Fact]
    public void Resolve_EmptyModules_ReturnsDot()
    {
        var map = MapWith();

        CommandWorkingDirectory.Resolve(map, ".").Should().Be(".");
    }

    [Fact]
    public void Resolve_NullMap_ReturnsDot()
    {
        CommandWorkingDirectory.Resolve(null, ".").Should().Be(".");
    }

    [Fact]
    public void Resolve_WorkdirOverride_WinsOverModulePaths()
    {
        var map = MapWith("Sample.Client", "Sample.Client/src");

        CommandWorkingDirectory.Resolve(map, "operator/override").Should().Be("operator/override");
    }

    [Fact]
    public void Resolve_WorkdirOverrideWithNullMap_ReturnsOverride()
    {
        CommandWorkingDirectory.Resolve(null, "frontend").Should().Be("frontend");
    }

    [Fact]
    public void Resolve_DeeplyNestedModules_ReturnsDeepestCommonDirectory()
    {
        var map = MapWith("services/api/core", "services/api/web", "services/api/core/util");

        CommandWorkingDirectory.Resolve(map, ".").Should().Be("services/api");
    }

    private static ProjectMap MapWith(params string[] modulePaths) =>
        new(
            PrimaryLanguage: "polyglot",
            Frameworks: [],
            Modules: modulePaths.Select(p => new Module(p, ModuleRole.Production, [])).ToArray(),
            TestProjects: [],
            EntryPoints: [],
            Conventions: new Conventions(null, null, null),
            Ci: new CiConfig(HasCi: false, BuildCommand: null, TestCommand: null, CiSystem: null));
}
