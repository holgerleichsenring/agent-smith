using AgentSmith.Application.Prompts;
using AgentSmith.Application.Services.BaseRefresh;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Sandbox;

/// <summary>2026-11-24-9dc0b: the base-merge master resolves and stages by name; the server builds and tests the
/// committed merge and hands a red stage back; a generated file is taken and regenerated, or both sides merged.</summary>
public sealed class BaseMergeMasterPromptTests
{
    private static readonly string Body = new EmbeddedPromptCatalog(
        new EnvDirectoryPromptOverrideSource(NullLogger<EnvDirectoryPromptOverrideSource>.Instance),
        NullLogger<EmbeddedPromptCatalog>.Instance).Get(BaseMergeMaster.MasterName);

    [Fact]
    public void Body_SaysTheServerRunsTheDeclaredStages() =>
        Body.Should().Contain("the server builds and tests").And.Contain("declared stages").And.Contain("hands a red stage back");

    [Fact]
    public void Body_AsksForStagingByName() =>
        Body.Should().Contain("`git add <path>`").And.Contain("`git rm <path>`");

    [Fact]
    public void Body_SaysHowAGeneratedFileIsResolved() =>
        Body.Should().Contain("generated file").And.Contain("regenerating it with the repository's own tool")
            .And.Contain("merging both sides' additions");
}
