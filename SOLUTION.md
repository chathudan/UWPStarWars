# Solution Notes — Star Wars Films Browser

Candidate write-up for the Drawboard UWP coding exercise.

> `README.md` is the supplied exercise brief and is deliberately left untouched — it states the
> problem, not the solution. All candidate-authored documentation lives here.

**Status: complete.** All four user stories are implemented test-first, 78 unit tests pass with no
network access, and the solution builds and deploys as a UWP app.

---

## 1. Chosen API

**Star Wars Movies API** — `https://swapi.info/api`. Public, unauthenticated, read-only.

Two properties of this particular mirror were verified by request rather than assumed, because
both contradict the widely-documented original `swapi.dev` contract:

- `GET /films` returns a **bare JSON array**, not a `{ count, next, previous, results }` envelope.
  Deserialising into a wrapper type yields `null` and surfaces as a permanently empty list.
- A film's identifier is its **release-order position, not its episode number**. `films/1` is
  Episode 4. Deriving one from the other opens the wrong film — plausibly enough to survive a
  casual manual check.

Full contract notes: [`specs/001-swapi-films-browser/contracts/swapi-endpoints.md`](specs/001-swapi-films-browser/contracts/swapi-endpoints.md).

## 2. Architecture decisions

**The starter framework is reused unchanged.** No file under `CoreFramework/`, no
`APIClient`, no `EventAggregator`, no `Shell` was modified. The two starter *sample pages*
(`Welcome`, `PageA`) were retired once superseded, but the framework they demonstrated is intact.

| Decision | Why |
|---|---|
| `IStarWarsService` / `StarWarsService` above `IAPIClient` | Keeps all SWAPI specifics — paths, DTO shapes, 404 semantics, concurrency — behind one abstraction. ViewModels never see a URL or `HttpClient`. |
| DTOs separate from display models, joined by `FilmMapper` | The wire shape (`snake_case`, string dates, absolute URLs) has nothing to do with what the UI needs (formatted dates, placeholders, Roman numerals). `FilmMapper` is pure and static, so mapping is tested with no container and no async. |
| Explicit `[JsonProperty]` on every DTO property | The shared serializer applies a camel-case strategy; without attributes `episode_id` silently binds to `0`. Attributes are applied even where the name already matches, so nothing depends on an incidental match. |
| Navigation by opaque `string` id, not the film object | A UWP navigation parameter must survive process suspend/resume; a string does, an object graph does not. It also lets Page 2 stand on its own. |
| `PageViewModelBase` owning busy/done pairing | `ShellViewModel.OnNotifyDone` calls `RemoveAt(IndexOf(...))` with no `-1` guard, so a mismatched pair **throws on the UI thread**. Capturing the message once and posting both events from it makes the mismatch structurally impossible rather than a convention to remember. |
| Independent state machines on the detail page | The film and each related category fail separately (FR-011). A category failure leaves the film's own fields and the other sections on screen; each section carries its own `PageLoadState`, progress pair, and retry command. |
| `SemaphoreSlim(6)` + `Task.WhenAll` for related resources | `WhenAll` returns results positionally, so request order is preserved for free regardless of completion order. The cap keeps each expanded category from overwhelming a free community mirror. |
| Localized text via ViewModel properties, not `x:Uid` | Discovered the hard way — see §11. |

## 3. Assumptions

- The detail page shows all five related categories: characters, planets, starships, vehicles and
  species. Characters auto-expand on arrival because they satisfy the brief's required related list;
  the other four sections load only when expanded.
- The film list is the app's landing page, so a reviewer lands on the deliverable rather than a
  placeholder.
- Films are ordered by **episode number ascending** (I–VI), not the API's release order. The list
  shows episode numbers, so a list starting at "Episode IV" reads as a bug.
- A 404 on a film id is an **invalid selection**, not a retryable failure.
- Each request gets a **15-second budget**; beyond that the user is told something went wrong.
- No persistence, caching, or offline support — data is fetched fresh per navigation.
- English only. The localization mechanism is used properly, so another language is a resource
  addition rather than a code change.

