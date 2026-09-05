# Phase 1 Data Model: Star Wars Films Browser

**Date**: 2026-09-05 | **Plan**: [plan.md](./plan.md) | **Research**: [research.md](./research.md)

Three layers, deliberately kept apart per Constitution VI and AC-007:

```
SWAPI JSON  ──►  DTOs (Services.StarWars.Dtos)  ──►  Display models (ViewModel.Models)  ──►  XAML
                 wire shape, [JsonProperty]          formatted, placeholder-filled          binds only here
                                              FilmMapper
```

XAML **never** binds to a DTO. The mapper is the only crossing point, which is what makes FR-024's mapping tests possible without instantiating a ViewModel.

---

## 1. API DTOs — `DrawboardCodingExercise.Services.StarWars.Dtos`

Wire-shaped, no behaviour, no formatting. Every property carries an explicit `[JsonProperty]`, including where the name already matches — see [research.md](./research.md) R1 for why incidental matches are not relied on.

### `FilmDto`

| Property | Type | JSON name | Notes |
|---|---|---|---|
| `Title` | `string` | `title` | Required for display. |
| `EpisodeId` | `int` | `episode_id` | **Not** the film's identifier. Sort key. |
| `OpeningCrawl` | `string` | `opening_crawl` | May be long; contains `\r\n`. Optional in practice. |
| `Director` | `string` | `director` | Optional in practice. |
| `Producer` | `string` | `producer` | Optional in practice. Comma-separated when multiple. |
| `ReleaseDate` | `string` | `release_date` | ISO `yyyy-MM-dd`. Kept as `string` — see note below. |
| `Characters` | `string[]` | `characters` | **Absolute** URLs. May be null or empty. |
| `Planets` | `string[]` | `planets` | **Absolute** URLs. May be null or empty. |
| `Starships` | `string[]` | `starships` | **Absolute** URLs. May be null or empty. |
| `Vehicles` | `string[]` | `vehicles` | **Absolute** URLs. May be null or empty. |
| `Species` | `string[]` | `species` | **Absolute** URLs. May be null or empty. |
| `Url` | `string` | `url` | Absolute self-link. The **only** source of the film's identifier. |

The five reference arrays are modelled identically because they *are* identical — five arrays of absolute URLs to records that all publish a `name`. `created` and `edited` remain unmodelled: Newtonsoft ignores unmapped JSON by default, and modelling them would imply support this feature does not provide.

> **`ReleaseDate` is `string`, not `DateTime`.** Deserialising straight to `DateTime` would let a malformed date throw inside `JsonConvert`, surfacing as an opaque serialization failure for the whole film. Parsing in the mapper keeps the failure local to one field, which is what FR-009 requires: a bad or missing date degrades to a placeholder while the rest of the film still renders.

### `NamedResourceDto`

One record type for all five related categories.

| Property | Type | JSON name | Notes |
|---|---|---|---|
| `Name` | `string` | `name` | The only field this feature displays. |
| `Url` | `string` | `url` | Self-link; useful for stable keys and logging. |

Only two fields are modelled because only `Name` is displayed (FR-010). Adding the rest would be speculative.

> **Why one type and not five.** `people`, `planets`, `starships`, `vehicles` and `species` records were each fetched live on 2026-09-05 and all five publish `name`. Since this feature displays nothing but the name, a `PlanetDto`/`StarshipDto`/`VehicleDto`/`SpeciesDto` set would be four copies of the same two properties, differing in nothing but their type name, and would force four more identical retrieval methods above them. Principle II forbids exactly that. The moment a category needs a field the others do not have — a planet's climate, a starship's class — this decision reverses, and the reversal is cheap because it is one type splitting, not a retrieval path unwinding.
>
> This type replaces the earlier `PersonDto`, which was correct only while Characters was the sole category.

---

## 2. Display models — `DrawboardCodingExercise.ViewModel.Models`

