using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DrawboardCodingExercise.Contracts;
using DrawboardCodingExercise.Contracts.CoreFramework;
using DrawboardCodingExercise.Contracts.Services;
using DrawboardCodingExercise.Services.StarWars;
using DrawboardCodingExercise.Services.StarWars.Dtos;
using DrawboardCodingExercise.Services.UnitTests.TestDoubles;
using DrawboardCodingExercise.ViewModel;
using NSubstitute;
using Serilog;
using Shouldly;
using Xunit;

namespace DrawboardCodingExercise.Services.UnitTests.ViewModels;

/// <summary>
/// T1, T3, T17, and the real-ViewModel half of T13/T16: the film list loads, an empty response
/// is a distinct state from an error, OnNavigatedToAsync never propagates an exception, and a
/// load posts a matched busy/done pair through the real ViewModel - not just the base class.
/// </summary>
public class FilmsViewModelTests
{
	private static FilmDto Film(string title, int episodeId, string urlId) => new FilmDto
	{
		Title = title,
		EpisodeId = episodeId,
		Director = "Someone",
		Producer = "Someone Else",
		OpeningCrawl = "Once upon a time...",
		ReleaseDate = "1977-05-25",
		Characters = new List<string>(),
		Url = $"https://swapi.info/api/films/{urlId}"
	};

	private static FilmsViewModel CreateSut(
		IStarWarsService starWarsService = null,
		IEventAggregator eventAggregator = null,
		IUserInteractionService userInteractionService = null,
		ILocalizationService localizationService = null,
		INavigationService navigationService = null)
	{
		return new FilmsViewModel(
			starWarsService ?? Substitute.For<IStarWarsService>(),
			eventAggregator ?? new RecordingEventAggregator(),
			userInteractionService ?? Substitute.For<IUserInteractionService>(),
			localizationService ?? new EchoLocalizationService(),
			Substitute.For<ILogger>(),
			navigationService ?? Substitute.For<INavigationService>());
	}

	// T1: a successful load exposes every returned film.
	[Fact]
	public async Task OnNavigatedToAsync_loads_films_and_exposes_them()
	{
		var service = Substitute.For<IStarWarsService>();
		service.GetFilmsAsync().Returns(new List<FilmDto>
		{
			Film("A New Hope", 4, "1"),
			Film("The Empire Strikes Back", 5, "2")
		});
		var sut = CreateSut(service);

		await sut.OnNavigatedToAsync(null);

		sut.State.ShouldBe(PageLoadState.Loaded);
		sut.Films.Count.ShouldBe(2);
	}

	// T3: an empty response is a distinct, explicit state - not the same as a failure.
	[Fact]
	public async Task OnNavigatedToAsync_shows_the_empty_state_for_an_empty_response()
	{
		var service = Substitute.For<IStarWarsService>();
		service.GetFilmsAsync().Returns(new List<FilmDto>());
		var sut = CreateSut(service);

		await sut.OnNavigatedToAsync(null);

		sut.State.ShouldBe(PageLoadState.Empty);
	}

	// T17: NavigationService.NavigateAsync logs Fatal and rethrows anything that escapes
	// OnNavigatedToAsync, so it must never propagate - every failure becomes a page state.
	[Fact]
	public async Task OnNavigatedToAsync_never_propagates_an_exception()
	{
		var service = Substitute.For<IStarWarsService>();
		service.GetFilmsAsync().Returns<Task<IReadOnlyList<FilmDto>>>(_ => throw new InvalidOperationException("boom"));
		var userInteraction = Substitute.For<IUserInteractionService>();
		userInteraction.ShowRetryDialogAsync().Returns(RetryDialogResult.Cancel);
		var sut = CreateSut(service, userInteractionService: userInteraction);

		await Should.NotThrowAsync(() => sut.OnNavigatedToAsync(null));

		sut.State.ShouldBe(PageLoadState.Error);
	}

