using AgentSmith.Application.Services.Builders;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Sandbox.Wire;
using AgentSmith.Tests.Sandbox;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Tools;

/// <summary>
/// 2026-09-22-46ef: the OTHER half of the rule. Refusing the shell bounds the program name
/// only, and two of the tools a source scope now serves take an operand from the model — so
/// each such operand is a server constant or a value the server has shaped, and a program
/// that could not run says so rather than answering with an absence it never established.
/// </summary>
public sealed class SourceScopeToolOperandTests
{
    private const string Url = "https://api.example.test/health";

    [Fact]
    public async Task FindFiles_ARootThatWouldReadAsAnExpression_CannotReachTheProgram()
    {
        var sandbox = new RecordingSandbox();
        var host = new FilesystemToolHost(sandbox);

        await host.FindFiles("*.cs", root: "-delete");

        var step = sandbox.Runs.Single();
        step.Command.Should().Be("find");
        step.Args!.First().Should().Be("/work/-delete",
            "the root reaches find as a path, never as the start of its expression");
        step.Args!.Should().NotContain("-delete");
    }

    [Fact]
    public async Task FindFiles_AnAbsoluteRoot_IsRefusedBeforeAnythingRuns()
    {
        var sandbox = new RecordingSandbox();
        var host = new FilesystemToolHost(sandbox);

        var answer = await host.FindFiles("*.cs", root: "/etc");

        answer.Should().StartWith("Error:");
        sandbox.Runs.Should().BeEmpty();
    }

    [Fact]
    public async Task FindFiles_AProgramMissingFromTheImage_IsReportedNotAnsweredEmpty()
    {
        var sandbox = new RecordingSandbox
        {
            Respond = _ => Result(exitCode: 127, output: string.Empty, error: "failed to start 'find'"),
        };
        var host = new FilesystemToolHost(sandbox);

        var answer = await host.FindFiles("*.cs");

        answer.Should().StartWith("Error:");
        answer.Should().Contain("not an absence");
    }

    [Fact]
    public async Task FindFiles_OnASourceScope_ReturnsMatchesBoundedByItsLimit()
    {
        var sandbox = new RecordingSandbox
        {
            Respond = step => step.Command == "find"
                ? Result(0, "/work/a.cs\n/work/b.cs\n/work/c.cs")
                : Result(0, "sha"),
        };
        var host = new FilesystemToolHost(Scope(sandbox));

        var answer = await host.FindFiles("*.cs", head_limit: 2);

        answer.Should().Be("a.cs\nb.cs\n(truncated: 2 matches)");
        sandbox.Runs.Should().Contain(step => step.Command == "find",
            "a read-only source scope serves the process the server builds");
    }

    [Fact]
    public void FindFiles_IsOnTheDesignSurfaceAgain()
    {
        var host = new FilesystemToolHost(new RecordingSandbox());

        var names = new AgenticToolSurface()
            .SpecDialog(host, NoTools()).Select(tool => tool.Name).ToList();

        names.Should().Contain("find_files");
        names.Should().Contain("http_request");
        names.Should().NotContain("run_command", "the surface does not widen what it may ask for");
        names.Should().NotContain("write_file");
    }

    [Fact]
    public async Task HttpRequest_AUrlThatWouldReadAsAnOption_IsRefused()
    {
        var sandbox = new RecordingSandbox();
        var host = new FilesystemToolHost(sandbox);

        var answer = await host.HttpRequest("GET", "-o/work/.git/hooks/pre-commit");

        answer.Should().StartWith("Error:");
        answer.Should().Contain("absolute http");
        sandbox.Runs.Should().BeEmpty();
    }

    [Fact]
    public async Task HttpRequest_AHeaderAskingTheProgramToReadAFile_IsRefused()
    {
        var sandbox = new RecordingSandbox();
        var host = new FilesystemToolHost(sandbox);

        var answer = await host.HttpRequest("POST", Url, headers: "@/work/.git/config");

        answer.Should().StartWith("Error:");
        answer.Should().Contain("read a file from disk");
        sandbox.Runs.Should().BeEmpty();
    }

    [Fact]
    public async Task HttpRequest_OnASourceScope_ReachesItsTarget()
    {
        var sandbox = new RecordingSandbox
        {
            Respond = step => step.Command == "curl" ? Result(0, "HTTP/1.1 200 OK") : Result(0, "sha"),
        };
        var host = new FilesystemToolHost(Scope(sandbox));

        var answer = await host.HttpRequest("GET", Url);

        answer.Should().Contain("HTTP/1.1 200 OK");
        sandbox.Runs.Should().Contain(step => step.Command == "curl",
            "the tool the surface offers must work where it is offered");
    }

    [Fact]
    public async Task HttpRequest_OnAnOrdinarySandbox_IsUnchanged()
    {
        var sandbox = new RecordingSandbox();
        var host = new FilesystemToolHost(sandbox);

        await host.HttpRequest("POST", Url, body: "{}", headers: "Accept: application/json");

        var step = sandbox.Runs.Single();
        step.Command.Should().Be("curl", "no shell is involved in a request the server builds");
        step.Args.Should().Equal(
            "-sS", "-i", "--max-time", "15", "-X", "POST",
            "-H", "Accept: application/json", "--data-raw", "{}", Url);
    }

    private static ISourceScopeSandbox Scope(ISandbox inner)
    {
        var factory = new Mock<ISandboxFactory>();
        factory.Setup(f => f.CreateAsync(It.IsAny<SandboxSpec>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(inner);
        var specBuilder = new SandboxSpecBuilder(
            new StubSandboxResourceResolver(),
            Mock.Of<IAgentImageResolver>(r => r.Resolve(It.IsAny<ResolvedProject>()) == "agent:test"));
        var runContext = new Mock<IRunContextAccessor>();
        runContext.SetupGet(r => r.CurrentRunId).Returns("run-1");
        return new SourceScopeSandbox(
            new ResolvedProject { Name = "p" },
            new RepoConnection { Name = "repo-a", Type = RepoType.GitHub, Url = "https://stub.test/repo-a" },
            revision: null,
            new SourceScopeOpener(new SourceScopeMaterialiser(), factory.Object, specBuilder, runContext.Object),
            new AsyncLocalSourceScopeObserverAccessor(), NullLogger<SourceScopeSandbox>.Instance);
    }

    private static IToolHost NoTools()
    {
        var host = new Mock<IToolHost>();
        host.Setup(h => h.GetTools(It.IsAny<Application.Models.SkillExecutionPhase?>(), It.IsAny<string?>()))
            .Returns(Array.Empty<AIFunction>());
        return host.Object;
    }

    private static StepResult Result(int exitCode, string output, string? error = null) => new(
        StepResult.CurrentSchemaVersion, Guid.NewGuid(), exitCode,
        TimedOut: false, DurationSeconds: 0.01, ErrorMessage: error, OutputContent: output);

    /// <summary>An inner sandbox that answers every step and remembers the processes it was sent.</summary>
    private sealed class RecordingSandbox : ISandbox
    {
        public string JobId => "recording";
        public List<Step> Runs { get; } = new();
        public Func<Step, StepResult>? Respond { get; init; }

        public Task<StepResult> RunStepAsync(
            Step step, IProgress<StepEvent>? progress, CancellationToken cancellationToken)
        {
            if (step.Kind == StepKind.Run) Runs.Add(step);
            var canned = Respond?.Invoke(step)
                ?? Result(0, step.Command == "git" ? "sha" : string.Empty);
            return Task.FromResult(canned with { StepId = step.StepId });
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
