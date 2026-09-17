"use client";

import { useId, useState } from "react";
import type {
  SpecDialogFilingPush,
  SpecDialogProject,
  SpecDialogProposalPush,
  SpecDialogSession,
} from "@/types/spec-dialog";
import { DialogFiledPanel } from "./DialogFiledPanel";
import { DialogProposalPanel } from "./DialogProposalPanel";
import { DialogScopePanel } from "./DialogScopePanel";

// 2026-09-15-6d9c: the right-hand column shows what the conversation is currently about —
// what the agent may read until it has proposed something, the proposal while it is being
// decided, and the tickets that exist once it has been.
// 2026-09-17-c7aed: the three are tabs, and a tab is offered only when it has something to
// show. The current one still follows the conversation: a new proposal or filing moves the
// pane to it, and a card in the exchange moves it to the proposal that card belongs to.

export type DialogPaneTab = "proposal" | "scope" | "filed";

export interface DialogPaneFocus {
  tab: DialogPaneTab;
  /** A card's proposal, when the operator inspected one that is not the latest. */
  proposal?: SpecDialogProposalPush;
}

export function DialogPane({
  session,
  projects,
  proposal,
  filed,
  focus,
  onFocus,
}: {
  session: SpecDialogSession | null;
  projects: SpecDialogProject[];
  proposal: SpecDialogProposalPush | null;
  filed: SpecDialogFilingPush | null;
  /** The operator's own choice, kept only while the conversation's outcome is unchanged. */
  focus: DialogPaneFocus | null;
  onFocus: (focus: DialogPaneFocus) => void;
}) {
  const shownProposal = focus?.tab === "proposal" && focus.proposal ? focus.proposal : proposal;
  const offered: DialogPaneTab[] = [
    ...(shownProposal ? (["proposal"] as const) : []),
    ...(session || projects.length > 0 ? (["scope"] as const) : []),
    ...(filed ? (["filed"] as const) : []),
  ];
  const fallback: DialogPaneTab = filed ? "filed" : proposal ? "proposal" : "scope";
  const tab = focus && offered.includes(focus.tab) ? focus.tab : fallback;
  const ids = useId();
  const tabId = (offer: DialogPaneTab) => `${ids}-tab-${offer}`;
  const panelId = `${ids}-panel`;

  return (
    <aside
      data-testid="dialog-pane"
      data-tab={tab}
      className="rounded-md border border-mute bg-canvas"
    >
      <div className="flex items-center justify-between gap-2 border-b border-mute px-3 py-2">
        <div role="tablist" className="flex gap-0.5">
          {offered.map((offer) => (
            <button
              key={offer}
              type="button"
              role="tab"
              id={tabId(offer)}
              data-testid={`dialog-tab-${offer}`}
              aria-selected={offer === tab}
              aria-controls={offer === tab ? panelId : undefined}
              onClick={() => onFocus({ tab: offer })}
              className="rounded-sm px-2 py-0.5 dsh-label font-semibold text-body aria-selected:bg-canvas-soft aria-selected:text-primary-deep"
            >
              {TAB_LABEL[offer]}
            </button>
          ))}
        </div>
        <span className="eyebrow-uppercase text-body">{statusOf(tab, shownProposal, proposal, filed)}</span>
      </div>
      <div
        role="tabpanel"
        id={panelId}
        aria-labelledby={offered.includes(tab) ? tabId(tab) : undefined}
        className="p-3"
      >
        {tab === "filed" && filed && <DialogFiledPanel filed={filed} />}
        {tab === "proposal" && shownProposal && <DialogProposalPanel proposal={shownProposal} />}
        {tab === "scope" && <DialogScopePanel session={session} projects={projects} />}
      </div>
    </aside>
  );
}

const TAB_LABEL: Record<DialogPaneTab, string> = {
  proposal: "Proposal",
  scope: "Scope",
  filed: "Filed",
};

function statusOf(
  tab: DialogPaneTab,
  shown: SpecDialogProposalPush | null,
  latest: SpecDialogProposalPush | null,
  filed: SpecDialogFilingPush | null,
): string {
  if (tab === "filed") return filed?.error ? "filing failed" : "filed";
  if (tab === "scope") return "what it may read";
  if (shown !== latest) return "superseded";
  return filed ? "filed" : "not filed yet";
}

/** The pane's choice lasts until the outcome changes; a new proposal or filing takes over. */
export function useDialogPaneFocus(
  proposal: SpecDialogProposalPush | null,
  filed: SpecDialogFilingPush | null,
): [DialogPaneFocus | null, (focus: DialogPaneFocus) => void] {
  const [held, setHeld] = useState<{
    proposal: SpecDialogProposalPush | null;
    filed: SpecDialogFilingPush | null;
    focus: DialogPaneFocus;
  } | null>(null);
  const focus = held && held.proposal === proposal && held.filed === filed ? held.focus : null;
  return [focus, (next) => setHeld({ proposal, filed, focus: next })];
}
