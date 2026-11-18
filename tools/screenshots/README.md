# Docs screenshot rig

Retakes every image in `docs/assets/screenshots/` from the real dashboard, running against a real
server whose scratch SQLite database holds an invented demo story (acme, shop, Sam Sample). Nothing
touches Docker, a running installation, or the network.

```bash
# once: a Chromium for Playwright (skipped on a Mac that has Chrome for Testing in the Playwright cache)
cd tools/screenshots && npm install && npx playwright install chromium && cd ../..

# seed, start, shoot, stop
tools/screenshots/run.sh

# keep server :5917 and dashboard :3917 running afterwards, to look around
tools/screenshots/run.sh --keep
```

Needs the .NET SDK, `pnpm`, Node 20+, `git` and `openssl`. Ports: `SERVER_PORT` (5917),
`DASHBOARD_PORT` (3917), `TRACKER_PORT` (5918). `DEMO_ROOT` chooses where the database, the demo
repositories and the logs go (default: a new temp directory). `CHROME_PATH` points at a browser.
To retake a subset: `ONLY=home,run-log node tools/screenshots/shoot.mjs` against a `--keep` rig.

## What runs

| Step | What it does |
|---|---|
| `tools/demo-seed/demo-config.yml` | The demo installation: agent `claude-default`, two local repositories `shop-api` and `shop-web`, tracker `acme-github`, projects `shop` (merge on green CI) and `billing`. Imported with `agentsmith config import`. |
| `agentsmith database migrate` | Creates the scratch SQLite schema. |
| `tools/demo-seed` (`DemoSeed.dll <demo-root>`) | Builds the server's own composition (`ServerHostFactory`) without starting it and writes the story through the product's services: runs as event streams fed to `RunDbProjector`, conversations through `SpecDialogSessionManager` and `SpecDialogProposalState`, the filing through `SpecDialogLatestOutcomeStore`, the series through `ISeriesRunStore` and `ISeriesConversation`, stored secrets through `StoredSecretRepository` (under a throwaway `AGENTSMITH_SECRET_MASTER_KEY`). Each ticket's approved specs are committed on its `agent-smith/<id>` branch in the local repositories, with the pointer filing records. Running it twice is a no-op. |
| `tracker-stub.mjs` | A read-only GitHub Enterprise-shaped REST API on localhost that answers issue bodies and comments for the demo tickets, so the ticket page reads a tracker without external access. Writes answer 201 and are dropped. |
| server + dashboard | `AgentSmith.Server` on the scratch database with no Redis: the server runs degraded by design (no queue, no live feed); every page reads persisted data. `next dev` proxies `/api` and `/hub` to it. |
| `shoot.mjs` | Headless Chromium, 1440×900 at 2x, dark scheme; one PNG per docs surface. |

## Departures from a live installation

- **Degraded banner hidden.** The rig has no Redis, so the shell shows "Running degraded"; `shoot.mjs`
  hides `[data-testid=degraded-banner]` in every shot. It is an artefact of the rig.
- **Series panel counts (route override).** `GET /api/spec-dialog/{id}/series` currently counts every
  ticket of a series as landed (`SeriesTicketFacts` turns a missing landing into a default timestamp).
  `series-status.png` corrects `landed`, `now` and the critical path to the seeded story (#44 landed,
  #45 running, #46 held on #45). Remove `seriesPanelRoute` once that is fixed.
- **No model ran.** Conversation turns, the series status messages and the conversation subjects are
  written as the product would have written them; the status table follows `SeriesStatusText` and the
  opening line follows `SeriesOpening`.
