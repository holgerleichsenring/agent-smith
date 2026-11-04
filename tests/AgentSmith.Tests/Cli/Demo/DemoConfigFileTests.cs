using AgentSmith.Application.Services.Configuration;
using AgentSmith.Cli.Services.Demo;
using AgentSmith.Contracts.Models.Configuration;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Tests.Cli.Demo;

/// <summary>2026-11-02-2b26a: the config the demo writes when none is found — from the key that is set, beside the
/// workspace, and loadable by the real loader.</summary>
public sealed class DemoConfigFileTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"agentsmith-2b26a-{Guid.NewGuid():N}");

    [Fact]
    public void TryWrite_OpenAiKeySet_WritesOpenAiAgent()
    {
        var path = DemoConfigFile.TryWrite(Env(("OPENAI_API_KEY", "sk-x"), ("ANTHROPIC_API_KEY", "sk-ant")), Workspace());

        File.ReadAllText(path!).Should().Contain("type: openai").And.NotContain("type: claude");
    }

    [Fact]
    public void TryWrite_OnlyAnthropicKeySet_WritesClaudeAgent()
    {
        var path = DemoConfigFile.TryWrite(Env(("OPENAI_API_KEY", ""), ("ANTHROPIC_API_KEY", "sk-ant")), Workspace());

        File.ReadAllText(path!).Should().Contain("type: claude", "an empty OPENAI_API_KEY is not a key");
    }

    [Fact]
    public void TryWrite_NoKey_ReturnsNull()
    {
        DemoConfigFile.TryWrite(Env(), Workspace()).Should().BeNull();
        Directory.Exists(_root).Should().BeFalse("nothing is written without a key");
    }

    [Fact]
    public void TryWrite_FileExists_ReturnsItsPathUnchanged()
    {
        var workspace = Workspace();
        Directory.CreateDirectory(_root);
        File.WriteAllText(DemoConfigFile.PathFor(workspace), "agents: {}\n");

        var path = DemoConfigFile.TryWrite(Env(("OPENAI_API_KEY", "sk-x")), workspace);

        path.Should().Be(DemoConfigFile.PathFor(workspace));
        File.ReadAllText(path!).Should().Be("agents: {}\n");
    }

    [Fact]
    public void TryWrite_WorkspaceWithTrailingSeparator_WritesOutsideIt()
    {
        var workspace = Workspace();

        var path = DemoConfigFile.TryWrite(Env(("OPENAI_API_KEY", "sk-x")), workspace + Path.DirectorySeparatorChar);

        Path.GetDirectoryName(path).Should().Be(_root, "the materializer replaces the workspace; the config must survive it");
    }

    [Theory]
    [InlineData("OPENAI_API_KEY", "demo-openai")]
    [InlineData("ANTHROPIC_API_KEY", "demo-claude")]
    public void Generated_LoadsAndValidates(string key, string agent)
    {
        var path = DemoConfigFile.TryWrite(Env((key, "sk-x")), Workspace())!;

        using var provider = AgentSmith.Cli.ServiceProviderFactory.Build(configPath: path, verbose: false, headless: true);
        var config = provider.GetRequiredService<AgentSmithConfig>();

        config.Agents.Should().ContainKey(agent);
        provider.GetRequiredService<AgentSmithConfigValidator>().Validate(config).Should().BeEmpty();
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private string Workspace() => Path.Combine(_root, "ws");

    private static Func<string, string?> Env(params (string Name, string Value)[] vars) =>
        name => vars.FirstOrDefault(v => v.Name == name).Value;
}
