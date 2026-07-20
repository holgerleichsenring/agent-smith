using System;
using System.Linq;

namespace AgentSmith.Application.Services;

/// <summary>
/// p0355: classifies a ledger step as READ-ONLY by its leading verb — a step
/// that inspects (audit / analyze / verify / …) but never mutates. Such a step
/// legitimately leaves its target untouched, so the keystone exempts a DONE
/// read-only step from the target-in-diff cross-check: "target absent from the
/// diff" is correct behaviour there, not an unbacked done. Deciding at
/// check time (instead of a ledger flag) keeps the exemption working for BOTH
/// seeded and master-authored ledgers without a contract change. Pure and
/// deterministic, like the keystone that consumes it.
/// </summary>
public static class ReadOnlyStepClassifier
{
    private static readonly string[] ReadOnlyVerbs =
    [
        "audit", "analyze", "analyse", "assess", "check", "confirm", "examine",
        "identify", "inspect", "investigate", "read", "review", "verify",
    ];

    public static bool IsReadOnly(string activity)
    {
        var verb = LeadingWord(activity);
        return ReadOnlyVerbs.Any(v => verb.Equals(v, StringComparison.OrdinalIgnoreCase));
    }

    private static string LeadingWord(string text)
    {
        var trimmed = text.TrimStart();
        var end = trimmed.IndexOfAny([' ', '\t', ':', '/']);
        return end < 0 ? trimmed : trimmed[..end];
    }
}
