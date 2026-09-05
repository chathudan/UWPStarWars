# Quickstart: Build, Run, Test & Validate

**Feature**: Star Wars Films Browser | **Plan**: [plan.md](./plan.md)

This is the validation guide that closes Constitution XIV. It says how to build, run and test the solution, and gives the checklist that decides whether the feature is actually done. It deliberately contains no implementation code — that belongs in `tasks.md` and the source.

---

## 1. Prerequisites

| Requirement | Version |
|---|---|
| Visual Studio | 2026 (18.7+) |
| Workloads | Windows application development · .NET desktop development |
| Components | Universal Windows Platform tools · Windows 11 SDK 10.0.26100.0 |
| .NET SDK | For the test projects (net8.0). Verified with 10.0.400 |
| Platform | x64 or ARM64 — **`AnyCPU` is not a valid configuration** for the UWP project |

Network access is needed to run the app, but **not** to run the tests.

---

## 2. Build

Open `DrawboardCodingExercise.slnx` in Visual Studio, select **x64** (or ARM64) and Build Solution.

From the command line, the UWP app needs MSBuild rather than `dotnet build`:

```powershell
# UWP app — requires MSBuild from a Developer prompt
msbuild DrawboardCodingExercise\DrawboardCodingExercise.csproj /p:Configuration=Debug /p:Platform=x64 /restore

# Shared + test projects build with the .NET SDK
dotnet build DrawboardCodingExercise.Services\DrawboardCodingExercise.Services.csproj
dotnet build DrawboardCodingExercise.ViewModel\DrawboardCodingExercise.ViewModel.csproj
dotnet build DrawboardCodingExercise.Services.UnitTests\DrawboardCodingExercise.Services.UnitTests.csproj
```

> The UWP project is old-style MSBuild — `dotnet build` cannot build it, and new `.xaml`/`.cs` files must be registered in the `.csproj` by hand. See [contracts/navigation.md](./contracts/navigation.md) § *UWP project file*.

---

## 3. Run

<kbd>F5</kbd> with `DrawboardCodingExercise` as the startup project (already `DefaultStartup` in the `.slnx`).

Expected on launch: the shell opens directly on the **Films** page, a progress indication appears in the title bar, and six films render sorted **Episode I → VI**.

To exercise failure paths without a debugger, change `ApplicationConfiguration.ServerAddress` to an unreachable host (e.g. `https://swapi.invalid/api`) and relaunch. See §5.

---

## 4. Test

```powershell
dotnet test DrawboardCodingExercise.Services.UnitTests\DrawboardCodingExercise.Services.UnitTests.csproj
```

Every test is deterministic and offline (FR-025). No test waits on a real timeout — the request budget is injected so it can be set to milliseconds.

### Required coverage (FR-024) — and the order they get written

Under Constitution XV these tests are not a verification pass at the end; **each one is written before the code it covers**, in the cycle order set out in [plan.md](./plan.md) § *Implementation Sequence*. The "Cycle" column is the build order — work down it, not across the table.

So this table is read twice: top-to-bottom by cycle while building, and as the completeness criterion for the suite when finishing.

