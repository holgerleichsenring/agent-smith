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
    /// <param name="mayGainAppliesTo">2026-11-19-6b0ba: a stand-in states no applies_to; the spec its master writes may.</param>
    public static bool Holds(SpecDraft original, SpecDraft revised, bool mayGainAppliesTo = false)
    {
        ArgumentNullException.ThrowIfNull(original);
        ArgumentNullException.ThrowIfNull(revised);
        try
        {
            var before = OutcomeYamlReader.ReadMap(original.Yaml);
            var after = OutcomeYamlReader.ReadMap(revised.Yaml);
            var gains = mayGainAppliesTo && before.GetValueOrDefault("applies_to") is null;
            return new[] { "spec", "requires", "repos", "applies_to" }.Where(key => !(gains && key == "applies_to"))
                .All(key => SpecRevisionEdit.Equal(before.GetValueOrDefault(key), after.GetValueOrDefault(key)));
        }
        catch (YamlDotNet.Core.YamlException)
        {
            return false;
        }
    }
}
