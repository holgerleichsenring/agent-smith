import type { ToolCallEvent } from "@/types/job-stream-events";

export function ToolCallEntry({ event }: { event: ToolCallEvent }) {
  return (
    <details data-testid="tool-call-entry" className="rounded-md border bg-stone-50 px-3 py-2 text-sm">
      <summary className="cursor-pointer text-xs">
        <span className="font-mono">{event.tool_name}</span>
      </summary>
      <pre className="mt-2 whitespace-pre-wrap break-all text-xs text-stone-600">
        {event.args_preview}
      </pre>
    </details>
  );
}
