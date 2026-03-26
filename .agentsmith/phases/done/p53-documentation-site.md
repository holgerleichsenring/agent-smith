# Phase 53: Documentation Site

## Goal

Technical documentation at code.agent-smith.org — deployed via GitHub Pages,
generated from Markdown with MkDocs Material. README.md trimmed to essentials
with links to the docs site.

## Motivation

The README has grown past the point of usefulness. Critical workflows
(phases, runs, context.yaml, result.md, decisions.md, discussion pipelines)
are not documented at all. Users can't discover how Agent Smith actually
works beyond the surface level. The README tries to be everything and
succeeds at nothing.

## Site Structure

```
code.agent-smith.org/
├── Getting Started
│   ├── Installation (binary, Docker, source)
│   ├── Quick Start — your first bug fix in 5 minutes
│   └── Quick Start — your first API scan
├── Pipelines
│   ├── fix-bug / add-feature
│   ├── security-scan
│   ├── api-scan
│   ├── legal-analysis
│   └── mad-discussion
├── Concepts
│   ├── Pipeline System (commands, handlers, presets)
│   ├── Phases & Runs (context.yaml, result.md, the workflow)
│   ├── Multi-Skill Architecture (roles, triage, convergence)
│   ├── Decision Logging (decisions.md, why not what)
│   └── Cost Tracking (token usage, pricing, result.md frontmatter)
├── Configuration
│   ├── agentsmith.yml Reference
│   ├── Skills YAML Reference
│   ├── Tool Configuration (nuclei.yaml, spectral.yaml)
│   └── Model Registry (providers, routing, pricing)
├── AI Providers
│   ├── Claude (Anthropic)
│   ├── OpenAI / GPT-4
│   ├── Gemini
│   ├── Ollama (local models)
│   └── OpenAI-Compatible (Groq, Together, vLLM, LiteLLM)
├── CI/CD Integration
│   ├── Azure DevOps
│   ├── GitHub Actions
│   ├── GitLab CI
│   └── Generic (binary in any pipeline)
├── Deployment
│   ├── Single Binary
│   ├── Docker / Docker Compose
│   ├── Kubernetes
│   └── Chat Gateway (Slack, Teams)
├── Architecture
│   ├── Clean Architecture Layers
│   ├── Project Structure
│   └── Extending Agent Smith (custom pipelines, providers, skills)
└── Contributing
    ├── Development Setup
    ├── Coding Principles
    └── Phase Workflow (how we plan and track work)
```

## Tech Stack

- **MkDocs** with **Material for MkDocs** theme
- Markdown source files in `docs/` directory
- GitHub Actions workflow for build + deploy to GitHub Pages
- Custom domain: code.agent-smith.org (CNAME → holgerleichsenring.github.io)

## README.md Reduction

The README gets cut down to:

1. Logo + one-liner
2. What It Does (the 13-step diagram stays — it's the hook)
3. Installation (binary / Docker / source — 3 short blocks)
4. Quick Start (one example)
5. Links to docs site for everything else
6. License

Everything else moves to the docs site. The README becomes a landing page
that gets people started in 30 seconds and points them to the real docs.

## DNS Setup

Add CNAME record at DNS provider:
```
code.agent-smith.org  CNAME  holgerleichsenring.github.io
```

In GitHub repo settings → Pages → Custom domain: code.agent-smith.org

## Files to Create

- `docs/` directory with all Markdown content
- `mkdocs.yml` — MkDocs configuration with Material theme
- `.github/workflows/docs.yml` — build and deploy on push to main
- `docs/CNAME` — custom domain file for GitHub Pages

## Files to Modify

- `README.md` — radical reduction, link to docs site

## Definition of Done

- [ ] MkDocs builds locally (`mkdocs serve`)
- [ ] All pipeline workflows documented with examples
- [ ] Phases & Runs workflow explained (the meta-workflow)
- [ ] Configuration reference complete
- [ ] CI/CD integration guides for Azure DevOps, GitHub Actions, GitLab
- [ ] GitHub Actions deploys to GitHub Pages on push
- [ ] code.agent-smith.org resolves with SSL
- [ ] README.md trimmed to landing page
- [ ] No broken internal links
- [ ] Search works

## Dependencies

- DNS CNAME record for code.agent-smith.org (manual setup by maintainer)
