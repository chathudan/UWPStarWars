---

description: "Task list for Star Wars Films Browser"
---

# Tasks: Star Wars Films Browser

**Input**: Design documents from `/specs/001-swapi-films-browser/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md), [data-model.md](./data-model.md), [contracts/](./contracts/), [quickstart.md](./quickstart.md)

**Tests**: **MANDATORY.** Constitution XV requires red-green-refactor for all non-trivial production behaviour, and FR-024 lists the required coverage. Every behaviour task below is immediately preceded by the failing test that drives it.

**Organization**: Grouped by user story. Because Constitution XV also fixes a *build order*, the shared layers every story depends on (URL handling, DTOs, mapping, service, ViewModel base) are completed in Phase 2 before any story begins — see [plan.md](./plan.md) § Implementation Sequence.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (US1–US4)
- **RED** / **GREEN**: the two halves of a TDD cycle. A RED task is complete only when the new test **fails on behaviour** — a compile error is not a valid red (see [research.md](./research.md) R9).

> **Why there is no separate "Tests" block per story.** The template groups all of a story's tests ahead of all its implementation. That shape suits test-after or test-alongside work. Under XV each behaviour gets its own red-green pair, so tests and implementation genuinely interleave, and task IDs are listed in **true execution order** — RED, then its GREEN, then the next RED. Grouping them would have made the IDs non-sequential.

## Path Conventions

Six existing projects, no new ones:

- `DrawboardCodingExercise.Contracts/` — shared abstractions (netstandard2.0)
- `DrawboardCodingExercise.Services/` — API + infrastructure (netstandard2.0)
- `DrawboardCodingExercise.ViewModel/` — UI state (netstandard2.0)
- `DrawboardCodingExercise/` — UWP app: views, modules, resources
- `DrawboardCodingExercise.Services.UnitTests/` — xUnit + Shouldly + NSubstitute (net8.0)

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Make the test suite capable of testing ViewModels, establish the evidence baseline, and add the additive configuration later phases need.

> **`PageKey` only gains members here and loses none.** Removing `Welcome`/`PageA` now would break `NavigationModule`, `ShellViewModel` and both sample ViewModels before their replacements exist. A suite that will not compile makes every subsequent red meaningless, so the samples are retired in Phase 7 once the real pages work. Keeping the build green between cycles is a TDD requirement, not tidiness.

- [X] T001 Commit the starter plus the `specs/` planning artifacts as the TDD evidence baseline, so the cycle commits that follow are demonstrably test-first (repo root, `git commit`)
- [X] T002 Add `<ProjectReference Include="..\DrawboardCodingExercise.Services\DrawboardCodingExercise.Services.csproj" />` to `DrawboardCodingExercise.ViewModel/DrawboardCodingExercise.ViewModel.csproj`
- [X] T003 Add `<ProjectReference Include="..\DrawboardCodingExercise.ViewModel\DrawboardCodingExercise.ViewModel.csproj" />` to `DrawboardCodingExercise.Services.UnitTests/DrawboardCodingExercise.Services.UnitTests.csproj`
- [X] T004 [P] Add `Films` and `FilmDetails` members to `DrawboardCodingExercise.Contracts/PageKey.cs`, keeping `Welcome` and `PageA` for now
- [X] T005 [P] Change `ServerAddress` to `https://swapi.info/api` in `DrawboardCodingExercise/Configuration/ApplicationConfiguration.cs`
- [X] T006 [P] Add captured real SWAPI payloads (snake_case, exactly as the API returns them) as string constants in `DrawboardCodingExercise.Services.UnitTests/TestData/SwapiPayloads.cs`
- [X] T007 [P] Add DTO and display-model builders for arranging test data in `DrawboardCodingExercise.Services.UnitTests/TestDoubles/Builders.cs`
- [X] T008 Verify the baseline is green by running `dotnet test DrawboardCodingExercise.Services.UnitTests/DrawboardCodingExercise.Services.UnitTests.csproj`

**Checkpoint**: ViewModels are reachable from tests. Nothing in the app has changed behaviour yet.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Build every layer the user stories sit on — URL handling, DTOs, mapping, the API service, and the ViewModel base that owns progress and retry/cancel.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete. All of it is plain `netstandard2.0` needing no UWP runtime, so the entire phase runs in the net8.0 test project.

