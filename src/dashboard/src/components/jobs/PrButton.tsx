"use client";

// p0372: THE pull-request button. Every surface that shows a PR reference —
// the pull-requests list rows, the Outcome beat, the result view and the
// full-pipeline trace — renders this one component, so clickability, draft
// labelling and styling are decided in a single place. A draft PR still has a
// valid URL and is ALWAYS clickable; draft only changes the tone and label.
interface PrButtonProps {
  url: string;
  /** Repo prefix, shown when a multi-repo run needs to tell its PRs apart. */
  repo?: string | null;
  isDraft?: boolean;
  testId?: string;
}

export function PrButton({ url, repo, isDraft = false, testId }: PrButtonProps) {
  return (
    <a
      className={isDraft ? "pr-btn draft" : "pr-btn"}
      href={url}
      target="_blank"
      rel="noreferrer"
      data-testid={testId ?? "pr-button"}
      data-draft={isDraft || undefined}
      onClick={(e) => e.stopPropagation()}
    >
      {repo ? <span className="pr-btn-repo">{repo}:</span> : null}
      <span>{isDraft ? "Draft pull request" : "Pull request"} ↗</span>
    </a>
  );
}
