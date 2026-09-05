# Contract: SWAPI external API

**Base**: `https://swapi.info/api` · **Auth**: none · **Verified live**: 2026-09-05

This is an *observed* contract, not a published one. Every claim below was confirmed by request against the live service on the date shown. It is recorded because the shape differs from the original `swapi.dev` API that most documentation and most examples describe — and following that older contract produces code that compiles, runs, and is wrong.

---

## Configuration

`ApplicationConfiguration.ServerAddress` changes from the `https://some.api.com/` placeholder to:

```
https://swapi.info/api
```

`APIClient` composes requests as `$"{ServerAddress.TrimEnd('/')}/{path.TrimStart('/')}"`, so service paths are bare relatives: `films`, `films/1`, `people/1`.

The UWP `Package.appxmanifest` already declares `<Capability Name="internetClient" />` — verified, no manifest change needed.

---

## `GET /films`

Returns **every** film. No pagination.

**Response**: HTTP 200, a **bare JSON array** of 6 objects.

```json
[
  {
    "title": "A New Hope",
    "episode_id": 4,
    "opening_crawl": "It is a period of civil war.\r\nRebel spaceships, ...",
    "director": "George Lucas",
    "producer": "Gary Kurtz, Rick McCallum",
    "release_date": "1977-05-25",
    "characters": ["https://swapi.info/api/people/1", "..."],
    "planets":  ["..."], "starships": ["..."],
    "vehicles": ["..."], "species":   ["..."],
    "created": "...", "edited": "...",
    "url": "https://swapi.info/api/films/1"
  }
]
```

> ⚠️ **No `{ count, next, previous, results }` envelope.** The original swapi.dev API wrapped collections this way and nearly every tutorial shows it. This mirror does not. Deserialising to a wrapper type yields `null`, which reaches the UI as a permanently empty list with no error. Deserialise to `List<FilmDto>` directly.

## `GET /films/{id}`

**Response**: HTTP 200, a **single object** (not a one-element array) with the same fields as above.

Verified: `GET /films/1` → 200, `title: "A New Hope"`, `episode_id: 4`.

## `GET /films/{unknown}`

**Response**: HTTP 404. `APIClient` converts any non-2xx into `HttpStatusException(HttpStatusCode.NotFound)`.

Verified: `GET /films/99` → 404.

## `GET /people/{id}`

**Response**: HTTP 200, a single object. Only `name` and `url` are consumed.

---

## Identifier semantics — the trap

`url` is the **only** source of a film's identifier. The id is the film's **release-order position and has no relationship to its episode number**:

| Identifier (`url`) | `episode_id` | Title |
|---|---|---|
| `films/1` | **4** | A New Hope |
| `films/2` | **5** | The Empire Strikes Back |
| `films/3` | **6** | Return of the Jedi |
| `films/4` | **1** | The Phantom Menace |
| `films/5` | **2** | Attack of the Clones |
| `films/6` | **3** | Revenge of the Sith |

All six rows verified live.

Because the list sorts by episode (FR-003) and the detail page navigates by id (FR-006), any code that computes one from the other opens the **wrong film**. Worse, it fails plausibly: tapping "Episode IV" would open *The Phantom Menace* — a wrong film, not a crash, so it can survive a casual manual check.

Mitigations, all of them structural rather than advisory:
- `FilmListItem.Id` is a `string` extracted from `url` (mapper rule M1) and documented as opaque.
- `EpisodeNumber` is an `int` used only for sorting and display.
- The two never convert into one another anywhere in the codebase.

---

## Field-naming contract

Fields are `snake_case`; C# properties are `PascalCase`; the starter's shared serializer applies a camel-case naming strategy. Every DTO property therefore carries an explicit `[JsonProperty("...")]`.

Verified against Newtonsoft.Json 13.0.3 under the exact settings the starter registers — including the counter-intuitive result that `CamelCasePropertyNamesContractResolver`'s `OverrideSpecifiedNames = true` does **not** break the attributes, because camel-casing `"episode_id"` leaves it unchanged. Full result table in [research.md](../research.md) R1.

| JSON | DTO property | Consequence if the attribute is omitted |
|---|---|---|
| `episode_id` | `EpisodeId` | binds as `0` — every film shows "Episode 0" |
| `opening_crawl` | `OpeningCrawl` | binds as `null` — bonus-point content silently missing |
| `release_date` | `ReleaseDate` | binds as `null` — placeholder shown for every film |
| `title`, `director`, `producer`, `characters`, `url` | matching | happen to match, attributed anyway for consistency |

Each of these fails **silently**: no exception, no log, just wrong data. That is why the mapping tests in FR-024 assert against a captured real payload rather than a hand-written camel-case fixture.

---

## Volume and reliability

| Property | Observed |
|---|---|
| Films | 6, fixed |
| Characters per film | 18 (*A New Hope*) up to ~34 (*The Phantom Menace*) |
| Films payload | ~20 KB |
| Auth / rate limit | None published |
| Availability | Community-run mirror; the brief itself offers a fallback API in case it is down |

No published rate limit, but the design does not rely on that: character retrieval is capped at 6 concurrent requests (FR-010) so that a throttling response would be an unexpected failure to handle rather than a predictable consequence of the design.

---

## Reproducing this verification

```powershell
# Shape: bare array, 6 films, snake_case fields
(Invoke-WebRequest 'https://swapi.info/api/films' -UseBasicParsing).Content | ConvertFrom-Json

# Single film: one object, not an array
(Invoke-WebRequest 'https://swapi.info/api/films/1' -UseBasicParsing).Content | ConvertFrom-Json

# Unknown id: 404
try { Invoke-WebRequest 'https://swapi.info/api/films/99' -UseBasicParsing }
catch { $_.Exception.Response.StatusCode.value__ }
```

Re-run these if the film list ever renders empty or every episode number reads zero — those two symptoms point straight back at this contract.
