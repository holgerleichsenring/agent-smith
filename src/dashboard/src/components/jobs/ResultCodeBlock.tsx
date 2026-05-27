"use client";

import type { ComponentPropsWithoutRef } from "react";

// p0169j-c: code-block renderer for react-markdown. Inline `code` stays
// inline; fenced code blocks (```lang ... ```) render in a terminal-panel
// styled <pre>, anchored on the DESIGN.md token vocabulary.

type CodeProps = ComponentPropsWithoutRef<"code"> & {
  inline?: boolean;
};

export function ResultCodeBlock({
  inline,
  className,
  children,
  ...rest
}: CodeProps) {
  if (inline) {
    return (
      <code
        className="rounded bg-stone-100 px-1 py-0.5 font-mono text-[0.95em] text-stone-800"
        {...rest}
      >
        {children}
      </code>
    );
  }
  const lang = (className ?? "").replace(/^language-/, "").trim();
  return (
    <pre
      className="overflow-auto rounded-md border border-stone-700 bg-stone-900 p-3 text-xs leading-relaxed text-emerald-100"
      data-language={lang || undefined}
      data-testid="result-code-block"
    >
      <code className={className} {...rest}>
        {children}
      </code>
    </pre>
  );
}
