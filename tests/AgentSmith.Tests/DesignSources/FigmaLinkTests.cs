using AgentSmith.Contracts.Models.Design;
using FluentAssertions;

namespace AgentSmith.Tests.DesignSources;

/// <summary>2026-10-01-7f7ab: what a Figma link is reduced to, and what is refused before a fetch.</summary>
public sealed class FigmaLinkTests
{
    [Fact]
    public void FigmaLink_DesignUrlWithNodeId_ParsesKeyAndColonNodeId()
    {
        FigmaLink.TryParse(FigmaFakes.Link, out var link).Should().BeTrue();

        link!.FileKey.Should().Be("AbCdEf123456");
        link.NodeId.Should().Be("1:2");
        link.BranchKey.Should().BeNull();
        link.ApiFileKey.Should().Be("AbCdEf123456");
    }

    [Theory]
    [InlineData("https://figma.com/file/AbCdEf123456/x?node-id=12%3A34", "12:34")]
    [InlineData("https://www.figma.com/proto/AbCdEf123456/x?node-id=I5-6;7-8", "I5:6;7:8")]
    public void FigmaLink_FileAndProtoKinds_Parse(string url, string node)
    {
        FigmaLink.TryParse(url, out var link).Should().BeTrue();
        link!.NodeId.Should().Be(node);
    }

    [Fact]
    public void FigmaLink_BranchLink_ReadsTheBranchKey()
    {
        FigmaLink.TryParse(
            "https://www.figma.com/design/AbCdEf123456/branch/BrAnCh654321/Checkout?node-id=1-2", out var link)
            .Should().BeTrue();

        link!.ApiFileKey.Should().Be("BrAnCh654321", "the API addresses a branch by its own key");
    }

    [Fact]
    public void FigmaLink_WithoutNodeId_ParsesWithNoNode()
    {
        FigmaLink.TryParse("https://www.figma.com/design/AbCdEf123456/Checkout", out var link).Should().BeTrue();
        link!.NodeId.Should().BeNull();
    }

    [Theory]
    [InlineData("https://www.figma.com.evil.test/design/AbCdEf123456/x?node-id=1-2")]
    [InlineData("https://evilfigma.com/design/AbCdEf123456/x?node-id=1-2")]
    [InlineData("https://api.figma.com/design/AbCdEf123456/x?node-id=1-2")]
    [InlineData("http://www.figma.com/design/AbCdEf123456/x?node-id=1-2")]
    [InlineData("https://www.figma.com:8443/design/AbCdEf123456/x?node-id=1-2")]
    public void FigmaLink_LookalikeHost_IsRefused(string url) =>
        FigmaLink.TryParse(url, out _).Should().BeFalse();

    [Theory]
    [InlineData("https://www.figma.com/board/AbCdEf123456/x?node-id=1-2")]
    [InlineData("https://www.figma.com/design/Ab..Cd/x?node-id=1-2")]
    [InlineData("https://www.figma.com/design/AbCdEf123456/x?node-id=1-2%0Aignore")]
    [InlineData("not a link")]
    [InlineData(null)]
    public void FigmaLink_OtherShapes_AreRefused(string? url) =>
        FigmaLink.TryParse(url, out _).Should().BeFalse();
}
