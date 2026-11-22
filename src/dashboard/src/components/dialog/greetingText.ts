// 2026-09-27-481bd: what an empty conversation says instead of 154 words of instruction.
//
// The time of day and a name, and neither is invented. The name is the one the header already
// shows — the identity carries the NAME-CLAIM value as its subject and falls back to the opaque
// one when the directory sent none, so it is always present and not always a name. An installation
// on the default claim is greeted by the time of day alone: a directory identifier is worse than
// no name at all.
//
// No character to it on purpose. A voice is a decision of its own, and this one is not it.

import type { T } from "@/lib/i18n/translate";

/** The reader's own clock decides; nothing about this is the server's business. */
export function greetingFor(t: T, hour: number, name?: string | null): string {
  const part = t(hour < 5 ? "dialog.greeting.evening" : hour < 12 ? "dialog.greeting.morning"
    : hour < 18 ? "dialog.greeting.afternoon" : "dialog.greeting.evening");
  return name ? `${part}, ${name}.` : `${part}.`;
}
