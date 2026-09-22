using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using AgentSmith.Server.Services.Sandbox;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Sandbox;

/// <summary>
/// 2026-09-22-2d11a: the collaborators the held-conversation rail is driven with. The
/// rail is a label plus a shared row, so a test supplies rows and a catalog and never
/// a running conversation.
/// </summary>
internal static class SandboxHoldRailDoubles
{
    /// <summary>The instant every hold-rail fixture is written against.</summary>
    public static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-09-22T12:00:00Z");

    public static TimeProvider Clock => new FixedClock(Now);
    /// <summary>A reader over the given session rows and an unconfigured catalog.</summary>
    public static HeldConversationReader Reader(params ConversationLiveness[] rows) =>
        Reader(new AgentSmithConfig(), rows);

    public static HeldConversationReader Reader(
        AgentSmithConfig config, params ConversationLiveness[] rows) =>
        new(Resolver(new CountingConfigLoader(config)), new StubConversations(rows), Clock,
            NullLogger<HeldConversationReader>.Instance);

    public static HeldConversationReader Reader(IConversationLivenessReader conversations) =>
        new(Resolver(new CountingConfigLoader(new AgentSmithConfig())), conversations, Clock,
            NullLogger<HeldConversationReader>.Instance);

    public static SandboxHoldWindowResolver Resolver(IConfigurationLoader loader) =>
        new(loader, new ServerContext("agentsmith.yml"),
            NullLogger<SandboxHoldWindowResolver>.Instance);

    /// <summary>A catalog read that counts its calls, and can be made to fail.</summary>
    internal sealed class CountingConfigLoader(AgentSmithConfig config) : IConfigurationLoader
    {
        public int Reads { get; private set; }
        public bool Fails { get; set; }
        public ConfigFileReadFact? LastRead => null;

        public AgentSmithConfig LoadConfig(string configPath)
        {
            Reads++;
            if (Fails) throw new InvalidOperationException("the configuration store is unreachable");
            return config;
        }
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    /// <summary>Session rows, or a read that fails.</summary>
    internal sealed class StubConversations(params ConversationLiveness[] rows) : IConversationLivenessReader
    {
        public bool Fails { get; set; }
        public int Reads { get; private set; }

        public Task<IReadOnlyList<ConversationLiveness>> ReadAsync(
            IReadOnlyCollection<string> conversationIds, CancellationToken cancellationToken)
        {
            Reads++;
            if (Fails) throw new InvalidOperationException("the session store is unreachable");
            return Task.FromResult<IReadOnlyList<ConversationLiveness>>(
                [.. rows.Where(r => conversationIds.Contains(r.ConversationId))]);
        }
    }
}
