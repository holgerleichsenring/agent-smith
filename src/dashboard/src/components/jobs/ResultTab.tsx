"use client";

import { useCallback, useState } from "react";
import { useResultMarkdown } from "@/hooks/useResultMarkdown";
import { usePlanMarkdown } from "@/hooks/usePlanMarkdown";
import { Markdown } from "@/components/ui/Markdown";
import { splitFrontmatter } from "@/lib/frontmatter";

interface Props {
  runId: string;
  prUrl: string | null;
}

export function ResultTab({ runId, prUrl }: Props) {
  const { content, loading, error } = useResultMarkdown(runId);
  // p0235: plan.md is best-effort — render it above the result when present,
  // never block the result view on its load/error.
  const { content: plan } = usePlanMarkdown(runId);
  const [copied, setCopied] = useState(false);

  const copy = useCallback(async () => {
    if (!content) return;
    try {
      await navigator.clipboard.writeText(content);
      setCopied(true);
      window.setTimeout(() => setCopied(false), 1500);
    } catch {
      /* clipboard blocked — silently no-op */
    }
  }, [content]);

  if (loading) {
    return (
      <p className="text-sm text-stone-500" data-testid="result-loading">
        Loading result…
      </p>
    );
  }

  if (error) {
    return (
      <p className="text-sm text-rose-700" data-testid="result-error">
        Failed to load result: {error}
      </p>
    );
  }

  if (content === null) {
    return <ResultEmptyState prUrl={prUrl} />;
  }

  return (
    <div className="space-y-4" data-testid="result-tab">
      {plan && (
        <section className="space-y-2" data-testid="plan-section">
          <span className="text-xs text-stone-500">plan.md</span>
          <article
            className="max-w-none rounded-lg border border-stone-200 bg-white p-6"
            data-testid="plan-markdown"
          >
            <ResultDocument content={plan} />
          </article>
        </section>
      )}
      <section className="space-y-2">
        <div className="flex items-center justify-between text-xs">
          <span className="text-stone-500">result.md (cache)</span>
          <button
            type="button"
            onClick={copy}
            className="rounded border border-stone-300 bg-white px-2 py-1 text-stone-600 hover:bg-stone-100"
            data-testid="result-copy"
          >
            {copied ? "Copied" : "Copy markdown"}
          </button>
        </div>
        <article
          className="max-w-none rounded-lg border border-stone-200 bg-white p-6"
          data-testid="result-markdown"
        >
          <ResultDocument content={content} />
        </article>
      </section>
    </div>
  );
}

// p0235: result.md leads with a YAML frontmatter block (ticket/cost/repos).
// react-markdown renders `---…---` as an <hr> + a run-on paragraph (ugly).
// Split it off and show it as a clean monospace header; render the body with
// the design-token Markdown component.
export function ResultDocument({ content }: { content: string }) {
  const { frontmatter, body } = splitFrontmatter(content);
  return (
    <>
      {frontmatter && (
        <pre
          data-testid="result-frontmatter"
          className="mb-4 overflow-x-auto rounded-md border border-stone-200 bg-stone-50 p-3 font-mono dsh-label leading-relaxed text-stone-600"
        >
          {frontmatter}
        </pre>
      )}
      <Markdown>{body}</Markdown>
    </>
  );
}

function ResultEmptyState({ prUrl }: { prUrl: string | null }) {
  return (
    <div
      className="rounded-lg border border-dashed border-stone-300 bg-white p-6 text-sm text-stone-600"
      data-testid="result-empty"
    >
      {prUrl !== null ? (
        <>
          <p className="mb-2 font-medium text-stone-800">Result not in cache.</p>
          <p>
            Live cache expires after 24h. The full result is in the PR —{" "}
            <a
              href={prUrl}
              target="_blank"
              rel="noreferrer"
              className="text-stone-800 underline hover:text-stone-900"
              data-testid="result-pr-link"
            >
              view in PR
            </a>
            .
          </p>
        </>
      ) : (
        <>
          <p className="mb-2 font-medium text-stone-800">No result yet.</p>
          <p>The result becomes visible when the run finishes.</p>
        </>
      )}
    </div>
  );
}
