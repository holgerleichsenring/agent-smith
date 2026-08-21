using AgentSmith.Infrastructure.Services.Providers.Source;
using FluentAssertions;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Tests.Providers.Source;

/// <summary>
/// p0500: config/agentsmith.example.yml states "default_branch comes from each
/// discovered repo; the connection value is only a fallback", and all three remote
/// providers had it exactly inverted — the configured value won unconditionally and
/// the platform was asked only when nothing was configured.
/// <para>
/// The cost was a repository that reads as EMPTY. With <c>default_branch: develop</c>
/// on the connection, a repo that has no develop answered TF401175 to every read, so
/// listing and file reads returned nothing, discovery saw an un-initialised
/// repository, and init-project opened no pull request — an outcome that never
/// mentions a branch. These pin the restored precedence AND the warning that makes a
/// disagreement visible, because silence was the other half of the defect.
/// </para>
/// </summary>
public sealed class DefaultBranchResolverTests
{
    private const string Repo = "sample/Sample.Terraform";

    [Fact]
    public async Task DefaultBranch_RepositoryHasItsOwn_WinsOverTheConfiguredValue()
    {
        var (sut, _) = Build(configured: "develop");

        var branch = await sut.ResolveAsync(_ => Task.FromResult<string?>("main"), CancellationToken.None);

        branch.Should().Be("main", "the repository's own default branch wins");
    }

    [Fact]
    public async Task DefaultBranch_ConfiguredDisagreesWithTheRepository_IsLoggedWithBothNames()
    {
        var (sut, log) = Build(configured: "develop");

        await sut.ResolveAsync(_ => Task.FromResult<string?>("main"), CancellationToken.None);

        log.Warnings.Should().ContainSingle()
            .Which.Should().Contain("develop").And.Contain("main").And.Contain(Repo);
    }

    [Fact]
    public async Task DefaultBranch_ConfiguredMatchesTheRepository_SaysNothing()
    {
        var (sut, log) = Build(configured: "develop");

        await sut.ResolveAsync(_ => Task.FromResult<string?>("develop"), CancellationToken.None);

        log.Warnings.Should().BeEmpty("agreement is not news");
    }

    [Fact]
    public async Task DefaultBranch_RepositoryAnswersNothing_FallsBackToTheConfiguredValue()
    {
        var (sut, _) = Build(configured: "develop");

        var branch = await sut.ResolveAsync(_ => Task.FromResult<string?>(null), CancellationToken.None);

        branch.Should().Be("develop", "the connection value is the documented fallback");
    }

    [Fact]
    public async Task DefaultBranch_RepositoryCallThrows_FallsBackToTheConfiguredValue()
    {
        var (sut, _) = Build(configured: "develop");

        var branch = await sut.ResolveAsync(
            _ => Task.FromException<string?>(new InvalidOperationException("boom")), CancellationToken.None);

        branch.Should().Be("develop");
    }

    [Fact]
    public async Task DefaultBranch_NeitherAnswers_FallsBackToMain()
    {
        var (sut, log) = Build(configured: null);

        var branch = await sut.ResolveAsync(_ => Task.FromResult<string?>(null), CancellationToken.None);

        branch.Should().Be(DefaultBranchResolver.LastResort);
        log.Warnings.Should().NotBeEmpty("a repository nobody can name a branch for is worth saying out loud");
    }

    [Fact]
    public async Task DefaultBranch_RefsHeadsPrefix_IsStripped()
    {
        var (sut, _) = Build(configured: null);

        var branch = await sut.ResolveAsync(
            _ => Task.FromResult<string?>("refs/heads/release/2026"), CancellationToken.None);

        branch.Should().Be("release/2026");
    }

    [Fact]
    public async Task DefaultBranch_ResolvedOnce_IsNotAskedAgain()
    {
        var (sut, _) = Build(configured: null);
        var asked = 0;
        Task<string?> Ask(CancellationToken _) { asked++; return Task.FromResult<string?>("main"); }

        await sut.ResolveAsync(Ask, CancellationToken.None);
        await sut.ResolveAsync(Ask, CancellationToken.None);

        asked.Should().Be(1, "every read of the repo goes through this; one lookup per provider");
    }

    private static (DefaultBranchResolver Sut, CapturingLogger Log) Build(string? configured)
    {
        var log = new CapturingLogger();
        return (new DefaultBranchResolver(configured, Repo, log), log);
    }

    private sealed class CapturingLogger : ILogger
    {
        public List<string> Warnings { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.Warning) Warnings.Add(formatter(state, exception));
        }
    }
}
