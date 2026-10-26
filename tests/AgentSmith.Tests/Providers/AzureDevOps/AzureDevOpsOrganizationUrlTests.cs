using AgentSmith.Domain.Models;
using FluentAssertions;

namespace AgentSmith.Tests.Providers.AzureDevOps;

/// <summary>
/// 2026-10-26-cb25b: the characterisation matrix of the five organization-URL compositions, run against their one
/// home. "was" names what the copies made before — tracker = TrackerConnections (url ignored, organization raw),
/// tracker-check = AzureDevOpsTrackerDraftCheck (url ignored, escaped), connection = the connection draft check
/// (host, escaped), discovery and builder (host, raw) — so each changed outcome is read off its row.
/// </summary>
public sealed class AzureDevOpsOrganizationUrlTests
{
    [Theory]
    [InlineData(null, "org", "https://dev.azure.com/org", "all five agreed")]
    [InlineData("https://dev.azure.com", "org", "https://dev.azure.com/org", "all five agreed")]
    [InlineData("https://dev.azure.com/", "org", "https://dev.azure.com/org", "all five agreed")]
    [InlineData("https://dev.azure.com/org/project", "org", "https://dev.azure.com/org", "trackers ignored the url; connections made .../org/project/org")]
    [InlineData("https://org.visualstudio.com", "org", "https://dev.azure.com/org", "trackers ignored the url; connections made https://org.visualstudio.com/org")]
    [InlineData("https://tfs.example.com:8080/tfs", "Coll", "https://tfs.example.com:8080/tfs/Coll", "trackers made https://dev.azure.com/Coll; connections agreed")]
    [InlineData("https://tfs.example.com/tfs/", "Coll", "https://tfs.example.com/tfs/Coll", "trackers made https://dev.azure.com/Coll; connections agreed")]
    [InlineData("https://tfs.example.com/tfs/Coll", "Coll", "https://tfs.example.com/tfs/Coll", "trackers made https://dev.azure.com/Coll; connections made .../Coll/Coll")]
    [InlineData("https://tfs.example.com/tfs/My%20Coll", "My Coll", "https://tfs.example.com/tfs/My%20Coll", "trackers made the cloud; connections appended the collection again")]
    [InlineData(null, "My Coll", "https://dev.azure.com/My%20Coll", "tracker, discovery, builder raw; tracker-check, connection escaped")]
    public void For_TheMatrix(string? baseUrl, string organization, string expected, string was) =>
        AzureDevOpsOrganizationUrl.For(baseUrl, organization).Should().Be(expected, was);
}
