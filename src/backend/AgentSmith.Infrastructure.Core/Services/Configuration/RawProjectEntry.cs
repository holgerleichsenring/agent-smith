using System.Text.Json.Serialization;
using AgentSmith.Contracts.Models.Configuration;
using YamlDotNet.Serialization;

namespace AgentSmith.Infrastructure.Core.Services.Configuration;

/// <summary>
/// Raw YAML shape for one entry inside the top-level `projects:` map.
/// Catalog references are still strings here; the resolver materializes them
/// into a <see cref="ResolvedProject"/>.
/// </summary>
public sealed class RawProjectEntry
{
    public string Agent { get; set; } = string.Empty;
    public string Tracker { get; set; } = string.Empty;
    public List<RawRepoRef> Repos { get; set; } = [];

    /// <summary>
    /// 2026-09-13-5fa0: what each context of this project is built after. Sibling to
    /// <see cref="Repos"/> rather than a field on a repo ref — a component has a template
    /// and a repository does not.
    /// </summary>
    public List<RawTemplateEntry> Templates { get; set; } = [];

    /// <summary>2026-10-01-7f7aa: names from the <c>design_sources:</c> catalog.</summary>
    public List<string> DesignSources { get; set; } = [];

    public string Pipeline { get; set; } = string.Empty;
    public List<RawPipelineEntry> Pipelines { get; set; } = [];
    public string? DefaultPipeline { get; set; }

    public string? CodingPrinciplesPath { get; set; }
    public string SkillsPath { get; set; } = "skills";

    /// <summary>
    /// p0281b: flat resolution shorthand — one entry whose KEY is the strategy
    /// (tag | area_path | repo | to_address) and whose value is the match. Combined
    /// with the tracker's type it produces the effective trigger block.
    /// </summary>
    public Dictionary<string, string>? Resolution { get; set; }

    // 2026-10-29-6bd3a: the per-project trigger blocks as written, under their YAML and document keys.
    // Read for LegacyTriggerFold only, which moves them into Resolution and the tracker and clears them.
    [YamlMember(Alias = "jira_trigger")] [JsonPropertyName("JiraTrigger")]
    public LegacyJiraTriggerBlock? LegacyJiraTrigger { get; set; }
    [YamlMember(Alias = "github_trigger")] [JsonPropertyName("GithubTrigger")]
    public LegacyTriggerBlock? LegacyGithubTrigger { get; set; }
    [YamlMember(Alias = "gitlab_trigger")] [JsonPropertyName("GitlabTrigger")]
    public LegacyTriggerBlock? LegacyGitlabTrigger { get; set; }
    [YamlMember(Alias = "azuredevops_trigger")] [JsonPropertyName("AzuredevopsTrigger")]
    public LegacyTriggerBlock? LegacyAzuredevopsTrigger { get; set; }

    // The EFFECTIVE trigger EffectiveTriggerBuilder builds from tracker and resolution — never
    // written, so no document or export carries a block again.
    [YamlIgnore] [JsonIgnore] public JiraTriggerConfig? JiraTrigger { get; set; }
    [YamlIgnore] [JsonIgnore] public WebhookTriggerConfig? GithubTrigger { get; set; }
    [YamlIgnore] [JsonIgnore] public WebhookTriggerConfig? GitlabTrigger { get; set; }
    [YamlIgnore] [JsonIgnore] public WebhookTriggerConfig? AzuredevopsTrigger { get; set; }

    public PollingConfig Polling { get; set; } = new();
    public SandboxConfig? Sandbox { get; set; }
}
