using System.Reflection;
using System.Runtime.CompilerServices;
using AgentSmith.Application.Prompts;
using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Application.Services.Tools;
using AgentSmith.Infrastructure.Core.Services.Configuration.Studio;
using AgentSmith.Infrastructure.Services.Events;
using FluentAssertions;

namespace AgentSmith.Tests.Architecture;

/// <summary>
/// p0401/p0402: a static class in Application or Infrastructure holds no state and
/// takes no collaborator. A static field IS the dependency a constructor would have
/// taken; a service parameter is the same dependency passed by every caller instead.
/// Either way the composition root cannot see it, a test cannot substitute it, and
/// a run cannot have its own.
/// <para>
/// Pure static FUNCTIONS over values stay static — a mapper needs no logger, so it
/// needs no DI. The line this rule draws is dependencies, not the static keyword.
/// </para>
/// <para>
/// <see cref="DataTableHolders"/> names the classes whose static fields are DATA:
/// lookup maps, extension lists, regexes, fixed durations. Entries are
/// <c>typeof</c>, so a rename follows the code and a deletion breaks the build —
/// and <see cref="EveryAllowlistEntry_StillDescribesAStaticClassWithState"/> fails
/// when an entry goes stale, because nobody remembers to prune a list by hand.
/// </para>
/// </summary>
public sealed class NoStaticStateRuleTests
{
    // Classes whose static fields are immutable data tables — the ONLY justification
    // this list accepts. A collaborator in a static field belongs in a service.
    private static readonly Type[] DataTableHolders =
    [
        typeof(MasterPromptTokens),
        // p0424: the markers that identify agent-smith's OWN ticket comments — a fixed
        // list of literals, so the thread can render what the operator said rather than
        // our echo of twenty-four runs.
        typeof(AgentSmith.Application.Services.Prompts.OwnTicketComment),
        // p0451: the shell no-ops a declared stage command may start with — a fixed list of
        // literals, so a build stage that cannot fail is never mistaken for a verification.
        typeof(AgentSmith.Application.Services.Handlers.VerificationCommand),
        // p0423: the whitelist of argument keys that may appear in an activity row — a
        // fixed list of literals, so a tool call reads "read_file src/Foo.cs" without the
        // arg blob ever leaving the process.
        typeof(AgentSmith.Application.Services.Events.ToolArgumentFacts),
        typeof(PromptOwnership),
        typeof(TicketBranchNamer),
        typeof(ApiScanFindingsCompressor),
        typeof(ErrorFormatter),
        typeof(ApiScanFindingsFormatter),
        typeof(RunIdGenerator),
        typeof(AgentSmith.Application.Services.Tools.PriorRunLedgerSeeder),
        typeof(EventStreamKeys),
        typeof(ConfigDocumentTaxonomy),
        typeof(ConfigSettingsAccess),
        typeof(AgentSmith.Infrastructure.Persistence.Extensions.PersistenceOptionsExtensions),
        // p0504: the language→image convention table and the git-bearing tag patterns —
        // fixed lookup data, so which image a language resolves to is one fact in one place.
        typeof(AgentSmith.Application.Services.Sandbox.ToolchainImageCatalog),
        // p0504: the command names that cannot run without a pod — a fixed list of
        // literals, extracted so the lifecycle that provisions one does not also hold it.
        typeof(AgentSmith.Application.Services.Sandbox.SandboxRequiringCommands),
    ];

    private static readonly string[] TargetAssemblies =
    [
        "AgentSmith.Application",
        "AgentSmith.Infrastructure",
        "AgentSmith.Infrastructure.Core",
        "AgentSmith.Infrastructure.Persistence",
    ];

    [Fact]
    public void StaticClasses_HoldNoState_OnlyClassifiedDataTables()
    {
        var allowed = DataTableHolders.ToHashSet();
        var offenders = StaticClasses()
            .Where(t => !allowed.Contains(t))
            .SelectMany(t => StateFields(t).Select(f => $"{t.FullName}::{f.Name}"))
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();

        offenders.Should().BeEmpty(
            "a static field is a dependency the composition root cannot see; move it "
            + "into a sealed DI service, or add the class to DataTableHolders if its "
            + "state is data.\n  " + string.Join("\n  ", offenders));
    }

