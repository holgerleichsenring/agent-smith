using System.Text.Json.Nodes;
using AgentSmith.Server.Extensions;
using FluentAssertions;

namespace AgentSmith.Tests.Dispatcher;

/// <summary>
/// 2026-11-10-ccd9 review (M3): the platform's own time of an inbound chat message, which a typed approve or reject
/// must be later than for it to name the card the thread was last shown.
/// </summary>
public sealed class ChatSentAtTests
{
    [Fact]
    public void SlackEvent_Ts_IsTheMomentSlackTookTheMessage() =>
        SlackPayloadExtractor.ExtractEventSentAt(JsonNode.Parse("""{"event":{"type":"message","ts":"1731234567.123456"}}""")!)
            .Should().Be(DateTimeOffset.FromUnixTimeSeconds(1731234567).AddTicks(1234560));

    [Fact]
    public void SlackEvent_NoTs_IsNull() =>
        SlackPayloadExtractor.ExtractEventSentAt(JsonNode.Parse("""{"event":{"type":"message"}}""")!).Should().BeNull();

    [Fact]
    public void TeamsActivity_Timestamp_IsReadAsUtc() =>
        TeamsActivityTime.Of(JsonNode.Parse("""{"type":"message","timestamp":"2026-11-10T10:00:01.5Z"}""")!)
            .Should().Be(new DateTimeOffset(2026, 11, 10, 10, 0, 1, 500, TimeSpan.Zero));

    [Fact]
    public void TeamsActivity_NoTimestamp_IsNull() =>
        TeamsActivityTime.Of(JsonNode.Parse("""{"type":"message"}""")!).Should().BeNull();
}
