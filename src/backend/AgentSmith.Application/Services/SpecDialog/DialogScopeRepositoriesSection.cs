using System.Text;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Commands;

namespace AgentSmith.Application.Services.SpecDialog;

/// <summary>
/// 2026-10-12-f5f7a: tells the design dialog which repositories are in scope and that a spec it
/// proposes names exactly one of them — the same rule <see cref="DialogDraftRepos"/> checks, said
/// first so a binary ahead of the skills release asks too. A scope of one or none renders nothing:
/// there is nothing to choose, and the one repository is filled in.
/// 2026-11-10-14a3e: the rule itself is the skill's; repeated here in capitals it read as "one child per
/// repository". The section lists the names a spec's `repos` is spelled from.
/// </summary>
public static class DialogScopeRepositoriesSection
{
    public static string Render(PipelineContext pipeline)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        var scope = DerivationLookFactory.ProposalRepositories(pipeline)?.Keys.ToList() ?? [];
        if (scope.Count < 2) return string.Empty;
        var sb = new StringBuilder();
        sb.AppendLine();
        sb.AppendLine("## Repositories in scope");
        sb.AppendLine("A spec's `repos` names one of these, spelled as listed:");
        foreach (var name in scope) sb.AppendLine($"- {name}");
        return sb.ToString();
    }
}