## 4. Limitations

- **The request budget stops the caller, not the request.** `IAPIClient` exposes no
  `CancellationToken` and its `HttpClient` is `static`, so `StarWarsService` races each call against
  a delay. After 15 seconds the user gets a failure, but the abandoned HTTP request keeps running in
  the background until the platform default elapses. Fixing this properly means adding cancellation
  support to `IAPIClient` — see §9.
- **DI and page registration have no automated safety net.** They are on the constitution's
  test-exempt list (mechanical registration), so a page that is built but never registered would
  compile and fail at runtime. The manual checklist covers it.
- **XAML is verified only manually.** There is no UI test project; layout and bindings were checked
  by running the app. §11 describes a defect this caught that every automated check missed.
- **Accessibility** goes no further than what the stock controls provide.
- **Related category lists show names only.** Each referenced resource is fetched individually, so
  richer detail is available but was out of scope.

---

## 5. How to build and run

### Prerequisites

| Requirement | Version |
|---|---|
| Visual Studio | 2026 (18.7+) — verified with 18 Community |
| Workloads | Windows application development · .NET desktop development |
| Components | Universal Windows Platform tools · Windows 11 SDK **10.0.26100.0** |
| .NET SDK | For the test projects (net8.0) — verified with 10.0.400 |
| Windows | **Developer Mode must be enabled** (Settings → System → For developers) |
| Platform | **x64** or **ARM64** only — `AnyCPU` is not a valid configuration for the UWP project |

### The easy path: Visual Studio

Open `DrawboardCodingExercise.slnx`, select **x64**, set `DrawboardCodingExercise` as the startup
project (it already is), and press <kbd>F5</kbd>.

**F5 handles build, deployment and activation correctly on its own.** Everything in the next
section only matters if you need to build and launch from a terminal.

### Building from the command line

The UWP app is an **old-style MSBuild project** — `dotnet build` cannot build it. Use MSBuild from
the Visual Studio installation:

```powershell
$msbuild = "C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe"

& $msbuild "DrawboardCodingExercise\DrawboardCodingExercise.csproj" `
    /p:Configuration=Debug /p:Platform=x64 /p:AppxBundle=Never /restore
```

The shared projects and tests build with the ordinary .NET SDK:

```powershell
dotnet build DrawboardCodingExercise.Services\DrawboardCodingExercise.Services.csproj
dotnet build DrawboardCodingExercise.ViewModel\DrawboardCodingExercise.ViewModel.csproj
```

> **`/p:AppxBundle=Never` is not optional here.** The project declares
> `<AppxBundlePlatforms>x64|arm64</AppxBundlePlatforms>`, so a default build tries to bundle both
> architectures. If a stale ARM64 package is present from an earlier build, `MakeAppx` fails with
> `error 80080204: All app package manifests in a bundle must declare the same values under …
> Dependencies`. A local run needs no bundle at all, so skipping bundling avoids the whole class of
> problem. (Alternatively, clean `bin\` first.)

### Deploying and launching from the command line

A UWP build produces **two different outputs**, and picking the wrong one is the single biggest
time sink here:

| Path | What it is |
|---|---|
| `bin\x64\Debug\` | raw compiler output — the `.exe` here is the **managed assembly** (~34 KB) |
| `bin\x64\Debug\AppX\` | the **deployment layout** — root `.exe` is an ~8 KB **native CoreCLR host stub**; the managed app sits in `entrypoint\` (~35 KB) |

**Register the layout folder, never the raw output folder:**

```powershell
Add-AppxPackage -Register "…\DrawboardCodingExercise\bin\x64\Debug\AppX\AppxManifest.xml"
```

Registering `bin\x64\Debug` instead makes Windows activate the *managed* exe directly. The loader
routes it through `MSCOREE.DLL` into the **desktop .NET Framework CLR**, which then fails looking
for `System.Private.CoreLib` — a CoreCLR assembly it does not have. The symptom is a
`BadImageFormatException` / `FileNotFoundException`, and the diagnostic tell is
`mscoreei!InvokeAppXMain` in the stack: that frame means the wrong CLR is hosting the app. The
exception message itself sends you looking in the wrong direction.

**If the layout is stale or missing.** A plain MSBuild run produces an `.msix` under
`AppPackages\` but does **not** necessarily refresh the loose `AppX\` layout — you can build
successfully and still launch yesterday's code. Regenerate it by unpacking the fresh package:

```powershell
$makeappx = "C:\Program Files (x86)\Windows Kits\10\bin\10.0.26100.0\x64\makeappx.exe"
& $makeappx unpack /p "…\AppPackages\DrawboardCodingExercise_1.0.0.0_x64_Debug_Test\DrawboardCodingExercise_1.0.0.0_x64_Debug.msix" `
                   /d "…\DrawboardCodingExercise\bin\x64\Debug\AppX" /o
```

