using System.Reflection;
using System.Runtime.CompilerServices;
using AgentSmith.Application.Services.Specs;
using FluentAssertions;

namespace AgentSmith.Tests.Architecture;

/// <summary>
/// p0401: a static class in Application or Infrastructure holds no state. A static
/// field IS the dependency a constructor would have taken — a serializer, json
/// options, a meter, an ambient scope, a warn-once set — and holding it statically
/// puts it out of reach of the composition root, of a test that needs a different
/// one, and of the run that needs its own.
/// <para>
/// Pure static FUNCTIONS over values stay static: they have nothing to inject. The
/// line this rule draws is state, not the static keyword.
/// </para>
/// <para>
/// <see cref="DataTables"/> lists the fields that are DATA rather than
/// collaborators — lookup maps, extension lists, fixed durations. Every entry is
/// classified where it stands. A field that is not data belongs in a service.
/// </para>
/// </summary>
public sealed class NoStaticStateRuleTests
{
    // Format: "{Type.FullName}::{fieldName}". Entries are immutable data tables,
    // which is the ONLY justification this list accepts.
    private static readonly HashSet<string> DataTables = new(StringComparer.Ordinal)
    {
        "AgentSmith.Application.Prompts.MasterPromptTokens::All",
        "AgentSmith.Application.Services.ProjectMapCacheKey::ManifestNames",
        "AgentSmith.Application.Services.ProjectMapCacheKey::ManifestExtensions",
        "AgentSmith.Application.Services.TicketBranchNamer::NonAlnum",
        "AgentSmith.Application.Services.ApiScanFindingsCompressor::SkillCategories",
        "AgentSmith.Application.Services.ErrorFormatter::Rules",
        "AgentSmith.Application.Services.ErrorFormatter::UsageLimitPattern",
        "AgentSmith.Application.Services.ErrorFormatter::ResetDatePattern",
        "AgentSmith.Application.Services.ApiScanFindingsFormatter::AuthSpectralCodes",
        "AgentSmith.Application.Services.ApiScanFindingsFormatter::AuthSpectralKeywords",
        "AgentSmith.Application.Services.ApiScanFindingsFormatter::AuthNucleiKeywords",
        "AgentSmith.Application.Services.ApiScanFindingsFormatter::HeaderKeywords",
        "AgentSmith.Application.Services.RunIdGenerator::CanonicalRegex",
        "AgentSmith.Application.Services.Tools.PriorRunLedgerSeeder::MaxAge",
        "AgentSmith.Application.Services.Handlers.ScannerObservationFactory::SeverityMap",
        "AgentSmith.Infrastructure.Services.Security.SourceFileEnumerator::ExcludedDirectories",
        "AgentSmith.Infrastructure.Services.Security.SourceFileEnumerator::ExcludedPathPrefixes",
        "AgentSmith.Infrastructure.Services.Security.SourceFileEnumerator::BinaryExtensions",
        "AgentSmith.Infrastructure.Services.Events.EventStreamKeys::StreamTtl",
        "AgentSmith.Infrastructure.Core.Services.Configuration.Studio.ConfigDocumentTaxonomy::All",
        "AgentSmith.Infrastructure.Core.Services.Configuration.Studio.ConfigSettingsAccess::Types",
        "AgentSmith.Infrastructure.Persistence.Extensions.PersistenceOptionsExtensions::MySqlVersion",
    };

    private static readonly string[] TargetAssemblies =
    [
        "AgentSmith.Application",
        "AgentSmith.Infrastructure",
        "AgentSmith.Infrastructure.Core",
        "AgentSmith.Infrastructure.Persistence",
    ];

    // p0401a: collaborators still held statically. Each one's conversion cascades
    // into a composition edge that is not a DI service today (the CLI's config
    // command, the config-studio endpoint class), so they land as one slice rather
    // than half-converted. Named here so the debt is readable, not implied.
    private static readonly HashSet<string> PendingConversions = new(StringComparer.Ordinal)
    {
        "AgentSmith.Infrastructure.Core.Services.Configuration.RawConfigYaml::Deserializer",
        "AgentSmith.Infrastructure.Core.Services.Configuration.Studio.ConfigDocJson::Options",
        "AgentSmith.Infrastructure.Core.Services.Configuration.Studio.ConfigYamlExporter::Serializer",
        "AgentSmith.Infrastructure.Services.Events.EventEnvelopeSerializer::Options",
    };

    [Fact]
    public void StaticClasses_HoldNoCollaborators_OnlyClassifiedDataTables()
    {
        var offenders = Scan(Assemblies())
            .Where(f => !DataTables.Contains(f) && !PendingConversions.Contains(f))
            .ToList();

        offenders.Should().BeEmpty(
            "a static field is a dependency the composition root cannot see; move it "
            + "into a sealed DI service, or classify it in DataTables if it is data.\n"
            + string.Join("\n  ", offenders));
    }

    [Fact]
    public void Sanity_TheScanReachesTheProductionAssemblies()
    {
        Assemblies().Should().HaveCount(TargetAssemblies.Length,
            "an unloaded assembly would silently pass the rule");
        Assemblies().SelectMany(a => a.GetTypes()).Where(IsStaticClass)
            .Should().HaveCountGreaterThan(50, "the scan must reach a meaningful slice");
    }

    [Fact]
    public void Rule_HasTeeth_ASyntheticStaticHoldingACollaboratorIsFlagged()
        => Scan([typeof(SyntheticStaticHolder).Assembly])
            .Should().Contain($"{typeof(SyntheticStaticHolder).FullName}::Collaborator");

    // Proves the rule bites. Lives in the test assembly, scanned only by the test above.
    internal static class SyntheticStaticHolder
    {
        internal static readonly DerivedPhaseYamlRenderer Collaborator = new();
    }

    private static IReadOnlyList<string> Scan(IEnumerable<Assembly> assemblies) =>
        [.. assemblies
            .SelectMany(a => a.GetTypes())
            .Where(IsStaticClass)
            .SelectMany(t => t
                .GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic
                           | BindingFlags.DeclaredOnly)
                // Literals are compile-time constants — they are not state, they are
                // the source text. Compiler-generated caches (lambda singletons) are
                // the compiler's business, not the author's.
                .Where(f => !f.IsLiteral && !IsCompilerGenerated(f))
                .Select(f => $"{t.FullName}::{f.Name}"))
            .OrderBy(x => x, StringComparer.Ordinal)];

    private static bool IsStaticClass(Type t) =>
        t is { IsClass: true, IsAbstract: true, IsSealed: true }
        && !IsCompilerGenerated(t)
        && t.DeclaringType is null or { IsClass: true };

    private static bool IsCompilerGenerated(MemberInfo m) =>
        m.Name.Contains('<', StringComparison.Ordinal)
        || m.IsDefined(typeof(CompilerGeneratedAttribute), inherit: false)
        || (m.DeclaringType is { } d && d != m && d.Name.Contains('<', StringComparison.Ordinal));

    private static IReadOnlyList<Assembly> Assemblies() =>
        [.. TargetAssemblies
            .Select(name => AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == name) ?? Assembly.Load(name))];
}
