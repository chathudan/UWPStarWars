# Implementation Plan: Star Wars Films Browser

**Branch**: `001-swapi-films-browser` | **Date**: 2026-09-05 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/001-swapi-films-browser/spec.md`

## Summary

Add a two-page Star Wars browsing flow to the existing Drawboard UWP starter: a **Films** page listing all six films by title and episode number (sorted ascending by episode), and a **FilmDetails** page reached by selecting a film, showing its title, episode number, release date, director, producer, opening crawl and its character list.

The technical approach is deliberately additive. Every piece of starter framework — `Shell`, `NavigationService`, `EventAggregator`, `UserInteractionService`, `LocalizationService`, `APIClient`, the Autofac modules — is reused **unchanged**. New code lands as: two DTOs and one service in `DrawboardCodingExercise.Services`, two ViewModels plus display models and a shared base class in `DrawboardCodingExercise.ViewModel`, two XAML pages in the UWP app, and a unit-test suite that never touches the network. The starter's `Welcome` and `PageA` samples are retired.

Three decisions carry most of the design weight, and all three were verified against the live API and the real Newtonsoft behaviour rather than assumed:

1. **DTOs carry explicit `[JsonProperty]` attributes.** This makes SWAPI's `snake_case` fields bind correctly *without* touching the shared `JsonSerializerSettings` the starter registers — verified empirically (see [research.md](./research.md) R1).
2. **The 15-second request budget is enforced inside `StarWarsService`**, not by modifying `IAPIClient` or the shared static `HttpClient`, keeping the starter framework untouched (R3).
3. **A small `PageViewModelBase`** owns the busy/done event pairing and the retry/cancel loop, because `ShellViewModel` *throws* if a done-message doesn't exactly match a busy-message (R4). Centralising it turns a latent crash into a structural guarantee.

**Build order is test-first.** Constitution XV makes red-green-refactor mandatory for every behaviour this feature adds — service response handling, mapping, sorting, ViewModel states, navigation, retry/cancel, and busy/done cleanup. That does not change the design above; it changes the order in which it is written and adds an evidence obligation to the definition of done. The sequencing is set out in [§ Implementation Sequence](#implementation-sequence-test-first), and R9 records how "red" is made meaningful in a statically-typed language where a missing type fails at compile time rather than at assertion time.

## Technical Context

**Language/Version**: C# `latest` (LangVersion), constrained to the UWP .NET Core 2.1-era runtime. PolySharp 1.14.1 back-fills modern types (records, init-only) with public accessibility across all projects.

**Primary Dependencies**: Autofac 8.2.0 + AutofacSerilogIntegration 5.0.0 (DI), CommunityToolkit.Mvvm 8.4.0 (observable state/commands), Newtonsoft.Json 13.0.3 (serialization), Serilog 4.2.0 + Serilog.Sinks.Debug (diagnostics), System.Reactive 6.0.1, System.Threading.Tasks.Dataflow 9.0.1 (used by the starter's `EventAggregator`). **No new packages are required.**

**Storage**: N/A — no persistence, no cache. Data is retrieved per navigation.

**Testing**: xUnit 2.9.3, Shouldly 4.2.1, NSubstitute 5.3.0 in `DrawboardCodingExercise.Services.UnitTests` (net8.0). One project-reference addition is required (see Structure Decision).

**Target Platform**: UWP — `TargetPlatformVersion` 10.0.26100.0, `TargetPlatformMinVersion` 10.0.18362.0, platforms x64 and ARM64. `internetClient` capability is already declared in `Package.appxmanifest` (verified).

**Project Type**: Desktop application (UWP + MVVM), 6-project solution.

**Performance Goals**: Film list visible within 5s (SC-001); any failure surfaced within 20s (SC-005); character retrieval capped at 6 concurrent requests (FR-010).

**Constraints**: Shared projects must stay `netstandard2.0`-compatible. All HTTP through `IAPIClient`. No SWAPI-wrapping NuGet package. The UWP project is **old-style MSBuild** — every new `.cs` and `.xaml` file must be added to `DrawboardCodingExercise.csproj` by hand as `<Compile>` / `<Page>` items or it will silently not compile into the app.

**Scale/Scope**: 6 films, ≤34 characters per film, 2 new pages, ~14 new source files, ~10 modified files, 35 required tests across 15 test-first cycles.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design.*

| # | Principle | Gate | How this plan satisfies it |
|---|-----------|------|----------------------------|
| I | Extend the Provided Application | **PASS** | Zero framework files modified. New code slots into the existing four-project layout. `Welcome`/`PageA` retired per the clarified spec (AC-001) — samples, not architecture. |
| II | SOLID and Proportionate Design | **PASS** | One service, one VM base class, one mapper. No repository, no CQRS, no mediator, no new projects. Each addition is justified in [research.md](./research.md). |
| III | MVVM and UWP Conventions | **PASS** | Both VMs derive from `ObservableObject` via `PageViewModelBase`, are `partial`, use `[ObservableProperty]`/`[RelayCommand]`, load via `OnNavigatedToAsync`, and expose explicit `PageLoadState`. Code-behind is `InitializeComponent()` only. |
| IV | Dependency Injection | **PASS** | `StarWarsService` registered in `WebServicesModule`; both view/VM pairs in `NavigationModule` via the existing `RegisterView<TView,TViewModel>`. Constructor injection throughout; no `new` on dependencies. |
| V | Navigation | **PASS** | `PageKey.Films` and `PageKey.FilmDetails` added. Detail page receives an opaque film id string. Invalid/unknown ids produce a visible invalid-selection state. Back navigation is untouched shell behaviour. |
| VI | API Integration | **PASS** | `IStarWarsService`/`StarWarsService` sit above `IAPIClient`; no `HttpClient` anywhere outside the starter's `APIClient`; no SWAPI package. DTOs separate from display models. Non-success, network, malformed, empty and partial cases all handled (R2, R5). |
| VII | Error Handling and User Reporting | **PASS** | `IUserInteractionService` drives retry/cancel; cancel lands on an on-page error state with its own retry. Serilog logs every failure with operation context. No silent failure, no permanent spinner (R3). |
| VIII | Busy and Progress Events | **PASS** | `PageViewModelBase.RunBusyAsync` captures the message string **once** into a local and posts `NotifyBusyEvent`/`NotifyDoneEvent` from that same local, with done in `finally`. Byte-identical by construction (R4). |
| IX | Localization | **PASS** | All new user-facing strings added to `Resources.resw`; views use `x:Uid`, VMs use `ILocalizationService`. Page headers follow the `PageHeader.<key>.Text` convention. |
| X | Logging | **PASS** | Serilog `ILogger` injected into `StarWarsService` and both VMs. Contextual logs for each retrieval and failure; no response payloads logged. |
| XI | Automated Testing | **PASS** | xUnit + Shouldly + NSubstitute. Every FR-024 scenario has a named test (see [quickstart.md](./quickstart.md) §4). `IAPIClient`, `IStarWarsService`, `INavigationService`, `IUserInteractionService`, `IEventAggregator`, `ILocalizationService` are all substituted — no network. |
| XII | UWP Compatibility | **PASS** | No new packages. All new shared code is `netstandard2.0`. No API newer than the UWP runtime is used; concurrency uses `SemaphoreSlim` + `Task.WhenAll`, both available (R5). |
| XIII | README and Interview Readiness | **PASS** | New `SOLUTION.md` carries API choice, architecture decisions, assumptions, limitations, build/run/test, extensions and AI-assisted development notes. `README.md` is the assignment brief and stays untouched (AC-014). |
| XIV | Validation Gates | **PASS** | [quickstart.md](./quickstart.md) is the executable checklist for build, tests, both pages, success/failure paths, navigation and progress clearing — now including the TDD-evidence gate. |
| XV | Test-Driven Development | **PASS** | Every behaviour on XV's mandatory list is driven by a failing test first, sequenced in [§ Implementation Sequence](#implementation-sequence-test-first). XV's exempt categories — XAML, resw text, manifest, mechanical Autofac/`PageKey` registration — are exactly the items this plan already routes to manual validation in [quickstart.md](./quickstart.md) §5. Evidence is the commit sequence (R9). |

**Result: 15/15 gates pass. No violations, so the Complexity Tracking table is omitted.**

Two additions warrant a proportionality note, since Principle II forbids gratuitous abstraction:

- **`PageViewModelBase`** — justified defensively, not stylistically. `ShellViewModel.OnNotifyDone` calls `_thingsInProgress.RemoveAt(IndexOf(...))`; a mismatched or unpaired done-message throws `ArgumentOutOfRangeException` on the UI thread. Two ViewModels running three separate operations each would duplicate that hazard six times. One base class makes the pairing structural. See R4.
- **`FilmMapper`** — justified by FR-024, which requires DTO→display-model mapping to be tested independently of any ViewModel. A static mapper is the smallest thing that makes that test possible.

## Project Structure

### Documentation (this feature)

```text
specs/001-swapi-films-browser/
├── plan.md              # This file
├── spec.md              # Feature specification (with Clarifications)
├── research.md          # Phase 0 output — 8 decisions, incl. 2 empirically verified
├── data-model.md        # Phase 1 output — DTOs, display models, states
├── quickstart.md        # Phase 1 output — build/run/test + validation checklist
├── contracts/           # Phase 1 output
│   ├── IStarWarsService.md   # App-facing service contract
│   ├── swapi-endpoints.md    # External API contract (verified live)
│   └── navigation.md         # PageKey + navigation-parameter contract
├── checklists/
│   └── requirements.md  # Spec quality checklist
└── tasks.md             # Phase 2 output (/speckit-tasks — NOT created here)
```

### Source Code (repository root)

Existing projects only. **No new projects.** `+` = new file, `~` = modified, `-` = removed.

```text
DrawboardCodingExercise.Contracts/          # netstandard2.0 — shared abstractions
└── PageKey.cs                              ~ add Films, FilmDetails