Check the timestamps on `AppX\DrawboardCodingExercise.Services.dll` before trusting a run.

**Launching.** `explorer.exe shell:appsFolder\<AUMID>` **fails silently** — no process, no error,
no event log entry. Use `IApplicationActivationManager::ActivateApplication`, which works and
returns the PID:

```powershell
$src = @"
using System; using System.Runtime.InteropServices;
public static class AppActivator {
    [ComImport, Guid("2e941141-7f97-4756-ba1d-9decde894a3d"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IApplicationActivationManager {
        IntPtr ActivateApplication(string appUserModelId, string arguments, int options, out uint processId);
    }
    [ComImport, Guid("45BA127D-10A8-46EA-8AB7-56EA9078943C")] class ApplicationActivationManager { }
    public static uint Launch(string aumid) {
        var mgr = (IApplicationActivationManager)new ApplicationActivationManager();
        uint pid; mgr.ActivateApplication(aumid, null, 0, out pid); return pid;
    }
}
"@
Add-Type -TypeDefinition $src -Language CSharp
[AppActivator]::Launch("e29e08cc-226f-4293-81f4-636ff042078b_qa6kvaw56d0me!App")
```

Find the AUMID with:

```powershell
$pkg = Get-AppxPackage | Where-Object { $_.Name -eq 'e29e08cc-226f-4293-81f4-636ff042078b' }
(Get-AppxPackageManifest $pkg).Package.Applications.Application.Id |
    ForEach-Object { "$($pkg.PackageFamilyName)!$_" }
```

### Deployment gotchas

| Symptom | Cause and fix |
|---|---|
| App launches but shows **old code** | Debug and Release share package identity **and version (1.0.0.0)**, so registering the other configuration is a **silent no-op**. Unregister first, or bump the version. |
| Same after a successful build | The `AppX\` layout was not refreshed — see *If the layout is stale* above. |
| `0x80073CFB … already installed … unpackaged version` | A dev-mode (loose-layout) registration blocks installing a packaged `.msix`. Run `Remove-AppxPackage` on the existing registration first. |
| `Get-AppxPackage -Name "*Drawboard*"` finds nothing | The package **Name is a GUID** (`e29e08cc-226f-4293-81f4-636ff042078b`), not a friendly name. Filter on that, or on `PackageFamilyName`. |
| Cannot find the app's window | UWP windows are owned by **`ApplicationFrameHost.exe`**, not the app process. Enumerating windows by the app's PID returns none — search all top-level windows by title (`Drawboard Coding Exercise`) instead. |
| `MSB1008: Only one project can be specified` | Git Bash mangles `/p:` switches into paths. Use PowerShell, or `-p:` form. |

### Uninstalling

```powershell
Get-AppxPackage | Where-Object { $_.Name -eq 'e29e08cc-226f-4293-81f4-636ff042078b' } |
    Remove-AppxPackage
