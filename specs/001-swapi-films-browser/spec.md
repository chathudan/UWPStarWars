# Feature Specification: Star Wars Films Browser

**Feature Branch**: `001-swapi-films-browser` *(spec directory identifier; no git branch was created — no `before_specify` hook is registered, work is currently on `main`)*

**Created**: 2026-09-05

**Status**: Draft

**Input**: User description: "Build the Drawboard UWP coding exercise using the Star Wars Movies API. Extend the provided UWP application. Do not replace the starter architecture. Page 1 lists all films (title + episode number) from https://swapi.info/api/films; selecting a film navigates to Page 2 showing title, episode number, release date, director, producer, opening crawl and the film's Characters. The app must show useful loading, empty and error states, with retry/cancel on recoverable API failures. Follow the existing UWP + MVVM structure (CommunityToolkit.MVVM, Autofac, INavigationService/PageKey, IAPIClient behind IStarWarsService, IUserInteractionService, IEventAggregator busy/done events, Serilog, Resources.resw, netstandard2.0). Add meaningful xUnit/Shouldly/NSubstitute tests that never touch the live service. Update documentation with API choice, architecture decisions, assumptions, limitations, build/run/test instructions, extension ideas and AI-assisted development notes."

## Clarifications

### Session 2026-09-05

- Q: In what order should the films be listed on Page 1? (FR-003) → A: Episode number ascending (I, II, III, IV, V, VI), sorted client-side rather than using the source's release order
- Q: How long should a single request to the Star Wars API be allowed to run before the app gives up and treats it as a failure? (FR-015) → A: 15 seconds per request, after which the request is abandoned and handled as a recoverable failure
- Q: What should Page 1 hand to Page 2 when the user selects a film — the film data it already has, or just an identifier that Page 2 uses to fetch the film itself? (FR-006) → A: The film's stable source identifier; Page 2 re-resolves the film from the source, then loads its characters. Page 2 is therefore self-sufficient and its navigation parameter survives process suspend/resume
- Q: How should a film's characters be retrieved and revealed — all at once after every character has loaded, or one by one as each arrives? (FR-010) → A: Retrieved with a concurrency limit of about 6 simultaneous requests, revealed as one complete list once all have settled, in the order the film lists them
- Q: What should happen to the starter `Welcome` and `PageA` sample pages once the film list and detail pages exist? (AC-001) → A: Removed from the production navigation path and replaced by the film list and detail flow. No demo placeholder text (for example "Downloading more RAM") may remain in the final user-facing app. The starter *framework* — shell, navigation service, DI modules, event aggregator, localization, API client — is untouched

### Session 2026-09-05 (increment 2 — all related categories)

- Q: The brief asks for "a list of **one** of the following categories". Should the detail view show only Characters, or all five? (FR-010, FR-027) → A: **All five** — Characters, Planets, Starships, Vehicles and Species — each as its own independently expandable section. This exceeds the brief deliberately: the brief's minimum is one category, and satisfying it five times over from a single code path is stronger evidence that the retrieval layer generalises than hard-coding the one category it asked for.
- Q: Should all five categories be retrieved when the detail view opens, or only when the user opens a section? (FR-028) → A: **On first expansion.** Characters is expanded on arrival and loads immediately, preserving the previous behaviour and the brief's "one category" minimum; the other four load the first time the user expands them, and are not re-requested on subsequent collapse/expand. Loading all five eagerly would mean up to 38 requests for a single film against a free community-run service, most of them for sections the user never opens.
- Q: How is a category section's progress reported, given the shell removes progress entries by exact string match? (FR-029) → A: Each section's busy/done message names its own category, so five sections loading at once produce five distinct, individually-matched progress entries rather than colliding on a single shared string.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Browse the film catalogue (Priority: P1)

A user opens the app and immediately sees the complete list of Star Wars films. Each entry shows the film's title and its episode number, so the user can recognise a film at a glance and understand where it sits in the saga's numbering.

