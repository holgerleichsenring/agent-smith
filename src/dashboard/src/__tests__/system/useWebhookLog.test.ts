import { describe, it, expect } from "vitest";
import { deriveWebhookLog } from "@/hooks/useWebhookLog";
import { SystemEventType, type SystemEvent } from "@/types/system-events";

function webhook(ts: string, actioned: boolean, skipReason: string | null = null): SystemEvent {
  return {
    type: SystemEventType.WebhookReceived,
    source: "webhook:github",
    eventType: "issues",
    path: "/webhooks/github",
    actioned,
    skipReason,
    timestamp: ts,
  };
}

describe("deriveWebhookLog", () => {
  it("orders entries newest-first", () => {
    const log = deriveWebhookLog(
      [
        webhook("2026-05-27T11:00:00Z", true),
        webhook("2026-05-27T12:00:00Z", false, "no-handler-matched"),
        webhook("2026-05-27T10:00:00Z", true),
      ],
      50,
    );
    expect(log.map((e) => e.timestamp)).toEqual([
      "2026-05-27T12:00:00Z",
      "2026-05-27T11:00:00Z",
      "2026-05-27T10:00:00Z",
    ]);
  });

  it("preserves actioned + skipReason", () => {
    const log = deriveWebhookLog(
      [webhook("2026-05-27T12:00:00Z", false, "signature-invalid")],
      50,
    );
    expect(log[0].actioned).toBe(false);
    expect(log[0].skipReason).toBe("signature-invalid");
  });

  it("respects the limit", () => {
    const events: SystemEvent[] = [];
    for (let i = 0; i < 30; i++) {
      events.push(webhook(`2026-05-27T12:00:${String(i).padStart(2, "0")}Z`, true));
    }
    expect(deriveWebhookLog(events, 10)).toHaveLength(10);
  });
});
