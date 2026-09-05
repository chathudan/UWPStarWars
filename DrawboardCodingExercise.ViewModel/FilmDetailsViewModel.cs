using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DrawboardCodingExercise.Contracts.CoreFramework;
using DrawboardCodingExercise.Contracts.Services;
using DrawboardCodingExercise.Services.StarWars;
using DrawboardCodingExercise.ViewModel.Models;
using Serilog;

namespace DrawboardCodingExercise.ViewModel;

/// <summary>
/// Page 2 - resolves the selected film from its opaque identifier and shows its title, episode
/// number, release date, director, producer, opening crawl and its five related categories.
/// </summary>
public partial class FilmDetailsViewModel : PageViewModelBase, INavigateToAware, IProvidePageHeader
{
	private readonly IStarWarsService _starWarsService;
	private readonly ILocalizationService _localizationService;
	private readonly ILogger _logger;

	private string? _filmId;

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(IsLoading))]
	[NotifyPropertyChangedFor(nameof(HasError))]
	[NotifyPropertyChangedFor(nameof(IsInvalidSelection))]
	[NotifyPropertyChangedFor(nameof(IsLoaded))]
	private PageLoadState _filmState = PageLoadState.Loading;

	[ObservableProperty]
	private FilmDetailsDisplay? _film;

	/// <summary>
	/// The five related-category sections, in RelatedCategory declaration order. Each carries its
	/// OWN state, independent of the film's and of the other four, so one section failing or
	/// being empty never blanks the film's fields or disturbs its siblings (FR-011, FR-012).
	/// </summary>
	public ObservableCollection<RelatedCategorySection> Categories { get; } = new();

	// Derived booleans so XAML can bind through the existing BoolToVisibilityConverter without a
	// new enum-to-visibility converter (research.md R8).
	public bool IsLoading => FilmState == PageLoadState.Loading;
	public bool HasError => FilmState == PageLoadState.Error;
	public bool IsInvalidSelection => FilmState == PageLoadState.InvalidSelection;
	public bool IsLoaded => FilmState == PageLoadState.Loaded;

	// Resolved via ILocalizationService directly rather than XAML x:Uid - see the identical
	// note on FilmsViewModel.EmptyMessage/ErrorMessage for why.
	public string LoadingMessage => _localizationService.Translate("Progress.LoadingFilmDetails.Text");
	public string InvalidSelectionMessage => _localizationService.Translate("FilmDetails.InvalidSelection.Text");
	public string ErrorMessage => _localizationService.Translate("FilmDetails.Error.Text");
	public string ReleaseDateLabel => _localizationService.Translate("FilmDetails.ReleaseDate.Label");
	public string DirectorLabel => _localizationService.Translate("FilmDetails.Director.Label");
	public string ProducerLabel => _localizationService.Translate("FilmDetails.Producer.Label");
	public string OpeningCrawlLabel => _localizationService.Translate("FilmDetails.OpeningCrawl.Label");
	public string RelatedLabel => _localizationService.Translate("FilmDetails.Related.Label");
	public string RetryLabel => _localizationService.Translate("Common.RetryButton.Content");

	public FilmDetailsViewModel(
		IStarWarsService starWarsService,
		IEventAggregator eventAggregator,
		IUserInteractionService userInteractionService,
		ILocalizationService localizationService,
		ILogger logger)
		: base(eventAggregator, userInteractionService)
	{
		_starWarsService = starWarsService;
		_localizationService = localizationService;
		_logger = logger;
	}

	public string PageHeader => "FilmDetails";

	public Task OnNavigatedToAsync(object parameter)
	{
		// V1, V2: a missing, non-string, or blank identifier is invalid and must NOT issue a
		// request - a broken navigation call must never generate network traffic (FR-013).
		if (!(parameter is string filmId) || string.IsNullOrWhiteSpace(filmId))
		{
			FilmState = PageLoadState.InvalidSelection;
			return Task.CompletedTask;
		}

		// Retained so the on-page retry can reload without a navigation parameter to hand.
		_filmId = filmId;
		return LoadFilmAndCategoriesAsync(filmId);
	}

	private async Task LoadFilmAndCategoriesAsync(string filmId)
	{
		FilmState = PageLoadState.Loading;

		// NavigationService.NavigateAsync logs Fatal and rethrows anything that escapes here, so
		// every failure must become a page state rather than propagate (contracts/navigation.md).
		try
		{
			var succeeded = await RunWithRetryAsync(
				_localizationService.Translate("Progress.LoadingFilmDetails.Text"),
				() => LoadFilmAsync(filmId)).ConfigureAwait(true);

			if (!succeeded)
			{
				FilmState = PageLoadState.Error;
				return;
			}

			// The sections are built only once the film has resolved, because the film's own
			// response is what supplies their reference URLs and their entry counts.
			if (FilmState == PageLoadState.Loaded)
			{
				BuildCategories();

				// FR-028: Characters opens and loads on arrival - it is the category the brief
				// asks for, and it keeps the page's opening cost the same as before this page
				// grew from one section to five. The other four wait to be asked for; loading
				// all five here would mean up to 38 requests for a single film, most of them
				// for sections the user never scrolls to.
				await ExpandCharactersAsync().ConfigureAwait(true);
			}
		}
		catch (Exception ex)
		{
			_logger.Error(ex, "Unexpected failure while loading Star Wars film {FilmId}", filmId);
			FilmState = PageLoadState.Error;
		}
	}

	private async Task LoadFilmAsync(string filmId)
	{
		var dto = await _starWarsService.GetFilmAsync(filmId).ConfigureAwait(true);

		if (dto is null)
		{
			// A 404 means the identifier is wrong; no retry can fix that (FR-013, V4). This
			// completes the operation normally rather than throwing, so RunWithRetryAsync never
			// prompts retry/cancel for it (T10).
			FilmState = PageLoadState.InvalidSelection;
			return;
		}

		Film = FilmMapper.ToDetailsDisplay(
			dto,
			_localizationService.Translate("Value.NotAvailable.Text"),
			numeral => _localizationService.Translate("Film.EpisodeLabel.Text", numeral));
		FilmState = PageLoadState.Loaded;
	}

	// Driven from the enum, not from whichever URL lists happen to be non-empty, so the page
	// always shows five sections - including the empty ones, whose "0" is itself information.
	private void BuildCategories()
	{
		Categories.Clear();

		foreach (RelatedCategory category in Enum.GetValues(typeof(RelatedCategory)))
		{
			Categories.Add(new RelatedCategorySection(
				category,
				Film!.RelatedUrls[category],
				_localizationService,
				LoadSectionAsync));
		}
	}

	private Task ExpandCharactersAsync()
	{
		var characters = Categories.FirstOrDefault(section => section.Category == RelatedCategory.Characters);
		return characters is null ? Task.CompletedTask : characters.ExpandAsync();
	}

	/// <summary>
	/// How a section loads. The section decides *when* to call this; the ViewModel owns the
	/// progress pairing and the retry/cancel loop, because both live in PageViewModelBase and
	/// duplicating them per section would have been five chances to get the pairing wrong.
	/// </summary>
	private async Task LoadSectionAsync(RelatedCategorySection section)
	{
		section.State = PageLoadState.Loading;

		try
		{
			// section.LoadingMessage names this section's own category, so five sections loading
			// at once produce five distinct progress entries. ShellViewModel removes done events
			// by exact string match, so sharing one message would have them cancel each other
			// out of its progress list (AC-009, V14).
			var succeeded = await RunWithRetryAsync(
				section.LoadingMessage,
				() => FetchSectionAsync(section)).ConfigureAwait(true);

			if (!succeeded)
			{
				section.State = PageLoadState.Error;
			}
		}
		catch (Exception ex)
		{
			// Never propagates: the film's own details and the other four sections stay on
			// screen regardless (FR-011, FR-012).
			_logger.Error(ex, "Unexpected failure while loading the {Category} section of a Star Wars film", section.Category);
			section.State = PageLoadState.Error;
		}
	}

	private async Task FetchSectionAsync(RelatedCategorySection section)
	{
		var result = await _starWarsService.GetRelatedResourcesAsync(section.Urls).ConfigureAwait(true);

		var items = result.Resources
			.Select(dto => FilmMapper.ToRelatedResourceListItem(
				dto,
				_localizationService.Translate("Value.NotAvailable.Text")))
			.ToList();

		section.ApplyResult(items, result.IsPartial);
	}

	/// <summary>On-page retry for the film itself, shown in the film's error state (FR-018).</summary>
	[RelayCommand]
	private Task RetryFilm() =>
		_filmId is null ? Task.CompletedTask : LoadFilmAndCategoriesAsync(_filmId);
}
