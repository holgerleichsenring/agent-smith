using System.Collections.Concurrent;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Server.Services.Rework;
using AgentSmith.Server.Services.SpecDialog;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.TestSupport;

/// <summary>
/// 2026-10-13-9a59t: the closed ticket's follow-up for hand-built rework handlers. Its scopes must resolve the
/// ledger repository and <see cref="FiledWorkTrackerProjects"/> (<see cref="Register"/>); filing also needs
/// FiledWorkStarter, FollowUpLink and TicketKindResolver.
/// </summary>
internal static class ClosedTicketFollowUps
{
    public static ClosedTicketActFollowUp Of(
        IServiceScopeFactory scopes, ITicketProviderFactory tickets, ReworkSpeech speech, IRedisClaimLock? locks = null) =>
        new(scopes, locks ?? new InMemoryLock(), tickets, speech, NullLogger<ClosedTicketActFollowUp>.Instance);

    /// <summary>What the served check resolves, over a configuration that knows no project.</summary>
    public static void Register(IServiceCollection services) =>
        services.AddSingleton(new Contracts.Models.Configuration.AgentSmithConfig()).AddTransient<FiledWorkTrackerProjects>();

    /// <summary>A lock held in memory; <see cref="Held"/> makes every acquire fail.</summary>
    internal sealed class InMemoryLock : IRedisClaimLock
    {
        private readonly ConcurrentDictionary<string, string> _held = new();

        public bool Held { get; set; }

        public Task<string?> TryAcquireAsync(string key, TimeSpan ttl, CancellationToken cancellationToken)
        {
            var token = Guid.NewGuid().ToString("N");
            return Task.FromResult<string?>(!Held && _held.TryAdd(key, token) ? token : null);
        }

        public Task ReleaseAsync(string key, string token, CancellationToken cancellationToken)
        {
            _held.TryRemove(new KeyValuePair<string, string>(key, token));
            return Task.CompletedTask;
        }
    }
}
