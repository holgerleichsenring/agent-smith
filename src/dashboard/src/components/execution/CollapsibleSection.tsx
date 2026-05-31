"use client";

import { useState, type ReactNode } from "react";

interface CollapsibleSectionProps {
  title: string;
  meta?: string;
  defaultOpen?: boolean;
  testId?: string;
  children: ReactNode;
}

// p0183: small card with a clickable header that toggles its body. Used
// by RunDetail's Architecture + Result sections — both orthogonal to the
// main execution tree, both worth keeping out of the operator's way until
// they want them.
export function CollapsibleSection({
  title,
  meta,
  defaultOpen = false,
  testId,
  children,
}: CollapsibleSectionProps) {
  const [open, setOpen] = useState(defaultOpen);
  return (
    <section
      data-testid={testId}
      className="overflow-hidden rounded-lg border border-stone-200 bg-white"
    >
      <button
        type="button"
        data-testid={testId ? `${testId}-toggle` : undefined}
        onClick={() => setOpen((o) => !o)}
        className="flex w-full items-center gap-2.5 px-4 py-2.5 text-left hover:bg-stone-50"
      >
        <span
          className={`text-[10px] text-stone-400 transition-transform ${open ? "rotate-90" : ""}`}
          aria-hidden="true"
        >
          ▶
        </span>
        <span className="flex-1 text-sm font-medium text-stone-900">
          {title}
          {meta && <small className="ml-2 font-normal text-stone-400">{meta}</small>}
        </span>
      </button>
      {open && <div className="border-t border-stone-100 p-4">{children}</div>}
    </section>
  );
}
