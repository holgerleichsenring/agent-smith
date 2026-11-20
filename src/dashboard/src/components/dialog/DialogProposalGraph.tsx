"use client";

import type {
  SpecDialogGraphTicket, SpecDialogProposalFinding, SpecDialogProposalPush, SpecDraftProposal,
} from "@/types/spec-dialog";
import { DialogProposalRow } from "./DialogProposalRow";
import { LIST_KEY, waitsFor } from "./graphTicketWaits";

// 2026-10-14-dbcek: a proposed LIST OF TICKETS, rendered as ticket texts in the order they must merge — each with its
// key, title, what it waits for, its requirement, its done items, its repositories and what the plan review found
// against it. 2026-11-17-5701a: under each ticket its specs, each with its id, goal and the findings on it. A
// finding about the whole list (a requirement no ticket covers, or a list proposed in a ticket's own conversation)
// stands above the tickets. design/mockups/ticket-graph.html is the mock this follows.
// 2026-11-19-1b72b: each ticket is a hairline row — order, title, what it waits for — that opens to the rest and its
// specs as indented rows (label · goal), like the run view's sections; no card.

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
      <ol data-testid="dialog-graph-tickets" className="dl-prop-rows">
        {tickets.map((ticket, index) => (
          <li key={ticket.key}>
            <GraphTicket ticket={ticket} number={index + 1} findings={findings} />
          </li>
        ))}
      </ol>
    </div>
  );
}

function GraphTicket({ ticket, number, findings: all }: { ticket: SpecDialogGraphTicket; number: number; findings: SpecDialogProposalFinding[] }) {
  const findings = all.filter((f) => f.specId === ticket.key);
  const specs = ticket.specs ?? [];
  const reviewed = all.some((f) => specs.some((spec) => spec.specId === f.specId));
  return (
    <DialogProposalRow testId={`dialog-graph-ticket-${ticket.key}`} initiallyOpen={number === 1 || findings.length > 0 || reviewed}
      head={
        <>
          <span className="dl-prop-n">{number}</span>
          <span className="dl-prop-main">
            <span className="dl-prop-title">{ticket.title}</span>
            <span className="font-mono dl-prop-dim">{ticket.key}</span>
          </span>
          <span data-testid={`dialog-graph-waits-${ticket.key}`} className="dl-prop-waits">{waitsFor(ticket)}</span>
        </>
      }>
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
        <div className="dl-prop-dim">
          repositories <span className="font-mono">{ticket.repos.join(", ")}</span>
        </div>
      )}
      {specs.length > 0 && (
        <div>
          <div className="fl mb-0.5">Specs</div>
          <ul data-testid={`dialog-graph-specs-${ticket.key}`} className="dl-prop-specs">
            {specs.map((spec) => (
              <li key={spec.specId}>
                <TicketSpec spec={spec} findings={all.filter((f) => f.specId === spec.specId)} />
              </li>
            ))}
          </ul>
        </div>
      )}
    </DialogProposalRow>
  );
}

function TicketSpec({ spec, findings }: { spec: SpecDraftProposal; findings: SpecDialogProposalFinding[] }) {
  return (
    <div data-testid={`dialog-graph-spec-${spec.specId}`} className="dl-prop-spec">
      <span>
        <span className="font-mono dl-prop-dim">{spec.specId}</span>
        <span className="dl-prop-dim"> · </span>
        <span className="text-ink">{spec.goal}</span>
      </span>
      {findings.map((finding, index) => (
        <Finding key={`${spec.specId}-${index}`} finding={finding} testId="dialog-graph-spec-finding" />
      ))}
    </div>
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
