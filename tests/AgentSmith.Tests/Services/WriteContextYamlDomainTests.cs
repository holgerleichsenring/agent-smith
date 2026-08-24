using System.Text.Json;
using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Sandbox;
using FluentAssertions;

namespace AgentSmith.Tests.Services;

/// <summary>
/// p0504: stack.image is mandatory whenever a stack is described — UNLESS the context
/// declares a domain, whose profile supplies one. Without that relaxation a data
/// repository declaring a domain would be FORCED to name an image, and the profile's
/// image would be unreachable for every repository agent-smith initialised itself.
/// </summary>
public sealed class WriteContextYamlDomainTests
{
    private const string ImageRequired = "stack.image is required";

    private static WriteContextYamlToolHost Host() =>
        new(
            new Dictionary<string, ISandbox>(),
            defaultRepo: "repo",
            serializer: new AgentSmith.Infrastructure.Services.ContextYamlSerializer(
                new AgentSmith.Infrastructure.Services.ContextYamlBuilders()));

    private static JsonElement Doc(string json) => JsonDocument.Parse(json).RootElement;

    [Fact]
    public async Task ContextYaml_DomainDeclaredWithoutAStackImage_IsAccepted()
    {
        var result = await Host().WriteContextYaml(
            "repo", "warehouse",
            Doc("""{"meta":{"workdir":".","domain":"sample-domain"},"stack":{"lang":"python"}}"""));

        result.Should().NotContain(ImageRequired);
        // No sandbox is registered, so the write fails at the sandbox lookup — AFTER the
        // image rule, which is the rule under test.
        result.Should().Contain("unknown repo");
    }

    [Fact]
    public async Task ContextYaml_StackBlockWithoutImageAndWithoutDomain_IsStillRejected()
    {
        var result = await Host().WriteContextYaml(
            "repo", "warehouse", Doc("""{"meta":{"workdir":"."},"stack":{"lang":"python"}}"""));

        result.Should().Contain(ImageRequired);
    }
}
