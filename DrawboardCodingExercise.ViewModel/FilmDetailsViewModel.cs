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
/// number, release date, director, producer, opening crawl and characters.
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

	// The character section carries its OWN state, independent of the film's, so a character
	// failure never blanks the film's own fields (FR-011, FR-012).
	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(IsCharactersLoading))]
	[NotifyPropertyChangedFor(nameof(IsCharactersEmpty))]
	[NotifyPropertyChangedFor(nameof(HasCharactersError))]
	[NotifyPropertyChangedFor(nameof(IsCharactersLoaded))]
	private PageLoadState _charactersState = PageLoadState.Loading;

	[ObservableProperty]
	private bool _hasPartialCharacterFailure;

	public ObservableCollection<CharacterListItem> Characters { get; } = new();

	public bool IsCharactersLoading => CharactersState == PageLoadState.Loading;
	public bool IsCharactersEmpty => CharactersState == PageLoadState.Empty;
	public bool HasCharactersError => CharactersState == PageLoadState.Error;
	public bool IsCharactersLoaded => CharactersState == PageLoadState.Loaded;

	// Derived booleans so XAML can bind through the existing BoolToVisibilityConverter without a
	// new enum-to-visibility converter (research.md R8).
	public bool IsLoading => FilmState == PageLoadState.Loading;
	public bool HasError => FilmState == PageLoadState.Error;
	public bool IsInvalidSelection => FilmState == PageLoadState.InvalidSelection;
	public bool IsLoaded => FilmState == PageLoadState.Loaded;

	// Resolved via ILocalizationService directly rather than XAML x:Uid - see the identical
	// note on FilmsViewModel.EmptyMessage/ErrorMessage for why.
	public string InvalidSelectionMessage => _localizationService.Translate("FilmDetails.InvalidSelection.Text");
	public string ErrorMessage => _localizationService.Translate("FilmDetails.Error.Text");
	public string ReleaseDateLabel => _localizationService.Translate("FilmDetails.ReleaseDate.Label");
	public string DirectorLabel => _localizationService.Translate("FilmDetails.Director.Label");
	public string ProducerLabel => _localizationService.Translate("FilmDetails.Producer.Label");
	public string OpeningCrawlLabel => _localizationService.Translate("FilmDetails.OpeningCrawl.Label");
	public string CharactersLabel => _localizationService.Translate("FilmDetails.Characters.Label");
	public string CharactersEmptyMessage => _localizationService.Translate("FilmDetails.Characters.Empty.Text");
	public string CharactersErrorMessage => _localizationService.Translate("FilmDetails.Characters.Error.Text");
	public string CharactersPartialMessage => _localizationService.Translate("FilmDetails.Characters.Partial.Text");
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
		return LoadFilmAndCharactersAsync(filmId);
	}

	private async Task LoadFilmAndCharactersAsync(string filmId)
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

			// Characters load only once the film has resolved, and as a separate operation with
			// its own state and its own progress pair - so a character failure cannot take the
			// film's own details down with it (FR-011).
			if (FilmState == PageLoadState.Loaded)
			{
				await LoadCharactersAsync(Film!.CharacterUrls).ConfigureAwait(true);
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

	/// <summary>On-page retry for the film itself, shown in the film's error state (FR-018).</summary>
	[RelayCommand]
	private Task RetryFilm() =>
		_filmId is null ? Task.CompletedTask : LoadFilmAndCharactersAsync(_filmId);

	/// <summary>
	/// On-page retry for the character section only. Deliberately does NOT re-request the film:
	/// it is already loaded and on screen, so re-fetching it would be wasted work and a visible
	/// flicker for the user.
	/// </summary>
	[RelayCommand]
	private Task RetryCharacters() =>
		Film is null ? Task.CompletedTask : LoadCharactersAsync(Film.CharacterUrls);

	private async Task LoadCharactersAsync(IReadOnlyList<string> characterUrls)
	{
		CharactersState = PageLoadState.Loading;

		try
		{
			// A distinct progress message from the film load's: ShellViewModel removes done
			// events by exact string match, so two concurrent operations sharing a message
			// would cancel each other out of its progress list (AC-009).
			var succeeded = await RunWithRetryAsync(
				_localizationService.Translate("Progress.LoadingCharacters.Text"),
				() => FetchCharactersAsync(characterUrls)).ConfigureAwait(true);

			if (!succeeded)
			{
				CharactersState = PageLoadState.Error;
			}
		}
		catch (Exception ex)
		{
			// Never propagates: the film's own details stay on screen regardless (FR-011).
			_logger.Error(ex, "Unexpected failure while loading characters for a Star Wars film");
			CharactersState = PageLoadState.Error;
		}
	}

	private async Task FetchCharactersAsync(IReadOnlyList<string> characterUrls)
	{
		var result = await _starWarsService.GetCharactersAsync(characterUrls).ConfigureAwait(true);

		var items = result.Characters
			.Select(dto => FilmMapper.ToCharacterListItem(
				dto,
				_localizationService.Translate("Value.NotAvailable.Text")))
			.ToList();

		Characters.Clear();
		foreach (var item in items)
		{
			Characters.Add(item);
		}

		// Successes are kept and the shortfall reported, rather than a partial batch being
		// discarded or silently passed off as complete (FR-012).
		HasPartialCharacterFailure = result.IsPartial;
		CharactersState = items.Count == 0 ? PageLoadState.Empty : PageLoadState.Loaded;
	}
}
