"use client";

import type { ReactNode } from "react";
import ReactMarkdown from "react-markdown";
import remarkGfm from "remark-gfm";
import { useT } from "@/hooks/useT";
import rehypeSanitize from "rehype-sanitize";

// 2026-11-18-e06bb: the log's text is written by agents in markdown. InlineText draws one line of it — bold, emphasis,
// code, links — inside the line it sits in, so "**Title**: reason" reads as a bold title and not as asterisks.
// BlockText draws a longer text — paragraphs, lists, code — in the log's own type, no card and no heading scale.

const INLINE = ["strong", "em", "code", "a", "del", "p", "br"];

const inline = {
  p: ({ children }: { children?: ReactNode }) => <span className="rl-p">{children}</span>,
  code: ({ children }: { children?: ReactNode }) => <code className="rl-code">{children}</code>,
  a: ({ href, children }: { href?: string; children?: ReactNode }) => <a href={href} target="_blank" rel="noreferrer">{children}</a>,
};

export function InlineText({ text }: { text: string }) {
  return (
    <ReactMarkdown remarkPlugins={[remarkGfm]} rehypePlugins={[rehypeSanitize]} allowedElements={INLINE} unwrapDisallowed
      components={inline}>
      {text}
    </ReactMarkdown>
  );
}

// 2026-11-22-7103a: a GFM table scrolls sideways inside its own wrapper instead of squeezing its columns to one letter.
const block = {
  table: ({ children }: { children?: ReactNode }) => <div className="rl-md-table"><table>{children}</table></div>,
  code: ({ children }: { children?: ReactNode }) => <code className="rl-code">{children}</code>,
  a: ({ href, children }: { href?: string; children?: ReactNode }) => <a href={href} target="_blank" rel="noreferrer">{children}</a>,
};

export function BlockText({ text }: { text: string }) {
  return (
    <div className="rl-md" data-testid="log-block-text">
      <ReactMarkdown remarkPlugins={[remarkGfm]} rehypePlugins={[rehypeSanitize]} components={block}>{text}</ReactMarkdown>
    </div>
  );
}

/** 2026-11-17-6d9eb: a long document behind a one-line disclosure, so the log stays a log. 2026-11-22-7103a: shared —
 *  the master's report folds through it on both pages. */
export function LogDoc({ label, testId, children }: { label: ReactNode; testId: string; children: ReactNode }) {
  return (
    <details className="rl-doc" data-testid={testId}>
      <summary>{label}</summary>
      <div className="rl-doc-body">{children}</div>
    </details>
  );
}

/** 2026-11-22-7103a: a spec master's report, closed under "Master's report" until someone wants to read it — "Spec n ·
 *  id · Master's report" where several share a place. Unfolded (`folded={false}`) it is shown whole, for a place that is
 *  itself a fold holding only this report. */
export function MasterReport({ text, spec, testId, folded = true }: { text: string; spec?: string | null; testId?: string; folded?: boolean }) {
  const t = useT();
  if (!folded) return <div className="rl-report" data-testid={testId ?? "master-report"}><BlockText text={text} /></div>;
  return (
    <LogDoc label={spec ? t("runs.log.reportOf", { spec }) : t("runs.log.report")} testId={testId ?? "master-report"}>
      <BlockText text={text} />
    </LogDoc>
  );
}
