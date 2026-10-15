using AgentSmith.Infrastructure.Core.Services.Configuration;
using AgentSmith.Tests.Architecture;
using FluentAssertions;

namespace AgentSmith.Tests.Configuration;

/// <summary>
/// 2026-10-15-4a4da: project bindings travel in an export and back in through an import, and
/// the exported block is one the shipped schema accepts.
/// </summary>
public sealed class RoleMappingYamlTests
{
    private const string Yaml = """
        role_mapping:
          role_claim: roles
          project_bindings:
            - project: billing
              groups:
                - billing-team
              people:
                - claim: preferred_username
                  value: ada
              roles:
                - operator
        """;

    [Fact]
    public void RoleMapping_ProjectBindings_RoundTripThroughTheYamlExportAndImport()
    {
        var yaml = new RawConfigYaml();

        var back = yaml.Deserialize(yaml.Serialize(yaml.Deserialize(Yaml))).RoleMapping.ProjectBindings;

        var binding = back.Should().ContainSingle().Subject;
        binding.Project.Should().Be("billing");
        binding.Groups.Should().Equal("billing-team");
        binding.People.Should().ContainSingle().Which.Value.Should().Be("ada");
        binding.People[0].Claim.Should().Be("preferred_username");
        binding.Roles.Should().Equal("operator");
    }

    [Fact]
    public void RoleMapping_ExportWithProjectBindings_ValidatesAgainstTheShippedSchema()
    {
        var yaml = new RawConfigYaml();

        var exported = yaml.Serialize(yaml.Deserialize(Yaml));

        exported.Should().Contain("project_bindings");
        ConfigSchemaFile.Validate(exported).Should().BeEmpty(exported);
    }
}
