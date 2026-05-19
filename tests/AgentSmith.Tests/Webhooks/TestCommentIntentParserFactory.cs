using AgentSmith.Application.Webhooks;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using Moq;

namespace AgentSmith.Tests.Webhooks;

/// <summary>
/// Builds a <see cref="CommentIntentParser"/> wired up with a stub IIntentParser
/// that resolves the post-slash body deterministically by inspecting common
/// keywords. Used by webhook-handler tests that do not actually want to exercise
/// the LLM call, only the surrounding plumbing (trust gate, payload shape, dialogue
/// routing).
/// </summary>
internal static class TestCommentIntentParserFactory
{
    public static CommentIntentParser Create()
    {
        var mock = new Mock<IIntentParser>();
        mock.Setup(p => p.ParseToPipelineRequestAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string input, string _, CancellationToken _) =>
                StubResolve(input));
        return new CommentIntentParser(mock.Object);
    }

    public static ServerContext Context => new("config.yml");

    private static PipelineRequest StubResolve(string body)
    {
        var lower = body.ToLowerInvariant();
        var pipeline =
            lower.Contains("security") ? "security-scan"
            : lower.Contains("unknown-cmd") ? "unknown-cmd"
            : "fix-bug";

        // Mirror the old PipelineAliases reach so the existing handler tests
        // (which feed "/agent-smith fix", "/agent-smith fix #123 in my-api",
        // "/agent-smith security-scan") keep their pipeline expectations.
        TicketId? ticket = null;
        var hashIdx = body.IndexOf('#');
        if (hashIdx >= 0)
        {
            var rest = body[(hashIdx + 1)..];
            var num = new string(rest.TakeWhile(char.IsDigit).ToArray());
            if (num.Length > 0) ticket = new TicketId(num);
        }

        return new PipelineRequest("test-project", pipeline, TicketId: ticket, Headless: true);
    }
}
