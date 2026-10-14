"use client";

import type { SpecDialogGraphTicket, SpecDialogProposalFinding, SpecDialogProposalPush } from "@/types/spec-dialog";
import { LIST_KEY, waitsFor } from "./graphTicketWaits";

// 2026-10-14-dbcek: a proposed LIST OF TICKETS, rendered as ticket texts in the order they must merge — each with its
// key, title, what it waits for, its requirement, its done items, its repositories and what the plan review found
// against it. A list carries no spec: each ticket's run derives its specs when it starts, which the foot says. A
// finding about the whole list (a requirement no ticket covers, or a list proposed in a ticket's own conversation)
// stands above the tickets. design/mockups/ticket-graph.html is the mock this follows.

export function DialogProposalGraph({ proposal }: { proposal: SpecDialogProposalPush }) {
  const tickets = proposal.tickets ?? [];
  const findings = proposal.findings ?? [];
  const listLevel = findings.filter((finding) => finding.specId === LIST_KEY);
  return (
    <div data-testid="dialog-proposal-graph" className="flex flex-col gap-3">
      <div>
        <div className="fl mb-0.5">A list of tickets</div>
        <h3 className="dsh-h3 font-semibold text-ink">{tickets.length} tickets, in the order they must merge</h3>
        <p className="dsh-label text-body">
          Each ticket is one run and one pull request per repository. A ticket starts only when every ticket it waits
          for is merged, or a person marked it done by hand.
        </p>
      </div>
      {listLevel.map((finding, index) => (
        <Finding key={`list-${index}`} finding={finding} testId="dialog-graph-list-finding" />
      ))}
      <ol data-testid="dialog-graph-tickets" className="flex flex-col gap-2">
        {tickets.map((ticket, index) => (
          <li key={ticket.key}>
            <GraphTicket ticket={ticket} number={index + 1} findings={findings.filter((f) => f.specId === ticket.key)} />
          </li>
        ))}
      </ol>
      <p data-testid="dialog-graph-derive" className="dsh-label italic text-body">
        No specs yet: each ticket&apos;s run derives and reviews its own specs when it starts. To settle them earlier,
        open the ticket&apos;s own conversation after filing.
      </p>
    </div>
  );
}

function GraphTicket({ ticket, number, findings }: { ticket: SpecDialogGraphTicket; number: number; findings: SpecDialogProposalFinding[] }) {
  return (
    <details data-testid={`dialog-graph-ticket-${ticket.key}`} open={number === 1 || findings.length > 0} className="ecard inert">
      <summary className="flex cursor-pointer items-baseline gap-2 px-2.5 py-2">
        <span className="ec-mark">{number}</span>
        <span className="min-w-0 flex-1">
          <span className="font-mono dsh-label text-body">{ticket.key}</span>
          <span className="block dsh-body font-medium text-ink">{ticket.title}</span>
          <span data-testid={`dialog-graph-waits-${ticket.key}`} className="block dsh-label text-body">{waitsFor(ticket)}</span>
        </span>
      </summary>
      <div className="flex flex-col gap-2 px-2.5 pb-2.5">
        {findings.map((finding, index) => (
          <Finding key={`${ticket.key}-${index}`} finding={finding} testId="dialog-graph-ticket-finding" />
        ))}
        <p className="dsh-body whitespace-pre-line text-ink">{ticket.requirement}</p>
        <div>
          <div className="fl mb-0.5">Done when</div>
          <ul className="ml-4 list-disc dsh-body text-ink">
            {ticket.done.map((item) => (
              <li key={item}>{item}</li>
            ))}
          </ul>
        </div>
        {ticket.repos.length > 0 && (
          <div className="dsh-label text-body">
            repositories <span className="font-mono">{ticket.repos.join(", ")}</span>
          </div>
        )}
      </div>
    </details>
  );
}

function Finding({ finding, testId }: { finding: SpecDialogProposalFinding; testId: string }) {
  return (
    <div data-testid={testId} className="border-l-2 border-state-stopped pl-2.5 dsh-body text-ink">
      <b>{finding.specId} — {finding.problem}</b>: {finding.why}
      {finding.quote && <span className="ml-1 dsh-label text-body">it states: “{finding.quote}”</span>}
    </div>
  );
}
