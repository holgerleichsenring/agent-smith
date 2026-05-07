using AgentSmith.Contracts.Models;
using AgentSmith.Infrastructure.Services.Output;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;

namespace AgentSmith.Tests.Output;

public sealed class MarkdownDetailsRenderingTests
{
    [Fact]
    public void BuildMarkdown_DetailsPresent_RendersDescriptionAndDetails()
    {
        var observations = new List<SkillObservation>
        {
            ObservationFactory.Make("HIGH", "src/A.cs", 10, "Headline", "fix it", 80) with
            {
                Details = "Long-form prose body that explains the finding in detail."
            }
        };

        var md = MarkdownOutputStrategy.BuildMarkdown(observations);

        md.Should().Contain("Headline", "description renders as headline");
        md.Should().Contain("Long-form prose body", "details renders as body");
    }

    [Fact]
    public void BuildMarkdown_DetailsNull_RendersDescriptionOnly()
    {
        var observations = new List<SkillObservation>
        {
            ObservationFactory.Make("HIGH", "src/A.cs", 10, "Headline only", "fix it", 80)
        };

        var md = MarkdownOutputStrategy.BuildMarkdown(observations);

        md.Should().Contain("Headline only");
        // No null reference; renders cleanly without Details paragraph
        md.Should().NotContain("null");
    }

    [Fact]
    public void BuildMarkdown_DetailsLong_RendersFullContent()
    {
        var longDetails = string.Join(
            " ",
            Enumerable.Repeat("Sentence describing the security context.", 50));
        var observations = new List<SkillObservation>
        {
            ObservationFactory.Make("HIGH", "src/A.cs", 10, "Headline", "fix it", 80) with
            {
                Details = longDetails
            }
        };

        var md = MarkdownOutputStrategy.BuildMarkdown(observations);

        md.Should().Contain(longDetails, "full Details body renders without truncation in Markdown");
    }
}
