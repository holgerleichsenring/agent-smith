using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Infrastructure.Services;
using FluentAssertions;

namespace AgentSmith.Tests.Services;

/// <summary>
/// p0193: Serializer + Parser share one YamlDotNet builder. Round-trip is
/// the central guarantee — what we accept on Serialize, we read back on
/// Parse. Two real-world failure modes (npm '@scope/pkg' in sdks and ': '
/// inside list strings) are pinned because they are the exact bugs the
/// operator hit in the customer's context.yaml files before this phase.
/// </summary>
public sealed class ContextYamlSerializerTests
{
    private readonly ContextYamlSerializer _sut = new();

    [Fact]
    public void Roundtrip_PreservesWorkdirAndLang()
    {
        var doc = new ContextYamlDocument(
            new ContextYamlMeta(Workdir: "src/Server", Project: "Sample", Type: "service"),
            new ContextYamlStack(Lang: "C#", Runtime: ".NET 8"));

        var yaml = _sut.Serialize(doc);
        var parsed = _sut.Parse(yaml);

        parsed.Summary!.Workdir.Should().Be("src/Server");
        parsed.Summary.Language.Should().Be("C#");
        parsed.ErrorReason.Should().BeNull();
    }

    [Fact]
    public void Roundtrip_NpmScopedPackageInSdks_ParsesBackClean()
    {
        // Operator's broken file had unquoted "- @azure/msal-angular" — the
        // typed writer must emit this in a form the YAML scanner accepts.
        var doc = new ContextYamlDocument(
            new ContextYamlMeta(Workdir: "."),
            new ContextYamlStack(
                Lang: "TypeScript",
                Sdks: new[] { "@azure/msal-angular", "@azure/msal-browser", "rxjs" }));

        var yaml = _sut.Serialize(doc);
        var parsed = _sut.Parse(yaml);

        parsed.ErrorReason.Should().BeNull();
        parsed.Summary!.Workdir.Should().Be(".");
        parsed.Summary.Language.Should().Be("TypeScript");
        // Sanity: yaml must contain the scoped name literally (quoted or not).
        yaml.Should().Contain("@azure/msal-angular");
    }

    [Fact]
    public void Roundtrip_StringWithColonInQualityPrinciples_ParsesBackClean()
    {
        // Second broken file: a `principles:` list item like
        // "Angular style: PascalCase for components/services" — the unquoted
        // ": " caused the scanner to think the list item was a mapping.
        var quality = new Dictionary<string, object?>
        {
            ["principles"] = new[]
            {
                "Angular style: PascalCase for components/services; file suffixes: .component.ts/.service.ts",
                "Strict TypeScript (\"strict\": true in tsconfig)",
            },
        };
        var doc = new ContextYamlDocument(
            new ContextYamlMeta(Workdir: "."),
            new ContextYamlStack(Lang: "TypeScript"),
            Quality: quality);

        var yaml = _sut.Serialize(doc);
        var parsed = _sut.Parse(yaml);

        parsed.ErrorReason.Should().BeNull();
        parsed.Summary!.Workdir.Should().Be(".");
    }

    [Fact]
    public void Serialize_MissingWorkdir_Throws()
    {
        var act = () => _sut.Serialize(
            new ContextYamlDocument(new ContextYamlMeta(Workdir: " ")));

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Workdir is required*");
    }

    [Fact]
    public void Parse_LegacyParserContractStillHonoured_MissingWorkdirThrows()
    {
        var yaml = """
            meta:
              project: example
            stack:
              lang: csharp
            """;
        var act = () => _sut.Parse(yaml);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*meta.workdir*");
    }

    [Fact]
    public void Parse_BareUnquotedAtScopedPackage_StillSurfacesScannerError()
    {
        // The defensive read-path still works for legacy / external files
        // that were not written through the typed writer.
        var yaml = """
            meta:
              workdir: "."
            stack:
              sdks:
                - @azure/msal-angular
            """;
        var result = _sut.Parse(yaml);

        result.Summary.Should().BeNull();
        result.ErrorReason.Should().Contain("hint:");
    }
}
