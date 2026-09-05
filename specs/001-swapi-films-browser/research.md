# Phase 0 Research: Star Wars Films Browser

**Date**: 2026-09-05 | **Plan**: [plan.md](./plan.md) | **Spec**: [spec.md](./spec.md)

The Technical Context contained no `NEEDS CLARIFICATION` markers — the five clarification answers already resolved scope, ordering, timeout, navigation parameter and page disposition. What remained were *integration* questions about how those answers land on this particular starter codebase. Two of them were settled by running code rather than by reasoning, because both had a plausible-but-wrong answer that would have produced silently broken behaviour.

---

## R1 — JSON field binding for `snake_case` responses

**Decision**: Annotate every DTO property with an explicit `[JsonProperty("...")]`. Leave the shared `JsonSerializerSettings` registered in `WebServicesModule` exactly as the starter has it.

**Rationale**: SWAPI returns `episode_id`, `opening_crawl`, `release_date`. The starter registers a **single shared** `JsonSerializerSettings` using `CamelCasePropertyNamesContractResolver` ([WebServicesModule.cs:19](../../DrawboardCodingExercise/Module/WebServicesModule.cs#L19)), consumed by `APIClient` for both directions. Without attributes, `EpisodeId` resolves to `episodeId`, which does not match `episode_id` — the property binds to `0` with **no exception and no warning**. This is the single most likely way for this feature to look finished and be wrong.

The non-obvious risk was the opposite failure. `CamelCasePropertyNamesContractResolver` configures its naming strategy with `OverrideSpecifiedNames = true`, which is documented to override names given in `[JsonProperty]`. Taken at face value, that would mean the attributes are ignored and the fix doesn't work.

**This was tested rather than assumed.** A probe against Newtonsoft.Json 13.0.3 (the exact version the solution uses) deserialised a real SWAPI film payload under five resolver configurations:

| Resolver configuration | `episode_id` | `opening_crawl` | `release_date` |
|---|---|---|---|
| `CamelCasePropertyNamesContractResolver` *(what the starter registers)* | ✅ 4 | ✅ | ✅ |
| `DefaultContractResolver` | ✅ 4 | ✅ | ✅ |
| `DefaultContractResolver` + `CamelCaseNamingStrategy` | ✅ 4 | ✅ | ✅ |
| `DefaultContractResolver` + `SnakeCaseNamingStrategy` | ✅ 4 | ✅ | ✅ |
| `CamelCasePropertyNamesContractResolver`, `OverrideSpecifiedNames = false` | ✅ 4 | ✅ | ✅ |

All five bind correctly. The reason `OverrideSpecifiedNames = true` is harmless here: `CamelCaseNamingStrategy` only lower-cases leading characters — it does not strip underscores. Applying it to `"episode_id"` yields `"episode_id"` unchanged. The override is real but idempotent for these particular names.

The same probe confirmed a bare top-level array deserialises to `List<FilmDto>` under the shared settings.

**Alternatives considered**:
- *Swap the shared resolver for `SnakeCaseNamingStrategy`* — rejected. It works, but it mutates starter wiring shared by every `IAPIClient` caller to solve a problem that per-property attributes solve locally. Constitution I favours the additive option.
- *Rely on the camel-case resolver with no attributes* — rejected outright; this is the silent-`0` failure mode.
- *Hand-roll parsing from `JObject`* — rejected as disproportionate (Constitution II).

**Consequence for implementation**: every DTO property gets an attribute, including ones where the name happens to match. Relying on an incidental match is exactly the fragility this decision exists to remove.

---

## R2 — SWAPI response shapes

**Decision**: Treat `/films` as a bare JSON array, retrieve single films from `/films/{id}`, and treat a 404 as "no such film" rather than a transient failure.

**Rationale**: Verified against the live service on 2026-09-05 (details in [contracts/swapi-endpoints.md](./contracts/swapi-endpoints.md)):

- `GET /api/films` → HTTP 200, a bare array of 6 objects. **No `count`/`next`/`results` envelope**, unlike the original swapi.dev contract that most examples and most model training data describe. There is no pagination to implement.
- `GET /api/films/1` → HTTP 200, a single object (not an array).
- `GET /api/films/99` → HTTP 404, which `APIClient` converts to `HttpStatusException(NotFound)`.
- `characters` is an array of **absolute** URLs, e.g. `https://swapi.info/api/people/1`.

**The trap**: a film's id is its **release-order position, not its episode number**. `films/1` is Episode 4. Since the list sorts by episode and the detail page navigates by id, any code that derives one from the other opens the wrong film — and does so plausibly enough to pass a casual manual test.

**Alternatives considered**: *Defensively accept both the enveloped and bare shapes* — rejected. It is speculative generality against a contract just verified, and would need a custom converter. If the mirror ever changes shape, deserialisation fails loudly into the existing error path, which is the correct outcome.

---

## R3 — Enforcing the 15-second request budget

**Decision**: Enforce the budget inside `StarWarsService`, by racing each `IAPIClient` call against a delay and raising a timeout failure when the delay wins. Do not modify `IAPIClient`, `APIClient`, or its static `HttpClient`.

**Rationale**: `IAPIClient` exposes no `CancellationToken`, and `APIClient` holds a `static HttpClient` shared process-wide with the default ~100s timeout ([APIClient.cs:32](../../DrawboardCodingExercise.Services/APIClient.cs#L32)). FR-015 requires 15s. Three routes exist:

1. Set `Client.Timeout` in the static constructor — one line, and it genuinely aborts the request. But it edits starter framework code and changes behaviour for every caller, which the spec's own assumption forbids.
2. Add a `CancellationToken` overload to `IAPIClient` — the technically best answer, and it *does* abort the underlying request. But it widens a starter interface, which Constitution I discourages, and pulls the change well beyond this feature's footprint.
3. Race the call in the service layer — no starter file touched, fully unit-testable with a substituted `IAPIClient` that delays.

Option 3 is chosen as the proportionate fit for an exercise judged on extending rather than rewriting.

**Accepted trade-off, to be documented as a limitation**: the abandoned HTTP request keeps running in the background until the platform timeout; only the *caller* stops waiting. For six films and at most 34 character requests this is immaterial, and the user-visible contract (FR-015, SC-005) is met exactly. "Add `CancellationToken` support to `IAPIClient`" is recorded as the first extension idea in `SOLUTION.md`, and is a deliberate interview talking point rather than an oversight.

**Alternatives considered**: *No timeout, document the ~100s default as a limitation* — rejected; it violates FR-021 in practice, since a stalled connection would leave a spinner running for over a minute.

---

## R4 — Busy/done event pairing and the retry/cancel loop

**Decision**: Introduce one small `PageViewModelBase : ObservableObject` in the ViewModel project, owning two protected helpers: one that brackets work in paired busy/done events, and one that wraps work in the retry/cancel loop.

**Rationale**: This is a defensive decision, not a stylistic one. `ShellViewModel.OnNotifyDone` does:

```csharp
var index = _thingsInProgress.IndexOf(obj.Event);
_thingsInProgress.RemoveAt(index);
```

([ShellViewModel.cs:58-59](../../DrawboardCodingExercise.ViewModel/ShellViewModel.cs#L58-L59)) — with **no `-1` guard**. A `NotifyDoneEvent` whose string does not exactly match a live `NotifyBusyEvent` throws `ArgumentOutOfRangeException` on the UI thread. AC-009's "byte-identical strings" is therefore a hard correctness requirement whose violation crashes the app, not a tidiness rule.

Two ViewModels running three retrievable operations between them would otherwise repeat that hazard six times, each an independent chance to interpolate a film title into one message and not the other. The base class captures the message into a single local and posts both events from it, with the done event in `finally`:

```csharp
protected async Task RunBusyAsync(string message, Func<Task> work)
{
    _eventAggregator.Post(new NotifyBusyEvent(message));   // same local...
    try { await work(); }
    finally { _eventAggregator.Post(new NotifyDoneEvent(message)); }  // ...as here
}
```

Mismatch becomes structurally impossible rather than a convention someone must remember. The retry/cancel loop lives alongside it because it wraps the same operations and must not double-post progress events when it retries.

**Proportionality check** (Constitution II): one base class, two methods, roughly 60 lines, shared by exactly the two ViewModels that need it, in service of two explicit constitutional requirements (VII and VIII). This is not a framework.

**Alternatives considered**:
- *Duplicate try/finally in each ViewModel method* — rejected; six chances to get the string pairing wrong.
- *Fix the `-1` guard in `ShellViewModel`* — rejected as the primary fix. It is starter framework code, and defending against the mismatch is not the same as not producing one. (It would be a reasonable belt-and-braces addition, but the pairing guarantee is what actually matters.)
- *An injected `IProgressScope` service* — rejected as over-abstraction for two call sites.

---

## R5 — Bounded, order-preserving character retrieval

**Decision**: `SemaphoreSlim(6)` gating a `Task.WhenAll` over the film's character URLs.

**Rationale**: FR-010 requires at most ~6 in-flight requests, results revealed as a complete set, ordered as the film lists them. `Task.WhenAll` returns results positionally, so input order is preserved for free — no post-sort, and no reliance on completion order. `SemaphoreSlim` is `netstandard2.0`-safe and needs no new package.

Partial failure is handled per-item: each character task catches its own failure and yields a null slot, so one bad record cannot fail the batch (FR-012). The caller reports how many succeeded and how many were lost. If *every* request fails, the service surfaces that as a recoverable failure eligible for retry/cancel.

**Alternatives considered**:
- *`ActionBlock` with `MaxDegreeOfParallelism = 6`* — genuinely viable; `System.Threading.Tasks.Dataflow` is already referenced by `Services` for the `EventAggregator`. Rejected because collecting ordered results out of a dataflow block needs more scaffolding than the semaphore, for no benefit at this scale.
- *Unbounded `Task.WhenAll`* — rejected; ~34 simultaneous requests at a free community mirror invites throttling.
- *Sequential* — rejected; ~34 round trips in series would breach the perceived-performance intent behind SC-004.

---

## R6 — Where `IStarWarsService` and the DTOs live

**Decision**: Both in `DrawboardCodingExercise.Services` (namespace `...Services.StarWars`). Add a `ProjectReference` from `DrawboardCodingExercise.ViewModel` to `DrawboardCodingExercise.Services`.

**Rationale**: Matches the stated implementation direction ("add `IStarWarsService` and `StarWarsService` above `IAPIClient`") and keeps the service beside the `IAPIClient` it wraps. Both projects are `netstandard2.0`, so the reference is safe, and the UWP app already references both.

**Alternatives considered**:
- *Interface + DTOs in `Contracts`* — cleaner on paper, since both projects already reference `Contracts`. Rejected because the DTOs carry `[JsonProperty]` attributes, which would force a Newtonsoft.Json dependency into `Contracts`, a project that currently depends only on Serilog and PolySharp. Trading a normal project reference for a new package dependency in the most shared project is a poor bargain.
- *Interface in `Contracts` returning domain models, DTOs private to `Services`* — the most layered option, and rejected on Constitution II. It adds a third model shape (DTO → domain → display) to a six-film app.

**Consequence**: ViewModels see the DTO types. AC-007 is still satisfied because ViewModels never *bind* to DTOs — `FilmMapper` converts them to display models at the boundary, and the XAML binds only to display models.

---

## R7 — Navigation parameter and not-found semantics

**Decision**: Pass the film's id as an opaque `string`. Validate it before any request. Map a 404 to the invalid-selection state, never to the retry/cancel path.

**Rationale**: Clarification 3 chose an identifier over the film record, so the parameter survives suspend/resume. Treating it as opaque is what protects against the R2 trap: nothing may reconstruct an id from an episode number.

The not-found distinction matters for user experience. Offering "Retry" for an id the service does not recognise invites the user to retry something that cannot ever succeed. So `StarWarsService.GetFilmAsync` catches `HttpStatusException` with `NotFound` and returns `null`, which the ViewModel renders as invalid-selection. Every other status keeps throwing and stays retryable.

Validation ordering is deliberate: a null, non-string, empty or whitespace parameter produces the invalid-selection state **without issuing a request** (FR-013), so a broken navigation call never generates network traffic.

**Alternatives considered**: *A dedicated `FilmNotFoundException`* — marginally more explicit than `null`, rejected as an extra type for a single call site. *Returning a result object* — rejected as disproportionate.

---

## R8 — Representing page state in the ViewModels

**Decision**: A `PageLoadState` enum (`Loading`, `Loaded`, `Empty`, `Error`, `InvalidSelection`) as the single source of truth, with derived boolean properties for XAML to bind through the existing `BoolToVisibilityConverter`.

**Rationale**: Constitution III wants explicit states. A single enum makes them genuinely mutually exclusive — impossible to be both `Empty` and `Error`, which independent booleans would allow. Tests assert one enum value instead of four flags.

The starter's `BoolToVisibilityConverter` already handles bool→`Visibility` and is registered in `Shell.xaml`, so exposing `IsLoading`, `IsEmpty`, `HasError`, `IsInvalidSelection`, `HasContent` as computed properties means **no new value converter is needed**. `[NotifyPropertyChangedFor]` on the state property keeps them in sync with one assignment.

The detail page carries **two** independent states — one for the film, one for the character section — because FR-011/FR-012 require the character section to load and fail independently of the film's own fields.

**Alternatives considered**: *An enum-to-visibility converter with a parameter* — a new converter and stringly-typed XAML parameters, for no gain. *Independent booleans only* — rejected; permits contradictory states.

---

## R9 — Making "red" meaningful in a statically-typed codebase

**Decision**: Each TDD cycle uses a two-beat red — **stub the signature, then write the failing behaviour test** — rather than writing a test against a type that does not exist.

**Rationale**: Constitution XV mandates red-green-refactor. In C# this collides with compilation. A test referencing a not-yet-written `FilmMapper` does not fail; it *fails to build*, taking every other test in the project down with it. That red is indiscriminate: it says nothing about the behaviour under test, it cannot be distinguished from a genuine regression elsewhere, and it makes it impossible to run the rest of the suite while the cycle is open.

So each cycle runs:

1. **Stub** — declare the type/member with its real signature and a `throw new NotImplementedException()` body. No logic, no branches.
2. **Red** — write the focused test. It compiles, the suite runs, and *this* test fails on behaviour.
3. **Green** — smallest change that passes.
4. **Refactor** — with the suite green.

The stub is not an end-run around test-first. It contains no behaviour, so nothing it does can make a test pass; it exists only to let the compiler express the contract the test is about to assert against. Every behavioural decision still originates in a test. This is the standard reading of TDD in compiled languages, and it is what makes the red diagnostic rather than incidental.

**Consequence for evidence**: because the stub and the test can land together, a cycle's evidence is the pair *(commit containing a failing test, commit containing the implementation that greens it)*. That ordering is only visible if commits begin before the first cycle — see the warning in [plan.md](./plan.md) § *Evidence obligation*.

**Alternatives considered**:
- *Write tests against non-existent types and accept the build break* — rejected. It blocks the whole suite for the duration of every cycle and produces a red that cannot distinguish "not written yet" from "broken".
- *Write the implementation and tests together, then reorder commits* — rejected outright. It manufactures the appearance of TDD rather than the discipline, and XIV asks for evidence, which a rewritten history is not.
- *Exempt everything hard to test and rely on manual validation* — rejected. XV lists mapping, sorting, states, navigation, retry/cancel and progress cleanup as mandatory, and every one of them is plain `netstandard2.0` code with substitutable dependencies. Nothing on that list needs the UWP runtime.

**Scope note**: XV's carve-out (XAML, resw text, manifest, mechanical Autofac/`PageKey` registration) maps exactly onto the parts of this feature that have no branching behaviour. That alignment is convenient but worth stating plainly: it means the DI and page registration wiring is the **only** part of the feature with no automated safety net, which is why the manual checklist leads with checks that would catch a page built but never registered.

---

## R10 — One record shape and one retrieval path for all five related categories

**Decision**: Model the five categories as a display-layer enum over a single `NamedResourceDto` and a single `GetRelatedResourcesAsync`, and load each section on first expansion rather than on page open.

**Rationale**: three separate findings, each verified rather than assumed.

*1. All five categories have the same displayable shape.* `people/1`, `planets/1`, `starships/2`, `vehicles/4` and `species/1` were each fetched live on 2026-09-05 and all five returned a `name` field ("Luke Skywalker", "Tatooine", "CR90 corvette", "Sand Crawler", "Human"). Since this feature displays nothing but the name, the categories are indistinguishable below the section header. Five DTOs and five service methods would differ only in their type names — the definition of the duplication Principle II forbids. The category distinction is real, but it is a *display* fact, so it lives in a `RelatedCategory` enum in the ViewModel layer and never crosses into `IStarWarsService`.

*2. Eager loading is a real cost, not a hypothetical one.* `films/1` references 18 characters, 3 planets, 8 starships, 4 vehicles and 5 species — **38 records** for one film. Loading all five sections on arrival would issue 38 requests against a free community-run mirror every time a user opens any film, the overwhelming majority for sections they never scroll to. Loading on first expansion keeps the page's opening cost identical to the shipped Characters-only behaviour and makes each further category one interaction away.

*3. A collapsed section must not re-request.* Once expanded and loaded, collapse/re-expand shows the retained items. A *failed* section is retried only through its own retry button, never by being re-expanded — otherwise a user idly toggling a broken section would repeatedly hammer a failing endpoint, and the retry/cancel prompt would reappear on a gesture the user did not intend as a retry.

**Alternatives considered**:

- *A DTO per category (`PlanetDto`, `StarshipDto`, …)* — rejected. Four extra types with identical members, four extra service methods, and four more places to update when the record shape changes. It would only earn its keep if a category displayed a field the others lack, which none currently do. The reversal is cheap if that changes: one type splits, and the retrieval path is untouched because it never referenced the type's identity, only its `name`.
- *Passing `RelatedCategory` into the service* — rejected. The service would gain a `switch` mapping enum to path prefix, but it already receives absolute URLs from the film's own response, so it does not need to know the path — the film already told it. The enum would be a parameter the method never reads.
- *Loading all five sections eagerly on page open* — rejected on the 38-request measurement above. It would also make the film's own details compete for the connection with five sections the user has not asked for.
- *Loading eagerly but rendering progressively* — rejected. Same request cost, and it contradicts the clarified reveal-when-complete behaviour (FR-010) that the shipped Characters section already follows.
- *A single shared concurrency semaphore across all five sections* — rejected. It caps the in-flight burst at 6 globally, but serialises sections behind each other: expand Planets while Characters is loading and Planets appears frozen until Characters finishes. The per-call cap keeps each section responsive; the burst it permits (30, only if the user deliberately expands all five at once) is the better trade.

**Consequence**: the five-category scope costs one enum, one section class and one collection. Notably it is *less* code than the Characters-only version would have been if the other four had been added later by copying it — which is the honest reason to generalise now rather than defer.

---

## Cross-cutting note: what the ViewModels must never do

Not a decision so much as a constraint discovered while reading the starter, recorded here because it shapes every load path:

`NavigationService.NavigateAsync` awaits `OnNavigatedToAsync` inside a `try` that logs `Fatal` and **rethrows** ([NavigationService.cs:92-97](../../DrawboardCodingExercise/CoreFramework/NavigationService.cs#L92-L97)). Any exception escaping a ViewModel's load propagates out of the shell's navigation call. Both ViewModels therefore guard their entire `OnNavigatedToAsync` body and convert every failure into a `PageLoadState`. This is asserted by a test, not left to discipline.

Relatedly, `BackAsync` resolves a **fresh** ViewModel (registrations are transient) and calls `OnNavigatedToAsync` again — so returning from the detail page genuinely re-loads the film list. This is starter behaviour and the spec was corrected during clarification to expect it; the practical consequence is that loading, empty and error states must behave correctly on re-entry, not only on first navigation.
