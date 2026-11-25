using System.Text.Json;
using AgentSmith.Contracts.Webhooks;
using AgentSmith.Server.Services.Webhooks;
using FluentAssertions;

namespace AgentSmith.Tests.Webhooks;

/// <summary>2026-11-25-9992g: an issue comment's author in the terms the host's write-access check reads — GitHub by the
/// repository's full name and the login, GitLab by the project's and the user's ids; none when the payload names none.</summary>
public sealed class TicketCommentWriterTests
{
    [Fact]
    public void GitHub_ReadsTheRepositoryAndTheLogin()
    {
        using var doc = JsonDocument.Parse("""
            {"comment":{"user":{"login":"alice"}},"repository":{"full_name":"o/api","html_url":"https://github.com/o/api"}}
            """);

        TicketCommentWriter.GitHub(doc.RootElement).Should().Be(new PrCommentAuthor("https://github.com/o/api", "o/api", "alice", "alice"));
    }

    [Fact]
    public void GitLab_ReadsTheIds()
    {
        using var doc = JsonDocument.Parse("""
            {"user":{"id":7,"username":"bob"},"project":{"id":42,"web_url":"https://gitlab.com/o/api"}}
            """);

        TicketCommentWriter.GitLab(doc.RootElement).Should().Be(new PrCommentAuthor("https://gitlab.com/o/api", "42", "7", "bob"));
    }

    [Fact]
    public void APayloadWithoutAnAuthor_NamesNone()
    {
        using var doc = JsonDocument.Parse("{}");

        TicketCommentWriter.GitHub(doc.RootElement).Should().BeNull();
        TicketCommentWriter.GitLab(doc.RootElement).Should().BeNull();
    }
}