    [Fact]
    public void StaticClasses_TakeNoCollaborators_AMapperNeedsNoLogger()
    {
        var offenders = StaticClasses()
            .SelectMany(t => t
                .GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic
                            | BindingFlags.DeclaredOnly)
                .Where(m => !IsCompilerGenerated(m))
                .SelectMany(m => m.GetParameters()
                    .Where(p => IsCollaborator(p.ParameterType))
                    .Select(p => $"{t.FullName}.{m.Name}({p.ParameterType.Name} {p.Name})")))
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();

        offenders.Should().BeEmpty(
            "a static that needs a collaborator is a service without a constructor: it "
            + "cannot be substituted, and every caller has to know what to hand it.\n  "
            + string.Join("\n  ", offenders));
    }

    [Fact]
    public void EveryAllowlistEntry_StillDescribesAStaticClassWithState()
    {
        var stale = DataTableHolders
            .Where(t => !IsStaticClass(t) || StateFields(t).Count == 0)
            .Select(t => t.FullName!)
            .ToList();

        stale.Should().BeEmpty(
            "an allowlist that outlives what it excuses stops being a classification "
            + "and becomes a place to hide. Remove the entry.\n  "
            + string.Join("\n  ", stale));
    }

    [Fact]
    public void Sanity_TheScanReachesTheProductionAssemblies()
    {
        Assemblies().Should().HaveCount(TargetAssemblies.Length,
            "an unloaded assembly would silently pass the rule");
        StaticClasses().Should().HaveCountGreaterThan(50, "the scan must reach a meaningful slice");
    }

    [Fact]
    public void Rule_HasTeeth_ASyntheticStaticHoldingACollaboratorIsFlagged()
    {
        var holder = typeof(SyntheticStaticHolder);

        StateFields(holder).Should().ContainSingle(f => f.Name == nameof(SyntheticStaticHolder.Collaborator));
        IsCollaborator(typeof(ISerializerLike)).Should().BeTrue();
        IsCollaborator(typeof(IReadOnlyList<string>)).Should().BeFalse("data is not a dependency");
    }

    // Proves the rule bites without waiting for someone to write the violation.
    internal static class SyntheticStaticHolder
    {
        internal static readonly DerivedPhaseYamlRenderer Collaborator = new();
    }

    internal interface ISerializerLike;

    private static IReadOnlyList<FieldInfo> StateFields(Type t) =>
        [.. t.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic
                        | BindingFlags.DeclaredOnly)
            // Literals are compile-time constants — they are not state, they are the
            // source text. Compiler-generated caches are the compiler's business.
            .Where(f => !f.IsLiteral && !IsCompilerGenerated(f))];

    private static IReadOnlyList<Type> StaticClasses() =>
        [.. Assemblies().SelectMany(a => a.GetTypes()).Where(IsStaticClass)];

    // Wiring is the exception the rule is built around: a static that takes the
    // composition root runs once at startup and hands services to OTHERS. A static
    // that takes a runtime collaborator uses one itself — that is the smell.
    private static readonly HashSet<string> CompositionRoots = new(StringComparer.Ordinal)
    {
        "IServiceCollection", "IServiceProvider", "IApplicationBuilder", "IHostBuilder",
        "IHostApplicationBuilder", "WebApplication", "IEndpointRouteBuilder",
    };

    // An interface that carries DATA is a parameter; an interface that DOES something
    // is a dependency. Collection contracts and the logger are the two ends of that line.
    private static bool IsCollaborator(Type t) =>
        !CompositionRoots.Contains(t.Name)
        && ((t.IsInterface && !IsDataContract(t)) || t.Name is "ILogger" or "ILoggerFactory");

    private static bool IsDataContract(Type t) =>
        t.Namespace?.StartsWith("System.Collections", StringComparison.Ordinal) == true
        || t.Namespace?.StartsWith("System.Threading", StringComparison.Ordinal) == true
        || t.Name is "IComparable" or "IFormattable" or "IConvertible" or "ISpanFormattable";

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
