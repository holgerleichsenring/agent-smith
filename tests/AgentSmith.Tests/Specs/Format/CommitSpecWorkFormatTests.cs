using AgentSmith.Application.Models;
using AgentSmith.Tests.Structure;
using FluentAssertions;

namespace AgentSmith.Tests.Specs.Format;

/// <summary>
/// 2026-10-12-fbbdb: the spec commit formats the spec's files BEFORE it pushes, so the branch
/// carries formatted code and the structure gate measures it; a formatter that fails is said,
/// never a failed step.
/// </summary>
public sealed class CommitSpecWorkFormatTests
{
    private const string Csharpier = "dotnet csharpier {files}";

    [Fact]
    public async Task CommitSpecWork_FormatDeclared_RunsBeforePush()
    {
        var sandbox = new FormatSandbox();
        sandbox.Changed.Add("src/A.cs");
        var pipeline = FormatFixture.Pipeline(sandbox, FormatFixture.Context("api", ".", Csharpier));

        var result = await Commit(pipeline);

        result.IsSuccess.Should().BeTrue(result.Message);
        var format = sandbox.RanSteps.FindIndex(FormatSandbox.IsShell);
        var commit = sandbox.RanSteps.FindIndex(step => FormatSandbox.IsGit(step, "commit"));
        format.Should().BeGreaterThanOrEqualTo(0, "the declared formatter ran");
        sandbox.RanSteps[format].Args![1].Should().Be("dotnet csharpier './src/A.cs'");
        commit.Should().BeGreaterThan(format, "the checkpoint commits what the formatter wrote");
    }

    [Fact]
    public async Task CommitSpecWork_FormatFails_CommitsAndSaysSo()
    {
        var sandbox = new FormatSandbox { ShellExitCode = 127 };
        sandbox.Changed.Add("src/A.cs");
        var pipeline = FormatFixture.Pipeline(sandbox, FormatFixture.Context("api", ".", Csharpier));

        var result = await Commit(pipeline);

        result.IsSuccess.Should().BeTrue("a formatter that cannot run never fails the spec");
        result.Message.Should().StartWith("Spec work is on the branch;", "the verdict leads, the formatter note follows")
            .And.Contain("\nformat: the api formatter exited 127");
        sandbox.RanSteps.Should().Contain(step => FormatSandbox.IsGit(step, "commit"), "the work is committed as written");
    }

    [Fact]
    public async Task CommitSpecWork_NoChangedFile_RunsNothing()
    {
        var sandbox = new FormatSandbox();
        var pipeline = FormatFixture.Pipeline(sandbox, FormatFixture.Context("api", ".", Csharpier));

        var result = await Commit(pipeline);

        result.IsSuccess.Should().BeTrue(result.Message);
        sandbox.ShellCommands.Should().BeEmpty("a formatter given no file would format nothing or everything");
    }

    private static Task<AgentSmith.Domain.Models.CommandResult> Commit(AgentSmith.Contracts.Commands.PipelineContext pipeline) =>
        FormatFixture.Handler().ExecuteAsync(new CommitSpecWorkContext(pipeline), CancellationToken.None);
}
