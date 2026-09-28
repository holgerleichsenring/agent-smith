using AgentSmith.Cli.Commands;
using FluentAssertions;

namespace AgentSmith.Tests.Cli.Commands;

public sealed class CliRootCommandTests
{
    [Fact]
    public void CliRoot_HasNoAutonomousVerb()
    {
        var verbs = CliRootCommand.Create().Subcommands.Select(c => c.Name).ToList();

        verbs.Should().NotContain("autonomous",
            "the autonomous preset was removed, and a verb that can only fail misleads in --help");
        verbs.Should().Contain("code");
    }
}