### Cycle 1 — URL normalisation

- [X] T009 **RED** Stub `SwapiResourcePath.ToRelativePath` and `ExtractId` (signatures only, `NotImplementedException`) in `DrawboardCodingExercise.Services/StarWars/SwapiResourcePath.cs`, then write failing tests T30 and T31 in `DrawboardCodingExercise.Services.UnitTests/StarWars/SwapiResourcePathTests.cs`
- [X] T010 **GREEN** Implement `ToRelativePath` and `ExtractId` in `DrawboardCodingExercise.Services/StarWars/SwapiResourcePath.cs` — case-insensitive base match, tolerant of trailing slashes, returning `null` rather than throwing

### Cycle 2 — DTOs and display mapping

- [X] T011 [P] Create `FilmDto` with an explicit `[JsonProperty]` on **every** property in `DrawboardCodingExercise.Services/StarWars/Dtos/FilmDto.cs` (`ReleaseDate` is `string`, not `DateTime` — see [data-model.md](./data-model.md) §1)
- [X] T012 [P] Create `PersonDto` with explicit `[JsonProperty]` on `name` and `url` in `DrawboardCodingExercise.Services/StarWars/Dtos/PersonDto.cs`
- [X] T013 [P] Create the `FilmListItem` display model in `DrawboardCodingExercise.ViewModel/Models/FilmListItem.cs`
- [X] T014 [P] Create the `FilmDetailsDisplay` display model in `DrawboardCodingExercise.ViewModel/Models/FilmDetailsDisplay.cs`
- [X] T015 [P] Create the `CharacterListItem` display model in `DrawboardCodingExercise.ViewModel/Models/CharacterListItem.cs`
- [X] T016 **RED** Stub `FilmMapper` in `DrawboardCodingExercise.ViewModel/Models/FilmMapper.cs`, then write failing tests T26–T29 in `DrawboardCodingExercise.Services.UnitTests/Mapping/FilmMapperTests.cs`, asserting against the captured payload from T006 rather than a hand-written camel-case fixture
- [X] T017 **GREEN** Implement mapping rules M1–M6 in `DrawboardCodingExercise.ViewModel/Models/FilmMapper.cs` — id from `url` only, placeholders for blank text, non-throwing date parse, no sorting

### Cycle 3 — Film list retrieval and failure modes

- [X] T018 Define `IStarWarsService` and `CharacterLoadResult` per [contracts/IStarWarsService.md](./contracts/IStarWarsService.md) in `DrawboardCodingExercise.Services/StarWars/IStarWarsService.cs`
- [X] T019 **RED** Stub `StarWarsService` (constructor taking `IAPIClient`, `IAPISettings`, `ILogger` and an injectable budget `TimeSpan`) in `DrawboardCodingExercise.Services/StarWars/StarWarsService.cs`, then write failing tests T24, T25, T32 and T35 in `DrawboardCodingExercise.Services.UnitTests/StarWars/StarWarsServiceFilmsTests.cs`
- [X] T020 **GREEN** Implement `GetFilmsAsync` in `DrawboardCodingExercise.Services/StarWars/StarWarsService.cs` — bare-array deserialization, null or empty yields an empty list, non-success and malformed bodies propagate, failures logged at `Error`

### Cycle 4 — Request budget

- [X] T021 **RED** Write failing test T23 in `DrawboardCodingExercise.Services.UnitTests/StarWars/StarWarsServiceBudgetTests.cs` using a substituted `IAPIClient` that completes after the injected budget — the test must use a millisecond budget and **must not wait 15 real seconds**
- [X] T022 **GREEN** Implement the per-request budget in `DrawboardCodingExercise.Services/StarWars/StarWarsService.cs` by racing each call against a delay, without touching `IAPIClient` or its static `HttpClient`

### Cycle 5 — Single film and not-found

- [X] T023 **RED** Write failing test T34 in `DrawboardCodingExercise.Services.UnitTests/StarWars/StarWarsServiceFilmTests.cs` — a 404 returns `null` while every other status still propagates
- [X] T024 **GREEN** Implement `GetFilmAsync` in `DrawboardCodingExercise.Services/StarWars/StarWarsService.cs`, catching only `HttpStatusException` with `NotFound`

### Cycle 6 — Character retrieval