**Why this priority**: This is the entry point of the whole experience. Without a film list there is nothing to select and no path to the detail view. Delivered alone it is already a usable product: a browsable catalogue of films.

**Independent Test**: Launch the app with a working connection and confirm the film list appears populated with titles and episode numbers, with a visible progress indication while the data is being retrieved and no progress indication left behind once it finishes.

**Acceptance Scenarios**:

1. **Given** the app is launched and the film source is reachable, **When** the film list page is shown, **Then** a progress indication appears while data is retrieved and every film returned by the source is listed with its title and episode number.
2. **Given** the film data has been retrieved, **When** the retrieval completes, **Then** the progress indication is cleared and the list is interactive.
3. **Given** the film source returns no films, **When** the retrieval completes, **Then** an explicit "no films available" message is shown instead of an empty screen, and the progress indication is cleared.
4. **Given** the film list is displayed, **When** the user inspects the ordering, **Then** films appear in ascending episode-number order (I, II, III, IV, V, VI), not in the release order the source returns.

---

### User Story 2 - Inspect a film's details (Priority: P1)

From the film list, the user selects a film and is taken to a detail view showing that film's title, episode number, release date, director, producer and opening crawl. The user can return to the list and pick a different film.

**Why this priority**: This is the second half of the exercise's core requirement and the reason the list exists. Paired with Story 1 it forms the minimum complete deliverable.

**Independent Test**: Select any film in the list and confirm the detail view opens, briefly shows progress while it retrieves that film, then shows that specific film's title, episode number, release date, director, producer and opening crawl — then use the app's back affordance to return to the list and confirm it comes back populated and interactive.

**Acceptance Scenarios**:

1. **Given** the film list is displayed, **When** the user selects a film, **Then** the detail view opens carrying that film's identifier and retrieves that film from the source.
2. **Given** the detail view is retrieving the selected film, **When** the retrieval is in progress, **Then** a progress indication is shown, and it is cleared when the retrieval finishes for any reason.
3. **Given** the detail view is open, **When** the user reads the page, **Then** the film's title, episode number, release date, director, producer and opening crawl are all shown.
4. **Given** the detail view is open, **When** the user activates the back affordance, **Then** they return to the film list and it reappears fully populated and interactive, with its loading, empty and error states behaving exactly as they did on first arrival if it re-retrieves its data.
5. **Given** the detail view is opened with a missing, blank, or wrong-typed identifier, **When** the page renders, **Then** a clear "film could not be opened" state is shown without any request being attempted, the user can return to the list, and the app does not crash or hang on a progress indication.
6. **Given** the detail view is opened with a well-formed identifier the source does not recognise, **When** the retrieval returns not-found, **Then** the same "film could not be opened" state is shown rather than a retry prompt, because retrying cannot succeed.
7. **Given** a film's release date is available, **When** it is displayed, **Then** it is formatted for reading rather than shown as a raw source value.

---

### User Story 3 - Explore a film's related categories (Priority: P2)

While viewing a film's details, the user also sees the five categories the source relates to that film — Characters, Planets, Starships, Vehicles and Species — each as its own expandable section headed by the category name and the number of entries it holds. Characters is open on arrival; the other four open on demand. Each section retrieves and reports its own contents independently of the film's core details and of every other section.

**Why this priority**: The exercise requires *one* related category in addition to the film's own fields. Showing all five from a single generalised code path both satisfies that requirement and demonstrates the retrieval layer is not special-cased to Characters. It depends on Story 2 existing but is separable: the detail view is valuable without it, and it can be added and tested on its own.

**Independent Test**: Open any film's detail view and confirm five category sections are listed with their entry counts, that Characters resolves to a list of names on arrival without blocking the film's own details, and that expanding any other section loads and lists that category's entries by name.

**Acceptance Scenarios**:

