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
| `Url` | `string` | `url` | Absolute self-link. The **only** source of the film's identifier. |

Unused SWAPI fields (`planets`, `starships`, `vehicles`, `species`, `created`, `edited`) are intentionally not modelled — Newtonsoft ignores unmapped JSON by default, and modelling them would imply support this feature does not provide.

> **`ReleaseDate` is `string`, not `DateTime`.** Deserialising straight to `DateTime` would let a malformed date throw inside `JsonConvert`, surfacing as an opaque serialization failure for the whole film. Parsing in the mapper keeps the failure local to one field, which is what FR-009 requires: a bad or missing date degrades to a placeholder while the rest of the film still renders.

### `PersonDto`

| Property | Type | JSON name | Notes |
|---|---|---|---|
| `Name` | `string` | `name` | The only field this feature displays. |
| `Url` | `string` | `url` | Self-link; useful for stable keys and logging. |

Only two fields are modelled because only `Name` is displayed (FR-010). Adding the rest would be speculative.

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
| `CharacterUrls` | `IReadOnlyList<string>` | From `FilmDto.Characters`; empty list if null. Drives the character load. |

### `CharacterListItem` — one row in the character section

| Property | Type | Derivation |
|---|---|---|
| `Name` | `string` | `PersonDto.Name`, placeholder if blank. |
| `Url` | `string` | `PersonDto.Url`; stable list key. |

---

## 3. Mapping rules — `FilmMapper`

Pure and static, so it is testable with no container, no substitutes and no async. Directly covered by FR-024's mapping requirement.

| Rule | Behaviour |
|---|---|
| **M1 — Identifier extraction** | Take the last non-empty segment of `FilmDto.Url` (`https://swapi.info/api/films/1` → `"1"`). Null/blank/unparseable URL ⇒ `Id` is null, and the item is excluded from the list rather than rendered unopenable. |
| **M2 — Missing text** | Null, empty or whitespace `Title`, `Director`, `Producer`, `OpeningCrawl` ⇒ the localized "not available" placeholder. Never an empty string, never `null` reaching XAML. |
| **M3 — Release date** | Parse `yyyy-MM-dd` invariantly; format for reading on success. Null, blank or unparseable ⇒ placeholder. **Never throws.** |
| **M4 — Episode label** | `EpisodeId` → Roman numeral for display. The underlying `int` is retained separately for sorting — the label is never sorted on. |
| **M5 — Null character array** | `Characters == null` ⇒ empty list, so the detail page shows the "no characters" empty state rather than dereferencing null. |
| **M6 — Ordering** | The mapper does **not** sort. Sorting by `EpisodeNumber` ascending is the ViewModel's job (FR-003), keeping the mapper a pure per-item transform. |

---

## 4. Page state

### `PageLoadState` enum

| Value | Meaning | Reached when |
|---|---|---|
| `Loading` | Retrieval in flight | Entry to `OnNavigatedToAsync`, and on each retry |
| `Loaded` | Content available | Retrieval returned ≥1 item |
| `Empty` | Retrieval succeeded, nothing to show | Source returned zero films / film has zero characters |
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

Two independent state machines on one page (FR-011, FR-012):

```
FILM STATE                                    CHARACTER STATE (starts only after film Loaded)
[entry] ──► validate id                       Loading ──► ≥1 resolved ──► Loaded (partial notice if some failed)
    │                                             │
    ├─ invalid ──► InvalidSelection               ├──► 0 urls ──► Empty
    │              (no request issued)            │
    └─ valid ──► Loading                          └──► all failed ──► retry/cancel ──► Cancel ──► Error
                     │
                     ├──► film found ──► Loaded ──► start character load
                     ├──► 404 ──────────► InvalidSelection
                     └──► failure ──────► retry/cancel ──► Cancel ──► Error
```

The film's own fields stay on screen through every character-state transition — the character section never blanks the page it lives on.

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
| V7 | ≤6 concurrent character requests | `StarWarsService` semaphore | FR-010 |
| V8 | Characters presented in the film's order, not response order | `Task.WhenAll` positional results | FR-010 |
| V9 | One failing character never fails the batch | per-item catch | FR-012 |
| V10 | Films sorted ascending by `EpisodeNumber` | `FilmsViewModel` | FR-003 |
| V11 | No display string is ever null or empty | `FilmMapper` M2–M4 | FR-009 |
| V12 | Every busy event has a byte-identical done event in `finally` | `PageViewModelBase` | FR-019, AC-009 |

---

## 6. Entity relationships

```
FilmDto 1 ──── * (absolute URLs) ────► PersonDto
   │                                       │
   │ FilmMapper                            │ FilmMapper
   ▼                                       ▼
FilmListItem ──(Id)──► FilmDetailsDisplay ──(CharacterUrls)──► CharacterListItem
```

The `Id` string is the only thing crossing the page boundary. No object graph is passed through navigation, which is precisely what makes the parameter survive suspend/resume (clarification 3).
