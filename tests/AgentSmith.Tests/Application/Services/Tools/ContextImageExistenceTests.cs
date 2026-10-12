using System.Text.Json;
using AgentSmith.Application.Models.Sandbox;
using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Infrastructure.Services;
using AgentSmith.Sandbox.Wire;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Moq;

namespace AgentSmith.Tests.Tools;

/// <summary>
/// 2026-10-12-50bdb: a stack.image its registry does not have is refused before anything is
/// written, with the tags the repository does list; an image the registry could not be asked
/// about is written and said, never refused.
/// </summary>
public sealed class ContextImageExistenceTests
{
    private const string Invented = "sbtscala/scala-sbt:eclipse-temurin-25_1.11.7_2.13.18";
    private const string Published = "eclipse-temurin-25.0.1_8_1.11.7_2.13.18";

    private readonly Mock<ISandbox> _sandboxMock = new();
    private readonly ContextYamlSerializer _serializer = new(new ContextYamlBuilders());

    public ContextImageExistenceTests() =>
        _sandboxMock.Setup(s => s.RunStepAsync(
                It.IsAny<Step>(), It.IsAny<IProgress<StepEvent>?>(), It.IsAny<CancellationToken>()))
            .Returns<Step, IProgress<StepEvent>?, CancellationToken>((step, _, _) =>
                Task.FromResult(new StepResult(StepResult.CurrentSchemaVersion, step.StepId, 0, false, 0.1, null)));

    [Fact]
    public async Task WriteContextYaml_MissingImage_RefusesAndNamesCandidates()
    {
        var host = BuildHost(Missing([Published, "eclipse-temurin-25.0.4_7_1.13.0_2.13.18"]));

        var result = await Write(host, Document(Invented));

        result.Should().StartWith("Error:");
        result.Should().Contain($"'{Invented}' does not exist");
        result.Should().Contain(Published);
        result.Should().Contain("Never compose a tag from version numbers");
        Written().Should().BeFalse("an image no sandbox can pull is never written");
        host.Outcome.Written.Should().BeFalse();
    }

    [Fact]
    public async Task WriteContextYaml_MissingImageTagsUnlistable_RefusesAndSaysSo()
    {
        var result = await Write(BuildHost(Missing(null)), Document(Invented));

        result.Should().StartWith("Error:");
        result.Should().Contain("tag list could not be read");
    }

    [Fact]
    public async Task WriteContextYaml_MissingImageSixTimes_ExhaustsTheBudget()
    {
        var host = BuildHost(Missing([Published]));

        for (var attempt = 0; attempt <= ContextWriteRejectionBudget.Limit; attempt++)
            await Write(host, Document(Invented));

        host.Outcome.BudgetExhausted.Should().BeTrue("a missing image spends the same budget as any defect");
        Written().Should().BeFalse();
    }

    [Fact]
    public async Task WriteContextYaml_MissingImageAndSchemaDefect_OneRefusalNamesBoth()
    {
        var document = $$"""
            {
              "meta": { "workdir": "." },
              "stack": { "lang": "Scala", "image": "{{Invented}}" },
              "verify": [
                { "label": "build", "command": "cd app && sbt compile" },
                { "label": "test", "command": "cd app && sbt test" }
              ]
            }
            """;

        var result = await Write(BuildHost(Missing([Published])), document);

        result.Should().Contain("meta.workdir").And.Contain("does not exist");
        result.Should().Contain("refusal 1 of", "both defects are one refusal");
    }

    [Fact]
    public async Task WriteContextYaml_UnverifiedImage_WritesAndSaysSo()
    {
        var check = new FixedImageReferenceCheck(image =>
            ImageReferenceVerdict.Unverified(image, "registry-1.docker.io gave no answer within 5 s"));

        var result = await Write(BuildHost(check), Document("node:20-bookworm"));

        result.Should().StartWith("context.yaml written:");
        result.Should().Contain("could not be checked against its registry (registry-1.docker.io gave no answer");
        result.Should().Contain("no rewrite is needed");
        Written().Should().BeTrue();
    }

    [Fact]
    public async Task WriteContextYaml_ExistingImage_AsksTheRegistryAndSaysNothingMore()
    {
        var check = new FixedImageReferenceCheck();

        var result = await Write(BuildHost(check), Document("node:20-bookworm"));

        check.Asked.Should().Equal("node:20-bookworm");
        result.Should().NotContain("Note:");
    }

    private static FixedImageReferenceCheck Missing(IReadOnlyList<string>? candidates) =>
        new(image => ImageReferenceVerdict.Missing(
            image, "registry-1.docker.io has no 'x' in repository sbtscala/scala-sbt", candidates));

    private static string Document(string image) => $$"""
        { "meta": { "workdir": "." }, "stack": { "lang": "Scala", "image": "{{image}}" } }
        """;

    private static Task<string> Write(WriteContextYamlToolHost host, string document) =>
        host.WriteContextYaml(repo: "client", context_name: "default", JsonDocument.Parse(document).RootElement);

    private bool Written() =>
        _sandboxMock.Invocations.Any(call => call.Arguments[0] is Step { Kind: StepKind.WriteFile });

    private WriteContextYamlToolHost BuildHost(FixedImageReferenceCheck check) =>
        new(new Dictionary<string, ISandbox>(StringComparer.Ordinal) { ["client"] = _sandboxMock.Object },
            defaultRepo: "client", _serializer, ContextGates.Build(), ContextGates.Writer(),
            ContextGates.DerivationStamp(), ContextGates.ImageReview(check));
}
