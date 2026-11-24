using System.Text.RegularExpressions;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Pocs;
using AgentSmith.Contracts.Runs;

namespace AgentSmith.Application.Services.Pocs.Variables;

/// <summary>
/// 2026-11-23-933dg: the ONE rule a PoC variable write is held to, checked by the route and the import before anything
/// is written and again by the run: an environment-variable name of at most the stored name's width, a secret value as
/// long as the masker masks, a value under pocs.variables.max_value_kb, and at most pocs.variables.max_names names once
/// written. Each problem names the value, the reason and the key; none names a value.
/// </summary>
public sealed partial class PocVariableRules
{
    public IReadOnlyList<string> Problems(
        IReadOnlyList<PocVariableEntry> entries, IEnumerable<string> existing, PocVariablesConfig limits)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(limits);
        var problems = entries.SelectMany(entry => EntryProblems(entry, limits)).ToList();
        foreach (var twice in entries.GroupBy(e => e.Name, StringComparer.Ordinal).Where(g => g.Count() > 1))
            problems.Add($"The PoC variable {twice.Key} is named {twice.Count()} times in one write; name it once.");
        var names = Resulting(entries, existing);
        if (names > limits.MaxNames)
            problems.Add($"The PoC would hold {names} variables; {PocVariablesConfig.MaxNamesKey} allows {limits.MaxNames}, "
                + $"because {PocVariablesConfig.Reason}.");
        return problems;
    }

    private static IEnumerable<string> EntryProblems(PocVariableEntry entry, PocVariablesConfig limits)
    {
        if (NameProblem(entry.Name) is { } name) yield return name;
        if (entry.Kind is not (PocVariableKinds.Secret or PocVariableKinds.Plain or PocVariableKinds.Delete))
            yield return $"'{entry.Kind}' is no PoC variable kind (secret, plain, delete).";
        if (entry.Kind == PocVariableKinds.Secret && (entry.Secret?.Length ?? 0) < SecretMasker.MinimumMaskedLength)
            yield return $"The secret PoC variable {entry.Name} must be at least {SecretMasker.MinimumMaskedLength} characters: "
                + "a shorter one would not be masked in what runs record. A value that need not be secret can be plain.";
        if (entry.Kind == PocVariableKinds.Plain && entry.Plain is null)
            yield return $"The plain PoC variable {entry.Name} has no value.";
        if (entry.ValueBytes > limits.MaxValueKb * 1024L)
            yield return $"The PoC variable {entry.Name} is {(entry.ValueBytes + 1023) / 1024} KB; {PocVariablesConfig.MaxValueKbKey} "
                + $"allows {limits.MaxValueKb} KB, because {PocVariablesConfig.Reason}.";
    }

    private static string? NameProblem(string name)
    {
        if (name.Length > SecretScope.NameMaxLength)
            return $"The PoC variable name {name[..20]}… is {name.Length} characters; at most {SecretScope.NameMaxLength} fit, "
                + "the width of the stored secret's name column (StoredSecrets.Name).";
        return VariableName().IsMatch(name) ? null : $"'{name}' is no PoC variable name: it must match ^[A-Z_][A-Z0-9_]*$ "
            + "(upper-case letters, digits and '_', not starting with a digit), because the container reads it as an environment variable.";
    }

    private static int Resulting(IReadOnlyList<PocVariableEntry> entries, IEnumerable<string> existing)
    {
        var names = new HashSet<string>(existing, StringComparer.Ordinal);
        foreach (var entry in entries)
            if (entry.Kind == PocVariableKinds.Delete) names.Remove(entry.Name);
            else names.Add(entry.Name);
        return names.Count;
    }

    [GeneratedRegex("^[A-Z_][A-Z0-9_]*$")]
    private static partial Regex VariableName();
}
