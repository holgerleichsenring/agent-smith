import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { browserLanguage, chooseLanguage, currentLanguage, resetLanguage, subscribeLanguage } from "../language";

// 2026-11-22-5485a: the choice is kept per browser; with none, the browser's language decides — de* is German,
// everything else English — and storage that throws still resolves a language.

function browserSays(language: string) {
  vi.spyOn(window.navigator, "language", "get").mockReturnValue(language);
}

// This jsdom build ships no localStorage; a Map stands in for it, as in AppSidebar.test. `blocked` throws instead.
function stubStorage(blocked = false): Map<string, string> {
  const backing = new Map<string, string>();
  const refuse = () => {
    throw new Error("storage blocked");
  };
  Object.defineProperty(window, "localStorage", {
    configurable: true,
    value: {
      getItem: blocked ? refuse : (k: string) => backing.get(k) ?? null,
      setItem: blocked ? refuse : (k: string, v: string) => void backing.set(k, v),
      removeItem: (k: string) => void backing.delete(k),
    },
  });
  return backing;
}

describe("language", () => {
  beforeEach(() => {
    stubStorage();
  });

  afterEach(() => {
    vi.restoreAllMocks();
    resetLanguage("en");
  });

  it("BrowserLanguage_German_IsGerman", () => {
    browserSays("de-DE");
    expect(browserLanguage()).toBe("de");
    browserSays("de");
    expect(browserLanguage()).toBe("de");
  });

  it("BrowserLanguage_AnythingElse_IsEnglish", () => {
    browserSays("en-US");
    expect(browserLanguage()).toBe("en");
    browserSays("fr-FR");
    expect(browserLanguage()).toBe("en");
  });

  it("CurrentLanguage_NoChoice_FollowsTheBrowser", () => {
    browserSays("de-AT");
    resetLanguage();
    expect(currentLanguage()).toBe("de");
  });

  it("CurrentLanguage_AStoredChoice_WinsOverTheBrowser", () => {
    browserSays("de-DE");
    window.localStorage.setItem("agentsmith.language", "en");
    resetLanguage();
    expect(currentLanguage()).toBe("en");
  });

  it("CurrentLanguage_BlockedStorage_StillFollowsTheBrowser", () => {
    browserSays("de-DE");
    stubStorage(true);
    resetLanguage();
    expect(currentLanguage()).toBe("de");
  });

  it("ChooseLanguage_StoresTheChoice_AndTellsEveryReader", () => {
    const heard = vi.fn();
    const stop = subscribeLanguage(heard);
    chooseLanguage("de");
    stop();
    expect(window.localStorage.getItem("agentsmith.language")).toBe("de");
    expect(currentLanguage()).toBe("de");
    expect(heard).toHaveBeenCalledTimes(1);
  });

  it("ChooseLanguage_BlockedStorage_KeepsTheChoiceForThisPage", () => {
    stubStorage(true);
    chooseLanguage("de");
    expect(currentLanguage()).toBe("de");
  });
});
