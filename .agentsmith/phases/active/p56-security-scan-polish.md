# Phase 56: Security Scan Polish

## Goal

Close the remaining gaps from the p54 security scan expansion:
secret provider identification, mandatory false-positive filtering,
severity breakdown in logs, SARIF output for security-scan, and
documentation updates.

## Items

### 1. Secret Provider Identification

Map pattern IDs to provider names and revoke URLs. When a secret is found
(static scan or git history), the finding includes actionable information:

```
CRITICAL: GitHub Personal Access Token found in git history
  Provider: GitHub
  Revoke: https://github.com/settings/tokens
  File: src/Config.cs (commit a1b2c3d, deleted from HEAD)
```

Implementation:
- New `SecretProviderRegistry` with static mapping:

```csharp
Dictionary<string, SecretProvider> Providers = {
    ["aws-access-key"] = ("AWS", "https://console.aws.amazon.com/iam/home#/security_credentials"),
    ["github-token"] = ("GitHub", "https://github.com/settings/tokens"),
    ["github-oauth"] = ("GitHub", "https://github.com/settings/tokens"),
    ["stripe-live-key"] = ("Stripe", "https://dashboard.stripe.com/apikeys"),
    ["stripe-restricted"] = ("Stripe", "https://dashboard.stripe.com/apikeys"),
    ["slack-token"] = ("Slack", "https://api.slack.com/apps"),
    ["discord-token"] = ("Discord", "https://discord.com/developers/applications"),
    ["twilio-api-key"] = ("Twilio", "https://console.twilio.com/us1/account/keys-credentials/api-keys"),
    ["sendgrid-api-key"] = ("SendGrid", "https://app.sendgrid.com/settings/api_keys"),
    ["openai-api-key"] = ("OpenAI", "https://platform.openai.com/api-keys"),
    ["google-api-key"] = ("Google Cloud", "https://console.cloud.google.com/apis/credentials"),
    ["npm-token"] = ("npm", "https://www.npmjs.com/settings/~/tokens"),
    ["pypi-token"] = ("PyPI", "https://pypi.org/manage/account/#api-tokens"),
};
```

- `PatternFinding` and `HistoryFinding` gain optional `Provider` and `RevokeUrl` fields
- `StaticPatternScanner` and `GitHistoryScanner` enrich findings via the registry
- No live API probing (ship-safe's approach) — too risky for automated scans.
  Provider + revoke URL is sufficient for the developer to act.

### 2. Mandatory False-Positive Filter

The `false-positive-filter` skill must always participate in security-scan,
regardless of triage decisions. Currently it has `triggers: [always_include]`
but the triage handler doesn't recognize this as a special signal.

Fix:
- `SecurityTriageHandler`: after triage selects participants, check if
  `false-positive-filter` is in the available roles. If yes and not already
  selected, append it to the participant list.
- This ensures FP filtering runs on every security scan, even if triage
  doesn't explicitly select it.
- Same pattern for api-scan's `ApiSecurityTriageHandler`.

### 3. Severity Breakdown in Logs

The `StaticPatternScanHandler` currently logs:
```
Static scan: 271 findings in 647 files (91 patterns)
```

Should log:
```
Static scan: 271 findings in 647 files (91 patterns) — 3 critical, 20 high, 51 medium, 197 low
```

Same for `GitHistoryScanHandler`:
```
Git history scan: 15 secrets in 177 commits — 3 critical (history-only), 12 high
```

Implementation: simple LINQ grouping on the result findings before logging.

### 4. SARIF Output for security-scan

The security-scan pipeline uses `DeliverOutputCommand` which delegates to
`IDeliverOutputStrategy`. The SARIF strategy exists in `SarifOutputStrategy`
but is registered for `DeliverFindingsCommand` (api-scan pipeline).

Options:
a) Refactor: make security-scan use `DeliverFindingsCommand` instead of
   `DeliverOutputCommand` — requires the compiled discussion to be
   converted to `Finding` records.
b) Add SARIF support to `DeliverOutputCommand` — convert the discussion
   markdown + raw findings into SARIF format.

