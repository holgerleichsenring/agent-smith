import { describe, expect, it } from "vitest";
import { messages, type MessageEntry } from "../messages";
import { fill, translator } from "../translate";

// 2026-11-22-5485a: the dictionary is complete in both languages — every key has English and German, a plural has both
// forms in both, and the German text names the same placeholders as the English one, so no value is dropped.

function placeholders(text: string): string[] {
  return [...text.matchAll(/\{(\w+)\}/g)].map((match) => match[1]).sort();
}

function texts(entry: MessageEntry, language: "en" | "de"): string[] {
  const form = entry[language];
  return typeof form === "string" ? [form] : [form.one, form.other];
}

describe("messages", () => {
  const entries = Object.entries(messages) as [string, MessageEntry][];

  it("Messages_EveryKey_HasEnglishAndGermanText", () => {
    const empty = entries.filter(([, entry]) => [...texts(entry, "en"), ...texts(entry, "de")].some((text) => text.trim() === ""));
    expect(empty.map(([key]) => key)).toEqual([]);
  });

  it("Messages_EveryKey_NamesTheSamePlaceholdersInBothLanguages", () => {
    const differ = entries.filter(([, entry]) => {
      const [en, de] = [texts(entry, "en"), texts(entry, "de")];
      return en.some((text, at) => placeholders(text).join() !== placeholders(de[at]).join());
    });
    expect(differ.map(([key]) => key)).toEqual([]);
  });

  it("Messages_APluralEntry_IsPluralInBothLanguages", () => {
    const mixed = entries.filter(([, entry]) => (typeof entry.en === "string") !== (typeof entry.de === "string"));
    expect(mixed).toEqual([]);
  });

  it("Messages_AKeyWithoutGerman_DoesNotCompile", () => {
    // @ts-expect-error a key with no German text is not an entry
    const missing: MessageEntry = { en: "Keep chatting" };
    expect(missing).toBeDefined();
  });
});

describe("translator", () => {
  it("Translator_German_ReadsTheGermanText", () => {
    expect(translator("de")("dialog.decision.keepChatting")).toBe("Weiter chatten");
    expect(translator("en")("dialog.decision.keepChatting")).toBe("Keep chatting");
  });

  it("Translator_APlural_PicksOneOrOtherByCount", () => {
    const de = translator("de");
    expect(de("dialog.approval.createTickets", { count: 1 })).toBe("1 Ticket anlegen");
    expect(de("dialog.approval.createTickets", { count: 3 })).toBe("3 Tickets anlegen");
    expect(de("dialog.approval.createTickets", { count: 0 })).toBe("0 Tickets anlegen");
  });

  it("Translator_Placeholders_AreFilledFromTheParams", () => {
    expect(translator("en")("dialog.ticket.on", { tracker: "jira" })).toBe("on jira");
  });

  it("Fill_AMissingParam_LeavesThePlaceholderAsWritten_AndAValueIsNotReadAgain", () => {
    expect(fill("{a} and {b}", { a: "{b}" })).toBe("{b} and {b}");
  });

  it("Translator_SameLanguage_IsTheSameFunction", () => {
    expect(translator("de")).toBe(translator("de"));
  });
});
