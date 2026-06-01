using System.Text.Json;
using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Models.Configuration;
using FluentAssertions;

namespace AgentSmith.Tests.Tools;

/// <summary>
/// p0191: get_artifact_credentials tool behaviour. Five named scenarios from
/// the spec pin:
///   - all-credentials returned when only one registry is configured,
///   - dot-boundary subdomain match (positive + negative — the negative is
///     security-critical so evil-dev.azure.com cannot steal a
///     dev.azure.com token),
///   - URL-shaped host_filter is reduced to hostname,
///   - multi-registry without host_filter returns an explicit error JSON.
/// </summary>
public sealed class GetArtifactCredentialsToolHostTests
{
    private const string AzureHost = "pkgs.dev.azure.com";
    private const string JfrogHost = "my-company.jfrog.io";

    [Fact]
    public async Task GetArtifactCredentials_ReturnsAllConfiguredRegistries_WhenSingleConfigured()
    {
        var sut = BuildHost(new RegistryConfig(AzureHost, "any", "azure-pat"));

        var json = await sut.GetArtifactCredentials(host_filter: null);

        var parsed = JsonSerializer.Deserialize<List<RegistryDto>>(json)!;
        parsed.Should().HaveCount(1);
        parsed[0].host.Should().Be(AzureHost);
        parsed[0].token.Should().Be("azure-pat");
    }

    [Fact]
    public async Task GetArtifactCredentials_HostFilterDotBoundaryMatch()
    {
        var sut = BuildHost(new RegistryConfig(AzureHost, "any", "azure-pat"));

        var json = await sut.GetArtifactCredentials(host_filter: "dev.azure.com");

        JsonSerializer.Deserialize<List<RegistryDto>>(json)!
            .Should().ContainSingle().Which.host.Should().Be(AzureHost);
    }

    [Fact]
    public async Task GetArtifactCredentials_DoesNotMatchPartialLabel()
    {
        // SECURITY: "dev.azure.com" must NOT match "evil-dev.azure.com".
        // String-suffix match would let a manipulated feed URL exfiltrate
        // the real Azure DevOps token.
        var sut = BuildHost(new RegistryConfig("evil-dev.azure.com", "x", "stolen?"));

        var json = await sut.GetArtifactCredentials(host_filter: "dev.azure.com");

        JsonSerializer.Deserialize<List<RegistryDto>>(json)!.Should().BeEmpty();
    }

    [Fact]
    public async Task GetArtifactCredentials_UrlInput_ReducesToHostname()
    {
        var sut = BuildHost(new RegistryConfig(AzureHost, "any", "azure-pat"));

        var json = await sut.GetArtifactCredentials(
            host_filter: "https://pkgs.dev.azure.com/Org/_packaging/Feed/nuget/v3/index.json");

        JsonSerializer.Deserialize<List<RegistryDto>>(json)!
            .Should().ContainSingle().Which.host.Should().Be(AzureHost);
    }

    [Fact]
    public async Task GetArtifactCredentials_MultipleRegistries_NoFilter_ReturnsError()
    {
        var sut = BuildHost(
            new RegistryConfig(AzureHost, "any", "azure-pat"),
            new RegistryConfig(JfrogHost, "ci", "jfrog-pat"));

        var json = await sut.GetArtifactCredentials(host_filter: null);

        var doc = JsonDocument.Parse(json).RootElement;
        doc.GetProperty("error").GetString().Should().Contain("host_filter required");
        doc.GetProperty("registries_configured").GetInt32().Should().Be(2);
    }

    [Fact]
    public async Task GetArtifactCredentials_NoRegistriesConfigured_ReturnsEmpty()
    {
        var sut = new GetArtifactCredentialsToolHost(Array.Empty<RegistryConfig>());

        var json = await sut.GetArtifactCredentials(host_filter: "anywhere");

        JsonSerializer.Deserialize<List<RegistryDto>>(json)!.Should().BeEmpty();
    }

    private static GetArtifactCredentialsToolHost BuildHost(params RegistryConfig[] registries) =>
        new(registries);

    private sealed record RegistryDto(string host, string username, string token);
}
