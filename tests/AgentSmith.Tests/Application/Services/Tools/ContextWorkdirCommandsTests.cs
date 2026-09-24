using System.Text.Json;
using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Infrastructure.Services;
using AgentSmith.Sandbox.Wire;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Moq;

namespace AgentSmith.Tests.Tools;

/// <summary>
/// 2026-09-23-72c7: a document claiming its source is the whole repository while EVERY
/// command it declares begins by entering one directory contradicts itself, and the write
/// path refuses it naming both halves. Every other shape — commands that disagree with
/// each other, commands that stay at the root, a workdir that already names a sub-tree —
/// says nothing certain and is written as before.
/// </summary>
public sealed class ContextWorkdirCommandsTests
{
    private readonly Mock<ISandbox> _sandboxMock = new();
    private readonly ContextYamlSerializer _serializer = new(new ContextYamlBuilders());

    public ContextWorkdirCommandsTests() =>
        _sandboxMock.Setup(s => s.RunStepAsync(
                It.IsAny<Step>(), It.IsAny<IProgress<StepEvent>?>(), It.IsAny<CancellationToken>()))
            .Returns<Step, IProgress<StepEvent>?, CancellationToken>((step, _, _) =>
                Task.FromResult(new StepResult(StepResult.CurrentSchemaVersion, step.StepId, 0, false, 0.1, null)));

    [Fact]
    public async Task WriteContextYaml_RootWorkdirWithCommandsAllEnteringOneDirectory_IsRefused()
    {
        var result = await Write(Document("""
            "verify": [
              { "label": "build", "command": "cd app && npm run build" },
              { "label": "test", "command": "cd ./app/ && npm run test" }
            ]
            """));

        result.Should().StartWith("Error:");
        result.Should().Contain("meta.workdir",
            "the refusal names the half that claims the source is the whole repository");
        result.Should().Contain("entering 'app'",
            "and the directory the commands agree on, so the round can answer it");
        // 2026-09-24-c71a: the refusal used to forbid reading the directory off the cd. Once
        // workdir is the directory the MANIFEST sits in, the cd usually enters exactly that —
        // so the caution is that a build script MAY go deeper, not that the cd is unusable.
        result.Should().NotContain("not copied from the cd");
        result.Should().Contain("manifest",
            "the refusal names what the round should look for, not just what it got wrong");
        result.Should().Contain("read it off the tree",
            "a build script may enter a sub-directory of a component that spans more");
        Written().Should().BeFalse("the refusal is returned instead of a file");
    }

    [Fact]
    public async Task WriteContextYaml_RootWorkdirWithCommandsEnteringDifferentDirectories_IsWritten()
    {
        var result = await Write(Document("""
            "verify": [
              { "label": "build", "command": "cd web && npm run build" },
              { "label": "test", "command": "cd api && npm run test" }
            ]
            """));

        result.Should().StartWith("context.yaml written:",
            "two components under one root is exactly what a repository-root workdir describes");
    }

    [Fact]
    public async Task WriteContextYaml_RootWorkdirWithSomeCommandsAtTheRoot_IsWritten()
    {
        var result = await Write(Document("""
            "prerequisites": "npm ci",
            "verify": [
              { "label": "build", "command": "cd web && npm run build" }
            ]
            """));

        result.Should().StartWith("context.yaml written:",
            "a command that runs at the root says something IS there, so the root workdir "
            + "is not contradicted and a check that fired here would refuse a shape that works");
    }

    [Fact]
    public async Task WriteContextYaml_SubTreeWorkdirWithCommandsEnteringIt_IsWritten()
    {
        var result = await Write("""
            {
              "meta": { "workdir": "web" },
              "stack": { "lang": "TypeScript", "image": "node:20-bookworm" },
              "verify": [
                { "label": "build", "command": "cd web && npm run build" },
                { "label": "test", "command": "cd web && npm run test" }
              ]
            }
            """);

        result.Should().StartWith("context.yaml written:",
            "a workdir that already names a sub-tree is not contested by a command entering one");
    }

    private static string Document(string commands) => $$"""
        {
          "meta": { "workdir": "." },
          "stack": { "lang": "TypeScript", "image": "node:20-bookworm" },
          {{commands}}
        }
        """;

    private Task<string> Write(string document) =>
        BuildHost().WriteContextYaml(
            repo: "client", context_name: "default", JsonDocument.Parse(document).RootElement);

    private bool Written() =>
        _sandboxMock.Invocations.Any(call => call.Arguments[0] is Step { Kind: StepKind.WriteFile });

    private WriteContextYamlToolHost BuildHost() =>
        new(new Dictionary<string, ISandbox>(StringComparer.Ordinal) { ["client"] = _sandboxMock.Object },
            defaultRepo: "client", _serializer, ContextGates.Build(), ContextGates.Writer(),
            ContextGates.DerivationStamp());
}
