"use client";

import { useState } from "react";
import { useLacksPermission } from "@/hooks/useLacksPermission";
import { overwriteTicketBody } from "@/lib/ticketBodyApi";
import { useTicketDetail, useTicketEdited, useTicketText } from "@/lib/tickets/TicketPageStore";
import { useTicketTabGo } from "./useTicketTabGo";

// 2026-11-17-dd05d: the ticket's text in its tracker no longer states goals of its approved specs — someone edited
// it there. One plain line says which, and the two ways to settle it: take the change into the specs (the Specs
// section's edit) or have the tracker say the specs again (a 'ticket-body' run). Nothing when the two agree.

export function TicketTextDivergence() {
  const text = useTicketText();
  const { project, ticketId } = useTicketDetail();
  const edited = useTicketEdited();
  const address = useTicketTabGo();
  const lacks = useLacksPermission("runs.control", project);
  const [busy, setBusy] = useState(false);
  const [said, setSaid] = useState<string | null>(null);
  const divergence = text.data?.divergence;
  if (!divergence || divergence.unsaid.length === 0) return said ? <div className="msub" data-testid="divergence-said">{said}</div> : null;

  const overwrite = async () => {
    setBusy(true);
    try {
      const result = await overwriteTicketBody(project, ticketId);
      setSaid(result.text);
      if (result.rewritten) edited();
    } catch (error) {
      setSaid(error instanceof Error ? error.message : "The overwrite could not be sent.");
    } finally {
      setBusy(false);
    }
  };

  const goals = divergence.unsaid.map((g) => `“${g}”`).join(", ");
  return (
    <div className="ro-note" data-testid="ticket-divergence">
      <span>
        The ticket&apos;s text in the tracker no longer states {divergence.unsaid.length} of its {divergence.specs} spec goals: {goals}.
        {said && <> {said}</>}
      </span>
      {!lacks && (
        <>
          <button className="btn sm" type="button" onClick={() => address.go("specs")} data-testid="divergence-edit">Edit the specs</button>
          <button className="btn sm" type="button" onClick={overwrite} disabled={busy} data-testid="divergence-overwrite">
            {busy ? "Overwriting…" : "Overwrite the tracker from the specs"}
          </button>
        </>
      )}
    </div>
  );
}