- [X] T025 **RED** Write failing tests T18–T21 in `DrawboardCodingExercise.Services.UnitTests/StarWars/StarWarsServiceCharactersTests.cs` — film order preserved regardless of completion order, at most 6 concurrent (assert the observed peak), partial failure keeps successes, total failure throws
- [X] T026 **GREEN** Implement `GetCharactersAsync` in `DrawboardCodingExercise.Services/StarWars/StarWarsService.cs` using `SemaphoreSlim(6)` with `Task.WhenAll`, a per-item catch, and URLs normalised through `SwapiResourcePath`

### Cycle 7 — Progress event pairing

- [X] T027 Create the `PageLoadState` enum (`Loading`, `Loaded`, `Empty`, `Error`, `InvalidSelection`) in `DrawboardCodingExercise.ViewModel/PageLoadState.cs`
- [X] T028 **RED** Stub `PageViewModelBase.RunBusyAsync` in `DrawboardCodingExercise.ViewModel/PageViewModelBase.cs`, then write failing tests T13–T16 against a minimal probe ViewModel in `DrawboardCodingExercise.Services.UnitTests/ViewModels/PageViewModelBaseProgressTests.cs` — assert the busy and done strings are **byte-identical**, and that done is posted on success, on failure, and on a thrown exception
- [X] T029 **GREEN** Implement `RunBusyAsync` in `DrawboardCodingExercise.ViewModel/PageViewModelBase.cs`, capturing the message into a single local and posting `NotifyDoneEvent` from that same local inside `finally`

### Cycle 8 — Retry/cancel loop

- [X] T030 **RED** Write failing tests T11 and T12 against the probe ViewModel in `DrawboardCodingExercise.Services.UnitTests/ViewModels/PageViewModelBaseRetryTests.cs` — Retry re-attempts and succeeds the second time, Cancel yields `Error`, and neither leaks a progress event
- [X] T031 **GREEN** Implement the retry/cancel helper in `DrawboardCodingExercise.ViewModel/PageViewModelBase.cs` using `IUserInteractionService`, re-attempting without double-posting busy events

### Wiring

- [X] T032 Register `StarWarsService` as `IStarWarsService` in `DrawboardCodingExercise/Module/WebServicesModule.cs`

**Checkpoint**: The entire API and state machinery exists and is fully covered by tests, with no UWP dependency. User stories can now begin.

---

## Phase 3: User Story 1 - Browse the film catalogue (Priority: P1) 🎯 MVP

**Goal**: The app opens on a list of all six films, each showing title and episode number, ordered Episode I → VI, with working loading and empty states.

**Independent Test**: Launch the app with a working connection — the film list appears populated with titles and episode numbers sorted by episode, with progress shown while loading and cleared afterwards.

- [X] T033 [US1] **RED** Stub `FilmsViewModel : PageViewModelBase, INavigateToAware, IProvidePageHeader` in `DrawboardCodingExercise.ViewModel/FilmsViewModel.cs`, then write failing tests T1, T3, T17 and the real-ViewModel half of T13/T16 in `DrawboardCodingExercise.Services.UnitTests/ViewModels/FilmsViewModelTests.cs` — films load, an empty response yields `Empty` rather than `Error`, `OnNavigatedToAsync` never propagates an exception, and a load posts **exactly one `NotifyBusyEvent` matched by a byte-identical `NotifyDoneEvent`** on the substituted `IEventAggregator`, on both the success and failure paths
- [X] T034 [US1] **GREEN** Implement the load with `Loaded`/`Empty`/`Error` states and progress in `DrawboardCodingExercise.ViewModel/FilmsViewModel.cs`, mapping through `FilmMapper` and bracketing the call in `RunBusyAsync`
- [X] T035 [US1] **RED** Write failing test T2 in `DrawboardCodingExercise.Services.UnitTests/ViewModels/FilmsViewModelTests.cs` — an **unordered** service response is presented ascending by episode number
- [X] T036 [US1] **GREEN** Implement ascending episode-number ordering in `DrawboardCodingExercise.ViewModel/FilmsViewModel.cs`, sorting on the `int` and never on the display label
- [X] T037 [US1] Add `PageHeader.Films.Text`, the film-list empty-state message and the episode-label format to `DrawboardCodingExercise/Strings/en/Resources.resw`
- [X] T038 [US1] Create `Films.xaml` and `Films.xaml.cs` in `DrawboardCodingExercise/View/` — a list of title and episode number, `x:Uid`-localized, with loading and empty regions bound through the existing `BoolToVisibilityConverter`; code-behind contains `InitializeComponent()` only
- [X] T039 [US1] Add `<Compile Include="View\Films.xaml.cs">` and `<Page Include="View\Films.xaml">` entries to `DrawboardCodingExercise/DrawboardCodingExercise.csproj` — the project is old-style MSBuild and does **not** glob files
- [X] T040 [US1] Register `builder.RegisterView<Films, FilmsViewModel>(PageKey.Films);` in `DrawboardCodingExercise/Module/NavigationModule.cs`
- [X] T041 [US1] Change the startup navigation target to `PageKey.Films` in `DrawboardCodingExercise.ViewModel/ShellViewModel.cs`

