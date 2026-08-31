using System.Text.Json;
using AgentSmith.Infrastructure.Services;
using AgentSmith.Tests.Architecture;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;

namespace AgentSmith.Tests.Services;

/// <summary>
/// 2026-08-31-77a8: <c>meta.domain</c> is retired the way <c>meta.project</c> and
/// <c>meta.version</c> were — the schema still ACCEPTS the key, because the root is
/// <c>additionalProperties: false</c> and deleting the property would refuse every
/// context written before it, and the write path simply does not carry it: the typed
/// meta record has no property to deserialise it into, so it is DROPPED, never rejected.
/// </summary>
public sealed class ContextYamlDomainRetiredTests
{
    private const string OfferedDocument = """
        {
          "meta": { "workdir": ".", "domain": "data-warehouse", "purpose": "Serves the warehouse." },
          "stack": { "lang": "python", "image": "python:3.12-bookworm" }
        }
        """;

    [Fact]
    public void ContextYaml_ADomainKey_IsDroppedNotWritten()
    {
        var gate = ContextGates.Build();

        gate.TryRead(JsonDocument.Parse(OfferedDocument).RootElement, out var typed, out var defect)
            .Should().BeTrue("a model working from an older prompt is not punished: {0}", defect);
        gate.Defect(typed!).Should().BeNull("the key is dropped, so nothing is left to refuse");

        ContextGates.Serializer().Serialize(typed!).Should().NotContain("domain",
            "the file simply does not carry the key any more");
    }

    [Fact]
    public void ContextSchema_ADomainKey_IsStillAccepted() =>
        ContextSchemaFile.Validate("""
            meta:
              workdir: "."
              domain: "data-warehouse"
            stack:
              lang: "python"
              image: "python:3.12-bookworm"
            """).Should().BeEmpty(
            "deprecated means no longer written — never refused, or every context "
            + "written before this phase would fail on its first run");

    [Fact]
    public void ContextYaml_AFileThatStillCarriesADomain_StillLoads()
    {
        var summary = new ContextYamlSerializer(new ContextYamlBuilders()).Parse("""
            meta:
              workdir: "warehouse"
              domain: "data-warehouse"
            stack:
              lang: "python"
              image: "python:3.12-bookworm"
            """).Summary;

        summary.Should().NotBeNull("an unmatched key is ignored by the read shape, not fatal");
        summary!.Workdir.Should().Be("warehouse");
        summary.Image.Should().Be("python:3.12-bookworm");
    }
}
