using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Infrastructure.Core.Services.Configuration;

/// <summary>
/// p0391a: the in-memory findings list, keyed by <see cref="StartupFinding.Identity"/>.
/// Startup reloads its configuration many times (every leader loop re-reads it), so the
/// same fault is recorded over and over — the key collapses those into one entry and the
/// newest wording wins. Insertion order is preserved so the operator reads the findings
/// in the order the server discovered them.
/// </summary>
public sealed class StartupFindings : IStartupFindings
{
    private readonly Lock _gate = new();
    private readonly Dictionary<string, StartupFinding> _byIdentity = [];
    private readonly List<string> _order = [];

    public void Record(StartupFinding finding)
    {
        lock (_gate)
        {
            if (!_byIdentity.ContainsKey(finding.Identity)) _order.Add(finding.Identity);
            _byIdentity[finding.Identity] = finding;
        }
    }

    public void Clear(string subsystem)
    {
        lock (_gate)
        {
            var stale = _byIdentity.Values
                .Where(f => string.Equals(f.Subsystem, subsystem, StringComparison.OrdinalIgnoreCase))
                .Select(f => f.Identity)
                .ToList();
            foreach (var identity in stale)
            {
                _byIdentity.Remove(identity);
                _order.Remove(identity);
            }
        }
    }

    public IReadOnlyList<StartupFinding> All
    {
        get
        {
            lock (_gate) return _order.Select(id => _byIdentity[id]).ToList();
        }
    }
}