**Checkpoint**: The app launches on a working, correctly ordered film list. This is the MVP — demonstrable on its own.

---

## Phase 4: User Story 2 - Inspect a film's details (Priority: P1)

**Goal**: Selecting a film opens a detail page that resolves the film from its identifier and shows title, episode number, release date, director, producer and opening crawl.

**Independent Test**: Select any film — the detail view opens, briefly shows progress, then displays that specific film's six required fields. Back returns to a populated list.

- [X] T042 [US2] **RED** Write failing tests T4 and T5 in `DrawboardCodingExercise.Services.UnitTests/ViewModels/FilmsViewModelTests.cs` — selecting a film navigates to `PageKey.FilmDetails` passing **that film's opaque id string**, and a film with a null id does not navigate at all
- [X] T043 [US2] **GREEN** Implement the `[RelayCommand]` film-selection handler in `DrawboardCodingExercise.ViewModel/FilmsViewModel.cs`, passing `FilmListItem.Id` and never deriving it from the episode number
- [X] T044 [US2] **RED** Stub `FilmDetailsViewModel` in `DrawboardCodingExercise.ViewModel/FilmDetailsViewModel.cs`, then write failing tests T7, T8 and T9 in `DrawboardCodingExercise.Services.UnitTests/ViewModels/FilmDetailsViewModelTests.cs` — null, non-string and whitespace parameters each yield `InvalidSelection` **and the service is never called**
- [X] T045 [US2] **GREEN** Implement navigation-parameter validation in `DrawboardCodingExercise.ViewModel/FilmDetailsViewModel.cs`, guarding before any service call is made
- [X] T046 [US2] **RED** Write failing tests T6, T10, T17 and the real-ViewModel half of T13/T16 in `DrawboardCodingExercise.Services.UnitTests/ViewModels/FilmDetailsViewModelTests.cs` — a valid id loads and exposes all six fields, a 404 yields `InvalidSelection` with **no retry prompt**, `OnNavigatedToAsync` never propagates, and the film load posts **exactly one `NotifyBusyEvent` matched by a byte-identical `NotifyDoneEvent`**; also assert that an `InvalidSelection` short-circuit posts **neither** event, since no work was started
- [X] T047 [US2] **GREEN** Implement film retrieval, `Loading`/`Loaded` states and the 404-to-`InvalidSelection` path in `DrawboardCodingExercise.ViewModel/FilmDetailsViewModel.cs`, bracketing the retrieval in `RunBusyAsync` and leaving the parameter guard outside it
- [X] T048 [US2] Add `PageHeader.FilmDetails.Text`, the six field labels, the "not available" placeholder and the invalid-selection message to `DrawboardCodingExercise/Strings/en/Resources.resw`
- [X] T049 [US2] Create `FilmDetails.xaml` and `FilmDetails.xaml.cs` in `DrawboardCodingExercise/View/` — the six fields plus a scrollable opening crawl and an invalid-selection region; code-behind contains `InitializeComponent()` only
- [X] T050 [US2] Add `<Compile Include="View\FilmDetails.xaml.cs">` and `<Page Include="View\FilmDetails.xaml">` entries to `DrawboardCodingExercise/DrawboardCodingExercise.csproj`
- [X] T051 [US2] Register `builder.RegisterView<FilmDetails, FilmDetailsViewModel>(PageKey.FilmDetails);` in `DrawboardCodingExercise/Module/NavigationModule.cs`

