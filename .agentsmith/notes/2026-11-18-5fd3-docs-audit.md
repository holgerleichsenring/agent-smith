# Docs audit 2026-11-18 (5fd3)

Every page under `docs/` read against origin/main at 7e870988c (release 0.188.0, `version.txt`).
Reference pages were last swept on 2026-11-17 by the rebrand and the feature series; they were
read for the items below, not re-derived line by line from the code.

## Stale

| Page | What is stale |
|---|---|
| `index.md` | "five-beat story", "verify beat"; the dashboard described as a runs board (home is now the "What should we work on?" composer, runs moved to `/runs`); all three screenshots show the pre-dark UI; services link points at agent-smith.org |
| `get-it-running/install.md` | version `0.108.0` (four places); no word on the secret master key; "Secrets … values stay in the environment" no longer true |
| `host-it/docker-compose.md` | `AGENTSMITH_VERSION=0.108.0`, `deployment.version: 0.108.0`; env list lacks `AGENTSMITH_SECRET_MASTER_KEY` |
| `connect-your-stuff/tracker-azure-devops.md`, `connect-your-stuff/repos-multi.md`, `reference/concepts/sandbox-agent.md` | `0.108.0` in examples; the Azure DevOps page embeds two old light-UI studio shots |
| `deploy/docker-compose.example.yml` | image default tag `0.129.0` |
| `configure-it/config-studio.md` | "toggle between them at the top of the left rail" (configuration now opens from the profile menu); the Secrets catalog row says values never enter the database (stored secrets, 2026-11-17-f3a8d); three old screenshots |
| `configure-it/settings.md` | old cost-cap screenshot |
| `reference/operations/dashboard.md` | "The shell" describes the Work / Monitor / Design / System rail (now the flat sidebar: New work, Your move, Tickets, Runs, Pull requests, Insight, Recent; settings and system behind the profile); "Runs" calls the runs board the home page and mentions story beats; "A ticket's page" describes the Summary tab, four cells, the attempt flow graph and the Conversation / Attempts / Cost & compute tabs (now the section bar What happened / Specs / Acceptance / Runs / Cost / Defaults, flow graph removed, 2026-11-17-8d1ca..d); old runs screenshot |
| `how-it-works/delivery-account.md` | "Verify card", "verify beat" (beats deleted with the graph view, 2026-11-17-6d9ed; the account reads in the run log's "Checked against the spec") |
| `how-it-works/work-it-out.md` | the page described as three columns with recents on the left (now the conversation bar, a 720px exchange and a plain pane; a new conversation opens on the home composer, 2026-11-17-d762a..c, 76f1); draft tickets' own specs and the File offer (5701a, bf77) and the Series tab (0207e) missing |
| `how-it-works/ticket-dependencies.md`, `how-it-works/lifecycle.md` | a dependent "starts only once that one has landed": the default is now `requires_start: on_green_push`, a stacked start on a green prerequisite (2026-11-17-9a39a..c) |
| `how-it-works/lifecycle.md` | step 7 ends with the ticket resolved; merge policy and the CI loop (023fa, 3435a..d) are not mentioned |

## Missing

- An end-to-end walkthrough of the current flow with pictures: Work it out → draft tickets with reviewed specs → `file` run (branch = the only spec truth) → coding run without re-deriving, review once, premise hint, `revise_spec`, mid-run messages → CI loop → stacked start → series run → deploy.
- Series run (0207a..e): no page; only three `dialogue.*` keys in the reference.
- CI loop and merge policy (023fa/b, 3435a..d): reference keys only, no page that explains the behaviour.
- Stacked start (9a39a..c): no page; `on_green_push` appears nowhere.
- Admin assistant (f3a8a..c, 00eaa, 78efa): one sentence in Settings.
- Secrets (f3a8d/e, 9ae8, 78efb): a section inside the Config studio page, no page of its own.
- Branch cleanup (713eb/c): `branch_cleanup` appears nowhere.

## Screenshots

All 13 files in `docs/assets/screenshots/` predate the dark redesign (2026-11-16-0440a).

- Referenced and stale: `config-changes.png`, `config-new-project.png`, `config-projects.png`, `config-settings-costcap.png`, `config-tracker-drawer.png`, `dashboard-runs.png`, `run-detail.png` (also embedded by `README.md`).
- Referenced by no page: `config-agent-drawer.png`, `config-agents.png`, `config-repos.png`, `config-secrets.png`, `config-settings-limits.png`, `config-trackers.png`.
- Missing: home, ticket list, Work it out with draft tickets, ticket page sections, run log with an opened step, series status, merge tab, secret drawer, admin assistant.

The logo PNGs at `docs/` root are referenced by no page; 2026-11-16-658cb kept them on purpose (external links), so they stay.
