import { cn } from "@/lib/utils";

const TONES: Record<string, string> = {
  done: "bg-emerald-100 text-emerald-900 border-emerald-300",
  success: "bg-emerald-100 text-emerald-900 border-emerald-300",
  failed: "bg-rose-100 text-rose-900 border-rose-300",
  error: "bg-rose-100 text-rose-900 border-rose-300",
  inprogress: "bg-amber-100 text-amber-900 border-amber-300",
  enqueued: "bg-slate-100 text-slate-900 border-slate-300",
};

export function StatusBadge({ status, className }: { status: string; className?: string }) {
  const tone = TONES[status.toLowerCase()] ?? "bg-stone-100 text-stone-900 border-stone-300";
  return (
    <span
      data-testid="status-badge"
      data-status={status.toLowerCase()}
      className={cn(
        "inline-flex items-center rounded-md border px-2 py-0.5 text-xs font-medium",
        tone,
        className,
      )}
    >
      {status}
    </span>
  );
}
