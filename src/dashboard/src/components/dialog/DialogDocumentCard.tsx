"use client";

import { useState } from "react";
import { Markdown } from "@/components/ui/Markdown";
import { documentTitle } from "./documentBlocks";

// 2026-10-01-aeb6b: a document the design partner wrote to be taken elsewhere — a hand-off
// prompt, a summary, a brief. It reads as markdown and copies as the raw text, because the
// place it is pasted into is the one that renders it.

export function DialogDocumentCard({ text }: { text: string }) {
  const [copied, setCopied] = useState(false);
  const title = documentTitle(text) ?? "Document";

  async function copy() {
    try {
      await navigator.clipboard.writeText(text);
      setCopied(true);
      window.setTimeout(() => setCopied(false), 1500);
    } catch {
      /* clipboard blocked — the text is still on the page to select */
    }
  }

  return (
    <div data-testid="dialog-document" className="ecard inert mt-2">
      <div className="flex items-center gap-2.5 px-3 py-2">
        <span className="ec-mark">document</span>
        <span className="ec-name sans min-w-0 flex-1">{title}</span>
        <button
          type="button"
          data-testid="dialog-document-copy"
          aria-label={`Copy the document: ${title}`}
          onClick={copy}
          className="d-link whitespace-nowrap"
        >
          {copied ? "Copied" : "Copy"}
        </button>
      </div>
      <div className="d-strip">
        <Markdown>{text}</Markdown>
      </div>
    </div>
  );
}
