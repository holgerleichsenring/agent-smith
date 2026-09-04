using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Core.Services;
using AgentSmith.Infrastructure.Core.Services.Skills;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Services.Skills;

/// <summary>
/// 2026-09-03-7bac: the framework runs every command a repository declares or the
/// analyzer emits at the REPOSITORY ROOT, and reads no field to place one elsewhere.
/// That is half a contract. The other half lives in the skills catalog, where the
/// analyzer master is told which frame to write its commands against — and the two
/// repositories move independently, so the backend can ship the rule while the pinned
/// catalog still teaches the old one.
/// <para>
/// Run 5a18 is what that gap costs: a command written against one frame and executed in
/// another passed its build and failed its test on a project that exists. This asserts
/// against the REAL embedded tarball, so a pin that predates the contract fails here
/// rather than on the first repository whose manifests sit one directory down.
/// </para>
/// </summary>
public sealed class AnalyzerPromptFrameTests : IDisposable
{
    private readonly string _cacheDir = Path.Combine(
        Path.GetTempPath(), $"agentsmith-frame-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_cacheDir)) Directory.Delete(_cacheDir, recursive: true);
    }

    [Fact]
    public async Task AnalyzerPrompt_StatesTheRepoRootFrame()
    {
        var master = await MasterAsync("project-analyzer-master");

        master.Should().Contain("REPOSITORY ROOT",
            "the analyzer writes ci.build_command, ci.test_command and prerequisites, and "
            + "the gate runs all three from the repository root");
        master.Should().Contain("cd ",
            "a command needing another directory carries its own cd — the analyzer is the "
            + "only party that read the tree and can know it needs one");
    }

    /// <summary>
    /// The operator authoring a verify block writes against the same frame. A catalog that
    /// told only the analyzer would leave every hand-written declaration guessing.
    /// </summary>
    [Fact]
    public async Task BootstrapPrompt_StatesTheRepoRootFrame()
    {
        var master = await MasterAsync("project-bootstrap");

        master.Should().Contain("REPOSITORY ROOT");
        master.Should().NotContain("relative to `meta.workdir`",
            "meta.workdir says where a context's source lives and places no command");
    }

    private async Task<string> MasterAsync(string name)
    {
        var root = await MaterializeAsync();
        var path = Path.Combine(root, "skills", "_masters", name, "SKILL.md");
        File.Exists(path).Should().BeTrue($"the pinned catalog must ship {name}");
        return await File.ReadAllTextAsync(path);
    }

    private async Task<string> MaterializeAsync()
    {
        var handler = new EmbeddedSourceHandler(
            new EmbeddedSkillsCatalog(),
            new CatalogTarballExtractor(NullLogger<CatalogTarballExtractor>.Instance),
            new SkillsCacheMarker(NullLogger<SkillsCacheMarker>.Instance),
            NullLogger<EmbeddedSourceHandler>.Instance);
        var resolution = await handler.ResolveAsync(
            new SkillsConfig { Source = SkillsSourceMode.Embedded, CacheDir = _cacheDir },
            CancellationToken.None);
        return resolution.Root;
    }
}