**Checkpoint**: Both pages work end to end. The exercise's core two-page requirement is satisfied.

---

## Phase 5: User Story 3 - See the characters in a film (Priority: P2)

**Goal**: The detail page also lists the film's characters, loading independently so the film's own fields stay readable throughout.

**Independent Test**: Open any film's detail view — the character section shows its own progress then a populated list of names, without ever blocking the film's own details.

- [X] T052 [US3] **RED** Write failing tests T22 and T33 in `DrawboardCodingExercise.Services.UnitTests/ViewModels/FilmDetailsViewModelTests.cs` — a film with no characters yields the `Empty` character state, a failing character load leaves the film's own state `Loaded` with its fields readable, and a partial result reports the shortfall while keeping the successes. Also assert the character load posts its **own matched busy/done pair, distinct from the film load's**, so the two operations cannot cancel each other out in the shell's progress list
- [X] T053 [US3] **GREEN** Implement the independent character-section state machine in `DrawboardCodingExercise.ViewModel/FilmDetailsViewModel.cs` — started after the film resolves, holding its own `PageLoadState`, and surfacing `CharacterLoadResult.IsPartial`
- [X] T054 [US3] Add the character-section heading, empty-state and partial-failure strings to `DrawboardCodingExercise/Strings/en/Resources.resw`
- [X] T055 [US3] Add the character list section with its own loading, empty and partial regions to `DrawboardCodingExercise/View/FilmDetails.xaml`

**Checkpoint**: All three content stories work. Only failure-path work remains.

---

## Phase 6: User Story 4 - Recover from a failed retrieval (Priority: P2)

**Goal**: Every recoverable failure reaches the user as a retry/cancel choice, and cancelling lands on a readable on-page error state with its own retry.

> **This story is deliberately cross-cutting.** The retry/cancel *mechanism* was built and tested in Phase 2 (cycles 7–8) because both ViewModels consume it — building it twice would be waste. What remains here is making it user-visible and verifying it through the real ViewModels rather than the probe.

**Independent Test**: Point `ServerAddress` at an unreachable host. Each page shows the retry/cancel dialog; Retry succeeds once reachable; Cancel leaves a readable error state with a working on-page retry and no progress left spinning.

- [ ] T056 [US4] **RED** Write failing retry/cancel tests for the real `FilmsViewModel` in `DrawboardCodingExercise.Services.UnitTests/ViewModels/FilmsViewModelTests.cs` — failure prompts through `IUserInteractionService`, Retry succeeds on the second attempt, Cancel yields `Error`, and progress is cleared in both outcomes
- [ ] T057 [US4] **GREEN** Route film-list retrieval through the retry/cancel helper and expose an on-page retry command in `DrawboardCodingExercise.ViewModel/FilmsViewModel.cs`
- [ ] T058 [US4] **RED** Write the equivalent failing tests for `FilmDetailsViewModel`, covering the film load and the character load separately, in `DrawboardCodingExercise.Services.UnitTests/ViewModels/FilmDetailsViewModelTests.cs`
- [ ] T059 [US4] **GREEN** Route both detail retrievals through the retry/cancel helper and expose their on-page retry commands in `DrawboardCodingExercise.ViewModel/FilmDetailsViewModel.cs` — retrying characters must not re-request the film
- [ ] T060 [US4] Add the error-state text and retry-affordance strings to `DrawboardCodingExercise/Strings/en/Resources.resw`
- [ ] T061 [P] [US4] Add the error-state region with its retry button to `DrawboardCodingExercise/View/Films.xaml`
- [ ] T062 [P] [US4] Add error-state regions with retry buttons for both the film and character sections to `DrawboardCodingExercise/View/FilmDetails.xaml`

**Checkpoint**: Every user story is complete and the failure matrix is covered.

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Retire the starter samples, write the documentation deliverable, and run the full validation gate.

> Sample retirement happens **last** so the solution compiles and the suite stays green through every TDD cycle. Removing `PageKey` members earlier would break `NavigationModule` and `ShellViewModel` before their replacements existed.

