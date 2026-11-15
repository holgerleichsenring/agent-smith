using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Models;
using YamlDotNet.Core;

namespace AgentSmith.Application.Services.SpecDialog.Writes;

/// <summary>2026-11-15-06fc: the draft's bug as its file <c>bug.yaml</c> — title, description, acceptance_criteria — and back.</summary>
public static class DraftBugText
{
    public static string Render(BugTicketDraft bug)
    {
        ArgumentNullException.ThrowIfNull(bug);
        var map = new Dictionary<string, object> { ["title"] = bug.Title, ["description"] = bug.Description };
        var criteria = (bug.AcceptanceCriteria ?? string.Empty).Split('\n')
            .Select(CriterionLine.StripMarker).Where(line => line.Length > 0).ToList();
        if (criteria.Count > 0) map["acceptance_criteria"] = criteria;
        return OutcomeYamlReader.ToYaml(map);
    }

    /// <summary>The fields the bug check takes, or why the file cannot be read.</summary>
    public static (string? Title, string? Description, IReadOnlyList<string> Criteria, string? Error) Read(string text)
    {
        try
        {
            var map = OutcomeYamlReader.ReadMap(text ?? string.Empty);
            return (OutcomeYamlReader.GetString(map, "title"), OutcomeYamlReader.GetString(map, "description"),
                SpecDraftLists.Strings(map, "acceptance_criteria"), null);
        }
        catch (YamlException ex)
        {
            return (null, null, [], $"the bug is not a YAML mapping of title, description, acceptance_criteria: {ex.Message}");
        }
    }
}
