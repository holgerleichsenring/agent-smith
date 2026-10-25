using AgentSmith.Contracts.Constants;
using AgentSmith.Infrastructure.Services.Sandbox;
using FluentAssertions;
using YamlDotNet.RepresentationModel;

namespace AgentSmith.Tests.Architecture;

/// <summary>
/// 2026-10-25-c5a7h: every variable the server deployment feeds from a Secret is either stripped
/// from an in-process step or named here as not a credential, so the list cannot drift again.
/// </summary>
public sealed class ServerSecretEnvNamesTests
{
    private static readonly string[] NotACredential =
    [
        "AGENTSMITH_AUTH_AUTHORITY", "AGENTSMITH_AUTH_AUDIENCE", "AGENTSMITH_AUTH_ENFORCE",
        "AGENTSMITH_AUTH_ROLE_CLAIM", "AGENTSMITH_AUTH_GROUP_CLAIM", "AGENTSMITH_AUTH_NAME_CLAIM",
        "AGENTSMITH_PERSISTENCE_PROVIDER", "AGENTSMITH_ADMIN_GRANT",
    ];

    [Fact]
    public void ServerSecretEnvNames_EverySecretFedManifestVariable_IsDeclaredOrNamedNotACredential()
    {
        var declared = ServerSecretEnvNames.All.Concat(AgentSecretBinding.All.Select(b => b.EnvVar))
            .Concat(NotACredential).ToHashSet(StringComparer.Ordinal);

        var fed = SecretFedVariables(Path.Combine(ArchitectureSources.RepositoryRoot, "deploy", "k8s", "8-deployment-server.yaml"));

        fed.Should().NotBeEmpty();
        fed.Where(name => !declared.Contains(name)).Should().BeEmpty(
            "a secret the server deployment injects must be stripped from in-process steps (ServerSecretEnvNames) "
            + "or named here as not a credential");
    }

    private static List<string> SecretFedVariables(string manifest)
    {
        var stream = new YamlStream();
        stream.Load(new StringReader(File.ReadAllText(manifest)));
        return [.. stream.Documents.SelectMany(d => Nodes(d.RootNode)).OfType<YamlMappingNode>()
            .Where(m => m.Children.ContainsKey("name") && m.Children.TryGetValue("valueFrom", out var from)
                        && from is YamlMappingNode f && f.Children.ContainsKey("secretKeyRef"))
            .Select(m => ((YamlScalarNode)m.Children["name"]).Value!)];
    }

    private static IEnumerable<YamlNode> Nodes(YamlNode node) => node switch
    {
        YamlMappingNode map => map.Children.Values.SelectMany(Nodes).Prepend(map),
        YamlSequenceNode seq => seq.Children.SelectMany(Nodes).Prepend(seq),
        _ => [node],
    };
}
