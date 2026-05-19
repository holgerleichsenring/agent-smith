using AgentSmith.Application.Services.Tools;
using FluentAssertions;

namespace AgentSmith.Tests.Services.Tools;

public sealed class CommandGuardTests
{
    [Theory]
    [InlineData("rm -rf /")]
    [InlineData("rm foo")]
    [InlineData("ls && rm bar")]
    [InlineData("ls; rm bar")]
    [InlineData("ls | rm")]
    [InlineData("rmdir mydir")]
    [InlineData("unlink foo")]
    [InlineData("shred secret.txt")]
    [InlineData("truncate -s 0 log.txt")]
    [InlineData("dd if=/dev/zero of=/dev/sda")]
    [InlineData("$(rm bar)")]
    [InlineData("`rm bar`")]
    public void Check_BlocksDestructiveCommands(string command)
    {
        CommandGuard.Check(command).Should().NotBeNull()
            .And.Contain("blocked");
    }

    [Theory]
    [InlineData(":(){ :|:& };:")]
    public void Check_BlocksForkBomb(string command)
    {
        CommandGuard.Check(command).Should().NotBeNull()
            .And.Contain("fork-bomb");
    }

    [Theory]
    [InlineData("cat foo > /dev/sda")]
    [InlineData("echo x > /dev/tty1")]
    public void Check_BlocksRawDeviceWrites(string command)
    {
        CommandGuard.Check(command).Should().NotBeNull()
            .And.Contain("device-write");
    }

    [Theory]
    [InlineData("ls -la")]
    [InlineData("find . -name '*.cs' | head -20")]
    [InlineData("grep -r 'TODO' src/")]
    [InlineData("wc -l Program.cs")]
    [InlineData("curl -s https://example.com/api")]
    [InlineData("cat Program.cs")]
    [InlineData("cat log > /dev/null")]            // /dev/null is allowed
    [InlineData("echo done > /dev/stdout")]         // /dev/stdout allowed
    [InlineData("dotnet build")]
    [InlineData("git log --oneline -5")]
    [InlineData("npm install")]                     // does not contain destructive token
    [InlineData("mv old new")]                       // mv intentionally not blocked (legitimate refactor)
    [InlineData("")]
    [InlineData(null)]
    public void Check_AllowsCommonReadAndBuildCommands(string? command)
    {
        CommandGuard.Check(command!).Should().BeNull();
    }
}
