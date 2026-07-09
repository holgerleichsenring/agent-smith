namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>
/// Operator-overridable Jira REST endpoint templates. Defaults target Jira Cloud
/// REST v3. Every path is overridable from the tracker config so an Atlassian API
/// change — e.g. CHANGE-2046 removing <c>/rest/api/3/search</c> (now 410 Gone) in
/// favour of <c>/rest/api/3/search/jql</c> — is a YAML edit, not a code change +
/// redeploy. No Jira path is hardwired beyond these defaults.
/// <para>
/// <c>{id}</c> in a template is replaced with the issue key. Paths are relative to
/// the tracker base URL. Each field deserialises independently, so overriding one
/// (e.g. <c>search:</c>) leaves the others on their defaults.
/// </para>
/// </summary>
public sealed record JiraEndpoints
{
    public string Search { get; init; } = "/rest/api/3/search/jql";
    public string Issue { get; init; } = "/rest/api/3/issue/{id}";
    public string Comment { get; init; } = "/rest/api/3/issue/{id}/comment";
    public string Transitions { get; init; } = "/rest/api/3/issue/{id}/transitions";

    /// <summary>Issue collection path (POST = create). No <c>{id}</c> — it addresses the collection.</summary>
    public string Create { get; init; } = "/rest/api/3/issue";

    public string IssueFor(string key) => Issue.Replace("{id}", key);
    public string CommentFor(string key) => Comment.Replace("{id}", key);
    public string TransitionsFor(string key) => Transitions.Replace("{id}", key);
}