| # | Cycle | Scenario | Target | Requirement |
|---|---|---|---|---|
| T30 | 1 | Absolute character URL normalised against the configured base | `SwapiResourcePath` | AC-006 |
| T31 | 1 | URL outside the configured base is rejected, not passed through | `SwapiResourcePath` | AC-006 |
| T26 | 2 | DTO → display mapping across all fields | `FilmMapper` | FR-024 |
| T27 | 2 | Missing director/producer/crawl ⇒ placeholder, never blank | `FilmMapper` | FR-009, M2 |
| T28 | 2 | Unparseable/missing release date ⇒ placeholder, never throws | `FilmMapper` | FR-008, M3 |
| T29 | 2 | Film id extracted from `url`; **never** from `episode_id` | `FilmMapper` | V3, R2 trap |
| T24 | 3 | Non-success status propagates as recoverable | `StarWarsService` | FR-015 |
| T25 | 3 | Null/empty deserialized response ⇒ empty list, not a crash | `StarWarsService` | FR-015 |
| T32 | 3 | **Malformed / unparseable response body ⇒ recoverable failure, not an unhandled crash** | `StarWarsService` | FR-015, XV |
| T35 | 3 | A failed retrieval writes an `Error`-level diagnostic | `StarWarsService` | FR-020 |
| T23 | 4 | Request exceeding the budget ⇒ recoverable failure | `StarWarsService` | FR-015, V6 |
| T34 | 5 | `GetFilmAsync` returns null on 404; **other statuses still propagate** | `StarWarsService` | FR-013, V4 |
| T18 | 6 | Characters load in **film order**, not response order | `StarWarsService` | FR-010, V8 |
| T19 | 6 | No more than 6 character requests in flight at once | `StarWarsService` | FR-010, V7 |
| T20 | 6 | Partial character failure keeps successes, reports the count | `StarWarsService` | FR-012, V9 |
| T21 | 6 | Total character failure is retryable | `StarWarsService` | FR-012 |
| T13 | 7, 9, 13, 14 | Progress cleared on success — busy and done counts match | `PageViewModelBase` **and** both real VMs | FR-004, FR-019 |
| T14 | 7, 9, 13 | Progress cleared on failure **and** on cancel | `PageViewModelBase` **and** both real VMs | FR-019 |
| T15 | 7 | Progress cleared when the operation throws unexpectedly | `PageViewModelBase` (probe VM) | FR-019 |
| T16 | 7, 9, 13, 14 | Busy and done event strings are **byte-identical** | `PageViewModelBase` **and** both real VMs | AC-009 |
| T11 | 8 | Failure prompts retry/cancel; **Retry** succeeds on the second attempt | both VMs | FR-016, FR-017 |
| T12 | 8 | Failure prompts retry/cancel; **Cancel** ⇒ `Error` state | both VMs | FR-018 |
| T1 | 9 | Film list loads and exposes all films | `FilmsViewModel` | FR-001 |
| T3 | 9 | Empty response ⇒ `Empty`, not `Error` | `FilmsViewModel` | FR-005 |
| T17 | 9, 13 | `OnNavigatedToAsync` **never propagates** an exception | both VMs | navigation contract |
| T2 | 10 | Films re-ordered ascending by episode from an **unordered** response | `FilmsViewModel` | FR-003 |
| T4 | 11 | Selecting a film navigates to `FilmDetails` with **that film's id** | `FilmsViewModel` | FR-006 |
| T5 | 11 | Selecting a film with a null id does **not** navigate | `FilmsViewModel` | V3 |
| T7 | 12 | `null` parameter ⇒ `InvalidSelection`, **service never called** | `FilmDetailsViewModel` | FR-013, V2 |
| T8 | 12 | Non-string parameter ⇒ `InvalidSelection`, service never called | `FilmDetailsViewModel` | FR-013 |
| T9 | 12 | Whitespace parameter ⇒ `InvalidSelection`, service never called | `FilmDetailsViewModel` | FR-013 |
| T6 | 13 | Detail loads from an id and exposes all six required fields | `FilmDetailsViewModel` | FR-007 |
| T10 | 13 | 404 ⇒ `InvalidSelection` and **no retry prompt shown** | `FilmDetailsViewModel` | FR-013, V4 |
| T22 | 14 | Category with no references ⇒ `Empty` section state | `FilmDetailsViewModel` | FR-012 |
| T33 | 14 | **A section load failing leaves the film's own details `Loaded` and readable** | `FilmDetailsViewModel` | FR-011, FR-012 |
| T36 | 15 | Mapping populates **all five** reference-url lists, with every `RelatedCategory` key present even when an array is null | `FilmMapper` | FR-027, M5, M7, V15 |
| T37 | 16 | A loaded film exposes **exactly five** sections, in declaration order, each with its localized title and the count from the film's own response | `FilmDetailsViewModel` | FR-027 |
| T38 | 16 | On arrival **only Characters requests**; the other four are collapsed with **zero** service calls | `FilmDetailsViewModel` | FR-028 |
| T39 | 17 | First expansion loads exactly once; collapse + re-expand issues **no** further request | `RelatedCategorySection` | FR-028, V13 |
| T40 | 17 | A section that failed does **not** silently re-request on re-expansion — only its own Retry does | `RelatedCategorySection` | FR-028 |
| T41 | 18 | Each section's busy/done messages **name its own category**, so two concurrent sections never share a progress string | `FilmDetailsViewModel` | FR-029, V14, AC-009 |
| T42 | 18 | One section failing leaves the **other four** untouched in their own states | `FilmDetailsViewModel` | FR-012 |

> **T29 earns its place.** `films/1` is Episode 4 — id and episode number are unrelated. A mapper that derives one from the other opens the wrong film while looking entirely plausible on screen. See [contracts/swapi-endpoints.md](./contracts/swapi-endpoints.md) § *Identifier semantics*.

> **T26–T28 should assert against a captured real payload** (snake_case, as the API actually returns it), not a hand-written camel-case fixture. A fixture written in the app's own naming convention would pass while the real thing silently binds `episode_id` to `0`.

