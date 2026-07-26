"use client";

import { PrButton } from "@/components/jobs/PrButton";
import { ResultTab } from "@/components/jobs/ResultTab";
import type { RunPullRequest } from "@/types/hub-events";

// p0205: the Result overview rendered in the detail pane. Wraps the existing
// p0169j-c ResultTab (cached result.md via react-markdown) as an Overview rail
// node so the run outcome lives beside the execution timeline, not stacked
// below it.
//
// p0372: the run's PRs render here as the ONE PrButton — one button per PR
// (per repo), draft PRs clickable, same component as the list and the Outcome
// beat. Older snapshots without the per-repo list fall back to their single
// prUrl.

interface ResultDetailProps {
  runId: string;
  prUrl: string | null;
  pullRequests?: RunPullRequest[] | null;
}

export function ResultDetail({ runId, prUrl, pullRequests }: ResultDetailProps) {
  const prs =
    pullRequests && pullRequests.length > 0
      ? pullRequests.filter((pr) => !!pr.url)
      : prUrl
      ? [{ repo: "", url: prUrl, status: "opened", isDraft: false }]
      : [];
  const multiRepo = prs.length > 1;
  return (
    <div data-testid="result-detail" className="h-full overflow-y-auto px-7 py-5">
      <div className="font-mono dsh-mono text-stone-400">Overview ›</div>
      <div className="dsh-h2 font-semibold tracking-tight">Result</div>
      {prs.length > 0 && (
        <div className="mt-3 flex flex-wrap gap-2" data-testid="result-detail-prs">
          {prs.map((pr) => (
            <PrButton
              key={pr.repo + pr.url}
              url={pr.url}
              repo={multiRepo && pr.repo ? pr.repo : undefined}
              isDraft={pr.isDraft}
              testId="result-detail-pr-link"
            />
          ))}
        </div>
      )}
      <div className="mt-4 border-t border-stone-100 pt-4">
        <ResultTab runId={runId} prUrl={prUrl} />
      </div>
    </div>
  );
}
