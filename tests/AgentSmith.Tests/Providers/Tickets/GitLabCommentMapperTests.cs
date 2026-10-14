using System.Text.Json;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Services.Providers.Tickets;
using FluentAssertions;

namespace AgentSmith.Tests.Providers.Tickets;

/// <summary>A GitLab note body is GitLab-flavoured markdown and is marked so.</summary>
public sealed class GitLabCommentMapperTests
{
    [Fact]
    public void MapMany_MarksMarkdown()
    {
        var notes = JsonDocument.Parse("""[{ "id": 7, "body": "**bold**", "created_at": "2026-07-01T10:15:00Z" }]""").RootElement;

        new GitLabCommentMapper().MapMany(notes).Single().Format.Should().Be(TicketTextFormat.Markdown);
    }
}
