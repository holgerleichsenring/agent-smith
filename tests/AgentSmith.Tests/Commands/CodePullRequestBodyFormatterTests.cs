using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using FluentAssertions;

namespace AgentSmith.Tests.Commands;

/// <summary>
/// 2026-10-13-a347a: a code PR body names its repository's failed formatters right after the
/// banner, with the command, its output and the context.yaml lever.
/// </summary>
public sealed class CodePullRequestBodyFormatterTests
{
    private const string Banner = "> banner\n\n";

    private static CommitAndPRContext Context(params SpecFormatFailure[] failures)
    {
        var pipeline = new PipelineContext();
        if (failures.Length > 0)
            pipeline.Set<IReadOnlyList<SpecFormatFailure>>(ContextKeys.SpecFormatFailures, failures);
        var ticket = new Ticket(new TicketId("1"), "T", "Ticket description", null, "Open", "GitHub");
        return new CommitAndPRContext(new Repository(new BranchName("fix/1"), "u"), [], ticket,
            [new RepoConnection { Name = "server" }], new TrackerConnection(), pipeline);
    }

    [Fact]
    public void CodePullRequestBody_FormatterFailure_NamesCommandAndLever()
    {
        var body = CodePullRequestBody.Compose(Context(
            new SpecFormatFailure("server", "api", "dotnet csharpier {files}", 1, "Unrecognized command 'src/A.cs'"),
            new SpecFormatFailure("client", "web", "prettier {files}", 2, "web output")), Banner, null, null, "server");

        body.Should().StartWith(Banner + SpecFormatFailureSection.Heading, "Azure DevOps cuts a long description from the end");
        body.Should().Contain("- `api`: `dotnet csharpier {files}` exited 1")
            .And.Contain("```\nUnrecognized command 'src/A.cs'\n```")
            .And.Contain("correct `format` in `.agentsmith/contexts/api/context.yaml` to the rewriting form the repository's CI uses")
            .And.NotContain("web output", "another repository's formatter is that repository's PR");
        body.IndexOf("Ticket description", StringComparison.Ordinal)
            .Should().BeGreaterThan(body.IndexOf(SpecFormatFailureSection.Heading, StringComparison.Ordinal));
    }

    [Fact]
    public void CodePullRequestBody_NoFailure_HasNoFormatterSection()
    {
        CodePullRequestBody.Compose(Context(), Banner, null, null, "server")
            .Should().NotContain(SpecFormatFailureSection.Heading);
        CodePullRequestBody.Compose(
                Context(new SpecFormatFailure("server", "api", "fmt {files}", 1, "out")), Banner, null, null)
            .Should().NotContain(SpecFormatFailureSection.Heading, "the spec-only body names no repository");
    }

    [Fact]
    public void SpecFormatFailureSection_OutputWithAFence_IsNotClosedByIt()
    {
        var pipeline = new PipelineContext();
        pipeline.Set<IReadOnlyList<SpecFormatFailure>>(ContextKeys.SpecFormatFailures,
            [new SpecFormatFailure("server", "api", "fmt {files}", 1, "a\n```\nb")]);

        SpecFormatFailureSection.Build(pipeline, "server").Should().Contain("````\na\n```\nb\n````");
    }
}