Immutable, already formatted, safe to bind. Everything a view needs is a plain property — no converters beyond the starter's `BoolToVisibilityConverter`.

### `FilmListItem` — one row on the Films page

| Property | Type | Derivation |
|---|---|---|
| `Id` | `string` | Extracted from `FilmDto.Url`. **Opaque** — never derived from `EpisodeId`. |
| `Title` | `string` | `FilmDto.Title`, placeholder if blank. |
| `EpisodeNumber` | `int` | `FilmDto.EpisodeId`. The sort key. |
| `EpisodeLabel` | `string` | Localized, e.g. `"Episode IV"`. Display only. |

### `FilmDetailsDisplay` — the Films detail page

| Property | Type | Derivation |
|---|---|---|
| `Id` | `string` | From `FilmDto.Url`. |
| `Title` | `string` | Placeholder if blank. |
| `EpisodeNumber` | `int` | `FilmDto.EpisodeId`. |
| `EpisodeLabel` | `string` | Localized. |
| `ReleaseDateDisplay` | `string` | Parsed from ISO, formatted for reading (FR-008); placeholder if missing or unparseable. |
| `Director` | `string` | Placeholder if null/blank (FR-009). |
| `Producer` | `string` | Placeholder if null/blank (FR-009). |
| `OpeningCrawl` | `string` | Placeholder if null/blank (FR-009). Line breaks preserved. |
| `RelatedUrls` | `IReadOnlyDictionary<RelatedCategory, IReadOnlyList<string>>` | From the film's five reference arrays; **always contains all five keys**, each mapped to an empty list if the array was null or absent. Drives each section's load. |

`RelatedUrls` always holding five keys is deliberate: a section reading its own urls never has to ask whether its key exists, so "this film has no vehicles" and "this film's vehicles are missing from the model" cannot be confused. It replaces the earlier `CharacterUrls`.

### `RelatedCategory` — the five fixed categories

```csharp
public enum RelatedCategory { Characters, Planets, Starships, Vehicles, Species }
```

An enum rather than a string, so a typo is a compile error and the five sections cannot silently become four. Declaration order **is** display order.

### `RelatedResourceListItem` — one row inside any category section

| Property | Type | Derivation |
|---|---|---|
| `Name` | `string` | `NamedResourceDto.Name`, placeholder if blank. |
| `Url` | `string` | `NamedResourceDto.Url`; stable list key. |

Replaces `CharacterListItem`. One row type, because a planet row and a character row differ in nothing this feature displays.

### `RelatedCategorySection` — one expandable section (ViewModel-layer, observable)

Unlike the models above this one is **mutable and observable**, because it owns a live state machine rather than a formatted snapshot. It lives beside the ViewModels, not with the immutable display models.

| Member | Type | Purpose |
|---|---|---|
| `Category` | `RelatedCategory` | Which of the five. |
| `Title` | `string` | Localized category name, resolved once at construction. |
| `Count` | `int` | Entries the film references. Known from the film's own response, so it is shown **before** the section is ever loaded. |
| `Items` | `ObservableCollection<RelatedResourceListItem>` | Populated on a successful load. |
| `State` | `PageLoadState` | This section's own state machine, independent of the film's and of the other four. |
| `IsExpanded` | `bool` | Two-way bound to the header toggle. `true` for Characters at construction, `false` for the rest. |
| `HasPartialFailure` | `bool` | Some entries resolved, some did not (FR-012). |
| `HasBeenLoaded` | `bool` | Guards FR-028: a successful load happens once, re-expansion does not re-request. |
| `ToggleCommand` / `RetryCommand` | commands | Expand-collapse, and the section-local retry. |

> **Why a section object rather than five sets of properties on the ViewModel.** Characters alone needed a collection, a state, a partial-failure flag, four derived visibility booleans, a retry command and four localized strings. Repeating that five times is roughly fifty members on one ViewModel, five copies of the same load method, and five chances to update four of five when the behaviour changes. One section type collapses that to one implementation and one collection of five instances — and it is what lets the page render the sections with a single `ItemsControl` template rather than five hand-maintained blocks of XAML.

