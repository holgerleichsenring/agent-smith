using System.Text.RegularExpressions;
using AgentSmith.Infrastructure.Services.Providers.Source;
using AgentSmith.Tests.Architecture;
using FluentAssertions;

namespace AgentSmith.Tests.Infrastructure;

/// <summary>
/// Regression: Azure DevOps rejects a PR whose description exceeds 4000
/// characters. A multi-repo run record easily exceeds it; the description is
/// truncated with a marker so the PR still opens.
/// </summary>
public sealed class AzureReposPrDescriptionTests
{
    // p0477: the limit was known and the UPDATE path did not apply it. Creating a pull
    // request truncated; updating its body sent the description raw, and a live run that
    // REUSED pull requests opened earlier took only that path — four rejections across two
    // repositories, every one "Invalid argument value", both pull requests left with no
    // description. A rule rather than a case, because the next writer forgets the same way.
    [Fact]
    public void AzureRepos_EveryDescriptionSent_GoesThroughTruncateDescription()
    {
        var source = File.ReadAllText(Path.Combine(
            ArchitectureSources.BackendRoot,
            "AgentSmith.Infrastructure", "Services", "Providers", "Source",
            "AzureReposSourceProvider.cs"));

        var assignments = Regex.Matches(source, @"Description\s*=\s*([^,}\r\n]+)")
            .Select(m => m.Groups[1].Value.Trim())
            .Where(v => !v.StartsWith("TruncateDescription", StringComparison.Ordinal))
            .ToList();

        assignments.Should().BeEmpty(
            "Azure DevOps refuses an over-long description whole and names no field, so every "
            + "path that sends one truncates it — not only the one that opens the pull request");
    }

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
