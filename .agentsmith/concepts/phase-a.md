# Phase A — Modell

**Zweck:** Begriffe scharf ziehen, aus denen Phase B–G abgeleitet werden.
**Kein Code, keine YAML.** Spec-Felder kommen später.

---

## Vier Begriffe

**Skill** — eine Markdown-Datei mit einem Job, einer Aktivierungsbedingung, einem Output-Format. Ein Skill, ein Body. Mehrere Bodies in einer Datei sind mehrere Skills.

**Rolle** — die Art der Arbeit, die ein Skill verrichtet. Eigenschaft des Skills, nicht der Pipeline. Vier Rollen, siehe unten.

**Phase** — ein Slot in der Pipeline-Sequenz, der eine bestimmte Rolle erwartet *oder* einen LLM-freien Schritt darstellt (externe Tools, regel-basierte Filter).

**Pipeline** — eine deklarierte Sequenz von Phasen. Beantwortet: welche Phasen, welche Rolle pro Phase, wie viele Skills, was bei Block.

---

## Vier Rollen

| Rolle | Input | Output | Tool-Loop |
|---|---|---|---|
| **Produzent** | Aufgabe + Kontext | Plan oder Diff oder Observation (mad) | Optional, abhängig vom Skill |
| **Untersucher** | Hinweis oder Such-Auftrag | Observation mit Confidence + Evidence | Pflicht |
| **Beurteiler** | Vorhandener Plan oder Liste | Observation | Nein |
| **Filter** | Liste von Observations | Reduzierte Liste — nur die behaltenen Observations werden weitergereicht; verworfene werden separat geloggt für Audit | Nein |

**Disziplin pro Rolle:**

- *Produzent*: YAGNI. Nur das anfassen, was die Aufgabe verlangt. Bei Unklarheit: offene Frage im Plan, kein Raten.
- *Untersucher*: Confidence ehrlich kalibrieren. Keine Behauptung ohne Code-Lokation als Evidence. Im Zweifel niedriger.
- *Beurteiler*: Genau das prüfen, was sein Job ist. Block nur, wenn der Skill-Body die Block-Bedingung explizit nennt. Sonst Notiz.
- *Filter*: Reduktion mit Begründung. Keine neuen Findings.

---

## Untersucher: drei Arbeitsweisen

| Arbeitsweise | Trigger | Such-Bereich | Skalierung | Beispiel |
|---|---|---|---|---|
| **VerifyHint** | Externer Finding (Nuclei, ZAP, Spectral) | Code-Lokation aus dem Finding | n Untersucher × m Findings | api-security-scan |
| **Survey** | Aktivierungsbedingung der Pipeline | Repo-Bereich (z. B. `**/*.cs`) | n Untersucher × 1 Survey | security-scan |
| **VerifyDiff** | Diff aus Implementation-Phase + Build/Test-Output | Geänderte Dateien plus Test-Files | n Untersucher × 1 Diff | fix-bug Verify, feature-implementation Verify |

Cost-Hebel (siehe unten) gelten unterschiedlich: Endpoint-Batching nur für VerifyHint. Survey-Untersucher sind nicht batched, ihre Anzahl ist die Pipeline-Konfiguration. VerifyDiff hat genau einen Diff als Input — alle Verify-Untersucher sehen denselben Diff, parallelisiert ohne Clustering.

---

## Drei Output-Schemas

Alle Outputs sind **JSON, einzeilig, ohne Preamble, ohne Markdown-Fences**.

### 1. Observation (existiert)

Genutzt von: Untersucher, Beurteiler, Filter, mad-Produzenten, Scanner. Schema in `observation-schema.md`. **Wird nicht angefasst.** Felder: `concern, description, suggestion, blocking, severity, confidence, rationale, file, start_line, end_line, api_path, schema_name, evidence_mode, review_status, category, effort`.

### 2. Plan (neu, Phase C)

Genutzt von: Produzent in Plan-Phase (fix-bug, feature-implementation).

```
{
  "summary": "ein Satz",
  "scope": {"files": [...], "modules": [...]},
  "steps": [{"id": 1, "action": "...", "file": "...", "reason": "..."}],
  "open_questions": [{"id": "q1", "question": "...", "options": [...]}],
  "test_impact": "string oder null",
  "consumer_impact": "string oder null",
  "status": "complete" | "needs_user_input"
}
```

