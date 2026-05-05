using AgentSmith.Sandbox.Agent.Models;
using FluentAssertions;

namespace AgentSmith.Sandbox.Agent.Tests.Models;

public class RedisKeysTests
{
    [Fact]
    public void InputKey_FormatsAsExpected()
    {
        RedisKeys.InputKey("job-42").Should().Be("sandbox:job-42:in");
    }

    [Fact]
    public void EventsKey_FormatsAsExpected()
    {
        RedisKeys.EventsKey("job-42").Should().Be("sandbox:job-42:events");
    }

    [Fact]
    public void ResultsKey_FormatsAsExpected()
    {
        RedisKeys.ResultsKey("job-42").Should().Be("sandbox:job-42:results");
    }
}