> **T32, T33 and T34 were added by the TDD re-plan.** XV names "malformed, null, partial, or empty API responses" as mandatory; the suite covered null, partial and empty but **not malformed** — T32 closes that. T33 covers the one FR that had no test at all: FR-011's requirement that the character section fails *independently*, leaving the film's own fields on screen. T34 makes cycle 5 concrete — the 404-to-null conversion is service behaviour and needs its own red, rather than being tested only through the ViewModel.

> **T13, T14 and T16 are asserted twice, deliberately.** Once against `PageViewModelBase` via a probe ViewModel (cycle 7), and again against each real ViewModel (cycles 9, 13, 14). The first proves the mechanism pairs events correctly; the second proves each ViewModel actually *uses* it. Without the second, a ViewModel that forgets to call `RunBusyAsync` passes the whole suite while leaving the shell's progress ring spinning — which is exactly the failure the base class exists to prevent.

> **T36–T42 came with the five-category increment.** T38 and T39 are the two that matter most: without them, "loads on first expansion" degrades silently into "loads everything on arrival" or "re-requests on every toggle", and both still *look* correct on screen — the user sees the right names either way, and only the request count differs. A behaviour whose failure mode is invisible is exactly the kind that needs a test rather than a manual check. T41 guards the crash described under V14 in [data-model.md](./data-model.md): five sections sharing one progress string would have the shell call `RemoveAt(-1)` on the UI thread.

> **T35 asserts the log level, not the message text.** FR-020 requires diagnostics, and `Serilog.ILogger` substitutes cleanly, so "not practical to test" would be untrue. But asserting message templates couples tests to prose that will be reworded. The test asserts that a failure produces an `Error`-level entry and nothing more.

---

## 5. Manual validation checklist

Automated tests cannot cover the UWP surface. Walk this before calling the feature done — it is Constitution XIV made concrete.

### Happy path

- [ ] App launches directly on the **Films** page — no `Welcome`, no `PageA`.
- [ ] Progress appears in the title bar while loading, and **clears** once loaded.
- [ ] **The populated list is on screen within 5 seconds of launch** on a working connection (SC-001). Time it from window activation to the first row rendering; if it regularly exceeds 5s, that is a finding, not a slow machine.
- [ ] Six films listed, each with title and episode number.
- [ ] Order is Episode I → VI (i.e. *The Phantom Menace* first, **not** *A New Hope*).
- [ ] Selecting a film opens the detail page for **that** film — check *Episode IV* opens *A New Hope*, not *The Phantom Menace*.
- [ ] Detail page shows title, episode number, release date, director, producer, opening crawl.
- [ ] Release date is human-readable, not `1977-05-25`.
- [ ] Opening crawl is fully readable and scrolls; it does not push other fields off-screen.
- [ ] **Five** category sections are listed — Characters, Planets, Starships, Vehicles, Species — each with its entry count in the header.
- [ ] The counts match the film: *A New Hope* shows 18 / 3 / 8 / 4 / 5.
- [ ] Characters is already expanded on arrival; it shows its own progress, then a populated list of names.
- [ ] The other four are collapsed on arrival and show **no** progress — nothing loads until you open one.
- [ ] Expanding Planets loads and lists planet names; the same for Starships, Vehicles and Species.
- [ ] Collapse and re-expand a loaded section: the names reappear **instantly**, with no progress indication (proof it did not re-request).
- [ ] Expand three sections in quick succession: each reports its own progress, and each clears its own — no crash, and no section left spinning after the others finish.
- [ ] Film details stay readable the whole time any section is loading.
- [ ] Back returns to the list; the list is populated and interactive again.
- [ ] Selecting a different film shows the new film — **no data from the previous film remains**.
- [ ] Repeat the list → detail → back cycle three times: progress still clears every time.

### Failure paths

Point `ServerAddress` at an unreachable host, or disconnect the network.

- [ ] Film list failure shows the retry/cancel dialog.
- [ ] **Retry** with the network restored loads the list normally.
- [ ] **Cancel** shows an on-page error state with its own retry, and progress clears.
- [ ] The on-page retry works.
- [ ] Nothing is stuck spinning after any of the above.
- [ ] Detail page failure behaves the same way.
- [ ] A section's failure leaves the film's own details on screen **and the other four sections usable**.
- [ ] A failed section's own retry reloads **only that section** — the film does not re-request and the other sections are undisturbed.
- [ ] Re-expanding a failed section does **not** silently retry it; only its retry button does.

### Edge cases

