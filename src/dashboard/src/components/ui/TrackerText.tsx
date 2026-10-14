"use client";

import { Fragment, useMemo, type ComponentPropsWithoutRef, type ReactNode } from "react";
import { jsx, jsxs } from "react/jsx-runtime";
import ReactMarkdown from "react-markdown";
import remarkGfm from "remark-gfm";
import rehypeRaw from "rehype-raw";
import rehypeParse from "rehype-parse";
import rehypeSanitize from "rehype-sanitize";
import { unified } from "unified";
import { toJsxRuntime, type Components } from "hast-util-to-jsx-runtime";
import type { TicketTextFormat } from "@/types/tickets";
import { markdownComponents } from "./Markdown";

// A text as a tracker or a conversation wrote it, in the format its source named: markdown (inline HTML
// rendered, then sanitized), html (parsed as a fragment and sanitized with the same schema) or plain text with
// its line breaks. Nothing reaches the page unsanitized and no tag or markdown source is shown.

const trackerComponents = {
  ...markdownComponents,
  li: (p: { className?: string; children?: ReactNode }) => (
    <li className={p.className === "task-list-item" ? "list-none -ml-5" : undefined}>{p.children}</li>
  ),
  input: (p: { type?: string; checked?: boolean }) => (
    p.type === "checkbox" ? <input type="checkbox" checked={!!p.checked} disabled readOnly className="mr-1.5 align-middle" /> : null
  ),
  blockquote: (p: { children?: ReactNode }) => (
    <blockquote className="my-2 border-l-2 border-stone-200 pl-3 text-stone-600">{p.children}</blockquote>
  ),
  strong: (p: { children?: ReactNode }) => <strong className="font-semibold">{p.children}</strong>,
  // A tracker attachment needs the tracker's sign-in, so an image is offered as the link it is.
  img: (p: ComponentPropsWithoutRef<"img">) => (
    typeof p.src === "string" && p.src
      ? <a href={p.src} target="_blank" rel="noreferrer" className="text-emerald-700 underline">{p.alt || p.src}</a>
      : null
  ),
};

const htmlProcessor = unified().use(rehypeParse, { fragment: true }).use(rehypeSanitize);

interface Props {
  text: string;
  format: TicketTextFormat;
}

export function TrackerText({ text, format }: Props) {
  if (format === "html") return <HtmlText html={text} />;
  if (format === "markdown") {
    return (
      <div data-testid="tracker-text" data-format="markdown">
        <ReactMarkdown remarkPlugins={[remarkGfm]} rehypePlugins={[rehypeRaw, rehypeSanitize]} components={trackerComponents}>
          {text}
        </ReactMarkdown>
      </div>
    );
  }
  return <div data-testid="tracker-text" data-format="text" style={{ whiteSpace: "pre-wrap" }}>{text}</div>;
}

function HtmlText({ html }: { html: string }) {
  const rendered = useMemo(() => {
    const tree = htmlProcessor.runSync(htmlProcessor.parse(html));
    return toJsxRuntime(tree, { Fragment, jsx, jsxs, components: trackerComponents as Partial<Components> });
  }, [html]);
  return <div data-testid="tracker-text" data-format="html">{rendered}</div>;
}
