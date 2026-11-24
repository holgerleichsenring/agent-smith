import type { MessageEntry } from "../messages";

// 2026-11-23-5669a: the delivery area's part of the one dictionary, spread into messages; English and German side by side.
// 2026-11-23-5669f: the Delivery page — its figures, timeline, breakdowns and tables.

export const deliveryMessages = {
  // --- delivery: the page ---
  "delivery.page.sub": {
    en: "The tickets the agent worked on in this period. Each row is a ticket and each bar is one attempt. Hover a bar for its duration, cost and why it ended.",
    de: "Die Tickets, an denen der Agent in diesem Zeitraum gearbeitet hat. Jede Zeile ist ein Ticket, jeder Balken ein Versuch. Fahr über einen Balken für Dauer, Kosten und warum er endete.",
  },
  "delivery.page.failed": { en: "The delivery overview could not be read.", de: "Die Delivery-Übersicht konnte nicht gelesen werden." },
  "delivery.page.reading": { en: "Reading the period.", de: "Der Zeitraum wird gelesen." },

  // --- delivery: the period and the project chips ---
  "delivery.period.label": { en: "Period", de: "Zeitraum" },
  "delivery.period.day": { en: "Day", de: "Tag" },
  "delivery.period.week": { en: "Week", de: "Woche" },
  "delivery.period.month": { en: "Month", de: "Monat" },
  "delivery.period.move": { en: "Move in time", de: "Zeitlich blättern" },
  "delivery.period.previous.day": { en: "Previous day", de: "Vorheriger Tag" },
  "delivery.period.previous.week": { en: "Previous week", de: "Vorherige Woche" },
  "delivery.period.previous.month": { en: "Previous month", de: "Vorheriger Monat" },
  "delivery.period.next.day": { en: "Next day", de: "Nächster Tag" },
  "delivery.period.next.week": { en: "Next week", de: "Nächste Woche" },
  "delivery.period.next.month": { en: "Next month", de: "Nächster Monat" },
  "delivery.chips.label": { en: "Projects", de: "Projekte" },
  "delivery.chips.yours": { en: "Your projects", de: "Deine Projekte" },

  // --- delivery: the key figures ---
  "delivery.kpis.label": { en: "Key figures", de: "Kennzahlen" },
  "delivery.kpi.ticketsWorked": { en: "Tickets worked", de: "Bearbeitete Tickets" },
  "delivery.kpi.merged": { en: "Merged", de: "Gemergt" },
  "delivery.kpi.mergeRate": { en: "Merge rate", de: "Merge-Quote" },
  "delivery.kpi.medianTimeToMerge": { en: "Median time to merge", de: "Median bis zum Merge" },
  "delivery.kpi.reworkPerTicket": { en: "Rework rounds per ticket", de: "Nacharbeitsrunden pro Ticket" },
  "delivery.kpi.costPerMerged": { en: "Cost per merged PR", de: "Kosten pro gemergtem PR" },
  "delivery.kpi.spend": { en: "Spend", de: "Ausgaben" },
  "delivery.kpi.agentHours": { en: "Agent hours", de: "Agent-Stunden" },
  "delivery.compared.day": { en: "vs yesterday", de: "ggü. gestern" },
  "delivery.compared.week": { en: "vs last week", de: "ggü. letzter Woche" },
  "delivery.compared.month": { en: "vs last month", de: "ggü. letztem Monat" },
  "delivery.delta.none": { en: "no change", de: "unverändert" },
  "delivery.delta.points": { en: "{sign}{value} pt", de: "{sign}{value} Pp." },

  // --- delivery: numbers ---
  "delivery.minutes": { en: "{value} min", de: "{value} min" },
  "delivery.hours": { en: "{value} h", de: "{value} h" },
  "delivery.days": { en: "{value} d", de: "{value} Tage" },
  "delivery.usd": { en: "${value}", de: "{value} $" },
  "delivery.percent": { en: "{value}%", de: "{value} %" },
  "delivery.specs": { en: { one: "1 spec", other: "{count} specs" }, de: { one: "1 Spec", other: "{count} Specs" } },
  "delivery.repos": { en: { one: "1 repo", other: "{count} repos" }, de: { one: "1 Repo", other: "{count} Repos" } },

  // --- delivery: how an attempt ended ---
  "delivery.outcome.running": { en: "Running", de: "Läuft" },
  "delivery.outcome.queued": { en: "Queued", de: "In der Warteschlange" },
  "delivery.outcome.waiting": { en: "Waiting for a person", de: "Wartet auf eine Person" },
  "delivery.outcome.delivered": { en: "Delivered", de: "Geliefert" },
  "delivery.outcome.shortfall": { en: "Criteria not met", de: "Kriterien nicht erfüllt" },
  "delivery.outcome.cancelled": { en: "Cancelled", de: "Abgebrochen" },
  "delivery.outcome.failed": { en: "Failed", de: "Fehlgeschlagen" },
  "delivery.cause.success": { en: "Delivered", de: "Geliefert" },
  "delivery.cause.shortfall": { en: "Delivered in part", de: "Teilweise geliefert" },
  "delivery.cause.criteria-unmet": { en: "Criteria not met", de: "Kriterien nicht erfüllt" },
  "delivery.cause.preflight": { en: "Preflight failed (build, deps)", de: "Preflight fehlgeschlagen (Build, Abhängigkeiten)" },
  "delivery.cause.bootstrap-missing": { en: "Repository not initialised", de: "Repository nicht initialisiert" },
  "delivery.cause.image-untrusted": { en: "Sandbox image not trusted", de: "Sandbox-Image nicht vertrauenswürdig" },
  "delivery.cause.readiness-unknown": { en: "Host did not answer", de: "Host hat nicht geantwortet" },
  "delivery.cause.superseded": { en: "Superseded by a newer attempt", de: "Von einem neueren Versuch abgelöst" },
  "delivery.cause.cancelled": { en: "Cancelled", de: "Abgebrochen" },
  "delivery.cause.failed": { en: "Failed", de: "Fehlgeschlagen" },

  // --- delivery: the timeline ---
  "delivery.timeline.title": { en: "Timeline", de: "Zeitachse" },
  "delivery.timeline.rework": { en: "Rework", de: "Nacharbeit" },
  "delivery.timeline.merged": { en: "PR merged", de: "PR gemergt" },
  "delivery.timeline.empty": { en: "No ticket was worked on in this period.", de: "In diesem Zeitraum wurde an keinem Ticket gearbeitet." },
  "delivery.timeline.attempts": {
    en: { one: "1 attempt", other: "{count} attempts" },
    de: { one: "1 Versuch", other: "{count} Versuche" },
  },
  "delivery.timeline.bar": { en: "Attempt {index}: {outcome}", de: "Versuch {index}: {outcome}" },
  "delivery.tip.attempt": { en: "attempt {index}", de: "Versuch {index}" },
  "delivery.tip.rework": { en: "rework", de: "Nacharbeit" },
  "delivery.tip.running": { en: "running", de: "läuft" },
  "delivery.tip.specs": { en: "specs {done}/{total}", de: "Specs {done}/{total}" },

  // --- delivery: why attempts ended, by project, the tickets ---
  "delivery.causes.title": { en: "Why attempts ended", de: "Warum Versuche endeten" },
  "delivery.causes.empty": { en: "No attempt ended in this period.", de: "In diesem Zeitraum endete kein Versuch." },
  "delivery.projects.title": { en: "By project", de: "Nach Projekt" },
  "delivery.col.project": { en: "Project", de: "Projekt" },
  "delivery.col.ticket": { en: "Ticket", de: "Ticket" },
  "delivery.col.tickets": { en: "Tickets", de: "Tickets" },
  "delivery.col.merged": { en: "Merged", de: "Gemergt" },
  "delivery.col.rate": { en: "Rate", de: "Quote" },
  "delivery.col.costPerMerged": { en: "Cost / merged", de: "Kosten / Merge" },
  "delivery.col.medianAttempt": { en: "Median attempt", de: "Versuch (Median)" },
  "delivery.col.outcome": { en: "Outcome", de: "Ergebnis" },
  "delivery.col.work": { en: "What was done", de: "Was getan wurde" },
  "delivery.col.attempts": { en: "Attempts", de: "Versuche" },
  "delivery.col.specs": { en: "Specs", de: "Specs" },
  "delivery.col.agentTime": { en: "Agent time", de: "Agent-Zeit" },
  "delivery.col.wallTime": { en: "Wall time", de: "Gesamtdauer" },
  "delivery.col.cost": { en: "Cost", de: "Kosten" },
  "delivery.col.lastActivity": { en: "Last activity", de: "Letzte Aktivität" },
  "delivery.tickets.sub": { en: "Latest activity first. A row opens the ticket.", de: "Neueste Aktivität zuerst. Eine Zeile öffnet das Ticket." },
} satisfies Record<string, MessageEntry>;
