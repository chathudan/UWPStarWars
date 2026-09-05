using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
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

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(IsLoading))]
	[NotifyPropertyChangedFor(nameof(HasError))]
	[NotifyPropertyChangedFor(nameof(IsInvalidSelection))]
	[NotifyPropertyChangedFor(nameof(IsLoaded))]
	private PageLoadState _filmState = PageLoadState.Loading;

	[ObservableProperty]
	private FilmDetailsDisplay? _film;

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

	public async Task OnNavigatedToAsync(object parameter)
	{
		// V1, V2: a missing, non-string, or blank identifier is invalid and must NOT issue a
		// request - a broken navigation call must never generate network traffic (FR-013).
		if (!(parameter is string filmId) || string.IsNullOrWhiteSpace(filmId))
		{
			FilmState = PageLoadState.InvalidSelection;
			return;
		}

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
			_localizationService.Translate("Film.EpisodeLabel.Text"));
		FilmState = PageLoadState.Loaded;
	}
}
