"use client";

import type {
  SpecDraftProposal,
  SpecDialogMock,
  SpecDialogProposalPush,
} from "@/types/spec-dialog";
import { DialogProposalGraph } from "./DialogProposalGraph";
import { MockVersions } from "./MockVersions";

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
// 2026-10-02-3f06c: what a spec rests on is shown under its plan — each fact with the evidence
// it cites, then what it states without having looked. The partner writes both now, and a
// finding that a citation does not resolve is only readable beside the fact it is against.
//
// 2026-09-17-c7aed: the raw form is one disclosure under the structure, as it will be filed.
//
// 2026-09-17-042ef: every heading above a list is the studio's field label and every slice is
// its panel card, so the local eyebrow this file declared is gone.
//
// 2026-10-13-8762t: a spec with mock versions shows them as a strip beside its sections, with Ratify.

/** What the strip needs, handed through to every spec and slice; absent, no strip is drawn. */
interface MockStrip {
  mocks?: SpecDialogMock[] | null;
  ratifiedMocks?: Record<string, number> | null;
  sessionId?: string | null;
  onRatified?: () => Promise<void>;
  filed?: boolean;
}

export function DialogProposalPanel({ proposal, ...strip }: { proposal: SpecDialogProposalPush } & MockStrip) {
  const specs = [proposal.parent, proposal.spec, ...proposal.children].filter(
    (spec): spec is SpecDraftProposal => spec !== null,
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
      {proposal.kind === "tickets" && <DialogProposalGraph proposal={proposal} />}
      {proposal.spec && <Spec spec={proposal.spec} label="One spec" strip={strip} />}
      {proposal.parent && <Spec spec={proposal.parent} label="The work ticket — one run works every slice" strip={strip} />}
      {proposal.children.length > 0 && (
        <div>
          <Label>Slices, in the order they will be filed</Label>
          <ol data-testid="dialog-proposal-children" className="ml-5 flex list-decimal flex-col gap-2 marker:font-mono marker:text-body">
            {proposal.children.map((child, index) => (
              <li key={child.specId}>
                <Slice slice={child} siblings={proposal.children} open={index === 0} strip={strip} />
              </li>
            ))}
          </ol>
        </div>
      )}
      {specs.some((spec) => spec.yaml) && (
        <details>
          <summary className="fl cursor-pointer">
            The same thing, as it will be filed
          </summary>
          {specs.filter((spec) => spec.yaml).map((spec) => (
            <pre
              key={spec.specId}
              data-testid={`dialog-proposal-raw-${spec.specId}`}
              className="card-terminal-panel mt-1.5 overflow-x-auto whitespace-pre px-3 py-2.5 dsh-mono"
            >
              {spec.yaml}
            </pre>
          ))}
        </details>
      )}
    </div>
  );
}

function Spec({ spec, label, strip }: { spec: SpecDraftProposal; label: string; strip: MockStrip }) {
  return (
    <div data-testid={`dialog-proposal-spec-${spec.specId}`}>
      <Label>{label}</Label>
      <h3 className="dsh-h3 font-semibold text-ink">{spec.goal}</h3>
      <div className="fv">{spec.specId}</div>
      <Sections spec={spec} />
      <Mocks specId={spec.specId} strip={strip} />
    </div>
  );
}

function Slice({
  slice,
  siblings,
  open,
  strip,
}: {
  slice: SpecDraftProposal;
  siblings: SpecDraftProposal[];
  open: boolean;
  strip: MockStrip;
}) {
  return (
    <details
      data-testid={`dialog-proposal-spec-${slice.specId}`}
      open={open}
      className="ecard inert"
    >
      <summary className="flex cursor-pointer items-baseline gap-2 px-2.5 py-2">
        <span className="ec-mark">{slice.specId}</span>
        <span className="flex-1 dsh-body font-medium text-ink">{slice.goal}</span>
      </summary>
      <div className="px-2.5 pb-2.5">
        <Lines
          label="Waits for"
          items={slice.requires.map((id) => {
            const at = siblings.findIndex((sibling) => sibling.specId === id);
            return at < 0 ? id : `Slice ${at + 1} · ${id}`;
          })}
        />
        <Sections spec={slice} withoutRequires />
        <Mocks specId={slice.specId} strip={strip} />
      </div>
    </details>
  );
}

function Sections({ spec, withoutRequires }: { spec: SpecDraftProposal; withoutRequires?: boolean }) {
  return (
    <>
      <Lines label="Steps" items={spec.steps} />
      <Lines label="Tests" items={spec.tests} mono />
      <Lines label="Done when" items={spec.done} />
      {!withoutRequires && <Lines label="Waits for" items={spec.requires} />}
      <RestsOn spec={spec} />
      <Lines label="Assumed, not looked at" items={spec.assumptions ?? []} />
    </>
  );
}

function Mocks({ specId, strip }: { specId: string; strip: MockStrip }) {
  const versions = (strip.mocks ?? []).filter((mock) => mock.specId === specId).sort((a, b) => a.version - b.version);
  if (!strip.sessionId || versions.length === 0) return null;
  return (
    <MockVersions specId={specId} mocks={versions} ratified={strip.ratifiedMocks?.[specId] ?? null}
      sessionId={strip.sessionId} onRatified={strip.onRatified} filed={strip.filed ?? false} />
  );
}

function RestsOn({ spec }: { spec: SpecDraftProposal }) {
  const facts = spec.facts ?? [];
  if (facts.length === 0) return null;
  return (
    <div className="mt-2" data-testid={`dialog-proposal-facts-${spec.specId}`}>
      <Label>Rests on</Label>
      <ul className="ml-4 list-disc dsh-body text-ink">
        {facts.map((fact) => (
          <li key={`${fact.claim}|${fact.evidence}`}>
            {fact.claim}
            {fact.evidence && <div className="font-mono dsh-mono text-body">{fact.evidence}</div>}
          </li>
        ))}
      </ul>
    </div>
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
