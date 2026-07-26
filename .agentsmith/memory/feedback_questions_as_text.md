---
name: feedback_questions_as_text
description: "User prefers clarifying questions as plain markdown text in the response, not via the AskUserQuestion dialog tool"
metadata:
  type: feedback
status: proposed
---
Stelle Klärungs- und Designfragen IMMER als normalen Markdown-Text in der Antwort, nicht via das AskUserQuestion-Dialog-Tool.

**Why:** User hat das explizit angefordert (2026-05-24). Der Dialog stört den Lese-/Antwortfluss; Text-Fragen lassen sich besser kopieren, zitieren und in einer freien Antwort beantworten. Außerdem kann er bei Text mehrere Optionen kombinieren oder eigene Varianten einbringen, ohne durch das "Other"-Feld zu müssen.

**How to apply:** Wenn du sonst `AskUserQuestion` benutzen würdest, schreib stattdessen die Frage(n) am Ende der Antwort als nummerierte Liste oder kurzes Auswahl-Menü in Markdown. Optionen als Bulletpoints, jede mit kurzer Begründung. Niemals das Dialog-Tool aufrufen — auch nicht für Single-Choice oder Multi-Choice. Ausnahme: nur wenn der User explizit "frag mich per Dialog" sagt.
