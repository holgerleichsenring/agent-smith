# p0160 — doc inventory + decisions

Audit trail for the docs restructure. One line per file in `docs/`, decision and target location.

## Top-level landing

| File | Decision | Target |
|---|---|---|
| `docs/index.md` | Rewrite | `docs/index.md` (overview + methodology + tables) |
| `docs/DESIGN.md` | Keep | Generated from repo-root DESIGN.md (p0159a build step). Stay in nav under Reference. |
| `docs/docker-k8s-troubleshooting.md` | Move | `docs/reference/host-it/docker-k8s-troubleshooting.md` |
| `docs/slack-setup.md` | Move | `docs/reference/setup/slack-setup.md` |
| `docs/run-log-001-first-e2e-test.md` | Move | `docs/reference/run-logs/001-first-e2e-test.md` |
| `docs/run-log-002-azure-devops-docker.md` | Move | `docs/reference/run-logs/002-azure-devops-docker.md` |

## Get it running (rewrite)

| File | Decision | Target |
|---|---|---|
| `docs/getting-started/index.md` | Delete (replaced by index hierarchy) | — |
| `docs/getting-started/installation.md` | Rewrite | `docs/get-it-running/install.md` |
| `docs/getting-started/first-bug-fix.md` | Rewrite | `docs/get-it-running/first-run.md` |
| `docs/getting-started/first-api-scan.md` | Move | `docs/reference/getting-started/first-api-scan.md` |

## Connect your stuff (all new content)

| New file | Origin / sources |
|---|---|
| `docs/connect-your-stuff/tracker-azure-devops.md` | new; absorbs `setup/webhooks/azure-devops.md` content as needed |
| `docs/connect-your-stuff/tracker-jira.md` | new; absorbs `setup/webhooks/jira.md` |
| `docs/connect-your-stuff/tracker-github-issues.md` | new; absorbs `setup/webhooks/github.md` |
| `docs/connect-your-stuff/tracker-gitlab-issues.md` | new; absorbs `setup/webhooks/gitlab.md` |
| `docs/connect-your-stuff/repos-mono.md` | new |
| `docs/connect-your-stuff/repos-multi.md` | new; supersedes `configuration/multi-repo.md` content |
| `docs/connect-your-stuff/ai-providers.md` | new; absorbs `providers/{claude,openai,gemini,ollama,openai-compatible}.md` |

## Trigger it (all new content)

| New file | Origin / sources |
|---|---|
| `docs/trigger-it/webhooks.md` | new; consolidates `setup/webhooks/*` |
| `docs/trigger-it/polling.md` | new; absorbs `setup/polling.md` + `setup/polling-vs-webhooks.md` |
| `docs/trigger-it/labels.md` | new; absorbs `setup/label-triggers.md` |
| `docs/trigger-it/cli.md` | new |

## Host it (all new content)

| New file | Origin / sources |
|---|---|
| `docs/host-it/cli.md` | new; absorbs `deployment/binary.md` |
| `docs/host-it/docker-compose.md` | new; absorbs `deployment/docker.md` |
| `docs/host-it/kubernetes.md` | new; absorbs `deployment/kubernetes.md` |

## How it works (new + moves)

| File | Decision | Target |
|---|---|---|
| `docs/how-it-works/methodology.md` | New | — |
| `docs/how-it-works/lifecycle.md` | New | — |
| `docs/how-it-works/multi-repo.md` | Rename + Holger-voice pass | from `docs/concepts/multi-repo-pipelines.md` |
| `docs/how-it-works/skills-catalog.md` | New (load-bearing — documents the separate `agent-smith-skills` repo + pin strategy) | — |

## Reference (demoted — moved, not rewritten)

