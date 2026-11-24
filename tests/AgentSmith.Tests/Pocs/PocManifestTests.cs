using AgentSmith.Application.Services.Pocs;
using FluentAssertions;

namespace AgentSmith.Tests.Pocs;

/// <summary>2026-11-23-933db: a revision's manifest is stored as JSON and read back unchanged, paths in ordinal order.</summary>
public sealed class PocManifestTests
{
    [Fact]
    public void ToJson_FromJson_RoundTripsInOrdinalOrder()
    {
        var manifest = new PocManifest(new Dictionary<string, PocManifestEntry>
        {
            ["src/b.py"] = new(new string('b', 64), 4000000000u, 12),
            ["Dockerfile"] = new(new string('d', 64), 7u, 40),
        });

        var json = manifest.ToJson();
        var back = PocManifest.FromJson(json);

        json.Should().StartWith("{\"Dockerfile\":{\"sha256\":\"" + new string('d', 64) + "\",\"crc32\":7,\"size\":40}");
        back.Files.Should().Equal(manifest.Files);
    }

    [Fact]
    public void FromJson_Null_Throws()
    {
        var read = () => PocManifest.FromJson("null");

        read.Should().Throw<System.Text.Json.JsonException>();
    }
}
