using AgentSmith.Contracts.Services;
using AgentSmith.Tests.Prompts;

namespace AgentSmith.PipelineHarness.Evals;

/// <summary>
/// p0505: IPromptCatalog over the PINNED catalog's master bodies. Production
/// resolves prompt names through PromptOwnership + SkillCatalogPromptCatalog,
/// whose eight-dependency loader chain is not what an eval is measuring; the body
/// it ends up serving is the master SKILL.md this reads, so the eval reads that
/// directly and stays honest about the one difference — no @-reference resolution.
/// </summary>
internal sealed class PackagedMasterPromptCatalog(string promptName, string masterSkillName)
    : IPromptCatalog
{
    public string Get(string name) => name == promptName
        ? PackagedMaster.Read(masterSkillName)
        : throw new InvalidOperationException($"the eval serves only '{promptName}', not '{name}'");

    public string Render(string name, IReadOnlyDictionary<string, string> tokens) =>
        tokens.Aggregate(Get(name), (body, t) => body.Replace($"{{{t.Key}}}", t.Value));
}
