using AgentSmith.Application.Services;
using FluentAssertions;

namespace AgentSmith.Tests.Security;

/// <summary>
/// p0192: scanner pattern coverage. Each test pins one of the patterns
/// listed in the spec — a regression in any of them lets a credential
/// slip through CommitAndPRHandler's gate.
/// </summary>
public sealed class SecretPatternScannerTests
{
    private readonly SecretPatternScanner _sut = new();

    [Fact]
    public void Scan_DetectsNuGetClearTextPassword()
    {
        var content = """
            <packageSourceCredentials>
              <Source>
                <add key="Username" value="any" />
                <add key="ClearTextPassword" value="xyzAZTOKEN12345678" />
              </Source>
            </packageSourceCredentials>
            """;
        var matches = _sut.Scan("NuGet.Config", content);

        matches.Should().NotBeEmpty();
        matches[0].Path.Should().Be("NuGet.Config");
    }

    [Fact]
    public void Scan_DetectsNpmAuthToken()
    {
        var content = "//pkgs.dev.azure.com/AcmeOrg/_packaging/feed/npm/registry/:_authToken=npm_abc123XYZ987654321";

        var matches = _sut.Scan(".npmrc", content);

        matches.Should().NotBeEmpty();
    }

    [Fact]
    public void Scan_DetectsCommonPatTokens()
    {
        var content = """
            github=ghp_abcdefghijklmnop1234
            anthropic=sk-ant-oat01_aaaaaaaa
            openai=sk-proj_abcdefghijklmnop1234
            gitlab=glpat-aaaaaaaaaaaaaaaaaaaaaaaa
            """;

        var matches = _sut.Scan("env.txt", content);

        matches.Should().HaveCountGreaterThanOrEqualTo(4);
    }

    [Fact]
    public void Scan_DetectsJwtShape()
    {
        var content =
            "Authorization: Bearer eyJhbGciOiJIUzI1NiJ9.eyJ1c2VyIjoiYWxpY2UifQ.abc-DEF_123";

        var matches = _sut.Scan("headers.txt", content);

        matches.Should().NotBeEmpty();
    }

    [Fact]
    public void Scan_CleanFile_ReturnsEmpty()
    {
        var content = """
            <PropertyGroup>
              <TargetFramework>net8.0</TargetFramework>
            </PropertyGroup>
            """;

        var matches = _sut.Scan("AnyProject.csproj", content);

        matches.Should().BeEmpty();
    }

    [Fact]
    public void Scan_EmptyContent_ReturnsEmpty()
    {
        _sut.Scan("anything", string.Empty).Should().BeEmpty();
    }
}
