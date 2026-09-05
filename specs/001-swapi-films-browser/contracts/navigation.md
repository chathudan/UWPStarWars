# Contract: Navigation and page registration

**Assemblies**: `Contracts` (PageKey), `DrawboardCodingExercise` (views, modules), `ViewModel` (view models)

---

## PageKey

```csharp
public enum PageKey
{
    Films,          // Page 1 — the list. Startup page.
    FilmDetails     // Page 2 — the detail view.
}
```

`Welcome` and `PageA` are removed (AC-001, clarification 5). Because `PageKey` is an enum used only as an Autofac resolution key and never persisted, removing members is safe — no stored value can dangle.

## Registration — `NavigationModule`

```csharp
builder.RegisterType<Shell>().AsSelf();            // unchanged — constructed, not navigated to
builder.RegisterType<ShellViewModel>().AsSelf();   // unchanged

builder.RegisterView<Films,       FilmsViewModel>(PageKey.Films);
builder.RegisterView<FilmDetails, FilmDetailsViewModel>(PageKey.FilmDetails);
```

Uses the starter's existing `RegisterView<TView, TViewModel>` extension unchanged. Registrations are **transient** — a fresh ViewModel per navigation, including on back navigation.

## Registration — `WebServicesModule`

```csharp
builder.RegisterInstance(jsonSettings).AsSelf();              // unchanged
builder.RegisterType<APIClient>().As<IAPIClient>();           // unchanged
builder.RegisterType<StarWarsService>().As<IStarWarsService>();  // added
```

`ApplicationConfiguration` is already registered `AsImplementedInterfaces()` in `App.BuildContainer`, so `IAPISettings` resolves into `StarWarsService` with no extra wiring.

## Startup

`ShellViewModel.OnNavigatedToAsync` changes its single navigation target:

```csharp
await _navigationService.NavigateAsync(PageKey.Films).ConfigureAwait(true);
```

This is the only change to `ShellViewModel`. Its busy/done subscriptions and back-command wiring are untouched.

---

## Navigation parameter contract

| Direction | Parameter | Type |
|---|---|---|
| Shell → `Films` | `null` | — |
| `Films` → `FilmDetails` | the selected film's opaque id | `string` |

```csharp
[RelayCommand]
private async Task OnSelectFilm(FilmListItem film)
{
    if (film?.Id is null) return;                 // defensive: unopenable rows never navigate
    await _navigationService.NavigateAsync(PageKey.FilmDetails, film.Id);
}
```

**A `string`, not a `FilmListItem`.** UWP serialises navigation parameters as part of frame state for suspend/resume; an object graph does not survive that round trip, a string does. This was clarification 3's deciding argument.

### Parameter validation in `FilmDetailsViewModel`

Validation runs **before any request** (V1, V2, FR-013):

| Received | Result |
|---|---|
| `null` | `InvalidSelection`, no request issued |
| not a `string` | `InvalidSelection`, no request issued |
| empty / whitespace | `InvalidSelection`, no request issued |
| a `string` the source doesn't know | request issued → 404 → `InvalidSelection`, **no retry offered** |
| a valid `string` | `Loading` → `Loaded` |

`NavigationService` passes the **raw** parameter to `OnNavigatedToAsync` (it wraps it in `NavigationDetails` only for the frame's own back-stack), so the ViewModel receives the `string` directly.

---

## Two starter behaviours that constrain the design

Both were found by reading `NavigationService`, and both are load-bearing.

### 1. `OnNavigatedToAsync` must never throw

```csharp
catch (Exception e)
{
    logger.Fatal(e, "A page of type {PageKey} did not properly handle navigation", pageKey);
    throw;   // ← propagates out of the shell's navigation call
}
```

[NavigationService.cs:92-97](../../DrawboardCodingExercise/CoreFramework/NavigationService.cs#L92-L97)

An exception escaping a ViewModel's load is logged `Fatal` and rethrown into the caller. Both ViewModels therefore guard their **entire** `OnNavigatedToAsync` body and convert every failure into a `PageLoadState`. This is asserted by a test — `OnNavigatedToAsync` is invoked with a service substitute that throws, and the test requires the call to complete normally with `HasError` set.

### 2. Back navigation re-runs the load

```csharp
var viewModel = _componentContext.ResolveOptionalKeyed<ObservableObject>(pageKey);
_frame.GoBack();
...
if (viewModel is INavigateToAware navigateToAware)
    await navigateToAware.OnNavigatedToAsync(navigationDetails.Parameter);
```

[NavigationService.cs:110-125](../../DrawboardCodingExercise/CoreFramework/NavigationService.cs#L110-L125)

`BackAsync` resolves a **fresh** ViewModel (registrations are transient) and calls `OnNavigatedToAsync` again with the original parameter. Returning from the detail page therefore genuinely re-retrieves the film list.

Consequences the implementation must respect:
- Loading, empty and error states must be correct on **re-entry**, not only on first navigation.
- No ViewModel may assume it is loaded at most once.
- The busy/done pairing must survive repeated loads — a leaked busy event on the first visit would keep the shell's progress ring spinning forever after the second.

The spec's Story 2 acceptance scenario 4 was reworded during clarification to expect this reload rather than promise a cached list.

## Back command

Untouched. `ShellViewModel.GoBackCommand` is bound in `Shell.xaml` and driven by `INavigationService.CanGoBack` / the `Navigated` event. Neither new page implements its own back affordance; the detail page's invalid-selection state offers a way back by invoking the same shell command path.

## Page headers

Both pages implement `IProvidePageHeader`. `PageHeaderValueConverter` resolves `PageHeader/{PageHeader}/Text` from `Resources.resw`, so the property returns a **key fragment**, not display text:

| ViewModel | `PageHeader` | resw key |
|---|---|---|
| `FilmsViewModel` | `"Films"` | `PageHeader.Films.Text` |
| `FilmDetailsViewModel` | `"FilmDetails"` | `PageHeader.FilmDetails.Text` |

The starter's `PageHeader.Welcome.Text` and `PageHeader.PageA.Text` entries are removed alongside their pages (FR-026).

---

## UWP project file — easy to miss

`DrawboardCodingExercise.csproj` is **old-style MSBuild**, not SDK-style. Files are not globbed. Every added or removed page needs matching edits:

```xml
<!-- ItemGroup with the other <Compile> entries -->
<Compile Include="View\Films.xaml.cs">
  <DependentUpon>Films.xaml</DependentUpon>
</Compile>
<Compile Include="View\FilmDetails.xaml.cs">
  <DependentUpon>FilmDetails.xaml</DependentUpon>
</Compile>

<!-- ItemGroup with the other <Page> entries -->
<Page Include="View\Films.xaml">
  <SubType>Designer</SubType>
  <Generator>MSBuild:Compile</Generator>
</Page>
<Page Include="View\FilmDetails.xaml">
  <SubType>Designer</SubType>
  <Generator>MSBuild:Compile</Generator>
</Page>
```

and the corresponding `View\Welcome.xaml*` / `View\PageA.xaml*` entries deleted.

Omitting a `<Page>` entry is the nastier of the two failure modes: the project still builds, and the page fails at runtime with a XAML parse or missing-type error. Visual Studio does this automatically when files are added through the IDE; edits made directly on disk do not.
