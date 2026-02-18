# Phase 1 - Schritt 4: Configuration

## Ziel
YAML-basierte Konfiguration laden und als stark typisierte Objekte bereitstellen.
Einzige echte Implementierung in Phase 1.

---

## Config Models

Alle in `AgentSmith.Contracts/Configuration/` (werden von mehreren Layern gebraucht).

### AgentSmithConfig
```
Datei: src/AgentSmith.Contracts/Configuration/AgentSmithConfig.cs
```
- `Dictionary<string, ProjectConfig> Projects`
- `Dictionary<string, PipelineConfig> Pipelines`
- `Dictionary<string, string> Secrets`

### ProjectConfig
```
Datei: src/AgentSmith.Contracts/Configuration/ProjectConfig.cs
```
- `SourceConfig Source`
- `TicketConfig Tickets`
- `AgentConfig Agent`
- `string Pipeline` (Name der Pipeline-Definition)
- `string? CodingPrinciplesPath`

### SourceConfig
```
Datei: src/AgentSmith.Contracts/Configuration/SourceConfig.cs
```
- `string Type` (GitHub, GitLab, AzureRepos, Local)
- `string? Url`
- `string? Path` (für Local)
- `string Auth` (token, ssh)

### TicketConfig
```
Datei: src/AgentSmith.Contracts/Configuration/TicketConfig.cs
```
- `string Type` (AzureDevOps, Jira, GitHub)
- `string? Organization`
- `string? Project`
- `string? Url`
- `string Auth` (token)

### AgentConfig
```
Datei: src/AgentSmith.Contracts/Configuration/AgentConfig.cs
```
- `string Type` (Claude, OpenAI)
- `string Model` (z.B. sonnet-4, gpt-4o)

### PipelineConfig
```
Datei: src/AgentSmith.Contracts/Configuration/PipelineConfig.cs
```
- `List<string> Commands` (Command-Klassennamen)

---

## Implementierung: YamlConfigurationLoader

```
Datei: src/AgentSmith.Infrastructure/Configuration/YamlConfigurationLoader.cs
```
Projekt: `AgentSmith.Infrastructure`

**Verantwortung:**
- Implementiert `IConfigurationLoader`
- Liest YAML-Datei von Dateipfad
- Deserialisiert zu `AgentSmithConfig`
- Löst `${ENV_VAR}` Platzhalter in Secrets auf
- Wirft `ConfigurationException` bei Fehlern

**Methoden:**
- `AgentSmithConfig LoadConfig(string configPath)` (aus Interface)
- Private: `string ResolveEnvironmentVariables(string value)` - ersetzt `${VAR}` mit `Environment.GetEnvironmentVariable`

**Verhalten:**
- Datei nicht gefunden → `ConfigurationException`
- YAML ungültig → `ConfigurationException`
- Environment Variable nicht gesetzt → Wert bleibt leer (kein Fehler, wird erst bei Nutzung validiert)

---

## Beispiel Config

```
Datei: config/agentsmith.yml
```

```yaml
projects:
  payslip:
    source:
      type: GitHub
      url: https://github.com/user/payslip
      auth: token
    tickets:
      type: AzureDevOps
      organization: myorg
      project: PayslipProject
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
Datei: config/coding-principles.md
```

Wird aus `prompts/coding-principles.md` kopiert.
Diese Datei ist die, die der Agent zur Laufzeit lädt und an das LLM schickt.

---

## Unit Tests

```
Datei: tests/AgentSmith.Tests/Configuration/YamlConfigurationLoaderTests.cs
```

**Testfälle:**
- `LoadConfig_ValidYaml_ReturnsConfig` - Happy Path
- `LoadConfig_FileNotFound_ThrowsConfigurationException`
- `LoadConfig_InvalidYaml_ThrowsConfigurationException`
- `LoadConfig_WithEnvVars_ResolvesPlaceholders`
- `LoadConfig_ProjectHasAllFields_MapsCorrectly`
- `LoadConfig_PipelineHasCommands_MapsCorrectly`

**Testdaten:**
- Erstelle Test-YAML Dateien unter `tests/AgentSmith.Tests/Configuration/TestData/`
- `valid-config.yml` - vollständige gültige Config
- `invalid-config.yml` - kaputtes YAML

---

## Verzeichnisstruktur nach Schritt 4

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

## Hinweise

- Config Models brauchen parameterlose Konstruktoren (YamlDotNet Deserialisierung).
- Properties mit `{ get; set; }` (nicht `init`, wegen Deserialisierung).
- YAML Property-Namen in snake_case, C# Properties in PascalCase → YamlDotNet `NamingConvention` konfigurieren.
- Secrets-Auflösung ist bewusst lazy: nicht gesetzte Env-Vars sind kein Fehler beim Laden.
