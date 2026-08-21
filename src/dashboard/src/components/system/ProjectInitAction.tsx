"use client";

import { useState, type CSSProperties } from "react";
import Link from "next/link";
import { startProjectInit } from "@/lib/projectInitApi";

// p0489: the operator's Initialize affordance for one project. Two failures,
// two places, and this must not invent a third: a REFUSED launch answers inline
// right here and the action stays pressable (pressing again IS the re-run — a
// second init on an initialized repo opens no PR and changes nothing), while a
// STARTED run that later fails is the ordinary run detail, so the action becomes
// a link to that run. A second press while an init is live opens the live run
// instead of starting another.
//
// p0490: the checkbox beside it defaults to ON. An init pull request is generated
// .agentsmith/ context on a repo that had none, and nobody reviews it — so the review
// step the tick skips was already not happening. It applies to the run it starts and
// to nothing else.
//
// p0497: and it LOOKS like it belongs. The toggle used to be a bare checkbox input,
// so it drew the operating system's accent — the one colour on the page no token
// controls — and the pair sat in the middle of the card's metadata row. Both controls
// now wear the studio's own chip idiom from --accent/--line, and they form one action
// group the card can separate from its badge. Appearance only: every test id, state
// and refusal path below is p0489's and p0490's, unchanged.

type InitState =
  | { kind: "idle" }
  | { kind: "starting" }
  | { kind: "running"; runId: string }
  | { kind: "refused"; reason: string };

/** The .pick chip the repo picker established, as inline tokens so the one
 *  definition covers both hosts this renders in — the config card (.mock-config)
 *  and the system project panel (.mock-system). */
// Longhand throughout, never `border`/`font` shorthand: the checked state overrides
// borderColor and fontWeight, and React warns that removing a longhand beside a
// shorthand is how styling bugs start.
const CHIP: CSSProperties = {
  fontSize: "12.5px",
  fontFamily: "var(--mono)",
  fontWeight: 400,
  padding: "6px 11px",
  borderRadius: "9px",
  borderWidth: "1px",
  borderStyle: "solid",
  borderColor: "var(--line)",
  background: "var(--panel)",
  color: "var(--ink-2)",
  cursor: "pointer",
  display: "inline-flex",
  alignItems: "center",
  gap: "7px",
};

export function ProjectInitAction({ project }: { project: string }) {
  const [state, setState] = useState<InitState>({ kind: "idle" });
  const [autoAccept, setAutoAccept] = useState(true);

  async function start() {
    setState({ kind: "starting" });
    try {
      const launch = await startProjectInit(project, { autoCompletePullRequests: autoAccept });
      setState(
        launch.runId
          ? { kind: "running", runId: launch.runId }
          : { kind: "refused", reason: launch.reason ?? "The initialization could not be started." },
      );
    } catch {
      setState({ kind: "refused", reason: "The server could not be reached." });
    }
  }

  if (state.kind === "running") {
    return <RunningLink project={project} runId={state.runId} />;
  }
  const starting = state.kind === "starting";
  return (
    <ActionGroup project={project}>
      <div className="flex items-center gap-2">
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
        <AutoAcceptToggle
          project={project}
          checked={autoAccept}
          disabled={starting}
          onChange={setAutoAccept}
        />
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

function AutoAcceptToggle({
  project,
  checked,
  disabled,
  onChange,
}: {
  project: string;
  checked: boolean;
  disabled: boolean;
  onChange: (next: boolean) => void;
}) {
  return (
    <label
      title="Merge the pull requests this initialization opens. A branch policy that refuses leaves the pull request open."
      onClick={(e) => e.stopPropagation()}
      style={{
        ...CHIP,
        ...(checked
          ? { borderColor: "var(--accent)", background: "var(--accent-wash)", color: "var(--accent)", fontWeight: 600 }
          : null),
        ...(disabled ? { opacity: 0.5, cursor: "not-allowed" } : null),
      }}
    >
      <input
        type="checkbox"
        className="sr-only"
        data-testid={`project-init-auto-accept-${project}`}
        checked={checked}
        disabled={disabled}
        onChange={(e) => onChange(e.target.checked)}
      />
      {/* The repo picker's .pk tick box, drawn from the same tokens. */}
      <span
        aria-hidden="true"
        data-testid={`project-init-auto-accept-box-${project}`}
        style={{
          width: 15,
          height: 15,
          borderRadius: 4,
          display: "grid",
          placeItems: "center",
          fontSize: 10,
          lineHeight: 1,
          borderWidth: "1.5px",
          borderStyle: "solid",
          borderColor: checked ? "var(--accent)" : "var(--line)",
          background: checked ? "var(--accent)" : "transparent",
          color: checked ? "var(--accent-ink)" : "transparent",
        }}
      >
        ✓
      </span>
      Auto-accept PRs
    </label>
  );
}

// While the init is live the affordance IS the way to it — the run page carries
// the ledger, the cost and the cancel, exactly like a polled run.
function RunningLink({ project, runId }: { project: string; runId: string }) {
  return (
    <Link
      href={`/jobs/${encodeURIComponent(runId)}`}
      style={{ ...CHIP, borderColor: "var(--accent)", color: "var(--accent)", textDecoration: "none" }}
      data-testid={`project-init-running-${project}`}
      onClick={(e) => e.stopPropagation()}
    >
      Initializing — view run
    </Link>
  );
}