`status: needs_user_input` bedeutet: offene Fragen verhindern Implementation. Plan geht zurück ans Ticket, Mensch antwortet.

### 3. Diff (neu, Phase C)

Genutzt von: Produzent in Implementation-Phase.

```
{
  "changes": [{"file": "...", "operation": "modify|add|delete", "summary": "...", "patch": "..."}],
  "tests_added": [...],
  "tests_modified": [...],
  "build_status": "ok | failed | not_run",
  "test_status": "ok | failed | not_run"
}
```

### Sonderfall: init-project

Der Bootstrap-Skill der init-project-Pipeline schreibt Dateien (`context.yaml`, `coding-principles.md`) direkt ins Repo, statt JSON-Output zu produzieren. Skill-Call-Ergebnis ist eine kleine Bestätigungs-Nachricht analog zum Plan-Schema (`status: complete | needs_user_input`, ggf. `open_questions`). Detail in Phase C.

---

## Fünf Arbeits-Pipelines plus init-project

| Pipeline | Phasen | Rolle pro Phase | Anzahl | Loop | Output |
|---|---|---|---|---|---|
| **fix-bug** | Plan | Produzent | 1 | ja | Plan |
| | Review | Beurteiler | 1 | nein | Observations |
| | Implementation | Produzent | 1 | ja | Diff |
| | Verify | Untersucher | 1–3 | ja | Observations |
| **feature-implementation** | Plan | Produzent | 1 | ja | Plan |
| | Review | Beurteiler | 1–2 | nein | Observations |
| | Implementation | Produzent | 1 | ja | Diff |
| | Verify | Untersucher | 2–4 | ja | Observations |
| **mad-discussion** | Discuss | Produzent | n (gleichberechtigt) | nein | Observations (`concern: discussion`), gehen direkt ins Ticket-Writeback ohne Filter oder Synthese |
| **api-security-scan** | Discover | — ¹ | — | — | Observations (kein LLM) |
| | Investigate | Untersucher (VerifyHint) | n | ja | Observations |
| | Filter | Filter | 1 (false-positive-filter) | nein | Reduzierte Observations |
| | Synthesize | Beurteiler | 1 (chain-analyst) | nein | Observations |
| **security-scan** | Investigate | Untersucher (Survey) | n | ja | Observations |
| | Filter | Filter | 1 | nein | Reduzierte Observations |
| | Synthesize | Beurteiler | 1 | nein | Observations |
| **init-project** | Bootstrap | Produzent | 1 | ja | Artifacts (`context.yaml`, `coding-principles.md`) |

¹ *Discover hat keine Rolle, da kein LLM beteiligt ist. Externe Tools (Spectral, Nuclei, ZAP) emittieren direkt Observations.*

**init-project** ist die Bootstrap-Pipeline für ein neues Repository. Sie läuft einmal pro Repository (oder bei expliziter Re-Generation) und erzeugt die nicht-optionalen Voraussetzungen für alle anderen Pipelines: `context.yaml` (Architektur-Topologie, Stack, Patterns) und `coding-principles.md` (Naming, Test-Style, Sprache). Ein Produzent mit Tool-Loop liest das Repo, fragt im Zweifel zurück (open_questions im Bootstrap-Output), schreibt die Dateien.

**Ausdrücklich nicht erzeugt:** keine `code-map.md`, keine Modul-Topologie, keine vor-deklarierten Skill-Aktivierungen. Code-Topologie wird in jedem späteren Pipeline-Run vom jeweiligen Skill im Tool-Loop ermittelt — eine generierte Code-Map veraltet sofort und kostet Tokens, ohne im konkreten Run dem Skill zu helfen.

**Voraussetzungs-Charakter:** alle anderen Pipelines (fix-bug, feature-implementation, mad, api-scan, security-scan) prüfen beim Start, ob `context.yaml` und `coding-principles.md` existieren. Mechanisch über die Konzepte `context_yaml_present` und `coding_principles_present` (gesetzt von einem Pre-Skill-Handler beim Pipeline-Start). Wenn beide nicht `true` → Pipeline endet sofort mit Hinweis *"erst init-project laufen lassen"*. Keine impliziten Defaults, keine Auto-Generation während des Runs.