1. **Given** a film detail view is open, **When** the page renders, **Then** exactly five category sections are shown — Characters, Planets, Starships, Vehicles, Species — each headed by its localized name and the number of entries the film references for it.
2. **Given** a film detail view has just opened, **When** the page renders, **Then** the Characters section is already expanded and retrieving, and the other four sections are collapsed and have made **no** request.
3. **Given** a category section is retrieving, **When** the retrieval is in progress, **Then** that section shows its own progress indication while the film's own details and every other section remain readable and usable.
4. **Given** a category's data is retrieved successfully, **When** it is displayed, **Then** each entry is listed by name, in the order the film lists them rather than the order the responses arrived.
5. **Given** the user expands a collapsed category for the first time, **When** the section opens, **Then** its contents are retrieved; **and when** the user collapses and re-expands it, **Then** the already-retrieved contents are shown again without a further request.
6. **Given** a film references more entries in a category than the concurrency limit allows at once, **When** they are retrieved, **Then** no more than the permitted number of requests are in flight at any moment, and the section reveals the complete list only once every request has settled.
7. **Given** a film references no entries for a category, **When** that section is opened, **Then** an explicit "nothing listed" message is shown for that category, with no request attempted.
8. **Given** some records in a category fail to retrieve while others succeed, **When** the section resolves, **Then** the successfully retrieved entries are listed in film order and the user is told that part of that list could not be loaded.
9. **Given** every record in a category fails to retrieve, **When** the section resolves, **Then** it is treated as a recoverable failure with the retry/cancel path affecting **that section only**, while the film's own details and the other four sections remain on screen and usable.
10. **Given** two or more category sections are retrieving at the same time, **When** each finishes, **Then** each clears its own progress indication, and no section's completion clears another's.

---

### User Story 4 - Recover from a failed retrieval (Priority: P2)

When a data retrieval fails — the network is down, the service is unavailable, or the response is unusable — the user is told what happened and offered the choice to retry or cancel. Choosing retry attempts the operation again; choosing cancel leaves the user on a clear error state from which they can still navigate.

**Why this priority**: Error checking and reporting to the user is an explicit evaluation criterion. It applies across Stories 1–3 and is what separates a demo from a usable app, but the happy paths must exist first.

**Independent Test**: Simulate a failing data source, confirm a retry/cancel prompt is presented, confirm retry re-attempts and succeeds once the source recovers, and confirm cancel leaves a readable error state with progress cleared and navigation still working.

**Acceptance Scenarios**:

1. **Given** a *recoverable* retrieval failure occurs — a network failure, a stalled request, an unusable response, or a non-success status other than not-found on a film identifier — **When** the failure is detected, **Then** the user is shown a retry/cancel choice describing that the data could not be retrieved.
2. **Given** the retry/cancel choice is shown, **When** the user chooses retry and the source has recovered, **Then** the data is retrieved and displayed normally.
3. **Given** the retry/cancel choice is shown, **When** the user chooses cancel, **Then** an error state is displayed on the page, the progress indication is cleared, and the user can still navigate away.
4. **Given** the user has cancelled into an error state, **When** they choose the on-page retry affordance, **Then** the retrieval is attempted again.
5. **Given** any retrieval finishes for any reason — success, failure, cancel, or unexpected error — **When** it completes, **Then** no progress indication is left behind.
6. **Given** a retrieval fails, **When** the failure occurs, **Then** a diagnostic record is written with enough context to identify which operation failed and why, without recording unnecessary response payload data.

---

### Edge Cases