- [ ] Kill the network mid-load: a failure is reported within ~20 seconds, not ~100 (FR-015).
- [ ] Navigate back while a load is still running: progress clears, no crash.
- [ ] Tap two films rapidly: the last one selected is the one shown.
- [ ] Resize the window narrow: no clipped text, no horizontal scroll.
- [ ] Switch Windows to dark mode and relaunch: both pages remain readable.

### Cleanliness (FR-026, SC-010)

- [ ] No "Downloading more RAM", "Reticulating Splines" or "Pretending to do work" anywhere.
- [ ] No "You have navigated to Page A", no "Navigate to Page A" button.
- [ ] No `[Some.Resource.Key]` bracketed text — that is `LocalizationService`'s missing-key marker and means a resw entry is absent.
- [ ] Page headers read correctly on both pages.

### Diagnostics

- [ ] With the debugger attached, the Output window shows Serilog entries for navigation and each retrieval.
- [ ] Failures log with enough context to identify the operation.
- [ ] **No response bodies are logged** (Constitution X).

---

## 6. Definition of done

- [ ] Solution builds for x64 with no new warnings.
- [ ] All tests in §4 pass; `dotnet test` is green with the network disabled.
- [ ] **TDD evidence exists for every non-trivial behaviour** (Constitution XIV + XV) — see below.
- [ ] Every §5 box is ticked.
- [ ] `README.md` is **unmodified** — confirm with `git status`. It is the supplied Drawboard exercise brief, not project documentation (AC-014).
- [ ] `SOLUTION.md` contains all **ten** required sections — see the section check below.

### `SOLUTION.md` section check

All candidate-authored documentation lives here. The full contract is in [plan.md](./plan.md) § Documentation Deliverable.

- [ ] 1. Chosen API — Star Wars Movies API, why it was chosen, verified response shapes, the id-vs-episode trap
- [ ] 2. Architecture decisions — starter reuse, `IStarWarsService` above `IAPIClient`, DTOs vs display models, `PageViewModelBase`, rejected alternatives
- [ ] 3. Assumptions — including the five clarification answers and what each reversed
- [ ] 4. Limitations — the budget stops the caller but does not abort the request; no caching; names only, with no drill-down into a related record; the concurrency cap is per section, so five expanded sections can reach 30 in-flight requests; English only; registration has no automated safety net
- [ ] 5. How to build and run — VS 2026, UWP workload, Windows 11 SDK, x64/ARM64 only, MSBuild for the UWP project
- [ ] 6. How to run tests — `dotnet test`, deterministic and offline, injected budget
- [ ] 7. **Error handling approach** — the failure taxonomy and why a 404 is an invalid selection rather than a retryable error
- [ ] 8. **Progress / loading behaviour** — busy/done pairing, why a mismatch would throw, per-category progress messages and why five sections cannot share one, lazy loading on first expansion, clearing on every path
- [ ] 9. Future extension ideas — led by `CancellationToken` on `IAPIClient`
- [ ] 10. AI-assisted development — what AI produced, challenges, manual corrections, and validation evidence

> Sections 7 and 8 are the ones most likely to be skipped. Section 7 is required by Constitution XIII but was missing from spec AC-014's enumeration; section 8 is where a reviewer will probe hardest, since a mismatched event pair crashes the shell rather than merely looking untidy.

### TDD evidence check (Constitution XIV, XV)

- [ ] Every behaviour on XV's mandatory list — service response handling, mapping, sorting, ViewModel states, selection navigation, detail parameter handling, retry/cancel, busy/done cleanup, malformed/null/partial/empty responses — has a corresponding test in §4.
- [ ] The commit history shows each cycle as *failing test → implementation*, not implementation followed by tests.
- [ ] Anything implemented without a test is either on XV's exempt list (XAML, resw, manifest, mechanical Autofac/`PageKey` registration) or has a documented reason in `SOLUTION.md`.
- [ ] `SOLUTION.md` points at the commit history as the evidence and names anything exempted.

> **Start committing before cycle 1.** The repository is currently untracked — commit-order evidence cannot be reconstructed after the fact, and rewriting history to look test-first would be a worse answer than stating plainly that TDD was followed without granular commits.

### Known limitation to carry into `SOLUTION.md`

The 15-second budget stops the *caller* waiting; it does not abort the underlying HTTP request, which runs on to the platform default. This follows from `IAPIClient` exposing no `CancellationToken` and its `HttpClient` being static and shared — see [research.md](./research.md) R3. The user-visible contract (FR-015, SC-005) is met exactly. Adding cancellation support to `IAPIClient` is the first extension idea, and a deliberate discussion point rather than an oversight.
