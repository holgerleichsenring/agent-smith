using System.Net;
using AgentSmith.Application.Models.Sandbox;
using AgentSmith.Application.Services.Sandbox.Registry;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using static AgentSmith.Tests.Sandbox.Registry.FakeRegistry;

namespace AgentSmith.Tests.Sandbox.Registry;

/// <summary>2026-10-12-50bdb: a tag list is read page by page, never past the bound or off the host.</summary>
public sealed class RegistryTagListerTests
{
    private static readonly RegistryImageReference Image =
        new("sbtscala/scala-sbt:x", "registry-1.docker.io", "sbtscala/scala-sbt", "x");

    private readonly FakeRegistry _registry = new();

    [Fact]
    public async Task ListAsync_FollowsLinkUpToThePageBound()
    {
        var served = 0;
        _registry.Answer = _ =>
        {
            served++;
            return Tags($"</v2/sbtscala/scala-sbt/tags/list?last=t{served}&n=1000>; rel=\"next\"", $"t{served}");
        };

        var tags = await Lister().ListAsync(Image, FakeRegistry.Token, CancellationToken.None);

        tags.Should().Equal("t1", "t2", "t3", "t4", "t5");
        _registry.Requests.Should().HaveCount(RegistryTagLister.MaxPages);
        _registry.Requests[1].RequestUri!.ToString().Should().Be(
            "https://registry-1.docker.io/v2/sbtscala/scala-sbt/tags/list?last=t1&n=1000");
        _registry.Requests[0].RequestUri!.Query.Should().Be("?n=1000");
    }

    [Fact]
    public async Task ListAsync_LinkToAnotherHost_IsNotFollowed()
    {
        _registry.Answer = _ => Tags("<https://elsewhere.example/v2/x/tags/list>; rel=\"next\"", "a");

        var tags = await Lister().ListAsync(Image, null, CancellationToken.None);

        tags.Should().Equal("a");
        _registry.Requests.Should().ContainSingle();
    }

    [Fact]
    public async Task ListAsync_FirstPageFails_ReturnsNull()
    {
        _registry.Answer = _ => Respond(HttpStatusCode.NotFound);

        var tags = await Lister().ListAsync(Image, null, CancellationToken.None);

        tags.Should().BeNull();
    }

    [Fact]
    public void Tags_NullTagList_IsEmpty()
    {
        new RegistryTagPageParser(NullLogger<RegistryTagPageParser>.Instance)
            .Tags("""{"name":"x","tags":null}""").Should().BeEmpty();
    }

    private RegistryTagLister Lister()
    {
        var transport = new RegistryTransport(_registry,
            new AgentSmith.Application.Services.Browser.RenderUrlGuard(
                _registry, new AgentSmith.Application.Services.Browser.PublicAddressRule()),
            NullLogger<RegistryTransport>.Instance);
        var client = new RegistryClient(transport, new RegistryChallengeParser(),
            new RegistryTokenSource(transport, NullLogger<RegistryTokenSource>.Instance));
        return new RegistryTagLister(client, new RegistryTagPageParser(NullLogger<RegistryTagPageParser>.Instance));
    }
}