- **Source unreachable at launch**: the film list shows a retry/cancel choice, then a persistent error state with an on-page retry; the app never sits on an unending progress indication.
- **Source accepts the connection then stalls**: the request is abandoned once it passes the 15-second budget and is handled like any other recoverable failure, so the user is never held on a spinner waiting for a response that will not arrive.
- **Empty film collection**: an explicit empty state, not a blank list.
- **Film with no entries in a category** (every film in the catalogue lists at least one character, but several list few or no vehicles): an explicit empty state within that one section; the film's own details and the other four sections still render.
- **Partially retrievable category**: successfully retrieved entries are listed alongside a notice that some entries in *that* category are unavailable.
- **Several category sections expanded in quick succession**: each retrieves and reports independently; one section's completion never clears another's progress indication, and one section's failure never disturbs another's contents.
- **A category re-expanded after being collapsed**: the retained contents are shown again with no repeat request. A section that previously *failed* is retried only when the user asks, not silently on re-expansion.
- **A category expanded while the film itself is still loading**: not reachable — sections are only interactive once the film has resolved, since the film's response is what supplies the category reference links.
- **Missing or blank optional film fields** (director, producer, opening crawl absent or empty in the response): the field shows a neutral placeholder rather than a blank gap or a crash.
- **Malformed or non-JSON response body**: treated as a recoverable failure with the retry/cancel path, not an unhandled crash.
- **Non-success status codes (4xx/5xx)**: treated as a recoverable failure with the retry/cancel path — with the single exception of a not-found response to a film identifier, which is an invalid selection rather than something a retry could fix.
- **Detail view reached with a missing, null, or wrong-typed identifier**: a clear invalid-selection state with a route back to the list, and no request attempted.
- **Detail view reached with an identifier the source does not recognise**: the not-found response resolves to the same invalid-selection state rather than the retry/cancel path, since retrying an unknown identifier cannot succeed.
- **Identifier confused with episode number**: because a film's identifier is its release-order position (identifier `1` is Episode 4), deriving the identifier from the displayed episode number would silently open the wrong film — the identifier is carried through untouched.
- **User navigates back while a retrieval is still running**: the progress indication is cleared and no state is applied to a page the user has left.
- **User selects a second film rapidly after a first**: the detail view shows the most recently selected film, and no stale result from the earlier selection overwrites it.
- **Very long opening crawl text**: the crawl remains readable and scrollable without pushing other detail fields off-screen or being silently truncated.
- **Source field naming differs from the display model** (the source uses `episode_id`, `opening_crawl`, `release_date`): every required field is populated correctly rather than silently defaulting to zero or empty.

## Requirements *(mandatory)*

### Functional Requirements

**Film list**

- **FR-001**: The system MUST retrieve the complete set of films from the Star Wars film source when the film list is opened.
- **FR-002**: The system MUST display, for each retrieved film, the film's title and its episode number.
- **FR-003**: The system MUST present films sorted by episode number in ascending order (I through VI), applying that ordering itself rather than relying on the order the source returns.
- **FR-004**: The system MUST show a progress indication while the film list is being retrieved and clear it when retrieval finishes for any reason.
- **FR-005**: The system MUST show a distinct empty state when the source returns no films.
- **FR-006**: The system MUST allow the user to select a film from the list, and MUST open the detail view passing that film's stable source identifier as the navigation parameter. The identifier MUST be treated as opaque and MUST NOT be derived from the displayed episode number.

**Film detail**

