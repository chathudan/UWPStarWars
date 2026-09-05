# Drawboard UWP Coding Exercise Constitution

## I. Extend the Provided Application

The solution MUST extend the supplied UWP application unless technically impossible.

New functionality MUST fit the existing project structure:

- UWP views in `DrawboardCodingExercise/View`
- ViewModels in `DrawboardCodingExercise.ViewModel`
- API and infrastructure services in `DrawboardCodingExercise.Services`
- shared contracts in `DrawboardCodingExercise.Contracts`
- app wiring in Autofac modules under `DrawboardCodingExercise/Module`

The solution MUST NOT replace the starter architecture with a new framework, new app shell, or unrelated architectural pattern.

## II. SOLID and Proportionate Design

Production code MUST follow SOLID principles.

- Single Responsibility: Views render UI, ViewModels coordinate UI state, services perform API/business operations, DTOs model API data.
- Open/Closed: New functionality extends existing modules and abstractions without rewriting framework services.
- Liskov Substitution: Interfaces must be mockable and safely replaceable in tests.
- Interface Segregation: Interfaces must stay small and purpose-specific.
- Dependency Inversion: ViewModels depend on abstractions, not concrete HTTP, dialogs, navigation, or threading.

SOLID MUST be applied proportionately. Do not introduce repositories, CQRS, MediatR, generic SDK layers, or extra projects unless they solve a real problem in this exercise.

## III. MVVM and UWP Conventions

ViewModels MUST inherit from `ObservableObject`.

Use `CommunityToolkit.Mvvm` for observable properties and commands.

Views MUST avoid business logic and API calls in code-behind.

Page data loading SHOULD happen through `INavigateToAware.OnNavigatedToAsync`.

ViewModels SHOULD expose explicit states for loading, loaded, empty, error, and invalid navigation parameter scenarios.

## IV. Dependency Injection

All services and view models MUST be resolved through Autofac.

New pages MUST be registered in `NavigationModule`.

New API services MUST be registered in `WebServicesModule` or an equivalent Autofac module.

Constructor injection is required for dependencies.

Manual service construction inside ViewModels or views is forbidden.

## V. Navigation

Navigation MUST use `INavigationService`.

New pages MUST have a `PageKey`.

Detail pages MUST receive deliberate navigation parameters, such as a selected film DTO or stable API URL/id.

Invalid or missing navigation parameters MUST be handled safely and visibly.

Back navigation MUST continue to work through the existing shell command.

## VI. API Integration

API access MUST go through `IAPIClient`.

ViewModels and views MUST NOT use `HttpClient` directly.

The solution MUST NOT use NuGet packages that directly wrap SWAPI or MET APIs.

API-specific logic MUST live behind an app-defined abstraction, for example `IStarWarsService`.

API DTOs MUST be separated from ViewModel display models when formatting, sorting, or UI-specific state is required.

The API layer MUST handle:

- non-success status codes
- network failures
- null or malformed responses
- empty result sets
- partial optional fields

## VII. Error Handling and User Reporting

Recoverable API failures MUST be reported to the user.

`IUserInteractionService` SHOULD be used for retry/cancel flows.

Errors MUST be logged with useful context.

The app MUST NOT fail silently or leave the user on a permanent loading state.

Empty data and invalid detail selections MUST have clear user-facing states.

## VIII. Busy and Progress Events

Long-running operations MUST publish `NotifyBusyEvent`.

The matching `NotifyDoneEvent` MUST be published in `finally`.

The busy and done event strings MUST match exactly.

Progress MUST clear after success, failure, retry cancel, or unexpected exception.

## IX. Localization

User-facing strings MUST be localizable.

Use `Resources.resw`, `x:Uid`, `ILocalizationService`, or existing page-header localization conventions.

Hard-coded user-facing strings are allowed only for temporary design-time text or test data.

## X. Logging

Use Serilog for diagnostics.

Navigation and API operations SHOULD include contextual logs.

Logs SHOULD help explain failures during review without exposing unnecessary payload data.

## XI. Automated Testing

The solution MUST include meaningful automated tests.

Tests SHOULD cover:

- list ViewModel successful load
- detail ViewModel successful load
- navigation command behavior
- invalid navigation parameter handling
- empty API responses
- API failure retry/cancel behavior
- DTO mapping
- malformed or partial API response handling

Unit tests MUST NOT depend on the live SWAPI or MET service.

Use xUnit, Shouldly, and NSubstitute unless there is a clear reason not to.

## XII. UWP Compatibility

Shared projects MUST remain compatible with `netstandard2.0`.

The UWP app MUST remain compatible with the configured Windows SDK and UWP runtime constraints.

New packages MUST be checked for UWP compatibility before use.

PolySharp may be used for modern C# language support where runtime APIs remain compatible.

## XIII. README and Interview Readiness

The submitted solution MUST include a README or equivalent documentation explaining:

- chosen API
- assumptions
- limitations
- how to build and run
- how to run tests
- error handling approach
- architecture decisions
- possible future extensions

If AI assistance was used, the README MUST also explain:

- what AI helped produce
- challenges the AI faced
- how issues were corrected
- how the final code was validated

## XIV. Validation Gates

A change is not complete until:

- the solution builds
- automated tests pass
- TDD evidence exists for non-trivial production behavior
- both pages can be manually exercised
- API success and failure paths are checked
- navigation and back navigation work
- loading/progress state clears correctly
- README is updated

## XV. Test-Driven Development

All non-trivial production behavior MUST be developed test-first.

Before implementing ViewModel logic, API service behavior, mapping logic, navigation decisions, retry/cancel behavior, error-state handling, empty-state handling, or busy/progress cleanup, a failing automated test MUST be written or updated to describe the expected behavior.

Implementation MUST follow the red-green-refactor cycle:

1. Red: write a focused failing test for the behavior.
2. Green: implement the smallest production change that makes the test pass.
3. Refactor: improve structure, naming, and duplication while keeping tests passing.

TDD is mandatory for:

- API service response handling
- DTO-to-domain or DTO-to-display mapping
- film list sorting
- ViewModel loading, loaded, empty, and error states
- film selection navigation
- detail page navigation parameter handling
- retry/cancel API failure behavior
- `NotifyBusyEvent` / `NotifyDoneEvent` cleanup
- malformed, null, partial, or empty API responses

Pure XAML layout changes, resource text changes, README updates, app manifest changes, and mechanical Autofac/PageKey registrations MAY be implemented without test-first development, but they MUST still be manually validated.

No production behavior is complete unless it has a corresponding automated test or a documented reason why automated testing is not practical in this UWP exercise.
