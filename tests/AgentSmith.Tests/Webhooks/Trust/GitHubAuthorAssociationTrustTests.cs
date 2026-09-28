using AgentSmith.Contracts.Webhooks;
using AgentSmith.Infrastructure.Services.Webhooks;
using FluentAssertions;

namespace AgentSmith.Tests.Webhooks.Trust;

public sealed class GitHubAuthorAssociationTrustTests
{
    [Theory]
    [InlineData("OWNER", true)]
    [InlineData("member", true)]
    [InlineData("COLLABORATOR", true)]
    [InlineData("CONTRIBUTOR", false)]
    [InlineData("FIRST_TIMER", false)]
    [InlineData("NONE", false)]
    [InlineData(null, false)]
    public async Task IsTrustedAsync_OnlyAssociationsThatCanWrite(string? association, bool trusted)
    {
        var author = new PrCommentAuthor("https://github.com/o/r", "o/r", "dev", "dev") { Association = association };

        (await new GitHubAuthorAssociationTrust().IsTrustedAsync(author, CancellationToken.None))
            .Should().Be(trusted);
    }
}