| File | Target under `docs/reference/` |
|---|---|
| `docs/concepts/branch-persistence.md` | `reference/concepts/branch-persistence.md` |
| `docs/concepts/context-compaction.md` | `reference/concepts/context-compaction.md` |
| `docs/concepts/cost-tracking.md` | `reference/concepts/cost-tracking.md` |
| `docs/concepts/decisions.md` | `reference/concepts/decisions.md` |
| `docs/concepts/index.md` | `reference/concepts/index.md` |
| `docs/concepts/interactive-dialogue.md` | `reference/concepts/interactive-dialogue.md` |
| `docs/concepts/knowledge-base.md` | `reference/concepts/knowledge-base.md` |
| `docs/concepts/multi-agent-orchestration.md` | `reference/concepts/multi-agent-orchestration.md` |
| `docs/concepts/multi-skill.md` | `reference/concepts/multi-skill.md` |
| `docs/concepts/phases-and-runs.md` | `reference/concepts/phases-and-runs.md` |
| `docs/concepts/pipeline-system.md` | `reference/concepts/pipeline-system.md` |
| `docs/concepts/sandbox-agent.md` | `reference/concepts/sandbox-agent.md` |
| `docs/concepts/sandbox-architecture.md` | `reference/concepts/sandbox-architecture.md` |
| `docs/concepts/self-documentation.md` | `reference/concepts/self-documentation.md` |
| `docs/concepts/ticket-lifecycle.md` | `reference/concepts/ticket-lifecycle.md` |
| `docs/concepts/triage.md` | `reference/concepts/triage.md` |
| `docs/pipelines/*` | `reference/pipelines/*` |
| `docs/architecture/*` | `reference/architecture/*` |
| `docs/configuration/agentsmith-yml.md` | `reference/configuration/agentsmith-yml.md` |
| `docs/configuration/agentsmith-yml-schema.md` | `reference/configuration/agentsmith-yml-schema.md` |
| `docs/configuration/concept-vocabulary.md` | `reference/configuration/concept-vocabulary.md` |
| `docs/configuration/pipeline-cost-cap.md` | `reference/configuration/pipeline-cost-cap.md` |
| `docs/configuration/project-resolution.md` | `reference/configuration/project-resolution.md` |
| `docs/configuration/security-scan.md` | `reference/configuration/security-scan.md` |
| `docs/configuration/skills.md` | `reference/configuration/skills.md` |
| `docs/configuration/skills/migration.md` | `reference/configuration/skills/migration.md` |
| `docs/configuration/tools.md` | `reference/configuration/tools.md` |
| `docs/configuration/webhooks.md` | `reference/configuration/webhooks.md` |
| `docs/configuration/multi-repo.md` | (content moved into connect-your-stuff/repos-multi.md; file deleted) |
| `docs/configuration/index.md` | `reference/configuration/index.md` |
| `docs/security/custom-patterns.md` | `reference/security/custom-patterns.md` |
| `docs/skills/*` | `reference/skills/*` |
| `docs/baselines/api-security-ide-buddy.md` | `reference/baselines/api-security-ide-buddy.md` |
| `docs/cicd/*` | `reference/cicd/*` |
| `docs/deployment/chat-gateway.md` | `reference/host-it/chat-gateway.md` |
| `docs/deployment/index.md` | `reference/host-it/index.md` |
| `docs/operations/*` | `reference/operations/*` |
| `docs/integrations/pr-comments.md` | `reference/integrations/pr-comments.md` |
| `docs/providers/*` | (content folded into connect-your-stuff/ai-providers.md; per-provider pages deleted) |
| `docs/setup/onboarding.md` | `reference/setup/onboarding.md` |
| `docs/setup/teams.md` | `reference/setup/teams.md` |
| `docs/setup/slack.md` | `reference/setup/slack.md` |
| `docs/setup/README.md` | `reference/setup/README.md` |

## Anonymisation

Every sample across the new content uses the fictional **TodoList** product. Catalog keys in kebab-case (`todolist-api`, `todolist-worker`, `todolist-web`, `todolist-docs`); Azure DevOps org `acme-org`, Azure DevOps project `Platform`; OpenAI Azure endpoint placeholder `https://oai-acme-dev.openai.azure.com`; agentsmith.yml project key `azuredevops-todolist` (or `jira-todolist`, `github-todolist`, `gitlab-todolist`). No real customer / employer names appear in any sample.

## Landing hero

Eleventy `hero-lifecycle.njk` headline + sub-line tuned per Rule 9 (drop negation-definitions, replace with positive descriptions). `site.json` description likewise.