- **FR-007**: The detail view MUST retrieve the film named by its navigation parameter, showing a progress indication while it does so and clearing that indication when the retrieval finishes for any reason. It MUST then display that film's title, episode number, release date, director, producer and opening crawl.
- **FR-008**: The detail view MUST display the release date in a human-readable format rather than the raw source value.
- **FR-009**: The detail view MUST display a neutral placeholder for any of director, producer or opening crawl that is missing or blank in the source data.
- **FR-010**: For each related category it displays, the detail view MUST list that category's entries by name, in the order the film lists them. Records MUST be retrieved with a bounded number of simultaneous requests, and a section's list MUST be revealed as a complete set once all its requests have settled rather than growing as individual results arrive.
- **FR-011**: Every related category section MUST load independently of the film's own details, so the film's details are readable before any section resolves.
- **FR-012**: Each related category section MUST show its own progress, empty, partial and error states independently of the film details **and of every other section**. If every request in a section fails, that section MUST offer the retry/cancel path; if only some fail it MUST show the successful results alongside a notice rather than discarding them. A failure in one section MUST NOT alter any other section.
- **FR-027**: The detail view MUST present all five related categories the source publishes for a film — Characters, Planets, Starships, Vehicles and Species — as separately expandable sections, each headed by its localized category name and the number of entries the film references for it. The entry count MUST come from the film's own response and therefore be shown before that category's records have been retrieved.
- **FR-028**: The Characters section MUST be expanded and begin retrieving when the detail view opens. The other four sections MUST start collapsed and MUST NOT issue any request until the user first expands them. Once a section has been retrieved successfully, collapsing and re-expanding it MUST NOT issue a further request.
- **FR-029**: Each related category section MUST publish progress under a message that names its own category, so that concurrently loading sections produce distinct, individually-matched progress entries and no section's completion clears another's.
- **FR-013**: The system MUST show a clear invalid-selection state, with a route back to the film list, when the detail view is opened with a missing, blank, or wrong-typed identifier, or with a well-formed identifier the source does not recognise. A missing or wrong-typed identifier MUST NOT cause a request to be attempted, and an unrecognised identifier MUST NOT be offered as a retryable failure.
- **FR-014**: The user MUST be able to return from the detail view to the film list using the app's existing back affordance.

**Failure handling and reporting**

- **FR-015**: The system MUST treat non-success responses, network failures, unresponsive requests, malformed responses and unusable payloads as recoverable failures rather than crashing. A single request that has not completed within 15 seconds MUST be abandoned and handled as one of these recoverable failures.
- **FR-016**: The system MUST offer the user a retry/cancel choice when a recoverable retrieval failure occurs.
- **FR-017**: Choosing retry MUST re-attempt the failed retrieval and display the result normally on success.
- **FR-018**: Choosing cancel MUST leave the affected page in a readable error state that offers a further retry and does not block navigation.
- **FR-019**: The system MUST clear all progress indication after success, failure, retry-cancel, or an unexpected error, with no orphaned progress entries.
- **FR-020**: The system MUST record diagnostics for navigation and retrieval operations that identify the operation and the failure cause, without recording unnecessary response payload data.
- **FR-021**: The system MUST NOT leave the user on an unending progress indication or fail silently under any of the above conditions.

**Presentation and content**

- **FR-022**: All user-facing text introduced by this feature MUST be sourced from the app's localizable string resources, not hard-coded in views or view models, except for design-time sample text and test data.
- **FR-023**: Each new page MUST provide a localized page header consistent with the existing page-header convention.
- **FR-026**: The shipped app MUST NOT present any placeholder or demo content carried over from the starter samples, including the simulated-work progress messages and the sample page headers. No user-facing string in the running app may refer to anything other than this feature.

**Verification**

- **FR-024**: The system MUST include automated tests covering: successful film list load; films being re-ordered into ascending episode order from an unordered source response; film selection triggering navigation with the correct film identifier; successful detail load resolved from an identifier; invalid navigation parameter handling for missing, blank, wrong-typed and unrecognised identifiers; empty film and empty category responses; category entries being returned in film order regardless of response order, with the concurrency limit respected; partial category failure preserving successful results; a request exceeding its time budget being surfaced as a recoverable failure; retrieval failure producing the retry/cancel prompt; retry succeeding on the second attempt; cancel producing the error state; source-record-to-display-model mapping including the differently named and optional fields **and the reference links for all five categories**; the detail view exposing exactly five category sections with counts taken from the film's own response; only the Characters section requesting on arrival; a first expansion triggering exactly one retrieval and a re-expansion triggering none; one section's failure leaving the film and the other sections untouched; each section's progress being published under a message naming its own category; and progress being cleared in every completion path including thrown exceptions.
- **FR-025**: Automated tests MUST NOT contact the live Star Wars service; all external data access MUST be substitutable in tests.

### Architectural Constraints *(mandatory — imposed by the assignment and project constitution)*

