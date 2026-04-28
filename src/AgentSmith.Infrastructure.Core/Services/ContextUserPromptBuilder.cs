using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Infrastructure.Core.Services;

/// <summary>
/// Builds the user prompt for .context.yaml generation from detected project metadata.
/// </summary>
public sealed class ContextUserPromptBuilder(IPromptCatalog prompts)
{
    public string Build(DetectedProject project, string keyFileContents, RepoSnapshot snapshot)
    {
        var detectedYaml = $"""
            language: {project.Language}
            runtime: {project.Runtime ?? "unknown"}
            package_manager: {project.PackageManager ?? "unknown"}
            build_command: {project.BuildCommand ?? "none"}
            test_command: {project.TestCommand ?? "none"}
            frameworks: [{string.Join(", ", project.Frameworks)}]
            infrastructure: [{string.Join(", ", project.Infrastructure)}]
            sdks: [{string.Join(", ", project.Sdks)}]
            """;

        var readmeSection = project.ReadmeExcerpt is not null
            ? $"\n## README (excerpt)\n{project.ReadmeExcerpt}" : "";

        var snapshotSection = BuildSnapshotSection(snapshot);
        const string emptyObj = "{}";
        var qualityTemplate = prompts.Get("context-quality-template");

        return $"""
            ## Detected Stack
            {detectedYaml}

            ## Key Files
            {keyFileContents}
            {readmeSection}

            ## Directory Structure
            {snapshot.DirectoryTree}
            {snapshotSection}

            ## Template
            Use this exact structure for the output. Fill in what you can determine,
            leave out optional sections you cannot determine. Be precise, not verbose.

            ```yaml
            meta:
              project: <repo-name>
              version: 1.0.0
              type: [<archetypes>]
              purpose: "<one sentence>"

            stack:
              runtime: <runtime>
              lang: <language>
              infra: [<infrastructure>]
              testing: [<test-frameworks>]
              sdks: [<significant-sdks>]

            arch:
              style: [<architecture-style>]
              patterns: [<key-patterns>]
              layers:
                - <layer1>
                - <layer2>

            {qualityTemplate}

            state:
              done: {emptyObj}
              active: {emptyObj}
              planned: {emptyObj}
            ```

            Generate the .context.yaml. Return ONLY valid YAML, no explanation.
            """;
    }

    public string BuildSnapshotSection(RepoSnapshot snapshot)
    {
        var lines = new List<string>();
        if (snapshot.ConfigFileContents.Count > 0)
        {
            lines.Add("\n## Config Files (for style and tooling detection)");
            lines.AddRange(snapshot.ConfigFileContents);
        }
        if (snapshot.CodeSamples.Count > 0)
        {
            lines.Add("\n## Code Samples (for style, architecture, and pattern detection)");
            lines.AddRange(snapshot.CodeSamples);
        }
        return string.Join('\n', lines);
    }
}
