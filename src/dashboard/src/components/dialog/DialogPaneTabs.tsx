"use client";

import type { SpecDialogFilingPush, SpecDialogProposalPush } from "@/types/spec-dialog";
import type { DialogPaneTab } from "./DialogPaneTab";

// 2026-09-25-8e51d: the tablist and the eyebrow beside it, lifted out of DialogPane so a fourth
// tab could be added at all — that file sits over the line limit and may only get shorter, which
// is the rule doing its job rather than something to work around.

export function DialogPaneTabs({
  offered,
  tab,
  tabId,
  panelId,
  onPick,
  status,
  bad,
}: {
  offered: DialogPaneTab[];
  tab: DialogPaneTab;
  tabId: (offer: DialogPaneTab) => string;
  panelId: string;
  onPick: (offer: DialogPaneTab) => void;
  status: string;
  bad: boolean;
}) {
  return (
    <div className="d-head items-center">
      <div role="tablist" className="flex flex-wrap gap-1">
        {offered.map((offer) => (
          <button
            key={offer}
            type="button"
            role="tab"
            id={tabId(offer)}
            data-testid={`dialog-tab-${offer}`}
            aria-selected={offer === tab}
            aria-controls={offer === tab ? panelId : undefined}
            onClick={() => onPick(offer)}
            className="dtab"
          >
            {TAB_LABEL[offer]}
          </button>
        ))}
      </div>
      {/* 2026-09-17-042ef: "Filed" stood three times in one corner — the selected tab, this
          eyebrow and the panel's own heading. The tab is the name, so this says only what the
          name cannot: a state, and nothing at all when there is no state to say. */}
      <span className={bad ? "fl bad" : "fl"}>{status}</span>
    </div>
  );
}

const TAB_LABEL: Record<DialogPaneTab, string> = {
  proposal: "Proposal",
  scope: "Scope",
  filed: "Filed",
  approved: "Approved spec",
};

export function statusOf(
  tab: DialogPaneTab,
  shown: SpecDialogProposalPush | null,
  latest: SpecDialogProposalPush | null,
  filed: SpecDialogFilingPush | null,
): string {
  if (tab === "filed") return filed?.error ? "filing failed" : "";
  // The scope pane has no state to report — it is a list of what a conversation may read, and
  // the panel says that in a sentence of its own. An eyebrow repeating it was the same noise
  // the heading was, one tab over.
  if (tab === "scope") return "";
  // 2026-09-25-8e51d: nor does the approved set — the panel names whose approval it shows and
  // when, which is the only state it has.
  if (tab === "approved") return "";
  if (shown !== latest) return "superseded";
  return filed ? "filed" : "not filed yet";
}