These are graded acceptance criteria for this exercise, not implementation preferences. They are recorded here because the submission is evaluated on *how* it is built as well as what it does. See `.specify/memory/constitution.md` for the governing text.

- **AC-001**: The existing UWP application MUST be extended; the starter architecture, shell, and project layout MUST NOT be replaced. The starter `Welcome` and `PageA` sample pages are superseded by the film list and detail flow and MUST NOT remain in the shipped navigation path. The framework those samples demonstrated — shell, navigation service, DI modules, event aggregator, localization service and API client — MUST be reused unchanged.
- **AC-002**: New UI state and commands MUST use the existing MVVM approach (`ObservableObject`, CommunityToolkit.Mvvm observable properties and relay commands).
- **AC-003**: All new services and view models MUST be resolved through Autofac using constructor injection; manual construction of dependencies inside views or view models is forbidden.
- **AC-004**: Navigation MUST go through `INavigationService`; each new page MUST have its own `PageKey` and MUST be registered in `NavigationModule`.
- **AC-005**: Star-Wars-specific retrieval and mapping logic MUST sit behind an app-defined abstraction (`IStarWarsService` / `StarWarsService`).
- **AC-006**: All HTTP access MUST go through the provided `IAPIClient`. Views and view models MUST NOT use `HttpClient` directly, and no NuGet package that wraps the Star Wars API may be added.
- **AC-007**: Source-shaped data records MUST be kept separate from the display models used by view models wherever formatting, ordering, or UI-specific state is required.
- **AC-008**: Retry/cancel handling MUST use `IUserInteractionService`.
- **AC-009**: Progress MUST be published via `IEventAggregator` using `NotifyBusyEvent` and `NotifyDoneEvent` with byte-identical message strings, with the done event published in a `finally` block.
- **AC-010**: Diagnostics MUST use Serilog.
- **AC-011**: User-facing strings MUST come from `Resources.resw` via the existing localization mechanisms (`x:Uid`, `ILocalizationService`, or the page-header convention).
- **AC-012**: Shared projects MUST remain `netstandard2.0`-compatible and the app MUST remain buildable against the configured Windows SDK / UWP runtime constraints.
- **AC-013**: Tests MUST use xUnit, Shouldly and NSubstitute.
- **AC-014**: All candidate-authored documentation MUST live in a new `SOLUTION.md` at the repository root, covering ten sections: (1) chosen API, (2) architecture decisions, (3) assumptions, (4) limitations, (5) how to build and run, (6) how to run tests, (7) **error handling approach**, (8) **progress/loading behaviour**, (9) future extension ideas, and (10) AI-assisted development — what AI produced, the challenges it hit, the manual corrections made, and the validation evidence. Sections 1–9 satisfy Constitution XIII's eight required topics plus progress behaviour; section 10 satisfies XIII's AI clause and XIV's evidence gate. `README.md` is the supplied Drawboard exercise brief — it states the problem, not the solution — and MUST NOT be modified.

### Key Entities

