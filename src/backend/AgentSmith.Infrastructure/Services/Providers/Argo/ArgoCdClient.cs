using System.Text.Json.Nodes;
using AgentSmith.Contracts.Exceptions;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;

namespace AgentSmith.Infrastructure.Services.Providers.Argo;

/// <summary>
/// 2026-11-23-c81ec: Argo CD's REST API (v1) for one PoC target — the probe (version, then the session's userinfo, then
/// the token's expiry), the Application read and refresh, the live Deployment through the resource endpoint, and the
/// ApplicationSet read and upsert. Never a sync, rollback or delete call.
/// </summary>
public sealed class ArgoCdClient(ArgoCdHttp http, PocArgoConfig argo, string token, TimeProvider clock) : IArgoCdClient
{
    public async Task<ArgoProbe> ProbeAsync(CancellationToken cancellationToken)
    {
        var (expiry, refusal) = ArgoTokenExpiry.Read(token);
        if (expiry is null) return new ArgoProbe(false, refusal!);
        await http.SendAsync(HttpMethod.Get, "api/version", null, cancellationToken);
        var info = await http.SendAsync(HttpMethod.Get, "api/v1/session/userinfo", null, cancellationToken);
        var loggedIn = info?["loggedIn"] is JsonValue value && value.TryGetValue<bool>(out var yes) && yes;
        var said = expiry.Sentence(clock.GetUtcNow(), argo.TokenWarnDays);
        return loggedIn
            ? new ArgoProbe(true, $"Argo CD {http.Server} takes the token. {said}")
            : new ArgoProbe(false, $"Argo CD {http.Server} does not take the token stored in token_secret. {said}");
    }

    public async Task<ArgoApplicationState?> GetApplicationAsync(string name, CancellationToken cancellationToken) =>
        await http.SendAsync(HttpMethod.Get, $"api/v1/applications/{Escape(name)}", null, cancellationToken) is { } app
            ? ArgoApplicationRead.From(app) : null;

    public async Task<ArgoApplicationState?> RefreshAsync(string name, CancellationToken cancellationToken) =>
        await http.SendAsync(HttpMethod.Get, $"api/v1/applications/{Escape(name)}?refresh=normal", null, cancellationToken) is { } app
            ? ArgoApplicationRead.From(app) : null;

    public async Task<ArgoLiveDeployment?> GetLiveDeploymentAsync(
        string application, string ns, string name, CancellationToken cancellationToken)
    {
        var query = $"namespace={Escape(ns)}&resourceName={Escape(name)}&group=apps&version=v1&kind=Deployment";
        var answer = await http.SendAsync(HttpMethod.Get, $"api/v1/applications/{Escape(application)}/resource?{query}", null,
            cancellationToken);
        return answer is null ? null : ArgoDeploymentRead.From(answer);
    }

    public async Task<JsonObject?> GetApplicationSetAsync(string name, CancellationToken cancellationToken) =>
        await http.SendAsync(HttpMethod.Get, $"api/v1/applicationsets/{Escape(name)}?appsetNamespace={Escape(argo.Namespace)}",
            null, cancellationToken) as JsonObject;

    public async Task UpsertApplicationSetAsync(JsonObject applicationSet, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(applicationSet);
        if (await http.SendAsync(HttpMethod.Post, "api/v1/applicationsets?upsert=true", applicationSet, cancellationToken) is null)
            throw new ArgoCdException($"Argo CD {http.Server} answered 404 for api/v1/applicationsets; the ApplicationSet API "
                + "needs Argo CD 2.8 or later");
    }

    private static string Escape(string value) => Uri.EscapeDataString(value);
}
