// 2026-09-27-481bd: what an empty conversation says instead of 154 words of instruction.
//
// The time of day and a name, and neither is invented. The name is the one the header already
// shows — the identity carries the NAME-CLAIM value as its subject and falls back to the opaque
// one when the directory sent none, so it is always present and not always a name. An installation
// on the default claim is greeted by the time of day alone: a directory identifier is worse than
// no name at all.
//
// No character to it on purpose. A voice is a decision of its own, and this one is not it.

/** The reader's own clock decides; nothing about this is the server's business. */
export function greetingFor(hour: number, name?: string | null): string {
  const part = hour < 5 ? "Good evening" : hour < 12 ? "Good morning" : hour < 18 ? "Good afternoon" : "Good evening";
  return name ? `${part}, ${name}.` : `${part}.`;
}
