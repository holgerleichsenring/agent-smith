"use client";

import { HubConnectionState } from "@microsoft/signalr";

export function ConnectionState({ state }: { state: HubConnectionState }) {
  const label = state === HubConnectionState.Connected ? "connected"
    : state === HubConnectionState.Connecting ? "connecting…"
    : state === HubConnectionState.Reconnecting ? "reconnecting…"
    : "offline";
  const dotClass = state === HubConnectionState.Connected ? "bg-green-500"
    : state === HubConnectionState.Reconnecting || state === HubConnectionState.Connecting ? "bg-amber-500"
    : "bg-stone-400";
  return (
    <div className="inline-flex items-center gap-2 text-xs text-stone-500" data-testid="hub-connection-state">
      <span className={`h-2 w-2 rounded-full ${dotClass}`} aria-hidden="true" />
      <span>{label}</span>
    </div>
  );
}
