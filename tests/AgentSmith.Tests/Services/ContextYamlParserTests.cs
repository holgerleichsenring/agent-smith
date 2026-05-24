using AgentSmith.Infrastructure.Services;
using FluentAssertions;

namespace AgentSmith.Tests.Services;

/// <summary>
/// p0161: workdir is REQUIRED on every context.yaml. Missing workdir is a
/// config error, never a silent default.
/// </summary>
public sealed class ContextYamlParserTests
{
    private readonly ContextYamlParser _sut = new();

    [Fact]
    public void TryParse_MissingWorkdir_ThrowsConfigError()
    {
        var yaml = """
            meta:
              project: example
            stack:
              lang: csharp
            """;

        var act = () => _sut.TryParse(yaml);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*meta.workdir*");
    }

    [Fact]
    public void TryParse_ValidYaml_ReturnsSummary()
    {
        var yaml = """
            meta:
              project: example
              workdir: src/Server
            stack:
              lang: csharp
            """;

        var result = _sut.TryParse(yaml);

        result!.Workdir.Should().Be("src/Server");
        result.Language.Should().Be("csharp");
    }

    [Fact]
    public void TryParse_WorkdirDotForSingleStack_ReturnsDot()
    {
        var yaml = """
            meta:
              project: example
              workdir: "."
            stack:
              lang: csharp
            """;

        var result = _sut.TryParse(yaml);

        result!.Workdir.Should().Be(".");
    }

    [Fact]
    public void TryParse_MissingStackLang_ReturnsSummaryWithNullLanguage()
    {
        var yaml = """
            meta:
              project: example
              workdir: "."
            """;

        var result = _sut.TryParse(yaml);

        result!.Workdir.Should().Be(".");
        result.Language.Should().BeNull();
    }

    [Fact]
    public void TryParse_Empty_ReturnsNull()
    {
        _sut.TryParse("").Should().BeNull();
        _sut.TryParse("   ").Should().BeNull();
    }

    [Fact]
    public void TryParse_Malformed_ReturnsNull()
    {
        _sut.TryParse("not: valid: yaml: at: all: :::").Should().BeNull();
    }
}
