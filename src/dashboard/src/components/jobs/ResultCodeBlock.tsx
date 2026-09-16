"use client";

import type { ComponentPropsWithoutRef, ReactNode } from "react";

// p0169j-c: code-block renderer for react-markdown. Inline `code` stays
// inline; fenced code blocks (```lang ... ```) render in a terminal-panel
// styled <pre>, anchored on the DESIGN.md token vocabulary.
//
// 2026-09-15-cb3e: `inline` no longer ARRIVES. react-markdown stopped passing it, so every
// inline span of code has been rendering as a terminal panel nested inside the paragraph
// that holds it — invalid HTML, and a warning on every markdown surface. The shape of the
// node decides instead: a fenced block carries its language on the class name or spans
// more than one line, and nothing else does.

type CodeProps = ComponentPropsWithoutRef<"code"> & {
  inline?: boolean;
};

export function ResultCodeBlock({
  inline,
  className,
  children,
  ...rest
}: CodeProps) {
  if (inline ?? isInline(className, children)) {
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
      className="card-terminal-panel overflow-auto p-3 dsh-mono leading-relaxed"
      data-language={lang || undefined}
      data-testid="result-code-block"
    >
      <code className={className} {...rest}>
        {children}
      </code>
    </pre>
  );
}

// A fence names its language or carries newlines; an inline span carries neither.
function isInline(className: string | undefined, children: ReactNode): boolean {
  if ((className ?? "").includes("language-")) return false;
  return !String(children ?? "").includes("\n");
}