```

---

## 6. How to run tests

```powershell
dotnet test DrawboardCodingExercise.Services.UnitTests\DrawboardCodingExercise.Services.UnitTests.csproj
```

The suite is **deterministic and runs with no network access** — `IAPIClient` is substituted, and
API responses are captured payloads held as string constants
(`DrawboardCodingExercise.Services.UnitTests/TestData/SwapiPayloads.cs`). The per-request timeout
budget is injected into `StarWarsService`, so the timeout test uses a millisecond-scale value and
**no test ever waits 15 real seconds**.

---

## 7. Error handling approach

The design turns on one distinction: **not every failure is retryable, and offering "Retry" for one
that isn't is a worse experience than not offering it.**

| Condition | Treated as | Retry offered? |
|---|---|---|
| Empty film list / no characters | `Empty` — a successful, empty result | — |
| **404 on a film id** | `InvalidSelection` | **No** — the id is wrong; retrying cannot fix it |
| Missing / non-string / blank navigation parameter | `InvalidSelection`, **no request issued** | **No** |
| 4xx (non-404), 5xx, network failure, malformed body, >15s | Recoverable failure | **Yes** |
| *Some* related entries fail | `Loaded` + "some entries could not be loaded" | No — successes are kept |
| *Every* related entry in a section fails | Recoverable failure for that section only | **Yes** |

How it works in practice:

1. `StarWarsService` decides what is exceptional. A 404 on a film returns `null` rather than
   throwing, so it never enters the retry path. Everything else propagates.
2. `PageViewModelBase.RunWithRetryAsync` catches, prompts via `IUserInteractionService`, and loops
   on **Retry**. Each attempt gets its own progress pair.
3. **Cancel** returns `false`, and the ViewModel renders its own on-page error state with a Retry
   button — the user keeps control and can recover without restarting.
4. `OnNavigatedToAsync` **never propagates**. `NavigationService.NavigateAsync` logs `Fatal` and
   rethrows anything escaping it, so both ViewModels guard their whole body and convert every
   failure into a page state. This is asserted by a test, not left to discipline.
5. Failures are logged through Serilog with the operation's context, and never the response body.

Each related-category section is deliberately independent: a section failure leaves the film's own
details and the other sections on screen, and its retry re-fetches **only** that section — asserted
by tests that require `GetFilmAsync` not to be called again.

## 8. Progress / loading behaviour

Long-running work publishes `NotifyBusyEvent` and its matching `NotifyDoneEvent` through
`IEventAggregator`; `ShellViewModel` renders the title-bar progress ring from them.

**Why the pairing is a correctness concern, not tidiness.** `ShellViewModel.OnNotifyDone` does:

```csharp
var index = _thingsInProgress.IndexOf(obj.Event);
_thingsInProgress.RemoveAt(index);      // no -1 guard
```

A done-event whose string doesn't exactly match a live busy-event throws
`ArgumentOutOfRangeException` **on the UI thread**. So `PageViewModelBase.RunBusyAsync` captures the
message into a single local and posts both events from that same local, with the done event in
`finally`:

- Mismatch becomes structurally impossible rather than a convention someone must remember.
- Progress clears on success, on failure, on retry-cancel, and on an unexpected exception alike.

The film load and the character load use **distinct** progress messages, and a test asserts this.
Sharing one would make the two operations remove each other from the shell's progress list, since
removal is by exact string match.

Each page also exposes its own `PageLoadState` (`Loading` / `Loaded` / `Empty` / `Error` /
`InvalidSelection`) as mutually exclusive derived booleans, bound through the starter's existing
`BoolToVisibilityConverter` — no new converter was needed.

## 9. Future extension ideas

1. **`CancellationToken` on `IAPIClient`** — the direct fix for the limitation in §4. Would let the
   timeout actually abort the request rather than only stopping the caller waiting, and would let a
   user navigating away cancel in-flight work.
2. **Richer related-resource detail pages** — the app currently shows only resource names. The same
   service boundary could support selecting a character, planet, starship, vehicle or species for a
   deeper view.
3. **Cache the film list** — six films that never change, re-fetched on every back-navigation.
   A short-lived in-memory cache would make back-navigation instant.
4. **Search and filter** on the list page, and sort by release date as well as episode.
5. **Deep linking** — the identifier-based navigation parameter already supports opening a film
   directly; it just needs a protocol activation handler.
6. **Richer character detail** — the API returns height, birth year, homeworld and more; the
   section shows names only.
7. **UI tests** — the one defect that reached a running app (§11) was invisible to unit tests. A
   WinAppDriver smoke test over the four page states would have caught it.
8. **Move tuning values to configuration** — the concurrency cap and request budget are currently a
   constant and a DI parameter respectively.

---

## 10. Creating a release

Release builds are packaged rather than loose-registered.

```powershell
$msbuild = "C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe"

