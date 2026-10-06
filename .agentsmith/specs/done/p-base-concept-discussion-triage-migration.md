# Notiz: Discussion-Triage Migration — Kann StructuredTriage LegacyTriage ersetzen?

**Datum:** 2026-05-10
**Auftrag:** Premise-Check vor p0131c. Ist LegacyTriageStrategy heute wirklich
unbenötigt, oder hat sie noch einen Job, den StructuredTriage nicht abdeckt?

## Tatsächliche Konsumenten heute

`TriageStrategySelector.Select(PipelineType)` routet `Discussion` →
`LegacyTriageStrategy`. Fünf Presets sind Discussion-Type, ABER nur drei
führen heute überhaupt einen `Triage`-Schritt aus:

| Preset            | Hat `Triage` step? | Skills mit `activates_when=pipeline_name`        | Status                          |
|-------------------|--------------------|--------------------------------------------------|---------------------------------|
| `init-project`    | **Nein**           | bootstrap-* aktivieren auf `project_language`    | LegacyTriage nicht erreicht     |
| `mad-discussion`  | **Ja**             | 5 mad/* skills, alle `pipeline_name = "mad-..."`| **Echter Konsument**            |
| `legal-analysis`  | **Ja**             | 5 legal/* skills, alle `pipeline_name = "legal-..."`| **Echter Konsument**         |
| `skill-manager`   | **Nein**           | n/a — Pipeline ist DiscoverSkills/Evaluate/...  | LegacyTriage nicht erreicht     |
| `autonomous`      | **Ja**             | **keine** Skills mit `pipeline_name="autonomous"` | LegacyTriage liefert No-op (no roles)|

`init-project` und `skill-manager` haben keinen `Triage`-Schritt im Preset.
LegacyTriageStrategy ist für sie irrelevant.

`autonomous`: hat den Schritt, aber im Skill-Katalog gibt es keine Skills mit
`activates_when='pipeline_name = "autonomous"'`. Die Project-Vision-Skills
wurden in p0103c gestrichen. Heute reicht der Triage-Aufruf in autonomous
durch zu LegacyTriage und kommt mit `roles=[]` zurück → früher Ok-Exit ("no
roles available"). Die Pipeline ist ohne Skills nicht produktiv. Die Lücke
ist real, aber sie blockiert p0131c nicht — sie ist ein offenes Loch
unabhängig davon, welche TriageStrategy aktiv ist.

**Fazit Konsumenten:** Echte Konsumenten von LegacyTriage sind heute
`mad-discussion` + `legal-analysis`. `autonomous` reicht nur formal durch.

## Kann StructuredTriage die zwei echten Konsumenten übernehmen?

`mad-discussion` + `legal-analysis` haben in den jeweiligen Skill-Katalogen
**vollständige `activates_when`-Abdeckung** (alle 10 Skills tragen die
pipeline-spezifische Aktivierung). Die `ActivationSkillFilter`-Vorstufe in
`TriageOutputProducer.ProduceAsync` engt die Skill-Liste also bereits
korrekt ein, bevor der LLM-Call läuft.

Was StructuredTriage konkret tut:

1. `ActivationSkillFilter.Filter(skills, concepts)` → 5 mad/* bzw. 5 legal/* skills.
2. LLM-Call mit dem strukturierten Triage-Prompt → erzeugt `TriageOutput`
   mit `Phases: { Plan, Review, Final }`.
3. `PhaseSpecificityTrimmer.Trim` cap't nach `MaxSkillsPerPhase`.
4. `PhaseCommandExpander.ExpandPhase(triage, Plan, ...)` → flache
   `SkillRound`-Sequenz für die Plan-Phase.

**Was LegacyTriage zusätzlich tut, das StructuredTriage nicht abdeckt:**

- `EnsureMandatoryRoles` schiebt jeden Skill mit `Triggers` enthält
  `"always_include"` in die Participants-Liste. Im neuen Format gibt es
  weder Triggers noch always_include — die `activates_when`-Logik ist
  Boolean und entweder feuert oder nicht. Die mad/* + legal/* Skills tragen
  heute keine `always_include`-Trigger; **kein Funktionsverlust**.
- LegacyTriage gibt das LLM eine Rolle-Auswahl frei zwischen "Lead" und
  "Participants" (flache Struktur). StructuredTriage zwingt eine Phase-
  Struktur (Plan/Review/Final). Für mad-discussion und legal-analysis ist
  das eine Annäherung — die Pipelines haben **keine RunReviewPhase /
  RunFinalPhase Steps im Preset**, also fallen Review-/Final-Phase-Skills
  einfach unter den Tisch. Der Trimmer muss da eingreifen, sonst landen
  4 von 5 Skills in Phasen, die nie gerufen werden.

## Identifizierte Migrations-Risiken (mad / legal)

1. **Phase-Verteilung-Problem.** Wenn das LLM 5 Skills auf Plan/Review/Final
   verteilt (wie für fix-bug normal ist), und nur Plan ausgeführt wird,
   verlieren wir Output. Lösung: entweder den Triage-Prompt so anpassen,
   dass er bei discussion-shaped Presets alle Skills in Plan kollabiert,
   ODER eine "discussion-only"-Variante in TriageOutputProducer einbauen
   die nur Plan-Phase produziert.
2. **`HasLoadedSkills` ist eine harte Vor-Bedingung.** StructuredTriage
   `Fail`t bei leerer AvailableRoles-Liste; LegacyTriage `Ok`t mit "no
   roles needed". Für autonomous (heute leer) ist das ein Verhaltens-Bruch.
   Lösung: weiches Skip für autonomous (entweder Skill-Set ergänzen oder
   Pipeline-Type-spezifisches Verhalten in StructuredTriage).
3. **`TriageOutputValidator` Plan-Phase-Pflicht.** Müsste geprüft werden,
   ob der Validator non-Plan-Phasen leer toleriert. Quick read der
   `ValidateAssignments`-Logik deutet darauf hin, dass leere Phasen
   strukturell ok sind (kein Lead, keine Analysts, kein Reviewer ist ein
   gültiger PhaseAssignment) — aber das müsste mit einem End-to-End-Test
   gegen mad-discussion + legal-analysis bestätigt werden, bevor wir die
   Migration drafen.

## Empfehlung

LegacyTriage **kann** retired werden, aber p0131c hat einen sinnvollen
Vor-Slice **`p0131c-pre`**:

- Erweitere StructuredTriage um einen "single-phase" Modus für
  Discussion-Presets ohne RunReviewPhase/RunFinalPhase. Entweder per
  Prompt-Variante oder per Phase-Trimmer der nicht-Plan-Phasen
  zusammenfaltet wenn das Preset keine Review/Final-Steps hat.
- Migriere `mad-discussion` + `legal-analysis` auf StructuredTriage; lasse
  den Selector die Discussion-Type-Routing-Logik beibehalten, aber
  innerhalb von Discussion auf StructuredTriage zeigen.
- `autonomous` bleibt ein offenes Loch — ohne Skills macht weder Legacy
  noch Structured produktiv etwas. Das ist ein Skills-Repo-Thema, nicht
  ein Triage-Thema. p0131c-pre lässt autonomous weiter durch LegacyTriage
  laufen oder schaltet es bewusst auf "noop until skills land".
- Erst dann p0131c original: `LegacyTriageStrategy` löschen,
  `ITriageStrategySelector` zu einem One-Liner kollabieren, DI-Eintrag
  rausnehmen.

**Geschätzter Umfang p0131c-pre:** Eine Slice. ~150-300 LoC
Strategie-Anpassung + Tests gegen mad-discussion + legal-analysis Smoke-
Pfad. Inkrementell, gut bisectbar.

**Wenn p0131c-pre nicht gewünscht ist:** LegacyTriage bleibt für die zwei
Presets erhalten, p0131c fällt aus dem D7-Plan, die Cleanup-LoC-Schätzung
sinkt entsprechend. Kein Drama — die anderen drei p0131-Slices (a/b/d)
bleiben unverändert wertvoll.
