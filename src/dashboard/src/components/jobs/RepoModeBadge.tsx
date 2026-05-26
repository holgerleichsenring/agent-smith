import { cn } from "@/lib/utils";

export function RepoModeBadge({ mode, repos }: { mode: string; repos: string[] }) {
  const label = mode === "multi" ? `multi-repo · ${repos.length}` : mode === "mono" ? "mono" : mode;
  return (
    <span
      data-testid="repo-mode-badge"
      data-mode={mode}
      className={cn(
        "inline-flex items-center rounded-md border px-2 py-0.5 text-xs font-medium",
        mode === "multi"
          ? "border-sky-300 bg-sky-50 text-sky-900"
          : "border-stone-300 bg-stone-50 text-stone-900",
      )}
      title={repos.length > 0 ? repos.join(", ") : undefined}
    >
      {label}
    </span>
  );
}
