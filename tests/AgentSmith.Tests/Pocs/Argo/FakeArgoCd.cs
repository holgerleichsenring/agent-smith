using System.Text.Json.Nodes;
using AgentSmith.Contracts.Exceptions;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;

namespace AgentSmith.Tests.Pocs.Argo;

/// <summary>2026-11-23-c81ec: an Argo CD in memory — a stored ApplicationSet, the upserts it received, and a script of
/// readings (Application, live Deployment) each poll takes the next of, the last one repeating.</summary>
internal sealed class FakeArgoCd : IArgoCdClient, IArgoCdClientFactory
{
    internal JsonObject? StoredSet { get; set; }

    internal List<JsonObject> Upserts { get; } = [];

    internal List<(ArgoApplicationState? App, ArgoLiveDeployment? Live)> Readings { get; } = [];

    internal List<string> Refreshed { get; } = [];

    internal ArgoCdException? Refusal { get; set; }

    private int _polls;

    internal int Polls => _polls;

    public IArgoCdClient Create(PocArgoConfig argo) => this;

    public IArgoCdClient Create(PocArgoConfig argo, string token) => this;

    public Task<ArgoProbe> ProbeAsync(CancellationToken cancellationToken) => Task.FromResult(new ArgoProbe(true, "takes the token"));

    public Task<ArgoApplicationState?> GetApplicationAsync(string name, CancellationToken cancellationToken)
    {
        if (Refusal is not null) throw Refusal;
        var index = Math.Min(_polls, Readings.Count - 1);
        _polls++;
        return Task.FromResult(index < 0 ? null : Readings[index].App);
    }

    public Task<ArgoApplicationState?> RefreshAsync(string name, CancellationToken cancellationToken)
    {
        Refreshed.Add(name);
        return Task.FromResult<ArgoApplicationState?>(null);
    }

    public Task<ArgoLiveDeployment?> GetLiveDeploymentAsync(string application, string ns, string name, CancellationToken cancellationToken) =>
        Task.FromResult(Readings.Count == 0 ? null : Readings[Math.Min(_polls - 1, Readings.Count - 1)].Live);

    public Task<JsonObject?> GetApplicationSetAsync(string name, CancellationToken cancellationToken) =>
        Task.FromResult(StoredSet?.DeepClone() as JsonObject);

    public Task UpsertApplicationSetAsync(JsonObject applicationSet, CancellationToken cancellationToken)
    {
        Upserts.Add(applicationSet);
        StoredSet = (JsonObject)applicationSet.DeepClone();
        return Task.CompletedTask;
    }
}