	// Real-ViewModel half of T13/T16: FilmsViewModel must actually call RunBusyAsync, not just
	// inherit it unused. A ViewModel that forgot to bracket its load would pass every other test
	// here while leaving the shell's progress ring spinning forever.
	[Fact]
	public async Task OnNavigatedToAsync_posts_exactly_one_matched_busy_and_done_pair_on_success()
	{
		var aggregator = new RecordingEventAggregator();
		var service = Substitute.For<IStarWarsService>();
		service.GetFilmsAsync().Returns(new List<FilmDto> { Film("A New Hope", 4, "1") });
		var sut = CreateSut(service, aggregator);

		await sut.OnNavigatedToAsync(null);

		aggregator.Posted.Count.ShouldBe(2);
		var busy = aggregator.Posted[0].ShouldBeOfType<DrawboardCodingExercise.Contracts.Events.NotifyBusyEvent>();
		var done = aggregator.Posted[1].ShouldBeOfType<DrawboardCodingExercise.Contracts.Events.NotifyDoneEvent>();
		busy.Event.ShouldBe(done.Event);
	}

	// T2: films are presented ascending by episode number, applied by the ViewModel itself -
	// never relying on the order the source happens to return them in.
	[Fact]
	public async Task OnNavigatedToAsync_orders_films_ascending_by_episode_number_from_an_unordered_response()
	{
		var service = Substitute.For<IStarWarsService>();
		service.GetFilmsAsync().Returns(new List<FilmDto>
		{
			Film("Return of the Jedi", 6, "3"),
			Film("A New Hope", 4, "1"),
			Film("The Empire Strikes Back", 5, "2")
		});
		var sut = CreateSut(service);

		await sut.OnNavigatedToAsync(null);

		sut.Films.Select(f => f.EpisodeNumber).ShouldBe(new[] { 4, 5, 6 });
	}

	[Fact]
	public async Task OnNavigatedToAsync_posts_a_matched_pair_even_when_the_load_ultimately_fails()
	{
		var aggregator = new RecordingEventAggregator();
		var service = Substitute.For<IStarWarsService>();
		service.GetFilmsAsync().Returns<Task<IReadOnlyList<FilmDto>>>(_ => throw new InvalidOperationException("boom"));
		var userInteraction = Substitute.For<IUserInteractionService>();
		userInteraction.ShowRetryDialogAsync().Returns(RetryDialogResult.Cancel);
		var sut = CreateSut(service, aggregator, userInteraction);

		await sut.OnNavigatedToAsync(null);

		aggregator.Posted.Count.ShouldBe(2);
		var busy = aggregator.Posted[0].ShouldBeOfType<DrawboardCodingExercise.Contracts.Events.NotifyBusyEvent>();
		var done = aggregator.Posted[1].ShouldBeOfType<DrawboardCodingExercise.Contracts.Events.NotifyDoneEvent>();
		busy.Event.ShouldBe(done.Event);
	}

	// T4: selecting a film navigates to the detail page, passing that film's opaque id -
	// never the episode number.
	[Fact]
	public async Task SelectFilmCommand_navigates_to_film_details_with_the_selected_films_id()
	{
		var navigationService = Substitute.For<INavigationService>();
		var sut = CreateSut(navigationService: navigationService);
		var film = new DrawboardCodingExercise.ViewModel.Models.FilmListItem("4", "The Phantom Menace", 1, "Episode I");

		await sut.SelectFilmCommand.ExecuteAsync(film);

		await navigationService.Received(1).NavigateAsync(PageKey.FilmDetails, "4");
	}

	// T5: a film with no id (unparseable url) cannot be opened, so selecting it must not navigate.
	[Fact]
	public async Task SelectFilmCommand_does_not_navigate_when_the_film_has_no_id()
	{
		var navigationService = Substitute.For<INavigationService>();
		var sut = CreateSut(navigationService: navigationService);
		var film = new DrawboardCodingExercise.ViewModel.Models.FilmListItem(null, "Unknown Film", 0, "Episode ?");

		await sut.SelectFilmCommand.ExecuteAsync(film);

		await navigationService.DidNotReceive().NavigateAsync(Arg.Any<PageKey>(), Arg.Any<object>());
	}

