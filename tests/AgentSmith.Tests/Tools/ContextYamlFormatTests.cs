using System.Text.Json;
using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Infrastructure.Services;
using AgentSmith.Sandbox.Wire;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Moq;

namespace AgentSmith.Tests.Tools;

/// <summary>
/// 2026-10-12-fbbdb: a context declares the formatter its repository already runs; the field
/// survives read, write and discovery, and a command that cannot name the changed files is refused.
/// </summary>
public sealed class ContextYamlFormatTests
{
    private readonly ContextYamlSerializer _serializer = new(new ContextYamlBuilders());
    private readonly Mock<ISandbox> _sandbox = new();
    private Step? _written;

    public ContextYamlFormatTests()
    {
        _sandbox.Setup(s => s.RunStepAsync(It.IsAny<Step>(), It.IsAny<IProgress<StepEvent>?>(), It.IsAny<CancellationToken>()))
            .Returns<Step, IProgress<StepEvent>?, CancellationToken>((step, _, _) =>
            {
                if (step.Kind == StepKind.WriteFile) _written = step;
                return Task.FromResult(new StepResult(StepResult.CurrentSchemaVersion, step.StepId, 0, false, 0.1, null));
            });
    }

    [Fact]
    public void RemoteContextDiscovery_FormatDeclared_CarriesIt()
    {
        var parsed = _serializer.Parse("""
            meta:
              workdir: src/Api
            format:
              command: dotnet csharpier {files}
              when_present: .config/dotnet-tools.json
            """);

        var discovery = RemoteContextDiscovery.From("api", parsed.Summary!);

        discovery.Format.Should().Be(
            new ContextYamlFormat("dotnet csharpier {files}", ".config/dotnet-tools.json"));
    }

    [Fact]
    public async Task WriteContextYaml_FormatWithoutFilesPlaceholder_IsRefused()
    {
        var result = await Host().WriteContextYaml("client", "default", Document("dotnet csharpier ."));

        result.Should().NotStartWith(SandboxContextYamlWriter.WrittenPrefix);
        result.Should().Contain("/format/command");
        _written.Should().BeNull("a formatter that cannot be handed the changed files would reformat the repository");
    }

    [Theory]
    [InlineData("dotnet csharpier format '{files}'")]
    [InlineData("dotnet csharpier format \\\"{files}\\\"")]
    public async Task ContextYamlFormat_QuotedPlaceholder_IsRefused(string command)
    {
        // 2026-10-13-2d0c: {files} is replaced by paths already quoted; quoting it again joins them into one word.
        var result = await Host().WriteContextYaml("client", "default", Document(command));

        result.Should().NotStartWith(SandboxContextYamlWriter.WrittenPrefix);
        result.Should().Contain("/format/command");
        _written.Should().BeNull();
    }

    [Fact]
    public async Task WriteContextYaml_FormatOnly_KeepsItsDerivation()
    {
        var result = await Host().WriteContextYaml("client", "default", Document("dotnet csharpier {files}"));

        result.Should().StartWith(SandboxContextYamlWriter.WrittenPrefix);
        var summary = _serializer.Parse(_written!.Content!).Summary!;
        summary.Format!.Command.Should().Be("dotnet csharpier {files}");
        summary.VerifyDerivedFrom!.Files.Should().Equal([".github/workflows/ci.yml"],
            "a csharpier-only context names the file it read the formatter from");
    }

    [Fact]
    public void ContextWorkdirCommandsRule_FormatEnteringADirectory_IsCounted()
    {
        var document = new ContextYamlDocument(
            new ContextYamlMeta("."), Format: new ContextYamlFormat("cd web && npx prettier --write {files}"));

        new ContextWorkdirCommandsRule().Defect(document).Should().Contain("'web'");
    }

    [Fact]
    public void WriteContextYamlToolDescription_DocumentsFormat() =>
        WriteContextYamlToolDescription.Document.Should()
            .Contain("format?: { command, when_present? }").And.Contain("{files}");

    [Fact]
    public void WriteContextYamlToolDescription_NamesWhenChanged() =>
        WriteContextYamlToolDescription.Document.Should()
            .Contain("{ label, command, when_present?, when_changed? }").And.Contain("'**' any number of path segments");

    private static JsonElement Document(string command) => JsonDocument.Parse($$"""
        {
          "meta": { "workdir": "." },
          "stack": { "lang": "csharp", "image": "mcr.microsoft.com/dotnet/sdk:10.0" },
          "format": { "command": "{{command}}" },
          "verify_derived_from": { "files": [".github/workflows/ci.yml"] }
        }
        """).RootElement;

    private WriteContextYamlToolHost Host() => new(
        new Dictionary<string, ISandbox>(StringComparer.Ordinal) { ["client"] = _sandbox.Object },
        defaultRepo: "client", _serializer, ContextGates.Build(), ContextGates.Writer(),
        ContextGates.DerivationStamp(), ContextGates.ImageReview());
}