Option (a) is cleaner. The `CompileDiscussionHandler` already produces
structured output. Add a step that extracts `Finding` records from the
discussion log and static scan results, stores them in the pipeline,
and lets `DeliverFindingsCommand` handle the rest.

New command: `ExtractFindingsCommand` runs after CompileDiscussion,
before DeliverOutput. Parses the LLM discussion output + raw static
findings into `IReadOnlyList<Finding>` for SARIF/markdown output.

Updated pipeline:
```
... → CompileDiscussion → ExtractFindings → DeliverFindings
```

This replaces `DeliverOutput` with `DeliverFindings` in the security-scan
pipeline, unifying the output path with api-scan.

### 5. Documentation Updates

Update the docs site (docs.agent-smith.org) to reflect p54 + p55 + p56:

- `docs/pipelines/security-scan.md` — document the 3 new tool commands,
  9 security skills, static pattern scanning, git history scanning,
  dependency auditing, SARIF output
- `docs/configuration/tools.md` — add pattern YAML reference
  (config/patterns/*.yaml format)
- `docs/concepts/pipeline-system.md` — update security-scan pipeline
  diagram with new commands
- `docs/cicd/` — add SARIF upload examples for security-scan
  (currently only api-scan has them)

## Files to Create

- `AgentSmith.Infrastructure/Services/Security/SecretProviderRegistry.cs`
- `AgentSmith.Contracts/Models/SecretProvider.cs`
- `AgentSmith.Application/Services/Handlers/ExtractFindingsHandler.cs`
- `AgentSmith.Application/Models/ExtractFindingsContext.cs`
- `AgentSmith.Application/Services/Builders/ExtractFindingsContextBuilder.cs`

## Files to Modify

- `AgentSmith.Contracts/Commands/CommandNames.cs` — add ExtractFindings
- `AgentSmith.Contracts/Commands/PipelinePresets.cs` — update security-scan
- `AgentSmith.Contracts/Models/PatternFinding.cs` — add Provider, RevokeUrl
- `AgentSmith.Contracts/Models/HistoryFinding.cs` — add Provider, RevokeUrl
- `AgentSmith.Infrastructure/Services/Security/StaticPatternScanner.cs` — enrich with provider
- `AgentSmith.Infrastructure/Services/Security/GitHistoryScanner.cs` — enrich with provider
- `AgentSmith.Application/Services/Handlers/StaticPatternScanHandler.cs` — severity log
- `AgentSmith.Application/Services/Handlers/GitHistoryScanHandler.cs` — severity log
- `AgentSmith.Application/Services/Handlers/SecurityTriageHandler.cs` — mandatory FP filter
- `AgentSmith.Application/Services/Handlers/ApiSecurityTriageHandler.cs` — mandatory FP filter
- `AgentSmith.Application/Extensions/ServiceCollectionExtensions.cs` — register new handler
- `docs/pipelines/security-scan.md` — full rewrite
- `docs/configuration/tools.md` — add patterns reference
- `docs/concepts/pipeline-system.md` — update diagram
- `docs/cicd/azure-devops.md` — add security-scan SARIF example
- `docs/cicd/github-actions.md` — add security-scan SARIF example
- `docs/cicd/gitlab-ci.md` — add security-scan SARIF example

## Definition of Done

- [ ] Secret findings include provider name and revoke URL
- [ ] SecretProviderRegistry covers all 13+ secret patterns
- [ ] false-positive-filter always participates in security-scan
- [ ] false-positive-filter always participates in api-security-scan
- [ ] StaticPatternScanHandler logs severity breakdown
- [ ] GitHistoryScanHandler logs severity breakdown
- [ ] security-scan pipeline produces SARIF output via --output sarif
- [ ] ExtractFindingsHandler converts discussion + raw findings to Finding records
- [ ] security-scan and api-scan share the same DeliverFindings output path
- [ ] Docs updated for security-scan pipeline, patterns config, CI/CD SARIF
- [ ] All existing tests pass
- [ ] New tests for SecretProviderRegistry, ExtractFindingsHandler

## Dependencies

- p54 (security scan expansion) — complete
- p55 (findings compression) — can be done in parallel
