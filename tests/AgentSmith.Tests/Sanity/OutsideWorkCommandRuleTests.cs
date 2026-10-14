using FluentAssertions;

namespace AgentSmith.Tests.Sanity;

public sealed class OutsideWorkCommandRuleTests
{
    private const string Output = "exit_code: 0\nstdout:\nok";

    private static Task<string> Run(string command) =>
        SanityCalls.Text(SanityCalls.Pipeline(withSpec: false), "run_command", Output, ("command", command));

    [Fact]
    public async Task OutsideWorkCommand_CdToAbsoluteWorkPathClimbingOut_IsFlagged()
    {
        var text = await Run("cd /work/src/../../etc && cat passwd");

        text.Should().StartWith(Output + "\n[framework] `cd /work/src/../../etc` climbs above /work to /etc");
    }

    [Fact]
    public async Task OutsideWorkCommand_CdToAbsoluteHostPath_IsNotJudged()
    {
        (await Run("cd /Users/dev/checkout/src && cd ../../.. && ls")).Should().Be(Output);
    }

    [Fact]
    public async Task OutsideWorkCommand_RelativeCdAfterAnEarlierCd_ResolvesAgainstIt()
    {
        (await Run("cd src/backend; cd ../.. && dotnet build")).Should().Be(Output);
        (await Run("cd src; cd ../.. | tee log")).Should().Contain("[framework] `cd ../..` climbs above /work to /");
    }

    [Theory]
    [InlineData("cd && ls ..")]
    [InlineData("cd ~ && cd ../..")]
    [InlineData("cd - ; cd ../..")]
    [InlineData("cd $HOME\ncd ../..")]
    public async Task OutsideWorkCommand_CdHomeDashOrVariable_IsNotJudged(string command)
    {
        (await Run(command)).Should().Be(Output);
    }

    [Theory]
    [InlineData("cd .. && ls")]
    [InlineData("pushd ../other || true")]
    public async Task OutsideWorkCommand_CdDotDotAboveWork_IsFlagged(string command)
    {
        (await Run(command)).Should().Contain("[framework]").And.Contain("climbs above /work");
    }

    [Theory]
    [InlineData("cd src/backend && dotnet test")]
    [InlineData("cd ./web/.. && npm test")]
    [InlineData("cd /work/web; npm ci")]
    [InlineData("echo cd .. > note.txt")]
    public async Task OutsideWorkCommand_CdInsideWork_IsNotFlagged(string command)
    {
        (await Run(command)).Should().Be(Output);
    }
}
