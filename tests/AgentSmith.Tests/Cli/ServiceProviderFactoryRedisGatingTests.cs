using AgentSmith.Application.Services.Claim;
using AgentSmith.Application.Services.RedisDisabled;
using AgentSmith.Cli;
using AgentSmith.Contracts.Services;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Tests.Cli;

public sealed class ServiceProviderFactoryRedisGatingTests : IDisposable
{
    private readonly string? _originalRedisUrl;

    public ServiceProviderFactoryRedisGatingTests()
    {
        _originalRedisUrl = Environment.GetEnvironmentVariable("REDIS_URL");
        Environment.SetEnvironmentVariable("REDIS_URL", null);
    }

    public void Dispose()
        => Environment.SetEnvironmentVariable("REDIS_URL", _originalRedisUrl);

    [Fact]
    public void Build_RedisUrlEmpty_RegistersNullRedisImplementations()
    {
        using var provider = ServiceProviderFactory.Build(
            verbose: false, headless: true, jobId: string.Empty, redisUrl: string.Empty);

        provider.GetService<IRedisJobQueue>().Should().BeOfType<NullRedisJobQueue>();
        provider.GetService<IRedisClaimLock>().Should().BeOfType<NullRedisClaimLock>();
        provider.GetService<IRedisLeaderLease>().Should().BeOfType<NullRedisLeaderLease>();
        provider.GetService<IJobHeartbeatService>().Should().BeOfType<NullJobHeartbeatService>();
        provider.GetService<IConversationLookup>().Should().BeOfType<NullConversationLookup>();
    }

    [Fact]
    public void Build_RedisUrlEmpty_RegistersNullTicketClaimService()
    {
        using var provider = ServiceProviderFactory.Build(
            verbose: false, headless: true, jobId: string.Empty, redisUrl: string.Empty);

        using var scope = provider.CreateScope();
        var claimService = scope.ServiceProvider.GetRequiredService<ITicketClaimService>();
        claimService.Should().BeOfType<NullTicketClaimService>();
    }

    [Fact]
    public void Build_RedisUrlEmpty_RegistersDisabledRedisHealth()
    {
        using var provider = ServiceProviderFactory.Build(
            verbose: false, headless: true, jobId: string.Empty, redisUrl: string.Empty);

        var redisHealth = provider.GetServices<ISubsystemHealth>()
            .FirstOrDefault(h => h.Name == "redis");
        redisHealth.Should().NotBeNull();
        redisHealth!.State.Should().Be(SubsystemState.Disabled);
        redisHealth.Reason.Should().Be("REDIS_URL not configured");
    }

    [Fact]
    public void Build_RedisUrlSet_RegistersRedisJobQueue()
    {
        using var provider = ServiceProviderFactory.Build(
            verbose: false, headless: true, jobId: string.Empty,
            redisUrl: "127.0.0.1:6390,abortConnect=false");

        provider.GetService<IRedisJobQueue>().Should().NotBeNull();
        provider.GetService<IRedisClaimLock>().Should().NotBeNull();
        provider.GetService<IRedisLeaderLease>().Should().NotBeNull();
    }

    [Fact]
    public void Build_RedisUrlSet_RegistersRealTicketClaimService()
    {
        using var provider = ServiceProviderFactory.Build(
            verbose: false, headless: true, jobId: string.Empty,
            redisUrl: "127.0.0.1:6390,abortConnect=false");

        using var scope = provider.CreateScope();
        var claimService = scope.ServiceProvider.GetRequiredService<ITicketClaimService>();
        claimService.Should().NotBeOfType<NullTicketClaimService>();
    }

    [Fact]
    public void Build_RedisUnreachable_StillBuildsServiceProvider()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        using var provider = ServiceProviderFactory.Build(
            verbose: false, headless: true, jobId: string.Empty,
            redisUrl: "redis-nonexistent.invalid:6379,abortConnect=false");

        sw.Stop();
        sw.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(15));
        provider.Should().NotBeNull();
    }
}
