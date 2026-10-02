using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// 2026-10-02-5f89a: <see cref="IMaterializingSecrets"/> on an <see cref="AsyncLocal{T}"/>, so a
/// materialization on one flow never lends its secrets to a call on another.
/// </summary>
public sealed class MaterializingSecrets : IMaterializingSecrets
{
    private readonly AsyncLocal<ISecretValues?> _current = new();

    public ISecretValues? Current => _current.Value;

    public IDisposable Begin(IReadOnlyDictionary<string, string> secrets)
    {
        var previous = _current.Value;
        _current.Value = new LoadedSecretValues(new AgentSmithConfig { Secrets = secrets });
        return new Scope(() => _current.Value = previous);
    }

    private sealed class Scope(Action end) : IDisposable
    {
        public void Dispose() => end();
    }
}