---

## Aktivierungsbedingungen

Eine Aktivierungsbedingung ist ein **boolescher Ausdruck über Run-State-Fakten**, nicht über LLM-Interpretation von Prosa.

Run-State-Fakten sind Konzept-Werte (`swagger_spec_present: true`, `source_available: true`, `nuclei_findings_count: 102`), die Pre-Skill-Handler in den Run-State schreiben. Triage evaluiert die Ausdrücke deterministisch — ja/nein, kein Halluzinations-Risiko.

Phase B definiert die Form (Ausdruck-Syntax, Konzept-Vokabular, wer schreibt welches Konzept).

---

## Block-Verhalten

Es gibt zwei Stellen, an denen die Pipeline anhalten kann.

### Plan-Block (Beurteiler in Review-Phase)

**Wer blockt:** nur Beurteiler.

**Aggregation bei mehreren Beurteilern:**
- Wenn *ein* Beurteiler blockt: Block.
- Notizen *aller* Beurteiler werden gesammelt und an den Re-Planner gegeben — nicht nur die der Blocker.

**Re-Plan:**
1. Erster Block → Plan geht einmal zurück zum Produzenten mit *allen* Notizen.
2. Überarbeiteter Plan → *alle* ursprünglichen Beurteiler reviewen erneut. Auch die, die vorher passten. Begründung: ein überarbeiteter Plan ist ein neuer Plan, jeder Aspekt muss neu geprüft werden.
3. Zweiter Block (egal von welchem Beurteiler) → keine dritte Runde. Plan geht ans Ticket mit akkumulierten Notizen, Mensch entscheidet.

**Notiz vs. Block:** der Skill-Body definiert die Block-Bedingung explizit. Beispiel: *"Block, wenn Plan strukturelle Lücken hat (kein Test-Impact bedacht, kein Scope erkennbar). Notiz, wenn der Plan grundsätzlich tragfähig ist, aber Verbesserungs-Hinweise möglich sind."* Notizen halten die Pipeline nicht an, sie wandern mit zur Implementation.

### Verify-Fail (Untersucher in Verify-Phase)

**Wer fail-meldet:** Verify-Untersucher mit `blocking: true` in ihrer Observation. Confidence ehrlich kalibriert — niedrige Confidence sollte nicht zu Block führen.

**Aggregation:** wie bei Beurteilern. Ein Block = Block. Alle Verify-Notizen + relevante Build/Test-Outputs werden gesammelt.

**Re-Implementation:**
1. Erster Verify-Fail → Diff geht einmal zurück zum Implementation-Produzenten mit Verify-Notizen.
2. Zweiter Verify-Fail → keine dritte Runde. Diff + Notizen + Build/Test-Outputs gehen ans Ticket, Mensch entscheidet.

**Beispiel:** *Verify-Untersucher findet, dass `dotnet test` nach dem Diff fehlschlägt. Erste Runde: Diff geht zurück, Implementer korrigiert. Zweiter Fail: Eskalation — der Mensch sieht Test-Output und kann entscheiden, ob das Ticket oder die Implementation falsch ist.*

### Zweite Eskalations-Route

Ein Plan mit `status: needs_user_input` umgeht Review komplett — er ist gar nicht reif für Beurteilung. Plan geht direkt ans Ticket, der Mensch beantwortet die offenen Fragen, der Plan-Skill läuft erneut.

Der Mensch antwortet pro Frage-ID (z. B. `q1: Option A`). Das Ticket-Writeback enthält die Fragen-IDs explizit. Der Re-Plan-Lauf bekommt die Antworten als zusätzlichen Input ins Plan-Schema. Format-Detail in Phase C.

---

## Tool-Loop: berechtigt und begrenzt

Tool-Loop = ein Skill-Call besteht aus mehreren LLM-Calls plus Tool-Calls (read, grep, find, bash) bis Skill mit Output zurückkommt.

