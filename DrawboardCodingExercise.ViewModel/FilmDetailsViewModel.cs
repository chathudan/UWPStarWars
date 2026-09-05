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
	private PageLoadState _filmState = PageLoadState.Loading;

	[ObservableProperty]
	private FilmDetailsDisplay? _film;

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

		throw new NotImplementedException();
	}
}