DrawboardCodingExercise.Services/           # netstandard2.0 — API + infrastructure
└── StarWars/
    ├── IStarWarsService.cs                 + service abstraction above IAPIClient
    ├── StarWarsService.cs                  + retrieval, URL normalisation, budget, concurrency
    ├── SwapiResourcePath.cs                + absolute-URL → relative-path + id helpers
    └── Dtos/
        ├── FilmDto.cs                      + [JsonProperty]-annotated film record
        └── PersonDto.cs                    + [JsonProperty]-annotated character record

DrawboardCodingExercise.ViewModel/          # netstandard2.0 — UI state
├── DrawboardCodingExercise.ViewModel.csproj ~ add ProjectReference → Services
├── PageViewModelBase.cs                    + busy/done pairing + retry/cancel loop
├── PageLoadState.cs                        + Loading | Loaded | Empty | Error | InvalidSelection
├── FilmsViewModel.cs                       + list page VM
├── FilmDetailsViewModel.cs                 + detail page VM
├── ShellViewModel.cs                       ~ startup navigation Welcome → Films
├── Models/
│   ├── FilmListItem.cs                     + display model (title, episode, opaque id)
│   ├── FilmDetailsDisplay.cs               + display model (formatted date, placeholders)
│   ├── CharacterListItem.cs                + display model (name)
│   └── FilmMapper.cs                       + DTO → display mapping (independently tested)
├── WelcomeViewModel.cs                     - retired
└── PageAViewModel.cs                       - retired

