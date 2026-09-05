using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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
/// T7, T8, T9: a missing, non-string, or whitespace navigation parameter must yield
/// InvalidSelection without ever calling the service (FR-013, V1, V2) - a broken navigation call
/// must never generate network traffic.
/// </summary>
public class FilmDetailsViewModelTests
{
	private static FilmDetailsViewModel CreateSut(
		IStarWarsService starWarsService = null,
		IEventAggregator eventAggregator = null,
		IUserInteractionService userInteractionService = null,
		ILocalizationService localizationService = null)
	{
		return new FilmDetailsViewModel(
			starWarsService ?? Substitute.For<IStarWarsService>(),
			eventAggregator ?? new RecordingEventAggregator(),
			userInteractionService ?? Substitute.For<IUserInteractionService>(),
			localizationService ?? new EchoLocalizationService(),
			Substitute.For<ILogger>());
	}

	// T7
	[Fact]
	public async Task OnNavigatedToAsync_with_a_null_parameter_yields_invalid_selection_without_calling_the_service()
	{
		var service = Substitute.For<IStarWarsService>();
		var sut = CreateSut(service);

		await sut.OnNavigatedToAsync(null);

		sut.FilmState.ShouldBe(PageLoadState.InvalidSelection);
		await service.DidNotReceive().GetFilmAsync(Arg.Any<string>());
	}

	// T8
	[Fact]
	public async Task OnNavigatedToAsync_with_a_non_string_parameter_yields_invalid_selection_without_calling_the_service()
	{
		var service = Substitute.For<IStarWarsService>();
		var sut = CreateSut(service);

		await sut.OnNavigatedToAsync(42);

		sut.FilmState.ShouldBe(PageLoadState.InvalidSelection);
		await service.DidNotReceive().GetFilmAsync(Arg.Any<string>());
	}

	// T9
	[Fact]
	public async Task OnNavigatedToAsync_with_a_whitespace_parameter_yields_invalid_selection_without_calling_the_service()
	{
		var service = Substitute.For<IStarWarsService>();
		var sut = CreateSut(service);

		await sut.OnNavigatedToAsync("   ");

		sut.FilmState.ShouldBe(PageLoadState.InvalidSelection);
		await service.DidNotReceive().GetFilmAsync(Arg.Any<string>());
	}

	// T6: a valid id loads and exposes all six required fields.
	[Fact]
	public async Task OnNavigatedToAsync_with_a_valid_id_loads_and_exposes_the_required_fields()
	{
		var service = Substitute.For<IStarWarsService>();
		service.GetFilmAsync("1").Returns(new FilmDto
		{
			Title = "A New Hope",
			EpisodeId = 4,
			Director = "George Lucas",
			Producer = "Gary Kurtz, Rick McCallum",
			OpeningCrawl = "It is a period of civil war.",
			ReleaseDate = "1977-05-25",
			Characters = new List<string>(),
			Url = "https://swapi.info/api/films/1"
		});
		// A successful film load now also triggers the character load; this film has none, so
		// an empty successful result keeps this test focused on the film's own fields.
		service.GetCharactersAsync(Arg.Any<IReadOnlyList<string>>())
			.Returns(new CharacterLoadResult(Array.Empty<PersonDto>(), 0, 0));
		var sut = CreateSut(service);

		await sut.OnNavigatedToAsync("1");

		sut.FilmState.ShouldBe(PageLoadState.Loaded);
		sut.Film.ShouldNotBeNull();
		sut.Film!.Title.ShouldBe("A New Hope");
		sut.Film.EpisodeNumber.ShouldBe(4);
		sut.Film.Director.ShouldBe("George Lucas");
		sut.Film.Producer.ShouldBe("Gary Kurtz, Rick McCallum");
		sut.Film.OpeningCrawl.ShouldBe("It is a period of civil war.");
		sut.Film.ReleaseDateDisplay.ShouldNotBeNullOrWhiteSpace();
	}

	// T10: a 404 (service returns null) is an invalid selection, not a retryable failure - no
	// retry/cancel prompt is ever shown, because retrying an unknown id can never succeed.
	[Fact]
	public async Task OnNavigatedToAsync_with_an_unknown_id_yields_invalid_selection_without_a_retry_prompt()
	{
		var service = Substitute.For<IStarWarsService>();
		service.GetFilmAsync("99").Returns((FilmDto)null);
		var userInteraction = Substitute.For<IUserInteractionService>();
		var sut = CreateSut(service, userInteractionService: userInteraction);

		await sut.OnNavigatedToAsync("99");

		sut.FilmState.ShouldBe(PageLoadState.InvalidSelection);
		await userInteraction.DidNotReceive().ShowRetryDialogAsync();
	}

	// T17: OnNavigatedToAsync must never propagate, even when the service always fails and the
	// user cancels the retry prompt.
	[Fact]
	public async Task OnNavigatedToAsync_never_propagates_an_exception()
	{
		var service = Substitute.For<IStarWarsService>();
		service.GetFilmAsync(Arg.Any<string>()).Returns<Task<FilmDto>>(_ => throw new InvalidOperationException("boom"));
		var userInteraction = Substitute.For<IUserInteractionService>();
		userInteraction.ShowRetryDialogAsync().Returns(RetryDialogResult.Cancel);
		var sut = CreateSut(service, userInteractionService: userInteraction);

		await Should.NotThrowAsync(() => sut.OnNavigatedToAsync("1"));

		sut.FilmState.ShouldBe(PageLoadState.Error);
	}