- [ ] T063 [P] Delete `DrawboardCodingExercise/View/Welcome.xaml`, `Welcome.xaml.cs`, `PageA.xaml` and `PageA.xaml.cs`
- [ ] T064 [P] Delete `DrawboardCodingExercise.ViewModel/WelcomeViewModel.cs` and `DrawboardCodingExercise.ViewModel/PageAViewModel.cs`
- [ ] T065 Remove the `Welcome` and `PageA` members from `DrawboardCodingExercise.Contracts/PageKey.cs`
- [ ] T066 Remove the sample `RegisterView` calls from `DrawboardCodingExercise/Module/NavigationModule.cs`
- [ ] T067 Remove the `Welcome` and `PageA` `<Compile>` and `<Page>` entries from `DrawboardCodingExercise/DrawboardCodingExercise.csproj`
- [ ] T068 Remove `NavigatedToPageA.Text`, `PageHeader.PageA.Text` and `PageHeader.Welcome.Text` from `DrawboardCodingExercise/Strings/en/Resources.resw`
- [ ] T069 Build `DrawboardCodingExercise/DrawboardCodingExercise.csproj` for x64 with MSBuild and confirm `dotnet test DrawboardCodingExercise.Services.UnitTests/DrawboardCodingExercise.Services.UnitTests.csproj` is green with the network disabled
> **All candidate-authored documentation goes in `SOLUTION.md` at the repository root. `README.md` is the supplied Drawboard exercise brief and MUST NOT be modified** — it states the problem, and editing it would overwrite the question with the answer. The ten required sections are specified in [plan.md](./plan.md) § Documentation Deliverable; tasks T070–T075 write them in order.

- [ ] T070 Create `SOLUTION.md` at the repository root with sections 1–4: chosen API (Star Wars Movies API, why it was chosen, and the verified response shapes including the id-vs-episode trap), architecture decisions, assumptions, and limitations
- [ ] T071 Add sections 5–6 to `SOLUTION.md` — how to build and run (VS 2026 + UWP workload + Windows 11 SDK, x64/ARM64 only, MSBuild for the UWP project) and how to run tests (`dotnet test`, deterministic and offline, injected budget so no test waits 15 real seconds)
- [ ] T072 Add section 7 to `SOLUTION.md` — the error handling approach, explaining the failure taxonomy and **why the distinctions exist**: recoverable failures get retry/cancel, a 404 on a film id is an invalid selection rather than a retryable error, invalid parameters short-circuit before any request, and partial character failure keeps its successes while total failure stays retryable
- [ ] T073 Add section 8 to `SOLUTION.md` — the progress/loading behaviour: how `NotifyBusyEvent`/`NotifyDoneEvent` drive the shell's progress ring, how `PageViewModelBase` makes a mismatched pair structurally impossible, why that matters given `ShellViewModel.OnNotifyDone` has no `-1` guard, that film and character loads carry independent progress, and that progress clears on success, failure, cancel and unexpected exception
- [ ] T074 Add section 9 to `SOLUTION.md` — future extension ideas, led by adding `CancellationToken` support to `IAPIClient` so a timeout truly aborts the request, plus the other four related categories, caching, search/filter, deep-linking and further languages
- [ ] T075 Add section 10 to `SOLUTION.md` — AI-assisted development: what AI produced, the challenges it hit, the manual corrections made, and the validation evidence, naming the TDD commit sequence and every item on the documented-exemptions table in [plan.md](./plan.md)
- [ ] T076 Run the manual validation checklist in [quickstart.md](./quickstart.md) §5, including the cleanliness checks for leftover demo text and `[Bracketed.Resource.Key]` markers, and confirm via `git status` that `README.md` is unmodified
- [ ] T077 Confirm every item in the definition of done in [quickstart.md](./quickstart.md) §6, including the TDD evidence check and that all ten `SOLUTION.md` sections are present

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies. T002 and T003 gate everything — ViewModels are untestable until they land.
- **Foundational (Phase 2)**: Depends on Setup. **Blocks all user stories.**
- **User Story 1 (Phase 3)**: Depends on Phase 2.
- **User Story 2 (Phase 4)**: Depends on Phase 3 — the selection command lives in `FilmsViewModel`, and there is nothing to select until US1 exists.
- **User Story 3 (Phase 5)**: Depends on Phase 4 — the character section lives on the detail page.
- **User Story 4 (Phase 6)**: Depends on Phases 3–5 for the surfaces it hardens; its mechanism came from Phase 2.
- **Polish (Phase 7)**: Depends on all stories.

