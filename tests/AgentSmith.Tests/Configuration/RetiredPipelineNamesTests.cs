using AgentSmith.Application.Services.Configuration;
using AgentSmith.Contracts.Commands;
using FluentAssertions;

namespace AgentSmith.Tests.Configuration;

public sealed class RetiredPipelineNamesTests
{
    [Fact]
    public void Explain_RenamedName_NamesTheReplacement()
    {
        RetiredPipelineNames.Explain("fix-bug").Should().Be(
            $"'fix-bug' was retired into '{PipelinePresets.CodeName}'. Write '{PipelinePresets.CodeName}' instead.");
    }

    [Fact]
    public void Explain_RemovedName_GivesTheReason()
    {
        RetiredPipelineNames.Explain("autonomous").Should().Be(RetiredPipelineNames.Removed["autonomous"]);
    }

    [Fact]
    public void Explain_NameThatWasNeverRetired_IsNull()
    {
        RetiredPipelineNames.Explain("nonesuch").Should().BeNull();
    }

    [Fact]
    public void Refusal_RetiredName_CarriesTheExplanation()
    {
        RetiredPipelineNames.Refusal("skill-manager").Should()
            .Contain("'skill-manager'").And.Contain(RetiredPipelineNames.Removed["skill-manager"]);
    }

    [Fact]
    public void Refusal_UnknownName_OffersThePresets()
    {
        RetiredPipelineNames.Refusal("nonesuch").Should()
            .Contain("'nonesuch'").And.Contain(PipelinePresets.CodeName);
    }

    [Fact]
    public void Lists_NoNameIsBothRenamedAndRemoved()
    {
        RetiredPipelineNames.Replacements.Keys.Should().NotIntersectWith(RetiredPipelineNames.Removed.Keys);
    }
}
