"use client";

import { useState } from "react";
import { useLacksPermission } from "@/hooks/useLacksPermission";
import { overwriteTicketBody } from "@/lib/ticketBodyApi";
import { useTicketDetail, useTicketEdited, useTicketText } from "@/lib/tickets/TicketPageStore";

// 2026-11-17-dd05d: the ticket's text in its tracker no longer states goals of its approved specs — someone edited
// it there. One plain line says which, and the two ways to settle it: discuss the change with the agent in the ticket
// conversation (2026-11-17-4860a: the composer under the thread, which only a ticket with a run has) or have the tracker
// say the specs again (a 'ticket-body' run). Nothing when the two agree.

// The conversation's composer sits under the thread this notice leads; the action brings it into view and focuses it.
function discuss() {
  const composer = document.querySelector<HTMLElement>('[data-testid="ticket-composer"]');
  composer?.scrollIntoView?.({ block: "center" });
  composer?.querySelector<HTMLTextAreaElement>("textarea")?.focus();
}

export function TicketTextDivergence() {
  const text = useTicketText();
  const { project, ticketId, data } = useTicketDetail();
  const edited = useTicketEdited();
  const conversation = (data?.attempts.length ?? 0) > 0;
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
          {conversation && (
            <button className="btn sm" type="button" onClick={discuss} data-testid="divergence-discuss">Discuss in the conversation</button>
          )}
          <button className="btn sm" type="button" onClick={overwrite} disabled={busy} data-testid="divergence-overwrite">
            {busy ? "Overwriting…" : "Overwrite the tracker from the specs"}
          </button>
        </>
      )}
    </div>
  );
}
