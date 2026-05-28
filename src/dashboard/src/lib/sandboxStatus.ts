import { EventType, type RunEvent } from "@/types/hub-events";

// p0169j-d: derive a sandbox's status from the event stream. Pure
// function — feed it the event list (which the topology graph already
// has) and a repo name, get back the operator-meaningful status.

export type SandboxStatus =
  | "waiting"      // mid-run, no SandboxCreated yet — will (probably) happen
  | "not_started"  // p0176c: run finished without SandboxCreated for this repo — terminal
  | "running"      // SandboxCreated, no SandboxDisposed yet, no step-failure
  | "success"      // SandboxDisposed without failure context
  | "failed"       // a StepFinished with status=failed has been observed
  | "disposed";    // SandboxDisposed AND the run has finished (terminal state, neutral)

export function sandboxStatusColor(
  events: readonly RunEvent[],
  repo: string,
): SandboxStatus {
  let created = false;
  let disposed = false;
  let runFinished = false;
  let stepFailed = false;
  let runSucceeded = false;

  for (const e of events) {
    switch (e.type) {
      case EventType.SandboxCreated:
        if (e.repo === repo) created = true;
        break;
      case EventType.SandboxDisposed:
        if (e.repo === repo) disposed = true;
        break;
      case EventType.StepFinished:
        if (e.status === "failed") stepFailed = true;
        break;
      case EventType.RunFinished:
        runFinished = true;
        if (e.status === "success") runSucceeded = true;
        break;
    }
  }

  // p0176c: a sandbox that never received SandboxCreated AND the run
  // is finished was never going to spawn — terminal, not in-progress.
  // waiting stays for the mid-run case where the create may still fire.
  if (!created && runFinished) return "not_started";
  if (!created) return "waiting";
  if (stepFailed) return "failed";
  if (disposed && runFinished && runSucceeded) return "success";
  if (disposed && !runFinished) return "running";
  if (disposed) return "disposed";
  return "running";
}

// Tailwind class fragments matched to the DESIGN.md status palette.
// Green is RESERVED for done (success); running is amber-pulsing
// (with prefers-reduced-motion fallback baked into the CSS class).
// Caller composes container + border + text fragments.
export interface StatusPalette {
  fill: string;
  stroke: string;
  text: string;
  pulse: boolean;
}

export function paletteFor(status: SandboxStatus): StatusPalette {
  switch (status) {
    case "running":
      return {
        fill: "fill-amber-50",
        stroke: "stroke-amber-400",
        text: "text-amber-700",
        pulse: true,
      };
    case "success":
      return {
        fill: "fill-emerald-50",
        stroke: "stroke-emerald-500",
        text: "text-emerald-700",
        pulse: false,
      };
    case "failed":
      return {
        fill: "fill-rose-50",
        stroke: "stroke-rose-500",
        text: "text-rose-700",
        pulse: false,
      };
    case "disposed":
      return {
        fill: "fill-stone-100",
        stroke: "stroke-stone-400",
        text: "text-stone-600",
        pulse: false,
      };
    case "not_started":
      // p0176c: terminal-grey mirroring disposed — the operator should
      // read "this never happened" with the same neutrality as a clean
      // teardown, not the in-progress beige of waiting.
      return {
        fill: "fill-stone-100",
        stroke: "stroke-stone-400",
        text: "text-stone-600",
        pulse: false,
      };
    case "waiting":
    default:
      return {
        fill: "fill-stone-50",
        stroke: "stroke-stone-300",
        text: "text-stone-500",
        pulse: false,
      };
  }
}
