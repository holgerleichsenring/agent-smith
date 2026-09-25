"use client";

import { useId, useState } from "react";
import type { Ref } from "react";
import type {
  FiledWork,
  SpecDialogFilingPush,
  SpecDialogProject,
  SpecDialogProposalPush,
  SpecDialogSession,
} from "@/types/spec-dialog";
import { DialogFiledPanel } from "./DialogFiledPanel";
import { DialogProposalPanel } from "./DialogProposalPanel";
import { DialogApprovedPanel } from "./DialogApprovedPanel";
import { DialogPaneTabs, statusOf } from "./DialogPaneTabs";
import { DialogScopePanel } from "./DialogScopePanel";
import type { DialogPaneTab } from "./DialogPaneTab";
export type { DialogPaneTab } from "./DialogPaneTab";

// 2026-09-15-6d9c: the right-hand column shows what the conversation is currently about —
// what the agent may read until it has proposed something, the proposal while it is being
// decided, and the tickets that exist once it has been.
// 2026-09-17-c7aed: the three are tabs, and a tab is offered only when it has something to
// show. The current one still follows the conversation: a new proposal or filing moves the
// pane to it, and a card in the exchange moves it to the proposal that card belongs to.
// 2026-09-17-042ef: the pane is the Projects page's panel card and its tabs are the studio's
// own drawer tabs — the selected state is read off aria-selected, which the tablist already
// carries, so nothing on this page has to maintain a second flag for it.



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
  work,
  focus,
  onFocus,
  paneRef,
  panelRef,
  marked = false,
}: {
  session: SpecDialogSession | null;
  projects: SpecDialogProject[];
  proposal: SpecDialogProposalPush | null;
  filed: SpecDialogFilingPush | null;
  /** 2026-09-17-042ej: what became of that filing; null until its own read comes back. */
  work: FiledWork | null;
  /** The operator's own choice, kept only while the conversation's outcome is unchanged. */
  focus: DialogPaneFocus | null;
  onFocus: (focus: DialogPaneFocus) => void;
  /** 2026-09-20-4b0ae: the card itself, for the surface to scroll to and to mark. */
  paneRef?: Ref<HTMLElement>;
  /** The panel, for the surface to move focus to. It is what an inspected proposal is read in. */
  panelRef?: Ref<HTMLDivElement>;
  /** Whether the pane is wearing the mark an inspect just put on it. */
  marked?: boolean;
}) {
  const shownProposal = focus?.tab === "proposal" && focus.proposal ? focus.proposal : proposal;
  const offered: DialogPaneTab[] = [
    ...(shownProposal ? (["proposal"] as const) : []),
    ...(session || projects.length > 0 ? (["scope"] as const) : []),
    ...(filed ? (["filed"] as const) : []),
    // 2026-09-25-8e51d: a conversation BOUND to a ticket has an approved specification to show
    // even when it filed nothing itself, which is the case this tab exists for.
    ...(work?.approved ? (["approved"] as const) : []),
  ];
  const fallback: DialogPaneTab = filed ? "filed" : proposal ? "proposal" : "scope";
  const tab = focus && offered.includes(focus.tab) ? focus.tab : fallback;
  const ids = useId();
  const tabId = (offer: DialogPaneTab) => `${ids}-tab-${offer}`;
  const panelId = `${ids}-panel`;

  return (
    // 2026-09-20-4b0ae: an inspect from the exchange usually selects the proposal the pane is
    // already showing, so the act leaves this card's contents identical. data-inspected is what
    // says it happened, and the surface that set it takes it off again.
    <aside
      ref={paneRef}
      data-testid="dialog-pane"
      data-tab={tab}
      data-inspected={marked ? "true" : undefined}
      className="ecard inert d-pane"
    >
      <DialogPaneTabs
        offered={offered}
        tab={tab}
        tabId={tabId}
        panelId={panelId}
        onPick={(offer) => onFocus({ tab: offer })}
        status={statusOf(tab, shownProposal, proposal, filed)}
        bad={tab === "filed" && !!filed?.error}
      />
      {/* 2026-09-20-4b0ae: tabIndex minus one is focusable programmatically and only so — it
          keeps the panel out of the tab order the tablist and the controls inside it already
          provide, while letting an inspect from the exchange land the reader on what it selected. */}
      <div
        ref={panelRef}
        role="tabpanel"
        id={panelId}
        aria-labelledby={offered.includes(tab) ? tabId(tab) : undefined}
        tabIndex={-1}
        className="d-body d-panel"
      >
        {tab === "filed" && filed && <DialogFiledPanel filed={filed} work={work} />}
        {tab === "proposal" && shownProposal && <DialogProposalPanel proposal={shownProposal} />}
        {tab === "scope" && <DialogScopePanel session={session} projects={projects} />}
        {tab === "approved" && work?.approved && (
          <DialogApprovedPanel approved={work.approved} />
        )}
      </div>
    </aside>
  );
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
