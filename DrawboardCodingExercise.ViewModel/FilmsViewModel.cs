using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using DrawboardCodingExercise.Contracts.CoreFramework;
using DrawboardCodingExercise.Contracts.Services;
using DrawboardCodingExercise.Services.StarWars;
using DrawboardCodingExercise.ViewModel.Models;
using Serilog;

namespace DrawboardCodingExercise.ViewModel;

/// <summary>
/// Page 1 - lists all Star Wars films by title and episode number, sorted ascending by episode.
/// </summary>
public partial class FilmsViewModel : PageViewModelBase, INavigateToAware, IProvidePageHeader
{
	private readonly IStarWarsService _starWarsService;
	private readonly ILocalizationService _localizationService;
	private readonly ILogger _logger;

	[ObservableProperty]
	private PageLoadState _state = PageLoadState.Loading;

	public FilmsViewModel(
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

	public ObservableCollection<FilmListItem> Films { get; } = new();

	public string PageHeader => "Films";

	public async Task OnNavigatedToAsync(object parameter)
	{
		// NavigationService.NavigateAsync logs Fatal and rethrows anything that escapes here, so
		// every failure must become a page state rather than propagate (contracts/navigation.md).
		try
		{
			var succeeded = await RunWithRetryAsync(
				_localizationService.Translate("Progress.LoadingFilms.Text"),
				LoadFilmsAsync).ConfigureAwait(true);

			if (!succeeded)
			{
				State = PageLoadState.Error;
			}
		}
		catch (Exception ex)
		{
			_logger.Error(ex, "Unexpected failure while loading the Star Wars film list");
			State = PageLoadState.Error;
		}
	}

	private async Task LoadFilmsAsync()
	{
		var dtos = await _starWarsService.GetFilmsAsync().ConfigureAwait(true);

		var items = dtos
			.Select(dto => FilmMapper.ToListItem(
				dto,
				_localizationService.Translate("Value.NotAvailable.Text"),
				_localizationService.Translate("Film.EpisodeLabel.Text")))
			// FR-003: ascending by episode number, applied here rather than relying on the
			// order the source returns - sorted on the int, never on the display label (M6).
			.OrderBy(item => item.EpisodeNumber)
			.ToList();

		Films.Clear();
		foreach (var item in items)
		{
			Films.Add(item);
		}

		State = items.Count == 0 ? PageLoadState.Empty : PageLoadState.Loaded;
	}
}