- **Film**: A Star Wars film as published by the source. Attributes: title, episode number, opening crawl, director, producer, release date, and reference links to its characters, planets, starships, vehicles and species. Identified by a stable, opaque source identifier that is independent of the episode number and is the sole means of re-retrieving the film.
- **Related resource**: An entry in one of the five categories a film relates to — a character, planet, starship, vehicle or species. All five publish a `name`, verified live against the source on 2026-09-05, so for this feature they are one entity with one shape: a name and the reference link it was reached by. There is no per-category record type because there is no per-category behaviour.
- **Related category**: One of the five fixed groupings — Characters, Planets, Starships, Vehicles, Species. It names itself for display, knows the film's reference links for its own entries, and owns its own retrieval state independently of the other four.
- **Film list item (display)**: What the list page shows for one film — its title and episode number in display form, plus the film's opaque identifier used to open the correct detail view.
- **Film detail (display)**: What the detail page shows for one film — its formatted release date, its text fields with placeholders applied where the source value is missing, and its character section.
- **Page state**: The mutually exclusive condition of a data-bearing region at any moment — loading, loaded, empty, error, or invalid-selection — that determines what the user sees.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: On a working connection, a user launching the app sees the complete film list, with title and episode number for every film, within 5 seconds and with no residual progress indication.
- **SC-002**: A user can go from launching the app to reading a chosen film's opening crawl in 2 interactions or fewer.
- **SC-003**: 100% of the required detail fields — title, episode number, release date, director, producer, opening crawl — are visible on the detail view for every film in the catalogue, with no blank gaps where a source value is missing.
- **SC-004**: Every related category section resolves without preventing the user from reading the film's own details, or from expanding another section, at any point during the load.
- **SC-011**: A user can reach every entry name in all five related categories for any film in the catalogue, and no section's failure or emptiness prevents reaching the other four.
- **SC-005**: In every failure scenario exercised — unreachable source, stalled request, non-success status, malformed response, empty response, invalid selection — the user reaches a readable state that names the problem and offers a next action within 20 seconds of the attempt starting, and no scenario leaves a progress indication running or requires restarting the app.
- **SC-006**: After a failure, a user who chooses retry against a recovered source reaches the normal populated view without restarting the app or losing their place.
- **SC-007**: A reviewer can return from the detail view to the list, select a different film, and see the newly selected film's details every time, with no data from the previously viewed film remaining on screen.
- **SC-008**: The automated test suite runs to completion with no network access available and covers every scenario listed in FR-024.
- **SC-009**: A reviewer following only the project documentation can build the solution, run the app, and run the tests without needing to ask a question.
- **SC-010**: A reviewer exercising every reachable screen in the shipped app encounters no placeholder or demo content from the starter samples.

## Assumptions

Decisions taken where the brief left room. Each is reversible during planning.

**Source data (verified against the live endpoint on 2026-09-05)**

- The films endpoint returns a bare JSON array of 6 film records, not a paged envelope. There is no pagination to implement for the film list.
- Film records use underscore-separated field names (`episode_id`, `opening_crawl`, `release_date`) that do not match the app's existing camel-case JSON convention, so field mapping must be stated explicitly rather than inferred. This is the most likely source of silently-empty fields and is called out as an edge case.
- A film's `characters` entries are **absolute** URLs (`https://swapi.info/api/people/1`), while the app's HTTP client composes requests from a configured base address plus a relative path. The Star Wars service is assumed to normalise those absolute references against the configured base before requesting them, so that `IAPIClient` remains the single HTTP surface (AC-006).
- A single film can be retrieved on its own by its identifier, returning one film object rather than an array; an unrecognised identifier returns a not-found status. This was verified live, and it is what makes the identifier-based navigation decision viable.
- **A film's identifier is its release-order position, not its episode number** — the record whose identifier is `1` is Episode 4. Treating the two as interchangeable would open the wrong film, so the identifier must be carried through as opaque and never derived from the displayed episode number.
- Every one of the five related categories publishes a `name` field on its records — verified live on 2026-09-05 against `people/1`, `planets/1`, `starships/2`, `vehicles/4` and `species/1`. That is what makes a single record shape and a single retrieval path correct for all five rather than a convenient guess.
- Per-category counts are modest — *A New Hope* lists 18 characters, 3 planets, 8 starships, 4 vehicles and 5 species, 38 references in total — so retrieving each record individually is acceptable without a caching or batching layer. 38 requests is nonetheless enough that issuing them all on page open would be discourteous to a free community-run service, which is why sections load on first expansion.
- The API is public, unauthenticated and read-only; no credentials, tokens, or persistence are required. It publishes no rate limit, but the app does not rely on that — the concurrency limit in FR-010 exists so that a throttling response would be an unexpected failure to handle rather than a predictable consequence of the design.

**Scope and behaviour**

