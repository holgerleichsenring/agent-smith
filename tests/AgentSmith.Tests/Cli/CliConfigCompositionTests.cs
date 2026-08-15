using AgentSmith.Contracts.Models.Configuration;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Tests.Cli;

/// <summary>
/// p0417: the CLI's composition must hand handlers the config the operator named.
/// AddAgentSmithCore registers AgentSmithConfig.Empty() as a placeholder; the server
/// overrides it, the CLI did not. Eight handlers inject AgentSmithConfig directly, so
/// a CLI run silently received "nothing is configured" — run 0adc lost its private-feed
/// credentials that way and spent a whole phase diagnosing a 401 that was never real.
/// </summary>
public sealed class CliConfigCompositionTests
{
    [Fact]
    public void CliProvider_WithAConfigPath_ResolvesTheLoadedConfigNotThePlaceholder()
    {
        var path = WriteConfig();
        try
        {
            using var provider = AgentSmith.Cli.ServiceProviderFactory.Build(
                configPath: path, verbose: false, headless: true);

            var config = provider.GetRequiredService<AgentSmithConfig>();

            config.Registries.Should().ContainSingle(
                "a handler injecting AgentSmithConfig must see what the operator configured, "
                + "never the empty placeholder")
                .Which.Host.Should().Be("packages.example.test");
        }
        finally { File.Delete(path); }
    }

    private static string WriteConfig()
    {
        var path = Path.Combine(Path.GetTempPath(), $"agentsmith-cli-{Guid.NewGuid():N}.yml");
        File.WriteAllText(path, """
            agents:
              default:
                type: claude
                model: sonnet
            registries:
            - host: packages.example.test
              username: any
              token: plain-token
            """);
        return path;
    }

    /// <summary>
    /// p0419: registering the config is not enough — the verb has to PASS it. `run`,
    /// the verb every pipeline goes through, read --config into a local, handed it to
    /// the use case and built its container without it, so p0417's registration never
    /// fired where it mattered most. Any verb that offers --config must give it to the
    /// container; the two verbs that own no agentsmith.yml say so at the call site.
    /// </summary>
    [Fact]
    public void EveryVerbThatOffersAConfigOption_PassesItToTheContainer()
    {
        var offenders = new List<string>();
        foreach (var file in Directory.EnumerateFiles(CommandsDirectory(), "*.cs"))
        {
            var source = File.ReadAllText(file);
            if (!source.Contains("ServiceProviderFactory.Build(")) continue;
            if (!source.Contains("configOption")) continue;
            if (!source.Contains("Build(configPath,")) offenders.Add(Path.GetFileName(file));
        }

        offenders.Should().BeEmpty(
            "a verb that lets the operator name a config must run on it — "
            + "building the container without it hands every handler an empty config");
    }

    private static string CommandsDirectory()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "src")))
            dir = dir.Parent;
        dir.Should().NotBeNull("the repository root has to be findable from the test binary");
        return Path.Combine(dir!.FullName, "src", "backend", "AgentSmith.Cli", "Commands");
    }
}