	private static FilmDto FilmWithCharacters(params string[] characterUrls) => new FilmDto
	{
		Title = "A New Hope",
		EpisodeId = 4,
		Director = "George Lucas",
		Producer = "Gary Kurtz",
		OpeningCrawl = "It is a period of civil war.",
		ReleaseDate = "1977-05-25",
		Characters = new List<string>(characterUrls),
		Url = "https://swapi.info/api/films/1"
	};

	private static PersonDto Person(string name, int id) =>
		new PersonDto { Name = name, Url = $"https://swapi.info/api/people/{id}" };

	// T22: a film with no characters yields an explicit Empty character state, not an error.
	[Fact]
	public async Task OnNavigatedToAsync_yields_the_empty_character_state_for_a_film_with_no_characters()
	{
		var service = Substitute.For<IStarWarsService>();
		service.GetFilmAsync("1").Returns(FilmWithCharacters());
		service.GetCharactersAsync(Arg.Any<IReadOnlyList<string>>())
			.Returns(new CharacterLoadResult(Array.Empty<PersonDto>(), 0, 0));
		var sut = CreateSut(service);

		await sut.OnNavigatedToAsync("1");

		sut.FilmState.ShouldBe(PageLoadState.Loaded);
		sut.CharactersState.ShouldBe(PageLoadState.Empty);
		sut.Characters.ShouldBeEmpty();
	}

	[Fact]
	public async Task OnNavigatedToAsync_loads_and_exposes_the_films_characters()
	{
		var service = Substitute.For<IStarWarsService>();
		service.GetFilmAsync("1").Returns(FilmWithCharacters("u/1", "u/2"));
		service.GetCharactersAsync(Arg.Any<IReadOnlyList<string>>())
			.Returns(new CharacterLoadResult(new[] { Person("Luke Skywalker", 1), Person("C-3PO", 2) }, 2, 0));
		var sut = CreateSut(service);

		await sut.OnNavigatedToAsync("1");

		sut.CharactersState.ShouldBe(PageLoadState.Loaded);
		sut.Characters.Select(c => c.Name).ShouldBe(new[] { "Luke Skywalker", "C-3PO" });
		sut.HasPartialCharacterFailure.ShouldBeFalse();
	}

	// T052: a partial result keeps the successes and reports the shortfall.
	[Fact]
	public async Task OnNavigatedToAsync_keeps_successful_characters_and_reports_a_partial_failure()
	{
		var service = Substitute.For<IStarWarsService>();
		service.GetFilmAsync("1").Returns(FilmWithCharacters("u/1", "u/2", "u/3"));
		service.GetCharactersAsync(Arg.Any<IReadOnlyList<string>>())
			.Returns(new CharacterLoadResult(new[] { Person("Luke Skywalker", 1), Person("R2-D2", 3) }, 3, 1));
		var sut = CreateSut(service);

		await sut.OnNavigatedToAsync("1");

		sut.CharactersState.ShouldBe(PageLoadState.Loaded);
		sut.Characters.Count.ShouldBe(2);
		sut.HasPartialCharacterFailure.ShouldBeTrue();
	}

	// T33: a failing character load must leave the film's OWN details intact and readable -
	// the character section fails independently (FR-011, FR-012).
	[Fact]
	public async Task OnNavigatedToAsync_leaves_the_film_loaded_when_the_character_load_fails()
	{
		var service = Substitute.For<IStarWarsService>();
		service.GetFilmAsync("1").Returns(FilmWithCharacters("u/1", "u/2"));
		service.GetCharactersAsync(Arg.Any<IReadOnlyList<string>>())
			.Returns<Task<CharacterLoadResult>>(_ => throw new InvalidOperationException("all characters failed"));
		var userInteraction = Substitute.For<IUserInteractionService>();
		userInteraction.ShowRetryDialogAsync().Returns(RetryDialogResult.Cancel);
		var sut = CreateSut(service, userInteractionService: userInteraction);

		await sut.OnNavigatedToAsync("1");

		sut.FilmState.ShouldBe(PageLoadState.Loaded);
		sut.Film.ShouldNotBeNull();
		sut.Film!.Title.ShouldBe("A New Hope");
		sut.CharactersState.ShouldBe(PageLoadState.Error);
	}

	// T052: the character load posts its OWN matched busy/done pair, with a message distinct
	// from the film load's - otherwise the two would cancel each other out in the shell's
	// progress list, which removes by exact string match.
	[Fact]
	public async Task OnNavigatedToAsync_posts_a_distinct_matched_busy_done_pair_for_the_character_load()
	{
		var aggregator = new RecordingEventAggregator();
		var service = Substitute.For<IStarWarsService>();
		service.GetFilmAsync("1").Returns(FilmWithCharacters("u/1"));
		service.GetCharactersAsync(Arg.Any<IReadOnlyList<string>>())
			.Returns(new CharacterLoadResult(new[] { Person("Luke Skywalker", 1) }, 1, 0));
		var sut = CreateSut(service, aggregator);

		await sut.OnNavigatedToAsync("1");

		// Two operations => two matched pairs => four events.
		aggregator.Posted.Count.ShouldBe(4);
		var busies = aggregator.Posted.OfType<DrawboardCodingExercise.Contracts.Events.NotifyBusyEvent>().Select(e => e.Event).ToList();
		var dones = aggregator.Posted.OfType<DrawboardCodingExercise.Contracts.Events.NotifyDoneEvent>().Select(e => e.Event).ToList();
		busies.Count.ShouldBe(2);
		dones.Count.ShouldBe(2);
		busies.ShouldBe(dones, ignoreOrder: true);
		busies.Distinct().Count().ShouldBe(2, "the film and character loads must use distinct progress messages");
	}
}
