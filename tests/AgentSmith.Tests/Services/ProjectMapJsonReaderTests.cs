using AgentSmith.Application.Services;
using AgentSmith.Domain.Models;
using FluentAssertions;

namespace AgentSmith.Tests.Services;

public sealed class ProjectMapJsonReaderTests
{
    private readonly ProjectMapJsonReader _reader = new();

    [Fact]
    public void TryRead_EmptyText_ReturnsFriendlyError()
    {
        var ok = _reader.TryRead("", out var map, out var error);

        ok.Should().BeFalse();
        map.Should().BeNull();
        error.Should().Contain("empty");
    }

    [Fact]
    public void TryRead_ValidJson_ProducesMap()
    {
        const string json = """
            {
              "primary_language": "csharp",
              "frameworks": ["aspnetcore"],
              "modules": [{"path": "src/A", "role": "production", "depends_on": []}],
              "test_projects": [{"path": "tests/A", "framework": "xunit", "file_count": 12}],
              "entry_points": ["src/A/Program.cs"],
              "conventions": {"naming_pattern": "PascalCase"},
              "ci": {"has_ci": true, "ci_system": "github"}
            }
            """;

        var ok = _reader.TryRead(json, out var map, out _);

        ok.Should().BeTrue();
        map!.PrimaryLanguage.Should().Be("csharp");
        map.Frameworks.Should().ContainSingle().Which.Should().Be("aspnetcore");
        map.Modules.Should().ContainSingle().Which.Role.Should().Be(ModuleRole.Production);
        map.TestProjects.Should().ContainSingle().Which.FileCount.Should().Be(12);
        map.Conventions.NamingPattern.Should().Be("PascalCase");
        map.Ci.HasCi.Should().BeTrue();
        map.Ci.CiSystem.Should().Be("github");
    }

    [Fact]
    public void TryRead_FencedJson_StripsFencesFirst()
    {
        const string json = """
            ```json
            {"primary_language": "go", "frameworks": [], "modules": [], "test_projects": [], "entry_points": [], "conventions": {}, "ci": {}}
            ```
            """;

        var ok = _reader.TryRead(json, out var map, out _);

        ok.Should().BeTrue();
        map!.PrimaryLanguage.Should().Be("go");
    }

    [Fact]
    public void TryRead_TrailingComma_ToleratedByLenientParser()
    {
        const string json = """
            {"primary_language": "rust", "frameworks": ["axum",], "modules": [], "test_projects": [], "entry_points": [], "conventions": {}, "ci": {}}
            """;

        var ok = _reader.TryRead(json, out var map, out _);

        ok.Should().BeTrue();
        map!.PrimaryLanguage.Should().Be("rust");
    }

    [Fact]
    public void TryRead_MissingPrimaryLanguage_DefaultsToUnknown()
    {
        const string json = """
            {"frameworks": [], "modules": [], "test_projects": [], "entry_points": [], "conventions": {}, "ci": {}}
            """;

        var ok = _reader.TryRead(json, out var map, out _);

        ok.Should().BeTrue();
        map!.PrimaryLanguage.Should().Be("unknown");
    }

    [Fact]
    public void TryRead_UnknownModuleRole_FallsBackToOther()
    {
        const string json = """
            {"primary_language": "csharp", "frameworks": [], "modules": [{"path": "x", "role": "something-weird", "depends_on": []}], "test_projects": [], "entry_points": [], "conventions": {}, "ci": {}}
            """;

        var ok = _reader.TryRead(json, out var map, out _);

        ok.Should().BeTrue();
        map!.Modules.Single().Role.Should().Be(ModuleRole.Other);
    }

    [Fact]
    public void TryRead_GarbageInput_ReturnsParseError()
    {
        var ok = _reader.TryRead("not json at all", out var map, out var error);

        ok.Should().BeFalse();
        map.Should().BeNull();
        error.Should().NotBeEmpty();
    }
}