	// T11 against the real ViewModel: a failure prompts retry/cancel, and choosing Retry
	// re-attempts and succeeds once the source recovers.
	[Fact]
	public async Task OnNavigatedToAsync_retries_and_succeeds_on_the_second_attempt()
	{
		var attempt = 0;
		var service = Substitute.For<IStarWarsService>();
		service.GetFilmsAsync().Returns<Task<IReadOnlyList<FilmDto>>>(_ =>
		{
			attempt++;
			if (attempt == 1)
			{
				throw new InvalidOperationException("first attempt fails");
			}
			return Task.FromResult<IReadOnlyList<FilmDto>>(new List<FilmDto> { Film("A New Hope", 4, "1") });
		});
		var userInteraction = Substitute.For<IUserInteractionService>();
		userInteraction.ShowRetryDialogAsync().Returns(RetryDialogResult.Retry);
		var sut = CreateSut(service, userInteractionService: userInteraction);

		await sut.OnNavigatedToAsync(null);

		attempt.ShouldBe(2);
		await userInteraction.Received(1).ShowRetryDialogAsync();
		sut.State.ShouldBe(PageLoadState.Loaded);
		sut.Films.Count.ShouldBe(1);
	}

	// T12 against the real ViewModel: choosing Cancel leaves a readable error state, with
	// progress cleared rather than left spinning.
	[Fact]
	public async Task OnNavigatedToAsync_yields_the_error_state_and_clears_progress_when_cancelled()
	{
		var aggregator = new RecordingEventAggregator();
		var service = Substitute.For<IStarWarsService>();
		service.GetFilmsAsync().Returns<Task<IReadOnlyList<FilmDto>>>(_ => throw new InvalidOperationException("boom"));
		var userInteraction = Substitute.For<IUserInteractionService>();
		userInteraction.ShowRetryDialogAsync().Returns(RetryDialogResult.Cancel);
		var sut = CreateSut(service, aggregator, userInteraction);

		await sut.OnNavigatedToAsync(null);

		sut.State.ShouldBe(PageLoadState.Error);
		await userInteraction.Received(1).ShowRetryDialogAsync();

		var busies = aggregator.Posted.OfType<DrawboardCodingExercise.Contracts.Events.NotifyBusyEvent>().Select(e => e.Event).ToList();
		var dones = aggregator.Posted.OfType<DrawboardCodingExercise.Contracts.Events.NotifyDoneEvent>().Select(e => e.Event).ToList();
		busies.ShouldBe(dones, ignoreOrder: true);
	}

	// The on-page retry affordance: after cancelling into the error state, the user can retry
	// from the page itself and reach the normal populated view (FR-018).
	[Fact]
	public async Task RetryCommand_reloads_the_film_list_after_the_user_cancelled_into_the_error_state()
	{
		var attempt = 0;
		var service = Substitute.For<IStarWarsService>();
		service.GetFilmsAsync().Returns<Task<IReadOnlyList<FilmDto>>>(_ =>
		{
			attempt++;
			if (attempt == 1)
			{
				throw new InvalidOperationException("first load fails");
			}
			return Task.FromResult<IReadOnlyList<FilmDto>>(new List<FilmDto> { Film("A New Hope", 4, "1") });
		});
		var userInteraction = Substitute.For<IUserInteractionService>();
		userInteraction.ShowRetryDialogAsync().Returns(RetryDialogResult.Cancel);
		var sut = CreateSut(service, userInteractionService: userInteraction);

		await sut.OnNavigatedToAsync(null);
		sut.State.ShouldBe(PageLoadState.Error);

		await sut.RetryCommand.ExecuteAsync(null);

		sut.State.ShouldBe(PageLoadState.Loaded);
		sut.Films.Count.ShouldBe(1);
	}
}
