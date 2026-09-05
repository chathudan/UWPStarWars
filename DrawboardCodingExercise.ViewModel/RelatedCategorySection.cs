using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DrawboardCodingExercise.Contracts.Services;
using DrawboardCodingExercise.ViewModel.Models;

namespace DrawboardCodingExercise.ViewModel;

/// <summary>
/// One expandable related-category section on the FilmDetails page.
///
/// This owns a live state machine rather than a formatted snapshot, so unlike the display models
/// in Models/ it is mutable and observable. There are five instances per film, and each is
/// entirely independent: one failing, being empty, or still loading says nothing about the other
/// four (FR-012).
///
/// It deliberately does NOT hold IStarWarsService. The section owns *when* to load - the
/// load-once rule and the expansion trigger; the owning ViewModel owns *how*, because loading
/// means progress events and retry/cancel, and those live in PageViewModelBase. Handing the
/// section a service would have duplicated that machinery five times over.
/// </summary>
public partial class RelatedCategorySection : ObservableObject
{
	private readonly Func<RelatedCategorySection, Task> _loadAsync;

	public RelatedCategorySection(
		RelatedCategory category,
		IReadOnlyList<string> urls,
		ILocalizationService localizationService,
		Func<RelatedCategorySection, Task> loadAsync)
	{
		Category = category;
		Urls = urls ?? Array.Empty<string>();
		_loadAsync = loadAsync;

		Title = localizationService.Translate(TitleKeyFor(category));
		EmptyMessage = localizationService.Translate("FilmDetails.Category.Empty.Text");
		ErrorMessage = localizationService.Translate("FilmDetails.Category.Error.Text");
		PartialMessage = localizationService.Translate("FilmDetails.Category.Partial.Text");
		RetryLabel = localizationService.Translate("Common.RetryButton.Content");

		// The progress message names this section's own category, so five sections loading at
		// once produce five distinct strings. ShellViewModel.OnNotifyDone removes progress
		// entries by exact string match with no -1 guard, so a shared message would have one
		// section's done event remove another's entry and the next call RemoveAt(-1) - an
		// ArgumentOutOfRangeException on the UI thread (V14).
		LoadingMessage = localizationService.Translate("Progress.LoadingCategory.Text", Title);
	}

	public RelatedCategory Category { get; }

	/// <summary>The film's reference URLs for this category, in the order the film lists them.</summary>
	public IReadOnlyList<string> Urls { get; }

	/// <summary>
	/// How many entries the film references. Taken from the film's own response, so it is shown
	/// in the header before this section has ever been loaded.
	/// </summary>
	public int Count => Urls.Count;

	public string Title { get; }
	public string LoadingMessage { get; }
	public string EmptyMessage { get; }
	public string ErrorMessage { get; }
	public string PartialMessage { get; }
	public string RetryLabel { get; }

	public ObservableCollection<RelatedResourceListItem> Items { get; } = new();

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(IsLoading))]
	[NotifyPropertyChangedFor(nameof(IsLoaded))]
	[NotifyPropertyChangedFor(nameof(IsEmpty))]
	[NotifyPropertyChangedFor(nameof(HasError))]
	private PageLoadState _state = PageLoadState.NotStarted;

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(IsCollapsed))]
	private bool _isExpanded;

	[ObservableProperty]
	private bool _hasPartialFailure;

	/// <summary>
	/// Whether a load has already completed successfully. Guards FR-028: re-expanding a section
	/// shows what is already there rather than requesting it again.
	/// </summary>
	public bool HasBeenLoaded { get; private set; }

	// Derived booleans so XAML binds through the starter's existing BoolToVisibilityConverter,
	// with no enum-to-visibility converter needed (research.md R8).
	public bool IsLoading => State == PageLoadState.Loading;
	public bool IsLoaded => State == PageLoadState.Loaded;
	public bool IsEmpty => State == PageLoadState.Empty;
	public bool HasError => State == PageLoadState.Error;
	public bool IsCollapsed => !IsExpanded;

	[RelayCommand]
	private Task Toggle()
	{
		IsExpanded = !IsExpanded;
		return IsExpanded ? EnsureLoadedAsync() : Task.CompletedTask;
	}

	/// <summary>The section-local retry, which reloads THIS section and nothing else (FR-012).</summary>
	[RelayCommand]
	private Task Retry() => _loadAsync(this);

	/// <summary>Expands the section and loads it if it has not been loaded already.</summary>
	public Task ExpandAsync()
	{
		IsExpanded = true;
		return EnsureLoadedAsync();
	}

	/// <summary>
	/// Loads this section unless it already has what it needs (FR-028, V13). Three separate
	/// reasons NOT to issue a request, each deliberate:
	///
	/// - Already loaded: re-expanding shows what is there. Re-requesting would be invisible on
	///   screen and cost a full round of requests every time the user toggled a section.
	/// - Already failed: only the section's own Retry may re-attempt. Otherwise idly toggling a
	///   broken section would hammer a failing endpoint and re-raise the retry/cancel prompt on
	///   a gesture the user never meant as a retry.
	/// - Nothing to fetch: a film that references no vehicles is Empty, and asking the service
	///   for zero URLs would be a pointless round trip.
	/// </summary>
	public Task EnsureLoadedAsync()
	{
		if (HasBeenLoaded || State == PageLoadState.Error)
		{
			return Task.CompletedTask;
		}

		if (Urls.Count == 0)
		{
			ApplyResult(Array.Empty<RelatedResourceListItem>(), isPartial: false);
			return Task.CompletedTask;
		}

		return _loadAsync(this);
	}

	/// <summary>
	/// Applies a completed load. An empty result is Empty, not Error - a film that genuinely
	/// references nothing in this category is a success with nothing to show (FR-012).
	/// </summary>
	public void ApplyResult(IReadOnlyList<RelatedResourceListItem> items, bool isPartial)
	{
		Items.Clear();
		foreach (var item in items)
		{
			Items.Add(item);
		}

		// Successes are kept and the shortfall reported, rather than a partial batch being
		// discarded or silently passed off as complete (FR-012).
		HasPartialFailure = isPartial;
		State = items.Count == 0 ? PageLoadState.Empty : PageLoadState.Loaded;
		HasBeenLoaded = true;
	}

	// An explicit switch rather than a key built from the enum name: a resw key that is only
	// ever assembled at runtime cannot be found by searching the resource file, and a missing
	// one renders as "[Bracketed.Key]" rather than failing.
	private static string TitleKeyFor(RelatedCategory category)
	{
		switch (category)
		{
			case RelatedCategory.Characters: return "FilmDetails.Category.Characters.Label";
			case RelatedCategory.Planets: return "FilmDetails.Category.Planets.Label";
			case RelatedCategory.Starships: return "FilmDetails.Category.Starships.Label";
			case RelatedCategory.Vehicles: return "FilmDetails.Category.Vehicles.Label";
			case RelatedCategory.Species: return "FilmDetails.Category.Species.Label";
			default:
				throw new ArgumentOutOfRangeException(
					nameof(category), category, "No localized title key is mapped for this related category.");
		}
	}
}
