# Illumination – Domain Vision

## 1. Purpose

Illumination is a personal learning application focused on fast, repeated interaction with small learning units.

Its purpose is to make knowledge and small practical skills easy to acquire, rehearse, retain, and revisit without requiring long learning sessions or high interaction overhead.

The primary initial use case is preparation for technical job interviews. Learning content may originate from topics and learning needs identified while working with Vocation, but Illumination is not a job-search application and does not depend on Vocation.

The same learning model is intended to support unrelated domains such as programming languages, technical concepts, vocabulary, Indonesian, French, or other personally relevant subjects.

## 2. Core Problem

The user frequently needs to acquire or refresh knowledge that may already be partially familiar but cannot reliably be recalled when needed.

Traditional learning workflows may introduce unnecessary friction:

- long-form material when focused repetition is needed,
- excessive text input,
- difficulty creating useful learning material quickly,
- weak visibility into what is already retained and what still requires repetition,
- cumbersome maintenance of larger decks of learning material.

Illumination addresses this through small independent learning units, immediate access to hints and reference solutions, and repeated presentation based on previous learning performance.

The intended interaction should often be fast enough to replace low-attention activities such as scrolling short-form content during otherwise idle moments.

This low-friction usage is important, but it does not require Illumination to provide a complete end-user UI. Wiiii Got This is the primary presentation on Windows and iPhone.

## 3. Fundamental Learning Loop

```text
present learning unit
        ↓
attempt recall or solution
        ↓
optionally request assistance
        ↓
reveal reference solution
        ↓
evaluate performance
        ↓
update learning state
        ↓
schedule future repetition
```

The interaction should normally take seconds rather than minutes.

Some learning units may require slightly more work, such as a small coding task, but a learning unit is always an individual learning interaction and never a container containing several independent questions.

## 4. Learning Units

Illumination manages small, independently reviewable learning units.

A learning unit may represent different kinds of activity, for example:

- recalling a factual answer,
- recognizing the correct answer from multiple choices,
- supplying a very short textual answer,
- recalling syntax,
- translating a short expression,
- mentally explaining a concept,
- solving a small programming task,
- inspecting a code fragment and determining its behavior.

`Learning Item` is the canonical term for the smallest independently reviewable unit.

## 5. Reference Solutions

Every learning unit has a reference solution.

The reference solution provides a reliable answer or example against which the learner can compare their own response.

A reference solution does not necessarily define the only valid byte-for-byte answer.

For example, a programming task may allow several correct implementations while still providing one canonical example solution.

The primary role of the reference solution is rapid feedback and self-correction.

## 6. Assistance and Hints

A learning unit may optionally provide assistance before its full solution is revealed.

Possible assistance includes:

- one or more hints,
- answer choices,
- partial information,
- other lightweight clues appropriate to the learning-unit type.

Requesting assistance and revealing the full reference solution are distinct learner actions.

The V1 policy is defined by the scheduling semantics: hints have no penalty by default, with optional configured hint influence.

## 7. Response and Evaluation Modes

Illumination must not require unnecessary input.

Depending on the learning-unit type, the learner may:

- answer mentally,
- answer verbally without recording the spoken response,
- select an answer,
- enter a very short textual response,
- enter a small code fragment,
- reveal the reference solution and assess their own performance.

Manual self-assessment is a first-class interaction mode rather than merely a fallback.

Several degrees of learning performance are desirable so difficult material can return more quickly and successful material can move further into the future.

The V1 scale is `Nochmal`, `Schwer`, `Unsicher`, `Gut`, `Leicht`; automatic evaluation may suggest a grade while the learner chooses the final assessment.

## 8. Repetition and Learning State

Repeated review is part of Illumination's core domain.

Learning units that are difficult or incorrectly answered should return sooner.

Successful reviews should increase the time until the next repetition.

Repeated success should progressively extend this interval from short periods toward days, weeks, months, and eventually intervals long enough that the unit is rarely encountered.

There is no requirement for a normal terminal state in which successfully learned material is permanently removed from the learning system.

A separate explicit user action may later allow a learning unit to be removed from normal repetition when the learner considers it permanently trivial or no longer useful.

The initial deterministic scheduling semantics are defined in `docs/08A_SCHEDULING_SEMANTICS.md`.

## 9. Learning Progress

Learning progress is not merely a statistical dashboard concern.

The current learning state of each learning unit and its next required repetition are part of Illumination's core domain.

Illumination must conceptually distinguish between material that is:

- new,
- currently requiring frequent repetition,
- becoming stable,
- retained over longer periods,
- due for another review.

Derived analytics may later include:

- number of learning units,
- new material,
- currently due material,
- difficult material,
- long-term retained material,
- review history,
- progression over time.

Analytics must not become the owner of the underlying learning state.

## 10. Decks

Users can freely organize learning units into decks comparable to Anki decks.

A deck is a user-defined grouping mechanism.

It may:

