using System.Text.Json;
using System.Text.RegularExpressions;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Infrastructure.Services;
using AgentSmith.Tests.Architecture;
using FluentAssertions;

namespace AgentSmith.Tests.Services;

/// <summary>
/// p0504: <c>meta.domain</c> has to survive five shapes — the deserializer's block class
/// (which drops an undeclared key without a warning), the summary, the discovery, the
/// writer, and the JSON schema whose <c>additionalProperties: false</c> would reject the
/// very file the writer emits. p0265 and p0272 each shipped a field without the schema.
/// </summary>
public sealed class ContextYamlDomainTests
{
    private readonly ContextYamlSerializer _sut = new(new ContextYamlBuilders());

    [Fact]
    public void ContextYaml_MetaDomainRoundTrips_ThroughReaderWriterAndDiscovery()
    {
        var document = new ContextYamlDocument(
            new ContextYamlMeta(Workdir: "warehouse", Project: "Sample", Domain: "sample-domain"),
            new ContextYamlStack(Lang: "python", Image: "python:3.12-bookworm"));

        var yaml = _sut.Serialize(document);
        yaml.Should().Contain("domain: sample-domain");

        var summary = _sut.Parse(yaml).Summary!;
        summary.Domain.Should().Be("sample-domain");

        // The discovery is what reaches the coordinator; a field that stops here is lost.
        var discovery = new AgentSmith.Contracts.Sandbox.RemoteContextDiscovery(
            "warehouse", summary.Workdir, summary.Language, summary.Prerequisites,
            summary.Image, summary.Resources, summary.Purpose, summary.Domain);
        discovery.Domain.Should().Be("sample-domain");
    }

    [Fact]
    public void ContextYaml_NoDomainDeclared_ParsesToNull() =>
        _sut.Parse("meta:\n  workdir: \".\"\n").Summary!.Domain.Should().BeNull();

    [Fact]
    public void ContextSchema_MetaDomain_Validates()
    {
        using var schema = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(ArchitectureSources.AgentSmithRoot, "context.schema.json")));

        var meta = schema.RootElement.GetProperty("properties").GetProperty("meta");
        meta.GetProperty("additionalProperties").GetBoolean().Should().BeFalse(
            "an undeclared meta key is REJECTED, which is why a new field must be declared here");

        var domain = meta.GetProperty("properties").GetProperty("domain");
        domain.GetProperty("type").GetString().Should().Be("string");
        var pattern = domain.GetProperty("pattern").GetString()!;
        Regex.IsMatch("sample-domain", pattern).Should().BeTrue();
        Regex.IsMatch("Sample Domain", pattern).Should().BeFalse("a domain is one lowercase word");
    }
}
