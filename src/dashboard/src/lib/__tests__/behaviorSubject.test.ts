import { describe, it, expect, vi } from "vitest";
import { makeBehaviorSubject } from "../JobsHubClient";

describe("makeBehaviorSubject", () => {
  it("BehaviorSubject_ReplaysLastValue_ToLateSubscriber", () => {
    // The Runs-list bug: AppRail subscribes first and consumes the one-time
    // snapshot; RunsList mounts later and must still receive the current value
    // without a page refresh.
    const subject = makeBehaviorSubject<number>();
    const early = vi.fn();
    subject.add(early);

    subject.emit(42); // snapshot arrives while only AppRail (early) is listening

    const late = vi.fn();
    subject.add(late); // RunsList mounts afterwards

    expect(early).toHaveBeenCalledWith(42);
    expect(late).toHaveBeenCalledWith(42); // replayed, no refresh needed
  });

  it("BehaviorSubject_NoEmitYet_DoesNotInvokeOnSubscribe", () => {
    const subject = makeBehaviorSubject<number>();
    const listener = vi.fn();
    subject.add(listener);
    expect(listener).not.toHaveBeenCalled();
  });

  it("BehaviorSubject_ReplaysOnlyTheLatest", () => {
    const subject = makeBehaviorSubject<string>();
    subject.emit("old");
    subject.emit("new");
    const late = vi.fn();
    subject.add(late);
    expect(late).toHaveBeenCalledTimes(1);
    expect(late).toHaveBeenCalledWith("new");
  });

  it("BehaviorSubject_Unsubscribe_StopsFurtherEmits", () => {
    const subject = makeBehaviorSubject<number>();
    const listener = vi.fn();
    const off = subject.add(listener);
    off();
    subject.emit(7);
    expect(listener).not.toHaveBeenCalled();
  });
});
