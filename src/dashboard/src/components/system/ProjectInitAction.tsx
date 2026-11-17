"use client";

import { useState } from "react";
import Link from "next/link";
import { startProjectInit, type LiveInitRun } from "@/lib/projectInitApi";
import { useProjectInitRun } from "@/hooks/useProjectInitRun";
import { CHIP, InitOptionChip } from "./InitOptionChip";

// p0489: the operator's Initialize affordance for one project. Two failures,
// two places, and this must not invent a third: a REFUSED launch answers inline
// right here and the action stays pressable (pressing again IS the re-run — a
// second init on an initialized repo opens no PR and changes nothing), while a
// STARTED run that later fails is the ordinary run detail, so the action becomes
// a link to that run. A second press while an init is live opens the live run
// instead of starting another.
//
// 2026-11-17-713ea: no auto-accept option any more — every init completes its own pull
// requests, whichever door started it (p0490's checkbox defaulted on for the same reason:
// nobody reviews generated .agentsmith/ context).
//
// p0497: and it LOOKS like it belongs. The toggle used to be a bare checkbox input,
// so it drew the operating system's accent — the one colour on the page no token
// controls — and the pair sat in the middle of the card's metadata row. Both controls
// now wear the studio's own chip idiom from --accent/--line, and they form one action
// group the card can separate from its badge. Appearance only: every test id, state
// and refusal path below is p0489's and p0490's, unchanged.
//
// 2026-10-02-5f89d: the live run is the SERVER's answer, not this component's memory. It
// used to live in useState alone, so a navigation away and back showed Initialize beside a
// running init. The server's live run is read on mount and on every relevant RunsChanged,
// and it says whether the run waits for a slot, runs, or is being cancelled.
//
// 2026-10-04-2bf2: the one option, "Refresh principles", defaults to OFF — it replaces a
// ratified principles.md (keeping its Project Specifics), and that is the operator's call per
// launch.

type InitState =
  | { kind: "idle" }
  | { kind: "starting" }
  | { kind: "started"; runId: string }
  | { kind: "refused"; reason: string };

export function ProjectInitAction({ project }: { project: string }) {
  const [state, setState] = useState<InitState>({ kind: "idle" });
  const [refreshPrinciples, setRefreshPrinciples] = useState(false);
  const { live, refresh } = useProjectInitRun(project);

  async function start() {
    setState({ kind: "starting" });
    try {
      const launch = await startProjectInit(project, { refreshPrinciples });
      if (!launch.runId) {
        setState({
          kind: "refused",
          reason: launch.reason ?? "The initialization could not be started.",
        });
        void refresh();
        return;
      }
      // Once the server has answered, its live run is what shows; without an answer the
      // run this press started is the best knowledge there is.
      setState((await refresh()) ? { kind: "idle" } : { kind: "started", runId: launch.runId });
    } catch {
      setState({ kind: "refused", reason: "The server could not be reached." });
    }
  }

  const shown = shownRun(state, live);
  if (shown) {
    return <RunningLink project={project} run={shown} />;
  }
  const starting = state.kind === "starting";
  return (
    <ActionGroup project={project}>
      <div className="flex flex-wrap items-center gap-2">
        <button
          type="button"
          data-testid={`project-init-${project}`}
          disabled={starting}
          onClick={(e) => {
            e.stopPropagation();
            void start();
          }}
          style={{ ...CHIP, ...(starting ? { opacity: 0.5, cursor: "not-allowed" } : null) }}
        >
          {starting ? "Initializing…" : "Initialize"}
        </button>
        {/* The options belong to the launch: one labelled group beside the button. */}
        <div
          role="group"
          aria-label="Initialization options"
          className="flex flex-wrap items-center gap-1.5"
          data-testid={`project-init-options-${project}`}
        >
          <InitOptionChip
            testId={`project-init-refresh-principles-${project}`}
            boxTestId={`project-init-refresh-principles-box-${project}`}
            title="Regenerate principles.md from the catalog: core, language delta and framework overlays. The Project Specifics section is kept."
            label="Refresh principles"
            checked={refreshPrinciples}
            disabled={starting}
            onChange={setRefreshPrinciples}
          />
        </div>
      </div>
      {state.kind === "refused" && (
        <span
          className="dsh-label text-rose-700"
          role="status"
          data-testid={`project-init-refusal-${project}`}
        >
          {state.reason}
        </span>
      )}
    </ActionGroup>
  );
}

/** p0497: the actions are their own group, ended by a rule, so the card's row reads
 *  [name] … [actions] | [badge] [edit ›] instead of interleaving the two kinds. */
function ActionGroup({ project, children }: { project: string; children: React.ReactNode }) {
  return (
    <div
      className="flex flex-col items-start gap-1"
      data-testid={`project-init-group-${project}`}
      style={{ paddingRight: 12, borderRight: "1px solid var(--line-2)" }}
    >
      {children}
    </div>
  );
}

// The server's answer wins. A press whose re-read failed keeps the run it started, shown as
// running — what the button showed before this read existed.
function shownRun(state: InitState, live: LiveInitRun | null): LiveInitRun | null {
  if (live) return live;
  if (state.kind === "started") return { runId: state.runId, state: "running" };
  return null;
}

const RUN_LABEL: Record<LiveInitRun["state"], string> = {
  running: "Initializing — view run",
  queued: "Waiting for a slot — view run",
  cancelling: "Cancelling — view run",
};

// While the init is live the affordance IS the way to it — the run page carries
// the ledger, the cost and the cancel, exactly like a polled run.
function RunningLink({ project, run }: { project: string; run: LiveInitRun }) {
  return (
    <Link
      href={`/jobs/${encodeURIComponent(run.runId)}`}
      style={{
        ...CHIP,
        borderColor: "var(--accent)",
        color: "var(--accent)",
        textDecoration: "none",
      }}
      data-testid={`project-init-running-${project}`}
      data-state={run.state}
      onClick={(e) => e.stopPropagation()}
    >
      {RUN_LABEL[run.state] ?? RUN_LABEL.running}
    </Link>
  );
}
