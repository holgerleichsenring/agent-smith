"use client";

import { useEffect, useId, useState, type ReactNode } from "react";

// 2026-11-19-1b72b: a proposed ticket or slice in the pane is a plain row under a hairline, like the run view's section
// line — no card, no fill. Its head is a button that opens the row in place (▸/▾ drawn by the stylesheet, so the row's
// text is only what it says); what it holds sits indented beneath it,
// kept in the page while closed as a closed <details> kept it.

export function DialogProposalRow({ testId, initiallyOpen, head, children }: {
  testId: string;
  initiallyOpen: boolean;
  head: ReactNode;
  children: ReactNode;
}) {
  const [open, setOpen] = useState(initiallyOpen);
  // A later review that finds something on the row opens it, as the controlled <details> did; a row the person opened
  // stays open.
  useEffect(() => {
    if (initiallyOpen) setOpen(true);
  }, [initiallyOpen]);
  const bodyId = useId();
  return (
    <div data-testid={testId} className="dl-prop-row">
      <button type="button" className="dl-prop-head" aria-expanded={open} aria-controls={bodyId} onClick={() => setOpen((o) => !o)}>
        {head}
      </button>
      <div id={bodyId} className="dl-prop-body" hidden={!open}>
        {children}
      </div>
    </div>
  );
}
