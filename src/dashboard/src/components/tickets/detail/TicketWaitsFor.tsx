"use client";

import Link from "next/link";
import { useTicketRequires } from "@/lib/tickets/TicketPageStore";
import { ticketPath } from "@/lib/tickets/ticketAddress";
import type { TicketRequirement } from "@/types/tickets";
import { DoneByHandAct } from "../landing/DoneByHandAct";
import { dependentWords, isLanded, landedByWords, waitWords } from "../landing/requirementWords";

// 2026-10-14-dbcen: ticket-graph.html's "Waits for" and "Required by" — each ticket by its tracker key, linked
// when it has a page here. Every unlanded ticket this one waits for carries its own "Mark done by hand", for THAT
// ticket in this page's project: a person's ticket or an older filing has no page to carry the act. A mark
// re-reads this page's requires and detail at once.

export function TicketWaitsFor({ project }: { project: string }) {
  const requires = useTicketRequires();
  const view = requires.data;
  if (!view) return null;
  return (
    <>
      {view.requires.length > 0 && (
        <div className="waits" data-testid="ticket-waits-for">
          <div className="wf-h">Waits for</div>
          {view.requires.map((r) => (
            <div key={r.ticketId} className="wf-row" data-testid={`ticket-waits-${r.ticketId}`}>
              <Entry project={project} entry={r} words={waitWords(r)} landed={isLanded(r)} />
              <DoneByHandAct project={project} ticketId={r.ticketId} label={r.key} landed={isLanded(r)}
                onLanded={requires.landed} small />
            </div>
          ))}
        </div>
      )}
      {view.requiredBy.length > 0 && (
        <div className="waits" data-testid="ticket-required-by">
          <div className="wf-h">Required by</div>
          {view.requiredBy.map((r) => (
            <div key={r.ticketId} className="wf-row">
              <Entry project={project} entry={r} words={dependentWords(r)} landed={false} />
            </div>
          ))}
        </div>
      )}
    </>
  );
}

function Entry({ project, entry, words, landed }: { project: string; entry: TicketRequirement; words: string; landed: boolean }) {
  const by = landedByWords(entry);
  const name = (
    <>
      <span className="wk">{entry.key}</span>
      {entry.title}
    </>
  );
  return (
    <span>
      {entry.hasPage ? <Link href={ticketPath(project, entry.ticketId)}>{name}</Link> : name}{" "}
      <span className={landed ? "ws landed" : "ws"}>{words}</span>
      {by && <span className="wb" data-testid={`ticket-landed-by-${entry.ticketId}`}>{by}</span>}
    </span>
  );
}
