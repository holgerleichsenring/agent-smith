"use client";

import type {
  SpecDialogPhaseProposal,
  SpecDialogProposalPush,
} from "@/types/spec-dialog";

// 2026-09-15-6d9c: WHAT WOULD BE FILED, rendered by kind. The proposal is the part of a
// turn that is typed — the reply beside it is prose the transcript already carries, and a
// second rendering of it would disagree with the first the moment the master worded one
// differently.
//
// An epic's children are listed IN THE ORDER THEY WILL BE FILED: the backend orders them
// with the orderer the filer itself runs, so this column cannot show a plan other than the
// one about to be created.
//
// 2026-09-17-0e79d: that order is also the order ONE run works them in — the parent draft is
// the work ticket carrying the whole approved set, and each slice is a record beside it. So
// "waits for" names the edge the cut declared, not a ticket held in a queue behind another.
//
// There is deliberately no "what it had to assume" section: the design-partner master's
// phase template emits neither facts nor assumptions, so one would be blank on every draft
// this conversation can produce.
//
// 2026-09-17-c7aed: the raw form is one disclosure under the structure, as it will be filed.
//
// 2026-09-17-042ef: every heading above a list is the studio's field label and every slice is
// its panel card, so the local eyebrow this file declared is gone.

export function DialogProposalPanel({ proposal }: { proposal: SpecDialogProposalPush }) {
  const phases = [proposal.parent, proposal.phase, ...proposal.children].filter(
    (phase): phase is SpecDialogPhaseProposal => phase !== null,
  );
  return (
    <div data-testid="dialog-proposal" data-kind={proposal.kind} className="flex flex-col gap-3.5">
      {proposal.bug && (
        <div data-testid="dialog-proposal-bug">
          <Label>A bug ticket</Label>
          <h3 className="dsh-h3 font-semibold text-ink">{proposal.bug.title}</h3>
          <p className="mt-1 dsh-body whitespace-pre-wrap text-ink">{proposal.bug.body}</p>
        </div>
      )}
      {proposal.phase && <Phase phase={proposal.phase} label="One phase" />}
      {proposal.parent && <Phase phase={proposal.parent} label="The work ticket — one run works every slice" />}
      {proposal.children.length > 0 && (
        <div>
          <Label>Slices, in the order they will be filed</Label>
          <ol data-testid="dialog-proposal-children" className="ml-5 flex list-decimal flex-col gap-2 marker:font-mono marker:text-body">
            {proposal.children.map((child, index) => (
              <li key={child.phaseId}>
                <Slice slice={child} siblings={proposal.children} open={index === 0} />
              </li>
            ))}
          </ol>
        </div>
      )}
      {phases.some((phase) => phase.yaml) && (
        <details>
          <summary className="fl cursor-pointer">
            The same thing, as it will be filed
          </summary>
          {phases.filter((phase) => phase.yaml).map((phase) => (
            <pre
              key={phase.phaseId}
              data-testid={`dialog-proposal-raw-${phase.phaseId}`}
              className="card-terminal-panel mt-1.5 overflow-x-auto whitespace-pre px-3 py-2.5 dsh-mono"
            >
              {phase.yaml}
            </pre>
          ))}
        </details>
      )}
    </div>
  );
}

function Phase({ phase, label }: { phase: SpecDialogPhaseProposal; label: string }) {
  return (
    <div data-testid={`dialog-proposal-phase-${phase.phaseId}`}>
      <Label>{label}</Label>
      <h3 className="dsh-h3 font-semibold text-ink">{phase.goal}</h3>
      <div className="fv">{phase.phaseId}</div>
      <Sections phase={phase} />
    </div>
  );
}

function Slice({
  slice,
  siblings,
  open,
}: {
  slice: SpecDialogPhaseProposal;
  siblings: SpecDialogPhaseProposal[];
  open: boolean;
}) {
  return (
    <details
      data-testid={`dialog-proposal-phase-${slice.phaseId}`}
      open={open}
      className="ecard inert"
    >
      <summary className="flex cursor-pointer items-baseline gap-2 px-2.5 py-2">
        <span className="ec-mark">{slice.phaseId}</span>
        <span className="flex-1 dsh-body font-medium text-ink">{slice.goal}</span>
      </summary>
      <div className="px-2.5 pb-2.5">
        <Lines
          label="Waits for"
          items={slice.requires.map((id) => {
            const at = siblings.findIndex((sibling) => sibling.phaseId === id);
            return at < 0 ? id : `Slice ${at + 1} · ${id}`;
          })}
        />
        <Sections phase={slice} withoutRequires />
      </div>
    </details>
  );
}

function Sections({ phase, withoutRequires }: { phase: SpecDialogPhaseProposal; withoutRequires?: boolean }) {
  return (
    <>
      <Lines label="Steps" items={phase.steps} />
      <Lines label="Tests" items={phase.tests} mono />
      <Lines label="Done when" items={phase.done} />
      {!withoutRequires && <Lines label="Waits for" items={phase.requires} />}
    </>
  );
}

// A section the draft says nothing about is left out rather than shown empty: a blank
// heading is a claim that the master wrote nothing there, which is not what it means.
function Lines({ label, items, mono }: { label: string; items: string[]; mono?: boolean }) {
  if (items.length === 0) return null;
  return (
    <div className="mt-2">
      <Label>{label}</Label>
      <ul className={mono ? "ml-4 list-disc font-mono dsh-mono text-ink" : "ml-4 list-disc dsh-body text-ink"}>
        {items.map((item) => (
          <li key={item}>{item}</li>
        ))}
      </ul>
    </div>
  );
}

/** The studio's field label — what the lines under it are. */
function Label({ children }: { children: string }) {
  return <div className="fl mb-0.5">{children}</div>;
}