& $msbuild "DrawboardCodingExercise\DrawboardCodingExercise.csproj" `
    /p:Configuration=Release /p:Platform=x64 `
    /p:UapAppxPackageBuildMode=SideloadOnly `
    /p:AppxBundle=Always /p:AppxBundlePlatforms="x64|arm64" `
    /restore
```

Output lands in `DrawboardCodingExercise\AppPackages\`.

Notes for an actual release, as opposed to a local run:

- **Signing is currently disabled** (`<AppxPackageSigningEnabled>false</AppxPackageSigningEnabled>`
  in the `.csproj`). Sideloading an unsigned package needs `Add-AppxPackage -AllowUnsigned`, or a
  trusted certificate installed into *Trusted People*. For distribution, enable signing and supply
  a real certificate.
- **Bump `Version` in `Package.appxmanifest`.** It is currently `1.0.0.0` for every build, which is
  what makes re-registration a silent no-op. Any release intended to replace an installed build
  must increment it.
- Building the bundle for both `x64|arm64` requires both architectures to build cleanly and to
  declare identical `Dependencies` in their manifests — otherwise `MakeAppx` fails with `80080204`.
- The Release configuration uses the .NET Native toolchain (`UseDotNetNativeToolchain`), so it is
  substantially slower to build than Debug and can surface AOT-only issues that Debug does not.

---

## 11. Continuous integration

GitHub Actions runs the deterministic unit test suite and builds the UWP app on a Windows runner.
The workflow is named **Drawboard UWP Validation** and uploads the generated package/build output
as artifacts for review.

The CI workflow does not launch the UWP app because interactive UWP activation is better validated
manually on a Windows development machine.

---

## 12. AI-assisted development

An AI agent (Claude, via Claude Code) was used throughout, driven by a
[Spec Kit](https://github.com/github/spec-kit) workflow: a project constitution, then
`specify → clarify → plan → tasks → analyze → implement`. All artifacts are in
[`specs/001-swapi-films-browser/`](specs/001-swapi-films-browser/) and the governing rules in
[`.specify/memory/constitution.md`](.specify/memory/constitution.md).

### What AI produced

Effectively all of the code and specification artifacts: the spec, plan, research decisions, task
breakdown, production classes, tests, and this document. Most production behaviour was implemented
through explicit red/green TDD cycles, visible in the commit history as paired `test:` and `feat:`
commits. The final five-category expansion was committed as one cohesive enhancement containing
both tests and implementation; the tests remain deterministic and cover the added behaviour.

Human direction shaped it at every step, and that mattered more than the volume of generated code:
choosing the API, mandating TDD partway through (the constitution gained Principle XV mid-project,
which forced a re-plan), requiring per-cycle review and separate test/implementation commits, and
— most valuably — asking *"run the app"* at points where the agent would otherwise have relied on
a green build.

### Challenges the agent hit, and how they were corrected

**1. Plausible-but-wrong knowledge about the API.** The agent's default assumption was the
widely-documented `swapi.dev` contract: a `{ count, next, results }` envelope. This mirror returns
a **bare array**. Verified by querying the live endpoint before writing a DTO. The same check
surfaced the id-vs-episode trap (`films/1` is Episode 4) — the single most dangerous thing in this
data set, because getting it wrong opens the *wrong film* rather than crashing.

**2. Confidently wrong reasoning about Newtonsoft.** The agent reasoned that
`CamelCasePropertyNamesContractResolver` sets `OverrideSpecifiedNames = true` and would therefore
*override* `[JsonProperty("episode_id")]`, breaking the fix. That reasoning was wrong — camel-casing
`"episode_id"` leaves it unchanged, since the strategy lowercases leading characters and doesn't
strip underscores. Rather than argue it either way, a throwaway probe project deserialised a real
payload under five resolver configurations. Evidence settled it in about a minute.

**3. Two Serilog/NSubstitute traps in a row.** A logging assertion failed twice for reasons that had
nothing to do with the code under test: first because `ILogger.Error` is a real interface member in
this version (so NSubstitute records `Error`, not a lower-level `Write`), then because Serilog
overloads `Error` by arity — passing one structured value silently resolves to a *different* member
than the `params object[]` overload being asserted. Both were diagnosed with a probe rather than
guesswork, and the resolution (keep production calls at a fixed arity) is commented in the test so
the next person doesn't repeat it.

**4. A defect that every automated check missed.** All eight `x:Uid` values the agent added used
dotted identifiers (`x:Uid="Films.Error"`). MSBuild succeeded, 66 tests passed — and the error text
rendered **blank** in the running app. The starter's one working `x:Uid` is a single undotted
segment, and the one place dotted resw keys *do* work resolves them explicitly through
`ResourceLoader` in `PageHeaderValueConverter`, never through `x:Uid`. The agent had assumed the two
mechanisms were equivalent. Fixed by exposing the strings as ViewModel properties bound directly,
then **rebuilt, redeployed, relaunched and screenshotted to confirm**. This was only caught because
a human said "run the app" — it is the clearest argument in this project for the constitution's
manual-exercise gate.

**5. Deployment mechanics.** A UWP build produces both a raw compiler output folder and an `AppX\`
deployment layout; registering the wrong one activates the managed exe directly, which routes
through the desktop .NET Framework CLR and fails looking for `System.Private.CoreLib`. The
misleading part is that the exception text points nowhere useful — the real tell is
`mscoreei!InvokeAppXMain` in the stack. Also found: a plain build refreshes the `.msix` but **not**
the loose layout, so a successful build can still launch yesterday's code. All documented in §5.

### Manual corrections and where AI judgement was overridden

- **Committing was kept under human control.** An early attempt by the agent to commit was
  rejected; from then on the agent proposed messages and staged changes only when asked.
- **Cycle-by-cycle review was imposed** after the agent batched two cycles together — later
  formalised as Principle XVI in the constitution.
- **TDD was made mandatory mid-project**, forcing a re-plan. That re-plan's coverage audit found
  three genuinely missing tests (malformed response body, character/film independence, and
  service-level 404) that the pre-TDD task list had not called for.
- **The agent's own analysis pass found its own gaps**: a `/speckit-analyze` run flagged that
  progress-pairing was only asserted against a test probe and never against the real ViewModels —
  meaning a ViewModel that forgot to call `RunBusyAsync` would have passed the entire suite.

### How the final code was validated

1. **78 unit tests**, deterministic and fully offline — `IAPIClient` is substituted and API
   responses are captured real payloads (`snake_case`, exactly as the service returns them), so the
   naming trap in §1 is exercised rather than assumed away.
2. **Test-first throughout.** Every behavioural change began with a failing test, confirmed to fail
   *on behaviour* rather than on a compile error, using a stub-then-assert approach. The commit
   history shows this pairing.
3. **Empirical verification over recall** for anything uncertain: the live API shape, the Newtonsoft
   resolver behaviour, and the Serilog overload resolution were each settled by running code.
4. **The app was built, deployed and run repeatedly**, with screenshots confirming the shell,
   the localized page header, the retry/cancel dialog over a real network failure, and the on-page
   error state with its Retry button.
5. **Cross-artifact analysis** (`/speckit-analyze`) was run repeatedly to check spec, plan and tasks
   stayed consistent; it caught several real inconsistencies, each fixed before implementation.

### Honest gaps

- **XAML has no automated coverage** (§4), which is exactly how the `x:Uid` defect survived to a
  running app.
- **Live public API integration tests are not part of the default test run.** The normal suite stays
  deterministic and offline; a future non-default integration suite could exercise `swapi.info`
  directly without making CI depend on third-party availability.
