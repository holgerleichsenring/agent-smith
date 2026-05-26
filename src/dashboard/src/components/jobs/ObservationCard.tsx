import type { SkillObservationEvent } from "@/types/job-stream-events";
import { cn } from "@/lib/utils";

const TONE: Record<string, string> = {
  blocking: "border-rose-300 bg-rose-50",
  high: "border-rose-300 bg-rose-50",
  medium: "border-amber-300 bg-amber-50",
  low: "border-stone-300 bg-stone-50",
};

export function ObservationCard({ event }: { event: SkillObservationEvent }) {
  const tone = TONE[event.severity.toLowerCase()] ?? "border-stone-300 bg-stone-50";
  return (
    <article
      data-testid="observation-card"
      data-severity={event.severity.toLowerCase()}
      className={cn("space-y-1 rounded-md border px-3 py-2 text-sm", tone)}
    >
      <header className="flex items-center justify-between text-xs">
        <span className="font-mono uppercase tracking-wider">{event.severity}</span>
        <span className="text-stone-500">{event.category}</span>
      </header>
      <p>{event.body_preview}</p>
      {event.source_ref && <p className="font-mono text-xs text-stone-500">{event.source_ref}</p>}
    </article>
  );
}