**Universelle Regeln:**
- Hartes Loop-Limit pro Skill-Call (Anzahl Tool-Calls + Token-Budget). Bei Überschreitung: bester Output mit Markierung *"unvollständig"*.
- Loop-Kosten werden pro Skill-Call getrackt und im Pipeline-Ergebnis ausgewiesen.
- Tool-Zugriff ist beschränkt auf das, was Aufgabe und Skill brauchen. Lesen: Dateien aus Ticket oder context.yaml. Schreiben: nur Implementation-Phase, nur Dateien aus dem freigegebenen Plan.

**Pipeline-spezifische Limits:**

| Pipeline | Limit-Niveau | Begründung |
|---|---|---|
| fix-bug, feature-implementation, mad | großzügig | wenige Skills, Korrektheit dominiert |
| api-security-scan | strikt | Findings-Anzahl × Untersucher-Anzahl wird teuer |
| security-scan | strikt | Untersucher-Anzahl × Repo-Größe wird teuer |

**Cost-Hebel für Scan-Pipelines:**

- **Pre-Filter ohne LLM** vor jeder Investigate-Phase. Severity-Schwellen, Pattern-Listen.
- **Endpoint-Batching** für VerifyHint-Untersucher: Findings nach Code-Lokation clustern, ein Untersucher-Call pro Cluster.
- **Single-Perspektive-Default**: pro Finding läuft *ein* Untersucher. Zweite Perspektive nur bei VerifyHint und nur, wenn Confidence < Schwellwert. Survey-Untersucher haben keine zweite Perspektive — sie suchen ihren Bereich einmal ab.
- **Aktivierungsbedingungen** schalten irrelevante Untersucher ab (z. B. `upload-validator` ohne Multipart-Endpoints).

---

## Lesbarkeits-Tests

Eine Pipeline-Spec ist gut, wenn ein Mensch sie lesen und in einem Satz nachvollziehen kann, was passiert.

**Test 1 — fix-bug:**
> *fix-bug nimmt ein Ticket. Ein Produzent (mit Loop) liest Ticket, context.yaml, coding-principles.md und schreibt einen Plan. Ein Beurteiler (Vier-Augen-Prinzip, kein Loop) prüft den Plan auf strukturelle Vollständigkeit und kann blocken. Bei Block geht der Plan einmal zurück, dann an den Menschen. Bei Pass setzt ein Produzent (mit Loop) den Plan um und liefert einen Diff. Ein bis drei Untersucher (mit Loop) prüfen den Diff gegen den realen Code — Build/Test laufen, Architektur-Constraints sind eingehalten. Bei Verify-Fail geht die Implementation einmal zurück, dann an den Menschen.*

**Test 2 — api-security-scan:**
> *api-security-scan nimmt einen API-Endpunkt. In der Discover-Phase laufen Spectral, Nuclei, ZAP — kein LLM. Ein regel-basierter Pre-Filter sortiert offensichtlich-irrelevante Findings raus. Verbleibende Findings werden nach Endpoint geclustert. In der Investigate-Phase läuft pro Cluster ein Untersucher mit Loop und liefert Observations. Bei Confidence < 60 läuft eine zweite Perspektive. Ein Filter reduziert die Liste auf bestätigte Observations. Ein Synthesizer (Beurteiler) bündelt zusammenhängende Observations zu Chains.*

Wenn Test 2 ohne Zurückblättern durchgeht, ist Phase A scharf genug.

---

## Erfolgskriterium

Ein Mensch liest dieses Dokument und kann ohne Rückblättern aufschreiben:
1. Welche vier Rollen es gibt und was jede tut.
2. Welche drei Output-Schemas es gibt und welche Rolle welches nutzt.
3. Wie fix-bug und api-security-scan ablaufen.
4. Was bei einem Block passiert.

Wenn nicht — zurück, nicht weiter.

---

## Bewusst offen für spätere Phasen

- Skill-Sequencing innerhalb einer Phase (parallel, sequenziell, mit oder ohne geteilten Kontext) — Phase B.
- Aktivierungsbedingung-Syntax und Konzept-Vokabular — Phase B.
- Plan- und Diff-Schemas im Detail (Felder, Validierung) — Phase C.
- Konkrete Tool-Loop-Limits in Zahlen — Phase B.
- autonomous-Pipeline (Agent Smith schreibt selbst Tickets) — nach Phase G.