- The film list is the app's landing page, so a reviewer lands directly on the deliverable. The starter `Welcome` and `PageA` sample pages are superseded by the film list and detail flow and are removed from the shipped navigation path, along with their placeholder text, rather than being left registered but unreachable. Evidence that the starter was extended rather than replaced comes from the framework itself — shell, navigation service, DI modules, event aggregator, localization and API client are all reused unchanged.
- The application's configured base address, currently a placeholder, is repointed at the Star Wars API. No other API is called.
- Related category entries are loaded after the film's own details are already on screen, using at most about 6 simultaneous requests. A section stays in its loading state until every one of its requests has settled, then reveals the complete list in the order the film lists them — not in the order responses returned. The concurrency limit keeps the app from firing dozens of simultaneous requests at a free community-run service; the exact number is a tuning value, not a contract. If *every* request in a section fails, that is a recoverable failure with the retry/cancel path for that section; if only *some* fail, the successful ones are shown with a notice.
- All five categories are shown, not the single category the brief asks for. They are served by **one** retrieval path and **one** record shape, because all five publish the same `name`/`url` shape — five near-identical record types and five near-identical retrieval methods would be duplication with no behavioural difference, which Principle II forbids. The cost of the wider scope is therefore one enum and one collection, not five times the code.
- Characters is expanded on arrival; the other four load on first expansion and are not re-requested afterwards. This keeps the page's opening cost identical to the previous Characters-only behaviour while making the additional four available in one interaction each.
- The selected film is carried to the detail view as its stable source identifier, not as the retrieved record. The detail view validates that identifier, retrieves the film itself, and then retrieves its characters. This keeps the navigation parameter serializable — so it survives process suspend/resume, which an object graph would not — and leaves the detail view able to open independently of the list. The accepted cost is one extra request and a brief loading state before the film's own fields appear.
- Each individual request is given a 15-second budget. `IAPIClient` exposes no cancellation token and its underlying HTTP client is shared and static, so enforcing that budget is a deliberate piece of design rather than a configuration value; the chosen mechanism must not change the timeout for unrelated callers.
- Retry/cancel is offered per failed operation. Cancelling leaves an on-page error state with its own retry affordance rather than automatically navigating away, so the user keeps control.
- "Empty state" and "error state" are page-level visual states, not dialogs; the retry/cancel prompt is the only modal interruption.
- No persistence, offline cache, or background refresh is in scope. Data is retrieved fresh per navigation.
- Only English strings are added. The localization mechanism is used correctly so that further languages are a content addition, not a code change.
- Accessibility is limited to what the existing controls and localized strings provide; no dedicated screen-reader or high-contrast work is in scope.

**Environment**

- The exercise is built and reviewed on Windows with Visual Studio 2026 (18.7+), the UWP tooling, and the Windows 11 SDK (10.0.26100.0), per the assignment brief.
- Reviewers run the app with an internet connection available; failure paths are demonstrated by disconnecting or pointing the base address at an unreachable host.

## Dependencies

- The public Star Wars API at `https://swapi.info/api` must be reachable for manual verification of the happy path. Automated tests do not depend on it (FR-025).
- The starter application's existing services — `IAPIClient`, `INavigationService`, `IUserInteractionService`, `IEventAggregator`, `ILocalizationService`, `IThreadDispatcher` — are reused as-is and are assumed to work as documented in `README.md`.

## Out of Scope

- The Metropolitan Museum of Art API alternative offered in the brief.
- Any field of a related-category record beyond its name. A planet's climate, a starship's hyperdrive rating and a species' language are all in the retrieved payload and none are shown — the sections list names, and there is no drill-down into an individual character, planet, starship, vehicle or species.
- Search, filter, sort controls, or favourites.
- Persistence, offline support, and caching between sessions.
- Authentication, user accounts, and telemetry.
- Replacing or restyling the existing shell or navigation chrome. (Retiring the superseded `Welcome` and `PageA` samples is in scope; changing the framework they sat on is not.)
