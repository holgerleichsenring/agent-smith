using AgentSmith.Infrastructure.Core.Services.Configuration;
using AgentSmith.Tests.Architecture;
using FluentAssertions;

namespace AgentSmith.Tests.Configuration;

/// <summary>
/// The shipped example is where an operator starts, and an export is what they keep: both
/// must pass the schema their editor validates them against. The example failed its own
/// schema, and an export emitted blocks the schema rejected.
/// </summary>
public sealed class ExampleConfigSchemaTests
{
    [Fact]
    public void ExampleConfig_ValidatesAgainstSchema() =>
        ConfigSchemaFile.ValidateFile(ConfigSchemaFile.ExamplePath).Should().BeEmpty();

    [Fact]
    public void ExportedConfig_ValidatesAgainstSchema()
    {
        var yaml = new RawConfigYaml();
        var exported = yaml.Serialize(yaml.Deserialize(File.ReadAllText(ConfigSchemaFile.ExamplePath)));

        ConfigSchemaFile.Validate(exported).Should().BeEmpty(exported);
    }

    [Fact]
    public void Schema_AMisspeltKey_IsReported() =>
        ConfigSchemaFile.Validate("""
            agents:
              a:
                type: openai
                modle: gpt-5
            """).Should().NotBeEmpty("the loader ignores it, so the schema is the only place it is caught");
}