### Within Each Cycle

RED before GREEN, always. A RED task is not complete until the new test fails **on behaviour**. If the suite fails to compile, the red is not yet meaningful — stub the signature first.

### Story Dependencies — an honest note

The template's ideal is fully independent stories. These are **not** independent, and forcing them to be would misrepresent the work: US2 selects from the list US1 renders, US3 renders inside the page US2 builds, and US4 hardens all three. The chain US1 → US2 → US3 is genuine. What each story does preserve is an independently *demonstrable* increment — every checkpoint is something you can run and show.

### Parallel Opportunities

- **Phase 1**: T004, T005, T006, T007 touch four independent files.
- **Phase 2, cycle 2**: T011–T015 are five separate model files, all parallel.
- **Phase 6**: T061 and T062 touch different XAML files.
- **Phase 7**: T063 and T064 touch different projects.
- **`Resources.resw` tasks (T037, T048, T054, T060) are deliberately not marked `[P]`** — they all edit the same file and would conflict.
- **A cycle's RED and GREEN are never parallel.** That is the whole point.

---

## Parallel Example: Phase 2, Cycle 2

```bash
# Five independent model files — safe to run together:
Task: "T011 Create FilmDto in DrawboardCodingExercise.Services/StarWars/Dtos/FilmDto.cs"
Task: "T012 Create PersonDto in DrawboardCodingExercise.Services/StarWars/Dtos/PersonDto.cs"
Task: "T013 Create FilmListItem in DrawboardCodingExercise.ViewModel/Models/FilmListItem.cs"
Task: "T014 Create FilmDetailsDisplay in DrawboardCodingExercise.ViewModel/Models/FilmDetailsDisplay.cs"
Task: "T015 Create CharacterListItem in DrawboardCodingExercise.ViewModel/Models/CharacterListItem.cs"

# Then strictly in order — never together:
Task: "T016 RED — stub FilmMapper and write failing tests T26–T29"
Task: "T017 GREEN — implement mapping rules M1–M6"
```

---

## Implementation Strategy

### MVP First (User Story 1)

1. Phase 1: Setup — 8 tasks.
2. Phase 2: Foundational — 24 tasks. The bulk of the work, all of it test-driven.
3. Phase 3: User Story 1 — 9 tasks.
4. **STOP and VALIDATE**: the app launches on a correctly ordered, populated film list.

At that point the starter samples still exist but are unreachable from the startup path; retiring them is Phase 7.

### Incremental Delivery

1. Setup + Foundational → every layer tested, nothing user-visible yet.
2. + US1 → **MVP**: a browsable film catalogue.
3. + US2 → the exercise's core two-page requirement is met.
4. + US3 → the related-category requirement, plus the bonus opening crawl.
5. + US4 → failure recovery visible on every surface.
6. + Polish → samples retired, documentation written, validation gate run.

### Where the risk actually is

- **Phase 2, cycle 2** is the highest-value work to get right. T29 (id from `url`, never `episode_id`) and T26–T28 (mapping against a *real* payload) are the two tests standing between a working app and one that plausibly opens the wrong film or shows "Episode 0" for every entry.
- **T039 and T050** are the easiest tasks to forget and the hardest to diagnose. A missing `<Page>` entry still compiles and fails at runtime.
- **T032** — if `StarWarsService` is never registered, every page fails at container resolution, and no test will catch it. It sits on the documented-exemptions list precisely because it has no automated safety net.

---

## Notes

- **77 tasks**: 8 setup, 24 foundational, 9 (US1), 10 (US2), 4 (US3), 7 (US4), 15 polish.
- `README.md` is never edited. All documentation is `SOLUTION.md`, written across T070–T075 against the ten-section contract in [plan.md](./plan.md).
- Every behaviour on Constitution XV's mandatory list is driven by a RED task before its GREEN.
- Commit after each RED and each GREEN — the pairing *is* the XIV evidence, and it cannot be reconstructed afterwards.
- Two numbering schemes are in play: **T1–T35** are the tests in [quickstart.md](./quickstart.md) §4; **T001–T074** are tasks here. A task references the tests it drives.
- Anything implemented without a test must appear on the documented-exemptions table in [plan.md](./plan.md).