DrawboardCodingExercise/                    # UWP app
├── DrawboardCodingExercise.csproj          ~ add/remove Compile + Page items (old-style!)
├── Configuration/ApplicationConfiguration.cs ~ ServerAddress → https://swapi.info/api
├── Module/NavigationModule.cs              ~ register Films + FilmDetails, drop samples
├── Module/WebServicesModule.cs             ~ register StarWarsService
├── Strings/en/Resources.resw               ~ add feature strings, drop sample strings
├── View/Films.xaml(.cs)                    + list page
├── View/FilmDetails.xaml(.cs)              + detail page
├── View/Welcome.xaml(.cs)                  - retired
└── View/PageA.xaml(.cs)                    - retired

DrawboardCodingExercise.Services.UnitTests/ # net8.0 — deterministic, offline
├── ...UnitTests.csproj                     ~ add ProjectReference → ViewModel
├── StarWars/StarWarsServiceTests.cs        + retrieval, budget, 404, partial failure
├── StarWars/SwapiResourcePathTests.cs      + URL normalisation + id extraction
├── Mapping/FilmMapperTests.cs              + DTO mapping incl. missing optional fields
├── ViewModels/FilmsViewModelTests.cs       + load, sort, empty, error, retry/cancel, progress
├── ViewModels/FilmDetailsViewModelTests.cs + load, invalid param, characters, progress
├── TestDoubles/                            + in-memory fakes and builders
└── XUnitTests.cs                           (kept — starter's framework smoke test)

DrawboardCodingExercise.Services.IntegrationTests/  # unchanged
README.md                                   # unchanged — assignment brief (AC-014)
SOLUTION.md                                 + the write-up deliverable
```

**Structure Decision**: Keep all six existing projects and add nothing. New code is placed by responsibility exactly as the constitution prescribes — API logic in `Services`, UI state in `ViewModel`, views and DI wiring in the UWP app, shared enums in `Contracts`.

Two structural changes are unavoidable and are called out because they are easy to miss:

1. **`DrawboardCodingExercise.ViewModel` gains a `ProjectReference` to `DrawboardCodingExercise.Services`.** The ViewModels depend on `IStarWarsService` and its DTOs, which live in `Services` per the implementation direction. Both projects are `netstandard2.0`, so this is safe. The alternative — hoisting the interface and DTOs into `Contracts` — would force a Newtonsoft.Json dependency into `Contracts`, which today has none. See R6.
2. **`DrawboardCodingExercise.Services.UnitTests` gains a `ProjectReference` to `DrawboardCodingExercise.ViewModel`.** It currently references only `Services`, so ViewModels are **not testable at all** as the solution stands. Since FR-024 mandates ViewModel tests, this reference is a prerequisite, not an option.

## Implementation Sequence (test-first)

Constitution XV requires red-green-refactor for every behaviour below. This section fixes the order so that `/speckit-tasks` emits test tasks *before* their implementation tasks, and so the commit history reads as TDD evidence.

### How "red" works here

In C#, a test naming a type that does not yet exist fails to **compile** — the whole suite goes red, which proves nothing about the behaviour under test. So each cycle uses a two-beat red (R9):

1. **Stub** — create the type/member with the real signature and a `throw new NotImplementedException()` body. Nothing else.
2. **Red** — write the focused test. It compiles, runs, and fails on the *behaviour* (`NotImplementedException` or a genuine assertion failure), not on a missing symbol.
3. **Green** — smallest change that passes.
4. **Refactor** — tidy with the suite green.

The stub step is not a TDD violation: it declares the contract the test is written against, and contains no behaviour. It is what makes the red meaningful rather than incidental.

### Order of cycles

Dependencies flow downward — each layer's tests are written against types the layer below has already made real.

| # | Cycle | Drives | Tests (see [quickstart.md](./quickstart.md) §4) |
|---|---|---|---|
| 0 | **Enable the suite** | Add `ProjectReference` UnitTests → ViewModel; add ViewModel → Services. *Prerequisite — ViewModels are untestable until this lands.* | — |
| 1 | URL normalisation | `SwapiResourcePath` | T30, T31 |
| 2 | DTO shape + mapping | `FilmDto`, `PersonDto`, `FilmMapper` | T26, T27, T28, **T29** |
| 3 | Film retrieval + failure modes | `StarWarsService.GetFilmsAsync` | T24, T25, **T32**, T35 |
| 4 | Request budget | budget enforcement in `StarWarsService` | T23 |
| 5 | Single film + not-found | `StarWarsService.GetFilmAsync` | **T34** |
| 6 | Character retrieval | `GetCharactersAsync`, `CharacterLoadResult` | T18, T19, T20, T21 |
| 7 | Progress pairing | `PageViewModelBase.RunBusyAsync` | T13, T14, T15, **T16** |
| 8 | Retry/cancel loop | `PageViewModelBase` retry helper | T11, T12 |
| 9 | Films page state | `FilmsViewModel` load/empty/error | T1, T3, T17 |
| 10 | Film sorting | `FilmsViewModel` ordering | **T2** |
| 11 | Selection navigation | `FilmsViewModel` select command | T4, T5 |
| 12 | Detail parameter validation | `FilmDetailsViewModel` guards | T7, T8, T9 |
| 13 | Detail load | `FilmDetailsViewModel` film state | T6, T10, T17 |
| 14 | Detail character states | `FilmDetailsViewModel` character state | T22, **T33** |

Cycles 1–8 need no UWP types at all and run entirely in the net8.0 test project. Only after cycle 14 is any XAML written.

### Coverage audit against XV's mandatory list

XV names nine behaviour categories as mandatory. Each maps to at least one cycle:

| XV mandatory behaviour | Cycles | Tests |
|---|---|---|
| API service response handling | 3, 5, 6 | T24, T25, T32, T34, T18–T21 |
| DTO-to-display mapping | 2 | T26–T29 |
| Film list sorting | 10 | T2 |
| ViewModel loading / loaded / empty / error states | 9, 13, 14 | T1, T3, T6, T22, T33 |
| Film selection navigation | 11 | T4, T5 |
| Detail navigation parameter handling | 12, 13 | T7, T8, T9, T10 |
| Retry/cancel API failure behaviour | 8 | T11, T12 |
| `NotifyBusyEvent` / `NotifyDoneEvent` cleanup | 7 | T13–T16 |
| Malformed, null, partial, empty responses | 3, 6 | **T32** (malformed), T25 (null/empty), T20 (partial), T3 (empty) |

This audit is what produced T32, T33 and T34. The pre-XV suite covered null, partial and empty responses but had **no test for a malformed body**, and no test at all for FR-011's requirement that the character section fails independently of the film's own fields. Both were invisible until the mandatory list was checked category by category rather than requirement by requirement.

### Documented exemptions (XV closing clause)

XV: *"No production behavior is complete unless it has a corresponding automated test **or a documented reason** why automated testing is not practical."* Everything not driven by a test is named here, so nothing is exempt by silence.

| Item | Reason |
|---|---|
| `Films.xaml`, `FilmDetails.xaml` + code-behind | Declarative layout; code-behind is `InitializeComponent()` only. Requires the UWP runtime. XV-exempt; [quickstart.md](./quickstart.md) §5. |
| `Resources.resw` entries | Resource text. XV-exempt. §5 checks for the `[Key.Not.Found]` marker. |
| `PageKey` members, `NavigationModule` / `WebServicesModule` registrations | Mechanical registration, explicitly XV-exempt. **This is the only part of the feature with no automated safety net** — §5 leads with checks that catch a page built but never registered. |
| `ApplicationConfiguration.ServerAddress` | A constant. Asserting it equals itself tests nothing. |
| `DrawboardCodingExercise.csproj` item entries | Build configuration. A missing `<Page>` still compiles and fails at runtime — §5 catches it. |
| `IProvidePageHeader.PageHeader` on both VMs | Trivial constant-returning property (XV applies to *non-trivial* behaviour). The resw key it names is validated in §5. |
| DTO auto-properties, display-model constructors | No behaviour. Exercised indirectly by T26–T29. |
| Serilog **message templates** | Level is asserted (T35); wording is not. Asserting prose couples tests to text that will be reworded, without testing behaviour. |
| `SOLUTION.md` | Documentation. |

Note what is deliberately **not** on this list: FR-020 logging. `Serilog.ILogger` substitutes cleanly with NSubstitute, so "not practical" would have been false — it gets T35 instead of an excuse.

### Evidence obligation (XIV)

XIV now requires TDD evidence for non-trivial behaviour. Evidence here is the **commit sequence**: each cycle lands as a commit containing its failing test, followed by a commit containing the implementation that turns it green. `SOLUTION.md` points at that history.

> ⚠️ **Nothing in this repository is committed yet** — `specs/` is untracked and the working tree holds only the starter plus these planning artifacts. Commit-based evidence only exists if committing starts *before* cycle 1. Retrofitting the history afterwards would not be evidence, and would be worse than honestly recording that TDD was followed without granular commits.

## Phase 0: Research

Complete — see [research.md](./research.md). Nine decisions recorded, two of them verified by executing code rather than by reasoning:

| ID | Decision | Basis |
|----|----------|-------|
| R1 | Explicit `[JsonProperty]` on DTOs; shared `JsonSerializerSettings` left alone | **Verified** by probe against Newtonsoft 13.0.3 |
| R2 | Bare-array film response; per-film endpoint; 404 for unknown id | **Verified** against the live API |
| R3 | 15s budget enforced in `StarWarsService`, not in `IAPIClient` | Constitution I + VI, spec assumption |
| R4 | `PageViewModelBase` owns busy/done pairing and retry/cancel | `ShellViewModel` crash hazard |
| R5 | `SemaphoreSlim(6)` + `Task.WhenAll` for bounded, order-preserving character loads | netstandard2.0 + FR-010 |
| R6 | `IStarWarsService` + DTOs in `Services`; ViewModel references it | Implementation direction, dependency hygiene |
| R7 | Film id passed as an opaque `string`; 404 ⇒ invalid selection, not retry | FR-006, FR-013 |
| R8 | Page state as an enum plus derived bools, reusing `BoolToVisibilityConverter` | No new converters needed |
| R9 | Two-beat red (stub → failing behaviour test) for TDD in a compiled language | Constitution XV |

## Phase 1: Design & Contracts

Complete. Artifacts:

- **[data-model.md](./data-model.md)** — `FilmDto`, `PersonDto`, the three display models, `PageLoadState` transitions, validation rules and mapping table.
- **[contracts/IStarWarsService.md](./contracts/IStarWarsService.md)** — the app-facing service contract: signatures, return semantics, and the exact failure each method raises.
- **[contracts/swapi-endpoints.md](./contracts/swapi-endpoints.md)** — the external API contract as verified live on 2026-09-05, including the id-vs-episode trap.
- **[contracts/navigation.md](./contracts/navigation.md)** — `PageKey` additions, the navigation-parameter contract, and the `OnNavigatedToAsync` must-not-throw rule.
- **[quickstart.md](./quickstart.md)** — how to build, run and test, plus the manual validation checklist that closes Constitution XIV.

### Post-Design Constitution Re-check

Re-evaluated after the design artifacts were written, and again after the constitution added Principle XV. **Still 15/15.** The design added no new projects, no new packages, and no new architectural patterns.

XV changed the plan's *sequencing* and its evidence obligation, not its design — every type in [data-model.md](./data-model.md) and [contracts/](./contracts/) remains as designed, and every behaviour on XV's mandatory list already had a required test in [quickstart.md](./quickstart.md) §4 before XV existed. What XV adds is that those tests must now be written first, and cycle 0 (the two project references) is promoted from a structural footnote to a hard prerequisite, since ViewModels cannot be tested at all until it lands.

Two risks surfaced during design and are mitigated rather than left open:

- **`OnNavigatedToAsync` must never throw.** `NavigationService.NavigateAsync` logs `Fatal` and rethrows anything that escapes, which would propagate out of the shell's navigation call. Both ViewModels therefore treat their entire load as a guarded operation and convert every failure into a page state. Recorded in [contracts/navigation.md](./contracts/navigation.md) and covered by a test.
- **Back navigation re-runs the load.** `NavigationService.BackAsync` resolves a *fresh* ViewModel and calls `OnNavigatedToAsync` again, so returning to the list re-retrieves it. This is starter behaviour, not a defect, and the spec was corrected during clarification to expect it. It means the list's loading/empty/error states must be correct on re-entry, not just first entry.

## Complexity Tracking

Not applicable — no constitution gate is violated, so no justification is owed.
