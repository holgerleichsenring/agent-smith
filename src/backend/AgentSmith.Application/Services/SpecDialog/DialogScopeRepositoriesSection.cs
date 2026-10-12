using System.Text;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Commands;

namespace AgentSmith.Application.Services.SpecDialog;

/// <summary>
/// 2026-10-12-f5f7a: tells the design dialog which repositories are in scope and that a spec it
/// proposes names exactly one of them — the same rule <see cref="DialogDraftRepos"/> checks, said
/// first so a binary ahead of the skills release asks too. A scope of one or none renders nothing:
/// there is nothing to choose, and the one repository is filled in.
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
        sb.AppendLine(
            "A spec you propose names in `repos` EXACTLY ONE of these repositories — the one whose code it "
            + "changes, spelled as listed. Work that changes two of them is proposed as an epic, one child "
            + "per repository, the child that depends on the other second.");
        foreach (var name in scope) sb.AppendLine($"- {name}");
        return sb.ToString();
    }
}
