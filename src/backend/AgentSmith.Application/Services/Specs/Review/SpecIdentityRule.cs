using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services.Specs.Review;

/// <summary>
/// 2026-10-13-8762g: what a rewrite of a spec may never change — its id, its requires, its repos and its
/// applies_to, compared PARSED so quoting and folding never decide. A rewrite that moves any of them is a
/// different spec in the set's place, and its edges and placement would no longer hold.
/// </summary>
public static class SpecIdentityRule
{
    public static bool Holds(SpecDraft original, SpecDraft revised)
    {
        ArgumentNullException.ThrowIfNull(original);
        ArgumentNullException.ThrowIfNull(revised);
        try
        {
            var before = OutcomeYamlReader.ReadMap(original.Yaml);
            var after = OutcomeYamlReader.ReadMap(revised.Yaml);
            return new[] { "spec", "requires", "repos", "applies_to" }.All(key => SpecRevisionEdit.Equal(before.GetValueOrDefault(key), after.GetValueOrDefault(key)));
        }
        catch (YamlDotNet.Core.YamlException)
        {
            return false;
        }
    }
}
