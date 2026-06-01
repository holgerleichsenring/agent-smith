using AgentSmith.Infrastructure.Services;
using FluentAssertions;

namespace AgentSmith.Tests.Services;

/// <summary>
/// p0161: workdir is REQUIRED on every context.yaml. Missing workdir is a
/// config error, never a silent default.
///
/// p0189: scanner / parser errors now surface as
/// ContextYamlParseResult.ErrorReason — not swallowed as null — so the
/// resolver can log line/col and the operator sees WHY (e.g. unquoted
/// '@scope/pkg' at line N col M).
/// </summary>
public sealed class ContextYamlParserTests
{
    private readonly ContextYamlParser _sut = new(new ContextYamlSerializer());

    [Fact]
    public void Parse_MissingWorkdir_ThrowsConfigError()
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
    public void Parse_ValidYaml_ReturnsSummary()
    {
        var yaml = """
            meta:
              project: example
              workdir: src/Server
            stack:
              lang: csharp
            """;

        var result = _sut.Parse(yaml);

        result.Summary!.Workdir.Should().Be("src/Server");
        result.Summary.Language.Should().Be("csharp");
        result.ErrorReason.Should().BeNull();
    }

    [Fact]
    public void Parse_WorkdirDotForSingleStack_ReturnsDot()
    {
        var yaml = """
            meta:
              project: example
              workdir: "."
            stack:
              lang: csharp
            """;

        var result = _sut.Parse(yaml);

        result.Summary!.Workdir.Should().Be(".");
    }

    [Fact]
    public void Parse_MissingStackLang_ReturnsSummaryWithNullLanguage()
    {
        var yaml = """
            meta:
              project: example
              workdir: "."
            """;

        var result = _sut.Parse(yaml);

        result.Summary!.Workdir.Should().Be(".");
        result.Summary.Language.Should().BeNull();
    }

    [Fact]
    public void Parse_Empty_ReturnsEmpty()
    {
        _sut.Parse("").Summary.Should().BeNull();
        _sut.Parse("").ErrorReason.Should().BeNull();
        _sut.Parse("   ").Summary.Should().BeNull();
    }

    [Fact]
    public void Parse_UnquotedNpmScopedPackage_SurfacesScannerError()
    {
        // Real-world repro from a Rhenus Angular client context.yaml:
        // unquoted '@azure/msal-angular' trips the YAML scanner at line 4
        // col 7 and used to silently fall back to the generic image.
        var yaml = """
            meta:
              workdir: "."
            stack:
              sdks:
                - @azure/msal-angular
            """;

        var result = _sut.Parse(yaml);

        result.Summary.Should().BeNull();
        result.ErrorReason.Should().NotBeNull();
        result.ErrorReason.Should().Contain("Line");
        result.ErrorReason.Should().Contain("@");
        result.ErrorReason.Should().Contain("hint:");
    }

    [Fact]
    public void Parse_StructurallyUnmatched_ReturnsEmpty()
    {
        // Document parses but has no recognised top-level keys → no error,
        // no summary. Resolver treats this like an empty context.yaml.
        _sut.Parse("foo: bar").Summary.Should().BeNull();
        _sut.Parse("foo: bar").ErrorReason.Should().BeNull();
    }
}
