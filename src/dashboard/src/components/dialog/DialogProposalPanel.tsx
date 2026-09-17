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
// There is deliberately no "what it had to assume" section: the design-partner master's
// phase template emits neither facts nor assumptions, so one would be blank on every draft
// this conversation can produce.

export function DialogProposalPanel({ proposal }: { proposal: SpecDialogProposalPush }) {
  return (
    <aside
      data-testid="dialog-proposal"
      data-kind={proposal.kind}
      className="rounded border border-stone-200 p-3"
    >
      <h2 className="dsh-h3 mb-1 font-semibold text-stone-800">Proposal</h2>
      <p className="mb-2 text-xs text-[var(--color-ink-mid)]">
        {HEADLINE[proposal.kind] ?? "What this turn would file."}
      </p>
      {proposal.bug && (
        <div data-testid="dialog-proposal-bug">
          <div className="dsh-body font-semibold text-stone-800">{proposal.bug.title}</div>
          <p className="mt-1 dsh-label whitespace-pre-wrap text-stone-700">
            {proposal.bug.body}
          </p>
        </div>
      )}
      {proposal.phase && <Phase phase={proposal.phase} />}
      {proposal.parent && <Phase phase={proposal.parent} label="Parent" />}
      {proposal.children.length > 0 && (
        <div className="mt-2">
          <div className="dsh-label uppercase tracking-wide text-[var(--color-ink-mid)]">
            Slices, in the order they will be filed
          </div>
          <ol data-testid="dialog-proposal-children" className="ml-4 list-decimal">
            {proposal.children.map((child) => (
              <li key={child.phaseId}>
                <Phase phase={child} />
              </li>
            ))}
          </ol>
        </div>
      )}
    </aside>
  );
}

const HEADLINE: Record<string, string> = {
  bug: "A fix-bug ticket would be filed.",
  phase: "One phase would be filed.",
  epic: "An epic would be filed: a parent record and its slices.",
};

function Phase({ phase, label }: { phase: SpecDialogPhaseProposal; label?: string }) {
  return (
    <div data-testid={`dialog-proposal-phase-${phase.phaseId}`} className="mb-2">
      {label && (
        <div className="dsh-label uppercase tracking-wide text-[var(--color-ink-mid)]">
          {label}
        </div>
      )}
      <div className="dsh-body font-semibold text-stone-800">
        {phase.phaseId} — {phase.goal}
      </div>
      <Lines label="Steps" items={phase.steps} />
      <Lines label="Tests" items={phase.tests} />
      <Lines label="Done" items={phase.done} />
      <Lines label="Requires" items={phase.requires} />
      {/* On this surface the reply no longer carries the draft, so its raw form lives here. */}
      {phase.yaml && (
        <details data-testid={`dialog-proposal-raw-${phase.phaseId}`} className="mt-1">
          <summary className="dsh-label cursor-pointer text-[var(--color-ink-mid)]">Raw spec</summary>
          <pre className="dsh-label mt-1 overflow-x-auto whitespace-pre text-stone-700">{phase.yaml}</pre>
        </details>
      )}
    </div>
  );
}

// A section the draft says nothing about is left out rather than shown empty: a blank
// heading is a claim that the master wrote nothing there, which is not what it means.
function Lines({ label, items }: { label: string; items: string[] }) {
  if (items.length === 0) return null;
  return (
    <div className="mt-1">
      <div className="dsh-label uppercase tracking-wide text-[var(--color-ink-mid)]">{label}</div>
      <ul className="dsh-label ml-4 list-disc text-stone-700">
        {items.map((item) => (
          <li key={item}>{item}</li>
        ))}
      </ul>
    </div>
  );
}
