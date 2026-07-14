using AgentSmith.Infrastructure.Services.Providers.Source;
using FluentAssertions;

namespace AgentSmith.Tests.Infrastructure;

/// <summary>
/// Regression: Azure DevOps rejects a PR whose description exceeds 4000
/// characters. A multi-repo run record easily exceeds it; the description is
/// truncated with a marker so the PR still opens.
/// </summary>
public sealed class AzureReposPrDescriptionTests
{
    [Fact]
    public void TruncateDescription_OverLimit_TruncatesToLimitWithMarker()
    {
        var result = AzureReposSourceProvider.TruncateDescription(new string('a', 5000));

        result.Length.Should().BeLessThanOrEqualTo(AzureReposSourceProvider.MaxDescriptionChars);
        result.Should().EndWith("full record)");
    }

    [Fact]
    public void TruncateDescription_UnderLimit_ReturnsUnchanged()
    {
        AzureReposSourceProvider.TruncateDescription("short body").Should().Be("short body");
    }

    [Fact]
    public void TruncateDescription_Null_ReturnsEmpty()
    {
        AzureReposSourceProvider.TruncateDescription(null).Should().BeEmpty();
    }
}
