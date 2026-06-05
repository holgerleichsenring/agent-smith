"use client";

import type { ReactNode } from "react";
import ReactMarkdown from "react-markdown";
import remarkGfm from "remark-gfm";
import rehypeSanitize from "rehype-sanitize";
import { ResultCodeBlock } from "@/components/jobs/ResultCodeBlock";

// p0221/p0235: shared markdown renderer. The dashboard has NO @tailwindcss/
// typography plugin (it uses the dsh-* dense scale), so `prose` was a no-op and
// markdown rendered raw/unstyled. Instead map each element to the dashboard's
// design tokens: real headings, bordered GFM tables, lists, links. Fenced code
// routes through ResultCodeBlock (DESIGN.md terminal surface, p0219).

const components = {
  h1: (p: { children?: ReactNode }) => (
    <h1 className="dsh-h2 mt-4 mb-2 font-semibold tracking-tight text-stone-900">{p.children}</h1>
  ),
  h2: (p: { children?: ReactNode }) => (
    <h2 className="dsh-h3 mt-4 mb-1.5 font-semibold tracking-tight text-stone-800">{p.children}</h2>
  ),
  h3: (p: { children?: ReactNode }) => (
    <h3 className="dsh-body mt-3 mb-1 font-semibold text-stone-800">{p.children}</h3>
  ),
  p: (p: { children?: ReactNode }) => (
    <p className="dsh-body my-2 leading-relaxed text-stone-700">{p.children}</p>
  ),
  ul: (p: { children?: ReactNode }) => (
    <ul className="my-2 ml-5 list-disc dsh-body space-y-0.5 text-stone-700">{p.children}</ul>
  ),
  ol: (p: { children?: ReactNode }) => (
    <ol className="my-2 ml-5 list-decimal dsh-body space-y-0.5 text-stone-700">{p.children}</ol>
  ),
  a: (p: { href?: string; children?: ReactNode }) => (
    <a href={p.href} target="_blank" rel="noreferrer" className="text-emerald-700 underline hover:text-emerald-800">{p.children}</a>
  ),
  hr: () => <hr className="my-3 border-stone-200" />,
  table: (p: { children?: ReactNode }) => (
    <div className="my-3 overflow-x-auto">
      <table className="w-full border-collapse font-mono dsh-label">{p.children}</table>
    </div>
  ),
  th: (p: { children?: ReactNode }) => (
    <th className="border border-stone-200 bg-stone-50 px-2 py-1 text-left font-semibold text-stone-700">{p.children}</th>
  ),
  td: (p: { children?: ReactNode }) => (
    <td className="border border-stone-200 px-2 py-1 align-top text-stone-600">{p.children}</td>
  ),
  code: ResultCodeBlock,
};

export function Markdown({ children }: { children: string }) {
  return (
    <div className="max-w-none" data-testid="markdown">
      <ReactMarkdown
        remarkPlugins={[remarkGfm]}
        rehypePlugins={[rehypeSanitize]}
        components={components}
      >
        {children}
      </ReactMarkdown>
    </div>
  );
}
