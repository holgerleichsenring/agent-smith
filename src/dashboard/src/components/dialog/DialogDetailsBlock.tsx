"use client";

import { useState } from "react";
import { useT } from "@/hooks/useT";
import { Markdown } from "@/components/ui/Markdown";

// 2026-11-28-d1ffc: what a message keeps one click away (a ````details fence) — the series check's evidence, the specs it could
// not read, its cost — collapsed under the message behind "show all", so the message itself stays a few lines.

export function DialogDetailsBlock({ text }: { text: string }) {
  const t = useT();
  const [open, setOpen] = useState(false);
  return (
    <div data-testid="dialog-details">
      <button type="button" className="d-link" aria-expanded={open} data-testid="dialog-details-toggle"
        onClick={() => setOpen((was) => !was)}>
        {open ? t("dialog.details.hide") : t("dialog.details.show")}
      </button>
      {open && <div data-testid="dialog-details-body"><Markdown>{text}</Markdown></div>}
    </div>
  );
}
