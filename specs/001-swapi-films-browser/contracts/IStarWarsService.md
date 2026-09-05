# Contract: `IStarWarsService`

**Assembly**: `DrawboardCodingExercise.Services` · **Namespace**: `DrawboardCodingExercise.Services.StarWars`
**Consumers**: `FilmsViewModel`, `FilmDetailsViewModel` · **Depends on**: `IAPIClient`, `IAPISettings`, `ILogger`

The only abstraction between the app and SWAPI. ViewModels never see `IAPIClient`, `HttpClient` or a URL.

```csharp
public interface IStarWarsService
{
    Task<IReadOnlyList<FilmDto>> GetFilmsAsync();
    Task<FilmDto> GetFilmAsync(string filmId);
    Task<RelatedResourceLoadResult> GetRelatedResourcesAsync(IReadOnlyList<string> resourceUrls);
}

public sealed class RelatedResourceLoadResult
{
    public IReadOnlyList<NamedResourceDto> Resources { get; }  // in requested order, failures removed
    public int RequestedCount { get; }
    public int FailedCount { get; }
    public bool IsPartial => FailedCount > 0 && Resources.Count > 0;
}
```

`RelatedResourceLoadResult` exists because "some entries failed" is a first-class outcome (FR-012), not an exception and not a silently shorter list. A bare `IReadOnlyList<NamedResourceDto>` would make partial failure indistinguishable from a film that genuinely references fewer entries.

> **One method, five categories.** This method is category-agnostic on purpose: it takes URLs and returns names, and nothing in it knows whether it is fetching people or starships. `RelatedCategory` never crosses this boundary — it is a display grouping, and pushing it down here would buy a `switch` and five near-identical code paths for no behavioural gain (Principle II). It replaces the earlier `GetCharactersAsync`/`CharacterLoadResult`/`PersonDto` trio, which were correct only while Characters was the sole category.

---

## `GetFilmsAsync()`

Retrieves every film.

| Aspect | Contract |
|---|---|
| Calls | `IAPIClient.GetAsync<List<FilmDto>>("films")` |
| Returns | All films, **unsorted** — ordering is `FilmsViewModel`'s job (FR-003, data-model M6) |
| Empty response | Returns an empty list. **Not** an error — the caller renders `Empty` (FR-005) |
| Null deserialization | Returns an empty list. A `null` body is treated as empty, never dereferenced |
| Non-success status | Propagates `HttpStatusException` — recoverable, retryable (FR-015) |
| Malformed JSON | Propagates `JsonException` — recoverable, retryable (FR-015) |
| Network failure | Propagates the transport exception — recoverable, retryable |
| Exceeds 15s | Throws `TimeoutException` — recoverable, retryable (FR-015, V6) |
| Logging | `Debug` on success with the film count; `Error` on failure with the operation name. Never the response body (Constitution X) |

## `GetFilmAsync(string filmId)`

Retrieves one film by its opaque identifier.

| Aspect | Contract |
|---|---|
| Calls | `IAPIClient.GetAsync<FilmDto>($"films/{filmId}")` |
| Precondition | `filmId` is non-null, non-whitespace. The **ViewModel** validates first (V1/V2); the service guards defensively and returns `null` without calling out |
| Returns | The film, or **`null` when the source returns 404** |
| Not-found | `HttpStatusException` with `StatusCode == NotFound` is caught and converted to `null`. Every other status propagates |
| Other failures | As `GetFilmsAsync` — propagate, recoverable, retryable |
| Logging | `Warning` on not-found with the id; `Error` on other failures |

> **Why `null` rather than an exception**: a 404 means the id is wrong, and no amount of retrying fixes a wrong id. Returning `null` lets `FilmDetailsViewModel` render `InvalidSelection` (which offers a way back to the list) instead of `Error` (which offers a pointless Retry). This is FR-013's whole point. See [research.md](../research.md) R7.

## `GetRelatedResourcesAsync(IReadOnlyList<string> resourceUrls)`

Retrieves the named records a film references, for any one of the five categories.

