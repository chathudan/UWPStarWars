using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DrawboardCodingExercise.Contracts;
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
	private readonly INavigationService _navigationService;

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(IsLoading))]
	[NotifyPropertyChangedFor(nameof(IsEmpty))]
	[NotifyPropertyChangedFor(nameof(HasError))]
	[NotifyPropertyChangedFor(nameof(IsLoaded))]
	private PageLoadState _state = PageLoadState.Loading;

	// Derived booleans so XAML can bind through the existing BoolToVisibilityConverter without a
	// new enum-to-visibility converter (research.md R8).
	public bool IsLoading => State == PageLoadState.Loading;
	public bool IsEmpty => State == PageLoadState.Empty;
	public bool HasError => State == PageLoadState.Error;
	public bool IsLoaded => State == PageLoadState.Loaded;

	// Resolved via ILocalizationService directly rather than XAML x:Uid: the only proven x:Uid
	// usage in this app (PageA's "NavigatedToPageA") has a single, undotted segment, and the
	// PageHeader convention that DOES use dotted keys resolves them itself via an explicit
	// ResourceLoader call (PageHeaderValueConverter), never through x:Uid's own resolution.
	// Binding directly to a ViewModel property sidesteps that untested x:Uid/dotted-key
	// combination entirely.
	public string EmptyMessage => _localizationService.Translate("Films.Empty.Text");
	public string ErrorMessage => _localizationService.Translate("Films.Error.Text");

	public FilmsViewModel(
		IStarWarsService starWarsService,
		IEventAggregator eventAggregator,
		IUserInteractionService userInteractionService,
		ILocalizationService localizationService,
		ILogger logger,
		INavigationService navigationService)
		: base(eventAggregator, userInteractionService)
	{
		_starWarsService = starWarsService;
		_localizationService = localizationService;
		_logger = logger;
		_navigationService = navigationService;
	}

	public ObservableCollection<FilmListItem> Films { get; } = new();

	public string PageHeader => "Films";

	// The films list takes no navigation parameter, so navigating in and retrying from the page
	// are the same operation.
	public Task OnNavigatedToAsync(object parameter) => LoadAsync();

	private async Task LoadAsync()
	{
		State = PageLoadState.Loading;

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

	/// <summary>
	/// The on-page retry affordance shown in the error state, distinct from the modal
	/// retry/cancel prompt: it re-runs the whole load from scratch (FR-018).
	/// </summary>
	[RelayCommand]
	private Task Retry() => LoadAsync();

	[RelayCommand]
	private Task SelectFilm(FilmListItem film)
	{
		// The id is opaque and comes only from FilmListItem.Id (extracted from the film's `url`
		// by FilmMapper) - it must never be derived from the episode number, since a film's id
		// is its release-order position and has no relationship to its episode number.
		if (film?.Id is null)
		{
			return Task.CompletedTask;
		}

		return _navigationService.NavigateAsync(PageKey.FilmDetails, film.Id);
	}
}
