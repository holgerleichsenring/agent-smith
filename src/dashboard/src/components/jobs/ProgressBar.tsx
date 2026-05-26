import { cn } from "@/lib/utils";

export function ProgressBar({ step, total, label }: { step: number; total: number; label?: string }) {
  const pct = total > 0 ? Math.min(100, Math.round((step / total) * 100)) : 0;
  return (
    <div className="space-y-1" data-testid="progress-bar" data-step={step} data-total={total}>
      <div className="flex items-center justify-between text-xs">
        <span>{label ?? "Progress"}</span>
        <span className="font-mono">
          {step}/{total}
        </span>
      </div>
      <div className="h-2 w-full overflow-hidden rounded-full bg-stone-200">
        <div
          className={cn("h-full bg-emerald-500 transition-all")}
          style={{ width: `${pct}%` }}
        />
      </div>
    </div>
  );
}