---

## 3. Mapping rules — `FilmMapper`

Pure and static, so it is testable with no container, no substitutes and no async. Directly covered by FR-024's mapping requirement.

| Rule | Behaviour |
|---|---|
| **M1 — Identifier extraction** | Take the last non-empty segment of `FilmDto.Url` (`https://swapi.info/api/films/1` → `"1"`). Null/blank/unparseable URL ⇒ `Id` is null, and the item is excluded from the list rather than rendered unopenable. |
| **M2 — Missing text** | Null, empty or whitespace `Title`, `Director`, `Producer`, `OpeningCrawl` ⇒ the localized "not available" placeholder. Never an empty string, never `null` reaching XAML. |
| **M3 — Release date** | Parse `yyyy-MM-dd` invariantly; format for reading on success. Null, blank or unparseable ⇒ placeholder. **Never throws.** |
| **M4 — Episode label** | `EpisodeId` → Roman numeral for display. The underlying `int` is retained separately for sorting — the label is never sorted on. |
| **M5 — Null reference array** | A null or absent reference array ⇒ an empty list for that category, so its section shows the "nothing listed" empty state rather than dereferencing null. Applies to all five arrays identically. |
| **M6 — Ordering** | The mapper does **not** sort. Sorting by `EpisodeNumber` ascending is the ViewModel's job (FR-003), keeping the mapper a pure per-item transform. Within a category, film order is preserved and never re-ordered. |
| **M7 — All five keys present** | `RelatedUrls` is built with an entry for **every** `RelatedCategory` value, empty list included. A section never has to handle a missing key, and adding a sixth category to the enum without mapping it becomes a test failure rather than a runtime `KeyNotFoundException`. |

---

## 4. Page state

### `PageLoadState` enum

| Value | Meaning | Reached when |
|---|---|---|
| `Loading` | Retrieval in flight | Entry to `OnNavigatedToAsync`, and on each retry |
| `Loaded` | Content available | Retrieval returned ≥1 item |
| `Empty` | Retrieval succeeded, nothing to show | Source returned zero films / a category has zero references |
| `Error` | Recoverable failure, cancelled by the user | Failure occurred and the user chose Cancel |
| `InvalidSelection` | Cannot be opened, retry is pointless | Missing/blank/wrong-typed id, or a 404 |

Exactly one value at a time. `Error` and `InvalidSelection` are distinct because only `Error` offers a retry — retrying an unknown id can never succeed (FR-013).

### Transitions — Films page

```
        ┌──────────────────────────── retry ◄───────────────┐
        ▼                                                   │
 [entry] ──► Loading ──► films.Count > 0 ──► Loaded         │
                   │                                        │
                   ├──► films.Count == 0 ──► Empty          │
                   │                                        │
                   └──► failure ──► retry/cancel prompt ────┤
                                          │                 │
                                          └── Cancel ──► Error ──► on-page Retry ──┘
```

### Transitions — FilmDetails page

**Six** independent state machines on one page — the film's, plus one per category section (FR-011, FR-012):

```
FILM STATE                                    SECTION STATE  ×5, each entirely its own
[entry] ──► validate id                       (none run until the film is Loaded)
    │
    ├─ invalid ──► InvalidSelection           Collapsed, never requested
    │              (no request issued)             │
    └─ valid ──► Loading                           │ first expansion (Characters: on arrival)
                     │                             ▼
                     ├──► 404 ──► InvalidSelection  0 urls ──► Empty (no request issued)
                     ├──► failure ──► retry/cancel ─┤
                     │               └─ Cancel ──► Error
                     │                             ├──► Loading ──► ≥1 resolved ──► Loaded
                     └──► film found ──► Loaded         │                    (+ partial notice
                                │                       │                     if some failed)
                                └─ expand Characters ──►└──► all failed ──► retry/cancel
                                                                     └─ Cancel ──► Error
                                                                            │
                                                            re-expand does NOT retry ─┘
                                                            (only the section's own Retry does)
```

