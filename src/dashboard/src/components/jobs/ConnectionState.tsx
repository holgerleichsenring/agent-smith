"use client";

import { HubConnectionState } from "@microsoft/signalr";
import { useT } from "@/hooks/useT";
import type { T } from "@/lib/i18n/translate";
import type { ApiRefusal } from "@/lib/apiResponse";

// 2026-09-21-291b: "offline" is a claim about the SERVER, and a server that
// refused this caller answered. Under enforcement the negotiate is a 401 from a
// reachable server, and an operator reading "offline" goes looking for a process
// that is running. The refusal is read beside the state rather than folded into
// it: HubConnectionState is SignalR's own enum, and a sixth value would be a lie
// told in SignalR's vocabulary.
//
// A CONNECTED hub outranks a refusal — the transport is genuinely up, and the
// refusal then belongs to whatever surface could not read its own data.

interface Props {
  state: HubConnectionState;
  refusal?: ApiRefusal | null;
}

export function ConnectionState({ state, refusal = null }: Props) {
  const t = useT();
  const refused = state === HubConnectionState.Connected ? null : refusal;
  const label = refused !== null ? refusedLabel(t, refused)
    : state === HubConnectionState.Connected ? t("runs.connection.connected")
    : state === HubConnectionState.Connecting ? t("runs.connection.connecting")
    : state === HubConnectionState.Reconnecting ? t("runs.connection.reconnecting")
    : t("runs.connection.offline");
  const dotClass = state === HubConnectionState.Connected ? "bg-green-500"
    : refused !== null ? "bg-amber-500"
    : state === HubConnectionState.Reconnecting || state === HubConnectionState.Connecting ? "bg-amber-500"
    : "bg-stone-400";
  return (
    <div className="inline-flex items-center gap-2 text-xs text-stone-500" data-testid="hub-connection-state">
      <span className={`h-2 w-2 rounded-full ${dotClass}`} aria-hidden="true" />
      <span>{label}</span>
    </div>
  );
}

function refusedLabel(t: T, refusal: ApiRefusal): string {
  return refusal.kind === "sign-in" ? t("runs.connection.signedOut") : t("runs.connection.notAllowed");
}
