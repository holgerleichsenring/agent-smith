using AgentSmith.Server.Services;
using FluentAssertions;

namespace AgentSmith.Tests.Server.Redis;

public sealed class RedisEndpointTextTests
{
    [Fact]
    public void RedisEndpointText_UriFormWithPassword_ShowsOnlyHostAndPort()
    {
        var text = RedisEndpointText.Of("redis://:s3cret-pass@redis:6380");

        text.Should().Be("redis:6380");
        text.Should().NotContain("s3cret-pass");
    }

    [Fact]
    public void RedisEndpointText_KeyValueFormWithPassword_ShowsOnlyEndpoints()
    {
        var text = RedisEndpointText.Of("redis-a:6379,redis-b:6379,user=agent,password=s3cret-pass,ssl=true");

        text.Should().Be("redis-a:6379, redis-b:6379");
        text.Should().NotContain("s3cret-pass");
    }

    [Fact]
    public void RedisEndpointText_NothingParses_IsHidden()
    {
        RedisEndpointText.Of("not a url at all!").Should().Be(RedisEndpointText.Hidden);
        RedisEndpointText.Of("").Should().Be(RedisEndpointText.Hidden);
        RedisEndpointText.Of(null).Should().Be(RedisEndpointText.Hidden);
    }
}