The film's own fields stay on screen through every section transition, and each section's transitions are invisible to the other four. A section that reached `Loaded` does not re-enter `Loading` on re-expansion (FR-028); a section that reached `Error` re-enters `Loading` only through its own retry, never by being re-expanded — otherwise a user toggling a broken section would silently hammer a failing endpoint.

---

## 5. Validation rules

| ID | Rule | Enforced in | Requirement |
|---|---|---|---|
| V1 | Navigation parameter must be a non-null, non-whitespace `string` | `FilmDetailsViewModel` before any call | FR-013 |
| V2 | An invalid parameter issues **no** request | `FilmDetailsViewModel` | FR-013 |
| V3 | A film id is opaque; never derived from an episode number | `FilmMapper` M1, `FilmListItem.Id` | FR-006, R2 trap |
| V4 | 404 ⇒ `InvalidSelection`, not retry | `StarWarsService.GetFilmAsync` returns null | FR-013 |
| V5 | Any non-404 failure ⇒ recoverable, retryable | `StarWarsService` propagates | FR-015 |
| V6 | A request exceeding 15s ⇒ recoverable failure | `StarWarsService` budget | FR-015 |
| V7 | ≤6 concurrent related-resource requests | `StarWarsService` semaphore | FR-010 |
| V8 | Entries presented in the film's order, not response order | `Task.WhenAll` positional results | FR-010 |
| V9 | One failing entry never fails its section's batch | per-item catch | FR-012 |
| V10 | Films sorted ascending by `EpisodeNumber` | `FilmsViewModel` | FR-003 |
| V11 | No display string is ever null or empty | `FilmMapper` M2–M4 | FR-009 |
| V12 | Every busy event has a byte-identical done event in `finally` | `PageViewModelBase` | FR-019, AC-009 |
| V13 | A section requests **at most once** without user action: not at all until expanded (Characters excepted), and never again once loaded | `RelatedCategorySection.HasBeenLoaded` | FR-028 |
| V14 | Each section's busy/done message names its own category, so concurrent sections never share a progress string | `FilmDetailsViewModel` per-section message | FR-029, AC-009 |
| V15 | `RelatedUrls` contains all five `RelatedCategory` keys | `FilmMapper` M7 | FR-027 |

> **V14 is not cosmetic.** `ShellViewModel.OnNotifyDone` removes a progress entry by exact string match with no `-1` guard. Two sections loading under one shared message would have the first `done` remove the second's entry, and the second `done` call `RemoveAt(-1)` — an `ArgumentOutOfRangeException` on the UI thread. With five sections a user can plausibly expand, this moves from theoretical to reachable, which is why it is a validation rule with its own test rather than a note.

---

## 6. Entity relationships

```
                    ┌── characters ──┐
                    ├── planets ─────┤
FilmDto ────────────┼── starships ───┼──► (absolute URLs) ──► NamedResourceDto
   │                ├── vehicles ────┤                              │
   │                └── species ─────┘                              │ FilmMapper
   │ FilmMapper                                                     ▼
   ▼                                                    RelatedResourceListItem
FilmListItem ──(Id)──► FilmDetailsDisplay                           ▲
                              │                                     │
                              └──(RelatedUrls[category])──► RelatedCategorySection ×5
```

The `Id` string is the only thing crossing the page boundary. No object graph is passed through navigation, which is precisely what makes the parameter survive suspend/resume (clarification 3).

Note that the five arrows into `NamedResourceDto` converge rather than fan out. That convergence is the whole design: the source distinguishes five categories, and this feature's display does not, so the distinction is carried by `RelatedCategory` — a label on the section — and disappears below it.
