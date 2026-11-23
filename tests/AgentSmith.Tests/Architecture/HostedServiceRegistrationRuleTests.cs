using System.Text.RegularExpressions;
using FluentAssertions;

namespace AgentSmith.Tests.Architecture;

/// <summary>
/// 2026-11-22-747db: a product worker reaches the host only through <c>AddWorker&lt;T&gt;</c>, whose launcher builds and
/// starts it after the listener is up. A bare <c>AddHostedService</c> or an <c>IHostedService</c> registered by hand
/// is resolved and started before Kestrel binds — the path that held the port shut. Structural, so a convention
/// nobody remembers cannot reopen it.
/// </summary>
public sealed class HostedServiceRegistrationRuleTests
{
    private static readonly Regex DirectRegistration = new(
        @"\bAddHostedService\b|typeof\(IHostedService\)|<IHostedService\s*,|<IHostedService>\(", RegexOptions.Compiled);

    // The one place that registers the launcher itself.
    private const string WorkerExtensions = "WorkerExtensions.cs";

    [Fact]
    public void Backend_RegistersEveryHostedService_ThroughAddWorker()
    {
        var offenders = ArchitectureSources.HandWrittenBackendFiles()
            .Where(f => Path.GetFileName(f) != WorkerExtensions)
            .Where(f => File.ReadLines(f).Any(line => !line.TrimStart().StartsWith("//", StringComparison.Ordinal)
                && DirectRegistration.IsMatch(line)))
            .Select(f => Path.GetRelativePath(ArchitectureSources.BackendRoot, f))
            .ToList();

        offenders.Should().BeEmpty("a worker registered past the launcher starts before the server listens");
    }

    [Fact]
    public void Rule_HasTeeth_EveryDirectShapeMatches()
    {
        DirectRegistration.IsMatch("services.AddHostedService<Reaper>();").Should().BeTrue();
        DirectRegistration.IsMatch("services.AddHostedService(sp => sp.GetRequiredService<Reaper>());").Should().BeTrue();
        DirectRegistration.IsMatch("services.AddSingleton(typeof(IHostedService), type);").Should().BeTrue();
        DirectRegistration.IsMatch("services.AddSingleton<IHostedService, Reaper>();").Should().BeTrue();
        DirectRegistration.IsMatch("services.AddWorker<Reaper>();").Should().BeFalse();
    }
}
