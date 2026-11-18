"use client";

import type { ReactNode } from "react";
import ReactMarkdown from "react-markdown";
import remarkGfm from "remark-gfm";
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

const block = {
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
