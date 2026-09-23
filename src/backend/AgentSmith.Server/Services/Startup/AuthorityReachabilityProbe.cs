using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Server.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Server.Services.Startup;

/// <summary>
/// p0503e: asks the configured authority for its discovery document, and publishes the
/// answer as a finding the server holds about ITSELF. It is not an <c>IStartupProbe</c>:
/// those run before the listener binds and exactly once, and this one must do neither —
/// it recurs, and it clears its own finding when the authority comes back, which is what
/// <see cref="IStartupFindings.Clear"/> exists for.
/// </summary>
internal sealed class AuthorityReachabilityProbe(
    [FromKeyedServices(AuthorityReachabilityProbe.ComposedAuthorityKey)] TokenAuthorityConfig auth,
    IStartupFindings findings,
    IHttpClientFactory clients,
    ILogger<AuthorityReachabilityProbe> logger) : IAuthorityReachability
{
    /// <summary>
    /// 2026-09-23-e7f0: the composition root registers the block it COMPOSED under this key,
    /// and this parameter asks for that one. The plain registration is read lazily on first
    /// resolution, so a probe built later could measure a different authority than the handler
    /// validates against — the one thing this finding must never be wrong about. The key is
    /// declared beside the parameter that reads it, so the registration cannot drift from it.
    /// </summary>
    public const string ComposedAuthorityKey = "authority-reachability-probe.composed-authority";

    private const string DiscoveryPath = "/.well-known/openid-configuration";

    // Bounded for the same reason the boot probes are: an authority that blackholes its
    // packets must cost one pass, not the OS connect timeout on every pass after it.
    private static readonly TimeSpan Budget = TimeSpan.FromSeconds(10);

    private volatile bool _unreachable;

    public bool IsUnreachable => _unreachable;

    public async Task ProbeAsync(CancellationToken cancellationToken)
    {
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budget.CancelAfter(Budget);
        try
        {
            using var response = await clients
                .CreateClient(nameof(AuthorityReachabilityProbe))
                .GetAsync(DiscoveryUrl, budget.Token);
            if (response.IsSuccessStatusCode) Reachable();
            else Unreachable($"its discovery document answered {(int)response.StatusCode}");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            logger.LogDebug("The token authority probe was cancelled while the server stops");
        }
        catch (OperationCanceledException)
        {
            Unreachable($"it did not answer within {Budget.TotalSeconds:0} seconds");
        }
        catch (HttpRequestException ex)
        {
            Unreachable(ex.Message);
        }
    }

    private string DiscoveryUrl => auth.Authority!.TrimEnd('/') + DiscoveryPath;

    private void Reachable()
    {
        if (!_unreachable) return;
        _unreachable = false;
        findings.Clear(StartupSubsystems.Auth);
        logger.LogInformation("The token authority at {Authority} answers again", auth.Authority);
    }

    // No project, deliberately: StartupFindingsQueries matches a blocking finding to a
    // trigger BY PROJECT NAME, and an authority nobody can reach is not one project's
    // fault. Severity follows enforcement, because that is what decides whether anybody
    // is actually being refused over it.
    private void Unreachable(string reason)
    {
        _unreachable = true;
        logger.LogError(
            "The token authority at {Authority} cannot be reached: {Reason}", auth.Authority, reason);
        findings.Record(new StartupFinding(
            StartupSubsystems.Auth,
            auth.Enforce ? StartupFindingSeverity.Blocking : StartupFindingSeverity.Advisory,
            $"This server cannot reach the token authority at {auth.Authority}: {reason}. Until it "
            + "answers again, a token this server holds no cached signing keys for cannot be "
            + "validated"
            + (auth.Enforce
                ? " and its caller is refused."
                : ", though enforcement is off and no route is refused."),
            Field: "authority"));
    }
}
