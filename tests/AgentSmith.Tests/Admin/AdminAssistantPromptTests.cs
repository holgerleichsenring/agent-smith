using AgentSmith.Application.Prompts;
using AgentSmith.Contracts.Commands;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Admin;

/// <summary>2026-11-17-f3a8a: the admin assistant's master is embedded in the server and owned there.</summary>
public sealed class AdminAssistantPromptTests
{
    [Fact]
    public void Get_AdminAssistantMaster_IsTheEmbeddedBodyWithoutFrontmatter()
    {
        var catalog = new EmbeddedPromptCatalog(new EnvDirectoryPromptOverrideSource(NullLogger<EnvDirectoryPromptOverrideSource>.Instance), NullLogger<EmbeddedPromptCatalog>.Instance);

        var body = catalog.Get(PipelinePresets.AdminAssistantMaster);

        body.Should().StartWith("You are Agent Smith's admin assistant");
        PromptOwnership.EmbeddedOwnedNames.Should().Contain(PipelinePresets.AdminAssistantMaster);
    }
}
