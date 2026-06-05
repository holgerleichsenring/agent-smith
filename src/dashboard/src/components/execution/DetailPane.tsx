"use client";

import type { ExecutionNodeProps } from "./ExecutionNode";
import type { NodeStatus } from "./TimingGutter";

// p0205: the right pane of the two-pane run detail. Renders the selected rail
// node in full: breadcrumb, title + status pill, a meta line (duration + cost),
// the handler outcome message, and the node's body (sandboxes / LLM calls /
// catalog binding / event stream — already composed by useRunExecutionTree).
// Overview nodes (Architecture / Result) are rendered by the page directly, so
// this component only ever sees execution nodes.

interface DetailPaneProps {
  node: ExecutionNodeProps | null;
  parentLabel: string | null;
}

const PILL_TEXT: Record<NodeStatus, string> = {
  ok: "done",
  fail: "failed",
  run: "running",
  wait: "waiting",
};

const PILL_CLS: Record<NodeStatus, string> = {
  ok: "bg-emerald-50 text-emerald-700",
  fail: "bg-rose-50 text-rose-700",
  run: "bg-amber-50 text-amber-700",
  wait: "bg-stone-100 text-stone-600",
};

export function DetailPane({ node, parentLabel }: DetailPaneProps) {
  if (!node) {
    return (
      <div data-testid="detail-pane" className="content-shell text-sm text-stone-400">
        Select a step from the rail to inspect it.
      </div>
    );
  }
  const meta = buildMeta(node);
  return (
    <div data-testid="detail-pane" className="content-shell h-full overflow-y-auto">
      <div className="breadcrumb">
        {parentLabel ? `${parentLabel} ›` : "Execution ›"}
      </div>
      <div className="flex items-center gap-3 dsh-h2 font-semibold tracking-tight">
        <span data-testid="detail-pane-title">{node.label}</span>
        <span
          data-testid="detail-pane-pill"
          className={`rounded-full px-2.5 py-0.5 dsh-label font-semibold ${PILL_CLS[node.status]}`}
        >
          {PILL_TEXT[node.status]}
        </span>
      </div>
      {meta.length > 0 && (
        <div className="mt-2 flex flex-wrap gap-x-4 gap-y-1 font-mono dsh-mono text-stone-500">
          {meta.map((m) => (
            <span key={m}>{m}</span>
          ))}
        </div>
      )}
      {node.message && (
        <p data-testid="detail-pane-message" className="mt-3 max-w-2xl text-sm leading-relaxed text-stone-600">
          <LinkifiedText text={node.message} />
        </p>
      )}
      {node.repoSummary && (
        <p className="mt-1 font-mono dsh-mono text-stone-500">{node.repoSummary.text}</p>
      )}
      <div className="mt-4 border-t border-stone-100 pt-4">
        {node.body ?? (
          <div className="text-sm text-stone-400">No sub-events — fully described above.</div>
        )}
      </div>
    </div>
  );
}

// p0228: step messages carry plain-text URLs (e.g. "Pull request created:
// https://…") that the operator wants to click. Split on URLs and render each
// as a link "button" — there may be several (one PR per repo on a multi-repo
// run). The proper per-repo, repo-labelled buttons come from PullRequestOutcome
// events (PrOutcomeList); this catches the plain-message fallback too.
const URL_SPLIT_RE = /(https?:\/\/[^\s]+)/g;
const IS_URL_RE = /^https?:\/\//; // non-global: safe for repeated .test()

function LinkifiedText({ text }: { text: string }) {
  const parts = text.split(URL_SPLIT_RE);
  return (
    <>
      {parts.map((part, i) =>
        IS_URL_RE.test(part) ? (
          <a
            key={i}
            data-testid="detail-pane-message-link"
            href={part}
            target="_blank"
            rel="noreferrer"
            className="mx-0.5 inline-flex items-center gap-1 rounded-md border border-emerald-200 bg-emerald-50 px-2 py-0.5 font-mono dsh-label text-emerald-700 hover:bg-emerald-100"
          >
            {prLinkLabel(part)} ↗
          </a>
        ) : (
          <span key={i}>{part}</span>
        ),
      )}
    </>
  );
}

// A short, human label for a PR/MR URL: "<repo> #<id>" when we can parse it,
// else the host. Azure: …/_git/<repo>/pullrequest/<id>. GitHub: …/<repo>/pull/<id>.
function prLinkLabel(url: string): string {
  const azure = url.match(/\/_git\/([^/]+)\/pullrequest\/(\d+)/i);
  if (azure) return `${decodeURIComponent(azure[1])} #${azure[2]}`;
  const gh = url.match(/github\.com\/[^/]+\/([^/]+)\/pull\/(\d+)/i);
  if (gh) return `${gh[1]} #${gh[2]}`;
  try {
    return new URL(url).hostname.replace(/^www\./, "");
  } catch {
    return "open link";
  }
}

function buildMeta(node: ExecutionNodeProps): string[] {
  const meta: string[] = [];
  if (node.durationLabel && node.durationLabel !== "—") meta.push(`${node.durationLabel} duration`);
  if (node.costBadge) meta.push(node.costBadge);
  return meta;
}
