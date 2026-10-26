"use client";

import { useState } from "react";
import { useLacksPermission } from "@/hooks/useLacksPermission";
import { deriveSpecs, type DeriveMode } from "@/lib/ticketsApi";
import { useTicketDerivations } from "@/lib/tickets/TicketPageStore";
import { RUNS_CONTROL } from "./RetryUnchanged";

// 2026-10-26-bdd0f: Derive specs — derive the ticket's specs and review them without starting an attempt. Store on
// the branch (with a draft spec pull request) or Preview only (nothing written). Hidden from a caller known to lack
// runs.control; a refusal is shown as the server wrote it.

export const DERIVE_MODES: { mode: DeriveMode; label: string }[] = [
  { mode: "branch", label: "Store on the branch" },
  { mode: "preview", label: "Preview only" },
];

type Outcome = { kind: "idle" } | { kind: "sending" } | { kind: "started" } | { kind: "said"; text: string };

export function DeriveSpecsAct({ project, ticketId, label = "Derive specs" }: { project: string; ticketId: string; label?: string }) {
  const lacks = useLacksPermission(RUNS_CONTROL, project);
  const { started } = useTicketDerivations();
  const [mode, setMode] = useState<DeriveMode>("branch");
  const [outcome, setOutcome] = useState<Outcome>({ kind: "idle" });
  if (lacks) return null;

  const derive = async () => {
    setOutcome({ kind: "sending" });
    try {
      const answer = await deriveSpecs(project, ticketId, mode);
      if (answer.kind === "started") started();
      setOutcome(answer.kind === "started" ? { kind: "started" } : { kind: "said", text: answer.text });
    } catch (thrown) {
      setOutcome({ kind: "said", text: thrown instanceof Error ? thrown.message : String(thrown) });
    }
  };

  return (
    <span className="derive-act" data-testid="derive-specs">
      <select aria-label="Where the specs go" value={mode} onChange={(e) => setMode(e.target.value as DeriveMode)}
        data-testid="derive-specs-mode">
        {DERIVE_MODES.map((m) => <option key={m.mode} value={m.mode}>{m.label}</option>)}
      </select>
      <button type="button" className="btn" onClick={() => void derive()} disabled={outcome.kind === "sending"}
        data-testid="derive-specs-start">{label}</button>
      {outcome.kind === "started" && <span className="cp-note" data-testid="derive-specs-started">Spec derivation started.</span>}
      {outcome.kind === "said" && <span className="cp-note bad" data-testid="derive-specs-refused">{outcome.text}</span>}
    </span>
  );
}