- cover one coherent subject,
- combine several subjects,
- contain material created for one purpose,
- be created manually,
- be created from imported content,
- be assembled from content originating from external learning needs.

Illumination must not impose a semantic rule that every deck corresponds to exactly one topic, learning cluster, source, or external system.

`Deck` is the canonical term for a user-defined grouping of Learning Items.

## 11. Low-Friction Learning

Illumination should support learning sessions with extremely low interaction overhead.

A learner should be able to move quickly through suitable learning units using only a small number of actions.

This includes the intended bed-mode usage in which the learner may prefer:

- mental or verbal answers,
- multiple-choice interactions,
- immediate hints,
- immediate solution reveal,
- minimal typing,
- fast movement to the next learning unit.

Not every learning unit must be suitable for this interaction style.

The precise model for expressing low-interaction suitability remains open.

## 12. Content Acquisition

Creating useful learning material quickly is an important capability.

Illumination should support structured import of externally generated learning content.

The initial intended workflow is:

```text
Illumination
    ↓
generate content-generation prompt
    ↓
external ChatGPT interaction
    ↓
structured versioned JSON
    ↓
Illumination validation and import
    ↓
new or updated learning content
```

Illumination does not require a paid language-model API for this workflow.

After successful import, the content becomes Illumination-owned data.

The import contract must eventually be explicit and versioned.

## 13. Initial Job-Interview Use Case

The first major content domain is technical job-interview preparation.

Vocation may identify learning clusters or learning needs relevant to job opportunities.

Those concepts remain Vocation-owned when they are part of Vocation's job-market model.

Illumination may create learning material that helps address such needs.

An external Vocation learning cluster is not identical to an Illumination deck.

One external learning need may result in no Illumination content, one deck, several decks, or learning units distributed across decks.

Likewise, an Illumination deck may have no relationship to Vocation at all.

## 14. Relationship to Vocation

Vocation and Illumination are independent bounded contexts and initially follow Separate Ways.

Vocation owns the job market and its own learning-cluster or learning-need concepts.

Illumination owns learning content, learning interaction, learning history, repetition state, and learning progress.

A future integration may allow Vocation to refer to a learning need and obtain limited information about its coverage or learning status.

The exact semantics, identity model, direction of references, and published contract are deliberately not yet defined.

## 15. Relationship to Wiiii Got This

Illumination remains an independent bounded context and executable capability runtime. Wiiii Got This is the primary end-user presentation on Windows and iPhone and may host Illumination locally in-process through explicit Illumination-owned application or published-contract boundaries.

Illumination must not expose internal domain objects to Wiiii Got This. A complete separate Illumination end-user UI is not required; the existing Avalonia project may remain an optional standalone/admin/dev host.

The internal implementation technology of Illumination must not become part of its integration contract.

## 16. Data Ownership

Illumination owns all authoritative data required for its learning domain, including conceptually:

- learning units,
- reference solutions,
- hints,
- user-defined decks,
- learning-unit configuration,
- review history,
- current learning state,
- repetition scheduling state,
- imported learning content and relevant provenance.

Authoritative learning data is local-first and is not intended to be stored remotely merely for convenience.

SQLite with EF Core is the accepted local persistence baseline. Optional server, Docker, or Conveyance-backed delivery infrastructure may later support connectivity or synchronization without changing Illumination's domain ownership. Illumination must first define future domain-specific publication, change, command, authority, merge, conflict, and reconciliation semantics; generic durable opaque delivery belongs to Conveyance and does not transfer that ownership. No concrete synchronization mechanism is selected.

## 17. Explicitly Outside the Domain

Illumination does not own:

- job opportunities,
- job-market research,
- job ranking,
- applications,
- companies,
- Vocation learning clusters as Vocation domain objects,
- devices,
- platform discovery,
- generic service registration,
- generic cross-application presentation,
- Wiiii Got This service orchestration.

It is also not currently defined as:

- a general note-taking system,
- a document-management system,
- a school or course-management platform,
- a social learning platform,
- an LMS,
- an AI tutor,
- an automatic examination system.

Such capabilities must not be inferred merely because they exist in other learning products.

## 18. Core Domain Hypothesis

The current core-domain hypothesis is:

> Illumination's core domain is the management of small learning interactions and the evolution of their learning state through repeated review.

Content organization and structured content acquisition support this core.

Analytics describe it.

External integrations may reference it.

But the central business problem is deciding what the learner should review, enabling that review with minimal friction, and preserving enough learning history to make future repetitions useful.

## 19. Product Success Direction

Illumination is successful when the learner can rapidly turn an identified knowledge gap into useful learning material, repeatedly practice that material with minimal friction, and progressively reduce the amount of attention required to retain it.

```text
I should know this.
        ↓
I create or import useful learning units.
        ↓
Illumination repeatedly shows me what I still struggle with.
        ↓
Stable knowledge appears less frequently.
        ↓
I can see where learning effort is still required.
```
