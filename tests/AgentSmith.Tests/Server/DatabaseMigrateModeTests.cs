using AgentSmith.Server.Services.Startup;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;

namespace AgentSmith.Tests.Server;

/// <summary>2026-11-22-747db: the server image's `database migrate` mode — which words select it, which file it reads.</summary>
[Trait("TestProcess", "env-1")]
[Collection(EnvVarCollection.Name)]
public sealed class DatabaseMigrateModeTests
{
    [Theory]
    [InlineData(new[] { "database", "migrate" }, true)]
    [InlineData(new[] { "database", "migrate", "--config", "/x.yml" }, true)]
    [InlineData(new[] { "database" }, false)]
    [InlineData(new string[0], false)]
    [InlineData(new[] { "--urls", "http://+:8081" }, false)]
    public void Matches_TheWords_SelectMigrateMode(string[] args, bool expected) =>
        DatabaseMigrateMode.Matches(args).Should().Be(expected);

    [Fact]
    public void ConfigPath_BothOptionForms_AreRead()
    {
        var mode = new DatabaseMigrateMode();

        mode.ConfigPath(["database", "migrate", "--config", "/a.yml"]).Should().Be("/a.yml");
        mode.ConfigPath(["database", "migrate", "--config=/b.yml"]).Should().Be("/b.yml");
    }

    [Fact]
    public void ConfigPath_NoOption_FallsBackToConfigPathThenTheMount()
    {
        var previous = Environment.GetEnvironmentVariable("CONFIG_PATH");
        try
        {
            Environment.SetEnvironmentVariable("CONFIG_PATH", "/env.yml");
            new DatabaseMigrateMode().ConfigPath(["database", "migrate"]).Should().Be("/env.yml");
            Environment.SetEnvironmentVariable("CONFIG_PATH", null);
            new DatabaseMigrateMode().ConfigPath(["database", "migrate"]).Should().Be("/app/config/agentsmith.yml");
        }
        finally
        {
            Environment.SetEnvironmentVariable("CONFIG_PATH", previous);
        }
    }

    [Fact]
    public async Task RunAsync_NoConfiguration_ExitsOne()
    {
        var missing = Path.Combine(Path.GetTempPath(), $"absent-{Guid.NewGuid():N}.yml");

        (await new DatabaseMigrateMode().RunAsync(["database", "migrate", "--config", missing])).Should().Be(1);
    }
}
