using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Infrastructure.Core.Services.Configuration;
using FluentAssertions;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace AgentSmith.Tests.Configuration;

/// <summary>
/// Pins the YAML→RepoConnection round-trip for the catalog's
/// <c>default_branch:</c> field on every supported repo type.
///
/// Regression context: operators set <c>default_branch: develop</c> on an
/// <c>azure_devops</c> entry and the discovery still hit <c>main</c> because
/// AzureReposSourceConnection didn't carry the field. The provider was fixed
/// (AzureReposSourceProviderConfiguredBranchE2eTests pins the SDK call); these
/// tests pin the layer above it — that YAML parsing + catalog assembly
/// actually surface the field for the provider to consume in the first place.
/// If <c>RepoCatalogBuilder</c> or <c>RawRepoEntry</c> ever drops the field
/// (deliberate or accidental refactor), this fails before any pipeline runs.
/// </summary>
public sealed class RepoCatalogDefaultBranchTests
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    [Theory]
    [InlineData("azure_devops", "https://dev.azure.com/Org/Project/_git/Repo")]
    [InlineData("github", "https://github.com/owner/repo")]
    [InlineData("gitlab", "https://gitlab.com/owner/repo")]
    public void DefaultBranchOnYamlEntry_FlowsThroughToRepoConnection(string typeYaml, string url)
    {
        var yaml = $$"""
            sample-server:
              type: {{typeYaml}}
              url: {{url}}
              auth: token
              default_branch: develop
            """;

        var raw = Deserializer.Deserialize<Dictionary<string, RawRepoEntry>>(yaml);
        var catalog = new RepoCatalogBuilder().Build(raw, new List<string>());

        catalog.Should().ContainKey("sample-server");
        catalog["sample-server"].DefaultBranch.Should().Be("develop");
    }

    [Fact]
    public void AbsentDefaultBranch_LandsAsNull_NotEmptyString()
    {
        var yaml = """
            repo-without-override:
              type: azure_devops
              url: https://dev.azure.com/Org/Project/_git/Repo
              auth: token
            """;

        var raw = Deserializer.Deserialize<Dictionary<string, RawRepoEntry>>(yaml);
        var catalog = new RepoCatalogBuilder().Build(raw, new List<string>());

        catalog["repo-without-override"].DefaultBranch.Should().BeNull();
    }

    [Fact]
    public void MultipleReposInCatalog_EachKeepsItsOwnDefaultBranch()
    {
        // The exact catalog-shape symptom from the operator's Sample
        // failure: multi-context monorepo on develop, single-context repo
        // on develop, single-context repo on main — all azure_devops.
        var yaml = """
            sample-server:
              type: azure_devops
              url: https://dev.azure.com/Org/Project/_git/Sample.Server
              auth: token
              default_branch: develop
            sample-docs:
              type: azure_devops
              url: https://dev.azure.com/Org/Project/_git/Sample.Docs
              auth: token
              default_branch: main
            sample-client:
              type: azure_devops
              url: https://dev.azure.com/Org/Project/_git/Sample.Client
              auth: token
              default_branch: develop
            """;

        var raw = Deserializer.Deserialize<Dictionary<string, RawRepoEntry>>(yaml);
        var catalog = new RepoCatalogBuilder().Build(raw, new List<string>());

        catalog["sample-server"].DefaultBranch.Should().Be("develop");
        catalog["sample-docs"].DefaultBranch.Should().Be("main");
        catalog["sample-client"].DefaultBranch.Should().Be("develop");
    }
}
