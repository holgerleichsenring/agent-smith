using System.Collections.Concurrent;
using AgentSmith.Infrastructure.Services.Providers;
using FluentAssertions;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;

namespace AgentSmith.Tests.Providers;

/// <summary>2026-11-25-d0e2: a rotated token's connections are dropped once stale; the asked key and fresh ones stay.</summary>
public sealed class VssConnectionEvictionTests
{
    [Fact]
    public void DropStale_RemovesEveryOtherStaleEntry_KeepsTheAskedAndTheFresh()
    {
        VssConnection Connection() => new(new Uri("https://dev.azure.com/org"), new VssBasicCredential(string.Empty, "pat"));
        var cache = new ConcurrentDictionary<string, (VssConnection Connection, bool Stale)>
        {
            ["old-token"] = (Connection(), true),
            ["asked"] = (Connection(), true),
            ["fresh"] = (Connection(), false),
        };

        VssConnectionEviction.DropStale(cache, "asked", e => e.Stale);

        cache.Keys.Should().BeEquivalentTo(["asked", "fresh"]);
    }
}
