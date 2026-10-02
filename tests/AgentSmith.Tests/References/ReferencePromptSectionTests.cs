using FluentAssertions;

namespace AgentSmith.Tests.References;

/// <summary>2026-10-01-283dc: the prompt says what a reference address is, and only when there is one.</summary>
public sealed class ReferencePromptSectionTests
{
    [Fact]
    public void ReferencePromptSection_SaysMaterialNotWebsite()
    {
        var section = AgentSmith.Application.Services.Handlers.ReferencePromptSection.Build(
            ["repo-a", "template:server", "reference:site"]);

        section.Should().Contain("## Material the operator uploaded").And.Contain("`reference:site`")
            .And.Contain("an application's source").And.Contain("exact values").And.NotContain("template:server");
    }

    [Fact]
    public void ReferencePromptSection_Build_WithoutAReference_IsEmpty()
    {
        AgentSmith.Application.Services.Handlers.ReferencePromptSection.Build(["repo-a", "template:server"])
            .Should().BeEmpty();
    }
}
