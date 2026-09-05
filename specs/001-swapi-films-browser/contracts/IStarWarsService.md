# Contract: `IStarWarsService`

**Assembly**: `DrawboardCodingExercise.Services` · **Namespace**: `DrawboardCodingExercise.Services.StarWars`
**Consumers**: `FilmsViewModel`, `FilmDetailsViewModel` · **Depends on**: `IAPIClient`, `IAPISettings`, `ILogger`

The only abstraction between the app and SWAPI. ViewModels never see `IAPIClient`, `HttpClient` or a URL.

```csharp
public interface IStarWarsService
{
    Task<IReadOnlyList<FilmDto>> GetFilmsAsync();
    Task<FilmDto> GetFilmAsync(string filmId);
    Task<CharacterLoadResult> GetCharactersAsync(IReadOnlyList<string> characterUrls);
}

public sealed class CharacterLoadResult
{
    public IReadOnlyList<PersonDto> Characters { get; }  // in requested order, failures removed
    public int RequestedCount { get; }
    public int FailedCount { get; }
    public bool IsPartial => FailedCount > 0 && Characters.Count > 0;
}
```

`CharacterLoadResult` exists because "some characters failed" is a first-class outcome (FR-012), not an exception and not a silently shorter list. A bare `IReadOnlyList<PersonDto>` would make partial failure indistinguishable from a film that genuinely has fewer characters.

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

## `GetCharactersAsync(IReadOnlyList<string> characterUrls)`

Retrieves the people a film references.

| Aspect | Contract |
|---|---|
| Input | Absolute SWAPI URLs, exactly as they appear in `FilmDto.Characters` |
| URL handling | Each is normalised to a path relative to `IAPISettings.ServerAddress` before being handed to `IAPIClient`, so `IAPIClient` stays the single HTTP surface (AC-006) |
| Concurrency | At most **6** in flight (V7) |
| Ordering | Results in **input order**, guaranteed by `Task.WhenAll`'s positional results — never completion order (V8, FR-010) |
| Null/empty input | Returns an empty result with `RequestedCount == 0`. Not an error — caller renders `Empty` |
| Partial failure | Failed items are omitted; `FailedCount` reports how many. **Does not throw** (V9, FR-012) |
| Total failure | When every request fails, throws the **first** captured exception, so the caller can offer retry/cancel (FR-012) |
| Unrecognised URL | A URL not under the configured base is skipped and counted as failed, with a `Warning` log. Never passed through raw |
| Logging | `Debug` with requested/succeeded/failed counts; `Warning` per failed character; `Error` on total failure |

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
| Some characters fail | `IsPartial == true` | `Loaded` + notice | **No** — partial results are kept |
| All characters fail | Throws first exception | prompt → `Error` | **Yes** |

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
| `FilmDetailsViewModel` | as above | Valid/invalid/unknown id, character states, progress pairing |
| `FilmMapper` | none — pure static | Mapping rules M1–M6 |
| `SwapiResourcePath` | none — pure static | Normalisation and id extraction |

The 15-second budget is tested by substituting an `IAPIClient` whose task completes later than the configured budget, with the budget injected as a `TimeSpan` so the test can use milliseconds. **No test waits 15 real seconds.**
