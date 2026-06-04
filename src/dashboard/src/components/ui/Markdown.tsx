"use client";

import ReactMarkdown from "react-markdown";
import remarkGfm from "remark-gfm";
import rehypeSanitize from "rehype-sanitize";
import { ResultCodeBlock } from "@/components/jobs/ResultCodeBlock";

// p0221: shared markdown renderer. Same wiring as the result.md view — GFM,
// sanitised HTML, and fenced code blocks routed through ResultCodeBlock so they
// land on the DESIGN.md card-terminal-panel surface (p0219).

export function Markdown({ children }: { children: string }) {
  return (
    <div className="prose prose-stone max-w-none dsh-body" data-testid="markdown">
      <ReactMarkdown
        remarkPlugins={[remarkGfm]}
        rehypePlugins={[rehypeSanitize]}
        components={{ code: ResultCodeBlock }}
      >
        {children}
      </ReactMarkdown>
    </div>
  );
}