| Aspect | Contract |
|---|---|
| Input | Absolute SWAPI URLs, exactly as they appear in `FilmDto.Characters` / `.Planets` / `.Starships` / `.Vehicles` / `.Species` |
| URL handling | Each is normalised to a path relative to `IAPISettings.ServerAddress` before being handed to `IAPIClient`, so `IAPIClient` stays the single HTTP surface (AC-006) |
| Concurrency | At most **6** in flight (V7), **per call** — five concurrent sections can therefore have up to 30 requests in flight between them |
| Ordering | Results in **input order**, guaranteed by `Task.WhenAll`'s positional results — never completion order (V8, FR-010) |
| Null/empty input | Returns an empty result with `RequestedCount == 0`. Not an error — caller renders `Empty` |
| Partial failure | Failed items are omitted; `FailedCount` reports how many. **Does not throw** (V9, FR-012) |
| Total failure | When every request fails, throws the **first** captured exception, so the caller can offer retry/cancel (FR-012) |
| Unrecognised URL | A URL not under the configured base is skipped and counted as failed, with a `Warning` log. Never passed through raw |
| Logging | `Debug` with requested/succeeded/failed counts; `Warning` per failed record; `Error` on total failure |

> **The concurrency cap is per call, not global.** Five expanded sections can put 30 requests in flight. That is accepted rather than overlooked: the cap exists to keep a single section from firing 34 requests at once, and reaching 30 requires the user to deliberately expand all five sections at once, which the lazy-load design (FR-028) makes an explicit act rather than the default. A shared global semaphore would serialise unrelated sections behind each other, making an expanded section appear frozen while another loads — a worse outcome than the burst it prevents.

---

## Failure taxonomy

How each failure must be surfaced. The distinctions are the contract — collapsing them produces the wrong user experience.

| Condition | Service behaviour | ViewModel renders | Retry offered? |
|---|---|---|---|
| Empty collection | Empty list / empty result | `Empty` | — |
| 404 on a film id | Returns `null` | `InvalidSelection` | **No** |
| Invalid parameter | Not called at all | `InvalidSelection` | **No** |
| 4xx (non-404) / 5xx | Throws `HttpStatusException` | prompt → `Error` | **Yes** |
| Network failure | Throws | prompt → `Error` | **Yes** |
| Malformed body | Throws | prompt → `Error` | **Yes** |
| >15s | Throws `TimeoutException` | prompt → `Error` | **Yes** |
| Some entries in a section fail | `IsPartial == true` | that section `Loaded` + notice | **No** — partial results are kept |
| All entries in a section fail | Throws first exception | that section: prompt → `Error` | **Yes**, for that section only |

---

## URL normalisation — `SwapiResourcePath`

A small internal helper, separately tested because it is where an absolute-vs-relative mistake would otherwise surface as a malformed request at runtime.

```csharp
internal static class SwapiResourcePath
{
    // "https://swapi.info/api/people/1" + base "https://swapi.info/api" -> "people/1"
    // Returns null when the URL is null, blank, or not under the base.
    public static string ToRelativePath(string absoluteUrl, string serverAddress);

    // "https://swapi.info/api/films/1" -> "1"; null when unparseable.
    public static string ExtractId(string absoluteUrl);
}
```

Both are case-insensitive on the base, tolerant of trailing slashes on either side, and **never throw** — they return `null` and let the caller decide. `APIClient` already trims a leading `/` from the path and a trailing `/` from the base, so the helper returns a bare relative path with no leading separator.

---

## Substitution in tests

Every dependency is an interface, so no test touches the network (FR-025).

Under Constitution XV these tests come **first** — this contract is the specification the failing tests are written against, before `StarWarsService` has a body. See [plan.md](../plan.md) § *Implementation Sequence* for cycle order and [research.md](../research.md) R9 for how a compiled language produces a meaningful red.

| Test target | Substituted | Purpose |
|---|---|---|
| `StarWarsService` | `IAPIClient`, `IAPISettings`, `ILogger` | Canned payloads, thrown exceptions, delays past the budget, concurrency observation |
| `FilmsViewModel` | `IStarWarsService`, `INavigationService`, `IUserInteractionService`, `IEventAggregator`, `ILocalizationService`, `ILogger` | Load, sort, empty, failure, retry/cancel, progress pairing |
| `FilmDetailsViewModel` | as above | Valid/invalid/unknown id, the five sections' states, lazy expansion, per-section progress pairing |
| `RelatedCategorySection` | `IStarWarsService` via the owning VM | Load-once, expand/collapse, empty/partial/error, section-local retry |
| `FilmMapper` | none — pure static | Mapping rules M1–M7 |
| `SwapiResourcePath` | none — pure static | Normalisation and id extraction |

The 15-second budget is tested by substituting an `IAPIClient` whose task completes later than the configured budget, with the budget injected as a `TimeSpan` so the test can use milliseconds. **No test waits 15 real seconds.**
