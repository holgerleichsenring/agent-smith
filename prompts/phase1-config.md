# Phase 1 - Step 4: Configuration

## Goal
Load YAML-based configuration and provide it as strongly typed objects.
Only real implementation in Phase 1.

---

## Config Models

All in `AgentSmith.Contracts/Configuration/` (needed by multiple layers).

### AgentSmithConfig
```
File: src/AgentSmith.Contracts/Configuration/AgentSmithConfig.cs
```
- `Dictionary<string, ProjectConfig> Projects`
- `Dictionary<string, PipelineConfig> Pipelines`
- `Dictionary<string, string> Secrets`

### ProjectConfig
```
File: src/AgentSmith.Contracts/Configuration/ProjectConfig.cs
```
- `SourceConfig Source`
- `TicketConfig Tickets`
- `AgentConfig Agent`
- `string Pipeline` (name of the pipeline definition)
- `string? CodingPrinciplesPath`

### SourceConfig
```
File: src/AgentSmith.Contracts/Configuration/SourceConfig.cs
```
- `string Type` (GitHub, GitLab, AzureRepos, Local)
- `string? Url`
- `string? Path` (for Local)
- `string Auth` (token, ssh)

### TicketConfig
```
File: src/AgentSmith.Contracts/Configuration/TicketConfig.cs
```
- `string Type` (AzureDevOps, Jira, GitHub)
- `string? Organization`
- `string? Project`
- `string? Url`
- `string Auth` (token)

### AgentConfig
```
File: src/AgentSmith.Contracts/Configuration/AgentConfig.cs
```
- `string Type` (Claude, OpenAI)
- `string Model` (e.g. sonnet-4, gpt-4o)

### PipelineConfig
```
File: src/AgentSmith.Contracts/Configuration/PipelineConfig.cs
```
- `List<string> Commands` (command class names)

---

## Implementation: YamlConfigurationLoader

```
File: src/AgentSmith.Infrastructure/Configuration/YamlConfigurationLoader.cs
```
Project: `AgentSmith.Infrastructure`

**Responsibility:**
- Implements `IConfigurationLoader`
- Reads YAML file from file path
- Deserializes to `AgentSmithConfig`
- Resolves `${ENV_VAR}` placeholders in Secrets
- Throws `ConfigurationException` on errors

**Methods:**
- `AgentSmithConfig LoadConfig(string configPath)` (from interface)
- Private: `string ResolveEnvironmentVariables(string value)` - replaces `${VAR}` with `Environment.GetEnvironmentVariable`

**Behavior:**
- File not found → `ConfigurationException`
- Invalid YAML → `ConfigurationException`
- Environment variable not set → value remains empty (no error, validated only at usage time)

---

## Example Config

```
File: config/agentsmith.yml
```

```yaml
projects:
  todo-list:
    source:
      type: GitHub
      url: https://github.com/user/todo-list
      auth: token
    tickets:
      type: AzureDevOps
      organization: myorg
      project: Todo-listProject
      auth: token
    agent:
      type: Claude
      model: sonnet-4
    pipeline: fix-bug
    coding_principles_path: ./config/coding-principles.md

pipelines:
  fix-bug:
    commands:
      - FetchTicketCommand
      - CheckoutSourceCommand
      - LoadCodingPrinciplesCommand
      - AnalyzeCodeCommand
      - GeneratePlanCommand
      - ApprovalCommand
      - AgenticExecuteCommand
      - TestCommand
      - CommitAndPRCommand

  add-feature:
    commands:
      - FetchTicketCommand
      - CheckoutSourceCommand
      - LoadCodingPrinciplesCommand
      - GeneratePlanCommand
      - ApprovalCommand
      - AgenticExecuteCommand
      - GenerateTestsCommand
      - TestCommand
      - GenerateDocsCommand
      - CommitAndPRCommand

secrets:
  azure_devops_token: ${AZURE_DEVOPS_TOKEN}
  github_token: ${GITHUB_TOKEN}
  anthropic_api_key: ${ANTHROPIC_API_KEY}
  openai_api_key: ${OPENAI_API_KEY}
  jira_token: ${JIRA_TOKEN}
  jira_email: ${JIRA_EMAIL}
```

---

## Coding Principles Template

```
File: config/coding-principles.md
```

Copied from `prompts/coding-principles.md`.
This is the file that the agent loads at runtime and sends to the LLM.

---

## Unit Tests

```
File: tests/AgentSmith.Tests/Configuration/YamlConfigurationLoaderTests.cs
```

**Test Cases:**
- `LoadConfig_ValidYaml_ReturnsConfig` - Happy Path
- `LoadConfig_FileNotFound_ThrowsConfigurationException`
- `LoadConfig_InvalidYaml_ThrowsConfigurationException`
- `LoadConfig_WithEnvVars_ResolvesPlaceholders`
- `LoadConfig_ProjectHasAllFields_MapsCorrectly`
- `LoadConfig_PipelineHasCommands_MapsCorrectly`

**Test Data:**
- Create test YAML files under `tests/AgentSmith.Tests/Configuration/TestData/`
- `valid-config.yml` - complete valid config
- `invalid-config.yml` - broken YAML

---

## Directory Structure After Step 4

```
src/AgentSmith.Contracts/Configuration/
├── AgentSmithConfig.cs
├── ProjectConfig.cs
├── SourceConfig.cs
├── TicketConfig.cs
├── AgentConfig.cs
└── PipelineConfig.cs

src/AgentSmith.Infrastructure/Configuration/
└── YamlConfigurationLoader.cs

config/
├── agentsmith.yml
└── coding-principles.md

tests/AgentSmith.Tests/Configuration/
├── YamlConfigurationLoaderTests.cs
└── TestData/
    ├── valid-config.yml
    └── invalid-config.yml
```

## Notes

- Config models need parameterless constructors (YamlDotNet deserialization).
- Properties with `{ get; set; }` (not `init`, due to deserialization).
- YAML property names in snake_case, C# properties in PascalCase → configure YamlDotNet `NamingConvention`.
- Secret resolution is intentionally lazy: unset env vars are not an error during loading.
