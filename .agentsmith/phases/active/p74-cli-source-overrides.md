# Phase 74: CLI Source Overrides (Refactoring)

## Goal

Make source configuration (type, url, auth) overridable via CLI arguments
across all commands that use source checkout. Follows the env-var pattern:
CLI arguments override config file values field-by-field.

---

## The Problem

The `--repo` flag on `security-scan` stores a path in `ScanRepoPath` context,
but `CheckoutSourceContextBuilder` always uses `project.Source` from the config
file. The CLI value is ignored, causing errors when the config has no source
defined.

There is no consistent way to override source settings from the CLI across
commands.

## The Solution

1. **Three new CLI options** added to commands that use source checkout:
   - `--source-type` (local, github, gitlab, azurerepos)
   - `--source-url` (path or URL)
   - `--source-auth` (auth method)

2. **`ISourceConfigOverrider`** — DI service with one method that merges
   CLI-provided values into `ProjectConfig.Source` before pipeline execution.

3. **Applied in `ExecutePipelineUseCase`** after config loading, before
   pipeline execution — transparent to all downstream builders and handlers.

4. **Commands with source options:** fix, feature, mad, security-scan,
   autonomous, init (via run).

5. **Commands without:** api-scan, legal, compile-wiki, security-trend, server.

6. **SecurityScanCommand** drops `--repo` in favor of the three generic options.
   `ScanRepoPath` context key becomes obsolete.

---

## Implementation

### Step 1: Contracts

- Add `SourceType`, `SourceUrl`, `SourceAuth` to `ContextKeys.cs`
- Add `ISourceConfigOverrider` interface in `Contracts/Services/`

### Step 2: Application Service

- `SourceConfigOverrider` in `Application/Services/`
- One method: `Apply(ProjectConfig, PipelineContext)`
- Field-by-field merge: if context key is set, it wins over config

### Step 3: CLI Shared Options

- Helper in `Cli/Commands/` to create the three options consistently
- Wire into fix, feature, mad, security-scan, autonomous, init commands

### Step 4: Integration

- Call `ISourceConfigOverrider.Apply()` in `ExecutePipelineUseCase`
- Refactor `SecurityScanCommand`: replace `--repo` with generic options
- Remove `ScanRepoPath` from `ContextKeys` and all usages

### Step 5: Verify

- `dotnet build` clean
- `dotnet test` all pass
- Manual test: `agent-smith security-scan --source-type local --source-url .`

---

## Coding Principles (apply throughout)

- No static service classes — instance + DI
- One type per file
- Max 120 lines per class, max 20 lines per method
- Interface for every injectable service
- Composition over inheritance

## Constraints

- **No behavior changes.** Existing config-only usage must work unchanged.
- **Backward compatible.** If no CLI overrides are provided, behavior is identical.

---

## Definition of Done

- [ ] `--source-type`, `--source-url`, `--source-auth` available on source-using commands
- [ ] CLI values override config values field-by-field
- [ ] `--repo` removed from SecurityScanCommand
- [ ] `ScanRepoPath` removed
- [ ] `ISourceConfigOverrider` with DI registration
- [ ] `dotnet build` + `dotnet test` clean
