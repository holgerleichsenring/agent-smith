"use client";

import { useState } from "react";
import Link from "next/link";
import { Button } from "@/components/ui/Button";
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

type InitState =
  | { kind: "idle" }
  | { kind: "starting" }
  | { kind: "running"; runId: string }
  | { kind: "refused"; reason: string };

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
  return (
    <div className="flex flex-col items-start gap-1">
      <div className="flex items-center gap-2">
        <Button
          variant="ghost"
          data-testid={`project-init-${project}`}
          disabled={state.kind === "starting"}
          onClick={(e) => {
            e.stopPropagation();
            void start();
          }}
        >
          {state.kind === "starting" ? "Initializing…" : "Initialize"}
        </Button>
        <AutoAcceptToggle
          project={project}
          checked={autoAccept}
          disabled={state.kind === "starting"}
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
      className="flex items-center gap-1.5 dsh-label text-stone-600"
      title="Merge the pull requests this initialization opens. A branch policy that refuses leaves the pull request open."
      onClick={(e) => e.stopPropagation()}
    >
      <input
        type="checkbox"
        data-testid={`project-init-auto-accept-${project}`}
        checked={checked}
        disabled={disabled}
        onChange={(e) => onChange(e.target.checked)}
      />
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
      className="inline-flex items-center gap-1.5 rounded-md border border-stone-300 px-3 py-1.5 dsh-body font-medium text-stone-700 hover:bg-stone-100"
      data-testid={`project-init-running-${project}`}
      onClick={(e) => e.stopPropagation()}
    >
      Initializing — view run
    </Link>
  );
}
