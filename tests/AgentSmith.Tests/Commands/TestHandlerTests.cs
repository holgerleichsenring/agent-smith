using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Builders;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Commands;

public sealed class TestHandlerTests
{
    private readonly TestHandler _handler = new(
        new TrxResultParser(),
        NullLogger<TestHandler>.Instance);

    [Fact]
    public async Task ExecuteAsync_ProjectMapMissing_SkipsWithDescriptiveMessage()
    {
        var repo = new Repository(new BranchName("main"), "https://github.com/org/repo.git");
        var pipeline = new PipelineContext();
        var context = new TestContext(repo, new List<CodeChange>(), pipeline);

        var result = await _handler.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Contain("ProjectMap missing");
    }

    [Fact]
    public void Resolve_EmptyCiTestCommand_SkipsWithDescriptiveMessage()
    {
        var pipeline = PipelineWithProjectMap(testCommand: null);

        var resolved = TestHandler.ResolveTestCommand(pipeline);

        resolved.Command.Should().BeNull();
        resolved.SkipReason.Should().Contain("Ci.TestCommand is empty");
    }

    [Fact]
    public void Resolve_UsesCiTestCommandVerbatim_WhenNonEmpty()
    {
        var pipeline = PipelineWithProjectMap(testCommand: "pytest -v");

        var resolved = TestHandler.ResolveTestCommand(pipeline);

        resolved.Command.Should().Be("pytest");
        resolved.Args.Should().Equal("-v");
        resolved.IsTrxCapable.Should().BeFalse();
    }

    [Fact]
    public void Resolve_TokenizesQuotedArguments_KeepingQuotedValueAsSingleArg()
    {
        var pipeline = PipelineWithProjectMap(testCommand: "dotnet test --filter \"Category=Unit\"");

        var resolved = TestHandler.ResolveTestCommand(pipeline);

        resolved.Command.Should().Be("dotnet");
        // First args reflect the analyzer-supplied tokens; TRX flags are appended after.
        resolved.Args!.Take(3).Should().Equal("test", "--filter", "Category=Unit");
        resolved.Args!.Should().Contain("--logger");
        resolved.Args!.Should().Contain("trx");
    }

    [Fact]
    public void Resolve_IsTrxCapable_WhenCommandStartsWithDotnet()
    {
        var pipeline = PipelineWithProjectMap(testCommand: "dotnet test");

        var resolved = TestHandler.ResolveTestCommand(pipeline);

        resolved.IsTrxCapable.Should().BeTrue();
        resolved.Args!.Should().Contain("--logger");
    }

    [Fact]
    public void Resolve_NotTrxCapable_ForNonDotnetCommands()
    {
        TestHandler.ResolveTestCommand(PipelineWithProjectMap("npm test"))
            .IsTrxCapable.Should().BeFalse();
        TestHandler.ResolveTestCommand(PipelineWithProjectMap("pytest"))
            .IsTrxCapable.Should().BeFalse();
        TestHandler.ResolveTestCommand(PipelineWithProjectMap("go test ./..."))
            .IsTrxCapable.Should().BeFalse();
        TestHandler.ResolveTestCommand(PipelineWithProjectMap("cargo test"))
            .IsTrxCapable.Should().BeFalse();
    }

    [Theory]
    [InlineData("lua tests/run.lua")]
    [InlineData("rake test")]
    [InlineData("mix test")]
    public void Resolve_ExecutesArbitraryLanguageCommand(string command)
    {
        var pipeline = PipelineWithProjectMap(command);

        var resolved = TestHandler.ResolveTestCommand(pipeline);

        resolved.Command.Should().NotBeNull();
        resolved.SkipReason.Should().BeNull();
    }

    private static PipelineContext PipelineWithProjectMap(string? testCommand)
    {
        var pipeline = new PipelineContext();
        var ci = new CiConfig(HasCi: testCommand is not null, BuildCommand: null, TestCommand: testCommand, CiSystem: null);
        var map = new ProjectMap(
            PrimaryLanguage: "polyglot",
            Frameworks: Array.Empty<string>(),
            Modules: Array.Empty<Module>(),
            TestProjects: Array.Empty<TestProject>(),
            EntryPoints: Array.Empty<string>(),
            Conventions: new Conventions("", "", ""),
            Ci: ci);
        pipeline.Set(ContextKeys.ProjectMap, map);
        return pipeline;
    }
}
