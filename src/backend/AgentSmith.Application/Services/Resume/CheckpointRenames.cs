using System.Reflection;
using System.Text.RegularExpressions;
using AgentSmith.Contracts.Commands;

namespace AgentSmith.Application.Services.Resume;

/// <summary>
/// 2026-10-12-967aa: the context keys and type names a checkpoint entry may carry under the
/// spec rename, as pairs of (phase name, spec name). Consulted in BOTH directions: whichever side
/// this build declares is the one an entry is restored under, so the same table serves the release
/// that still declares the phase names and the one that declares the spec names.
/// <para>
/// A checkpoint names its entries by <see cref="ContextKeys"/> value and its values by
/// assembly-qualified type name, and an entry whose type does not resolve is skipped. A parked run
/// written by a newer replica and resumed by an older one would otherwise lose its spec silently.
/// </para>
/// </summary>
internal static partial class CheckpointRenames
{
    private static readonly (string Phase, string Spec)[] ContextKeyPairs =
    [
        ("PhaseSpec", "CurrentSpec"),
        ("PhaseStartHeads", "SpecStartHeads"),
        ("PhaseReviewFindings", "SpecReviewFindings"),
        ("PhaseReviewFixPass", "SpecReviewFixPass"),
        ("PhaseReviewReverted", "SpecReviewReverted"),
        ("PhaseAccountsSnapshot", "SpecAccountsSnapshot"),
        ("RunPhaseReviews", "RunSpecReviews"),
        ("PhaseCommands", "SpecRunCommands"),
        ("PhaseAccounts", "SpecAccounts"),
        ("PhaseRepairAttempted", "SpecRepairAttempted"),
        ("PhaseAppliesTo", "SpecAppliesTo"),
        ("ActivePhaseStep", "ActiveStep"),
    ];

    private static readonly (string Phase, string Spec)[] TypeNamePairs =
    [
        ("AgentSmith.Contracts.Models.PhaseDraft", "AgentSmith.Contracts.Models.SpecDraft"),
        ("AgentSmith.Contracts.Models.PhaseStep", "AgentSmith.Contracts.Models.SpecStep"),
        ("AgentSmith.Contracts.Models.PhaseFact", "AgentSmith.Contracts.Models.SpecFact"),
        ("AgentSmith.Contracts.Specs.SpecPhase", "AgentSmith.Contracts.Specs.DerivedSpec"),
        ("AgentSmith.Contracts.Specs.PhaseProgress", "AgentSmith.Contracts.Specs.SpecProgress"),
        ("AgentSmith.Contracts.Specs.PhaseRunState", "AgentSmith.Contracts.Specs.SpecRunState"),
        ("AgentSmith.Application.Services.Specs.PhaseCommandLog", "AgentSmith.Application.Services.Specs.SpecRunCommandLog"),
        ("AgentSmith.Application.Services.Specs.PhaseCommandEntry", "AgentSmith.Application.Services.Specs.SpecRunCommandEntry"),
        ("AgentSmith.Domain.Models.RunPhaseReviews", "AgentSmith.Domain.Models.RunSpecReviews"),
        ("AgentSmith.Domain.Models.PhaseReviewEntry", "AgentSmith.Domain.Models.SpecReviewEntry"),
        ("AgentSmith.Domain.Models.PhaseReviewReport", "AgentSmith.Domain.Models.SpecReviewReport"),
        ("AgentSmith.Domain.Models.PhaseFinding", "AgentSmith.Domain.Models.SpecFinding"),
    ];

    private static readonly HashSet<string> DeclaredKeys = typeof(ContextKeys)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(f => f.IsLiteral && f.FieldType == typeof(string))
        .Select(f => (string)f.GetRawConstantValue()!)
        .ToHashSet(StringComparer.Ordinal);

    /// <summary>The key an entry is restored under: as stored, or its declared counterpart.</summary>
    public static string Key(string stored)
    {
        if (DeclaredKeys.Contains(stored)) return stored;
        var counterpart = Counterpart(ContextKeyPairs, stored);
        return counterpart is not null && DeclaredKeys.Contains(counterpart) ? counterpart : stored;
    }

    /// <summary>The type the name resolves to here — as written, or with every renamed type swapped.</summary>
    public static Type? Resolve(string assemblyQualifiedName) =>
        Type.GetType(assemblyQualifiedName, throwOnError: false)
        ?? Type.GetType(Swap(assemblyQualifiedName, toSpec: true), throwOnError: false)
        ?? Type.GetType(Swap(assemblyQualifiedName, toSpec: false), throwOnError: false);

    private static string? Counterpart((string Phase, string Spec)[] pairs, string name)
    {
        foreach (var (phase, spec) in pairs)
        {
            if (name == phase) return spec;
            if (name == spec) return phase;
        }
        return null;
    }

    private static string Swap(string name, bool toSpec) =>
        FullTypeName().Replace(name, match =>
            TypeNamePairs.FirstOrDefault(p => (toSpec ? p.Phase : p.Spec) == match.Value) is { Phase: not null } pair
                ? (toSpec ? pair.Spec : pair.Phase)
                : match.Value);

    // A dotted CLR full name, delimited by anything that is not part of one ('[', ',', ']', '`', '+').
    [GeneratedRegex(@"[A-Za-z_][\w.]*")]
    private static partial Regex FullTypeName();
}
