using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DrawboardCodingExercise.Contracts.Events;
using DrawboardCodingExercise.Contracts.Services;
using DrawboardCodingExercise.Services.StarWars;
using DrawboardCodingExercise.Services.StarWars.Dtos;
using DrawboardCodingExercise.Services.UnitTests.TestDoubles;
using DrawboardCodingExercise.ViewModel;
using DrawboardCodingExercise.ViewModel.Models;
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
		service.GetRelatedResourcesAsync(Arg.Any<IReadOnlyList<string>>())
			.Returns(new RelatedResourceLoadResult(Array.Empty<NamedResourceDto>(), 0, 0));
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

	private static NamedResourceDto Person(string name, int id) =>
		new NamedResourceDto { Name = name, Url = $"https://swapi.info/api/people/{id}" };

	private static RelatedCategorySection SectionFor(FilmDetailsViewModel sut, RelatedCategory category) =>
		sut.Categories.Single(section => section.Category == category);

	private static RelatedCategorySection Characters(FilmDetailsViewModel sut) =>
		SectionFor(sut, RelatedCategory.Characters);

	// T22: a film with no characters yields an explicit Empty section state, not an error.
	[Fact]
	public async Task OnNavigatedToAsync_yields_the_empty_character_state_for_a_film_with_no_characters()
	{
		var service = Substitute.For<IStarWarsService>();
		service.GetFilmAsync("1").Returns(FilmWithCharacters());
		service.GetRelatedResourcesAsync(Arg.Any<IReadOnlyList<string>>())
			.Returns(new RelatedResourceLoadResult(Array.Empty<NamedResourceDto>(), 0, 0));
		var sut = CreateSut(service);

		await sut.OnNavigatedToAsync("1");

		sut.FilmState.ShouldBe(PageLoadState.Loaded);
		Characters(sut).State.ShouldBe(PageLoadState.Empty);
		Characters(sut).Items.ShouldBeEmpty();
	}

	[Fact]
	public async Task OnNavigatedToAsync_loads_and_exposes_the_films_characters()
	{
		var service = Substitute.For<IStarWarsService>();
		service.GetFilmAsync("1").Returns(FilmWithCharacters("u/1", "u/2"));
		service.GetRelatedResourcesAsync(Arg.Any<IReadOnlyList<string>>())
			.Returns(new RelatedResourceLoadResult(new[] { Person("Luke Skywalker", 1), Person("C-3PO", 2) }, 2, 0));
		var sut = CreateSut(service);

		await sut.OnNavigatedToAsync("1");

		Characters(sut).State.ShouldBe(PageLoadState.Loaded);
		Characters(sut).Items.Select(c => c.Name).ShouldBe(new[] { "Luke Skywalker", "C-3PO" });
		Characters(sut).HasPartialFailure.ShouldBeFalse();
	}

	// T052: a partial result keeps the successes and reports the shortfall.
	[Fact]
	public async Task OnNavigatedToAsync_keeps_successful_characters_and_reports_a_partial_failure()
	{
		var service = Substitute.For<IStarWarsService>();
		service.GetFilmAsync("1").Returns(FilmWithCharacters("u/1", "u/2", "u/3"));
		service.GetRelatedResourcesAsync(Arg.Any<IReadOnlyList<string>>())
			.Returns(new RelatedResourceLoadResult(new[] { Person("Luke Skywalker", 1), Person("R2-D2", 3) }, 3, 1));
		var sut = CreateSut(service);

		await sut.OnNavigatedToAsync("1");

		Characters(sut).State.ShouldBe(PageLoadState.Loaded);
		Characters(sut).Items.Count.ShouldBe(2);
		Characters(sut).HasPartialFailure.ShouldBeTrue();
	}

	// T33: a failing section load must leave the film's OWN details intact and readable -
	// each section fails independently (FR-011, FR-012).
	[Fact]
	public async Task OnNavigatedToAsync_leaves_the_film_loaded_when_the_character_load_fails()
	{
		var service = Substitute.For<IStarWarsService>();
		service.GetFilmAsync("1").Returns(FilmWithCharacters("u/1", "u/2"));
		service.GetRelatedResourcesAsync(Arg.Any<IReadOnlyList<string>>())
			.Returns<Task<RelatedResourceLoadResult>>(_ => throw new InvalidOperationException("all characters failed"));
		var userInteraction = Substitute.For<IUserInteractionService>();
		userInteraction.ShowRetryDialogAsync().Returns(RetryDialogResult.Cancel);
		var sut = CreateSut(service, userInteractionService: userInteraction);

		await sut.OnNavigatedToAsync("1");

		sut.FilmState.ShouldBe(PageLoadState.Loaded);
		sut.Film.ShouldNotBeNull();
		sut.Film!.Title.ShouldBe("A New Hope");
		Characters(sut).State.ShouldBe(PageLoadState.Error);
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
		service.GetRelatedResourcesAsync(Arg.Any<IReadOnlyList<string>>())
			.Returns(new RelatedResourceLoadResult(new[] { Person("Luke Skywalker", 1) }, 1, 0));
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

	private static FilmDto FilmWithEveryCategory() => new FilmDto
	{
		Title = "A New Hope",
		EpisodeId = 4,
		Director = "George Lucas",
		Producer = "Gary Kurtz",
		OpeningCrawl = "It is a period of civil war.",
		ReleaseDate = "1977-05-25",
		Characters = new List<string> { "people/1", "people/2", "people/3" },
		Planets = new List<string> { "planets/1" },
		Starships = new List<string> { "starships/2", "starships/3" },
		Vehicles = new List<string>(),
		Species = new List<string> { "species/1" },
		Url = "https://swapi.info/api/films/1"
	};

	// T37 (FR-027): exactly five sections, in RelatedCategory declaration order, each carrying
	// its own localized title and the entry count from the FILM's own response - the count is
	// known before the section has been loaded, which is what lets the header show "3" next to a
	// collapsed, never-requested section.
	[Fact]
	public async Task OnNavigatedToAsync_exposes_all_five_related_categories_with_their_counts()
	{
		var service = Substitute.For<IStarWarsService>();
		service.GetFilmAsync("1").Returns(FilmWithEveryCategory());
		service.GetRelatedResourcesAsync(Arg.Any<IReadOnlyList<string>>())
			.Returns(new RelatedResourceLoadResult(Array.Empty<NamedResourceDto>(), 0, 0));
		var sut = CreateSut(service);

		await sut.OnNavigatedToAsync("1");

		sut.Categories.Select(c => c.Category).ShouldBe(new[]
		{
			RelatedCategory.Characters,
			RelatedCategory.Planets,
			RelatedCategory.Starships,
			RelatedCategory.Vehicles,
			RelatedCategory.Species
		});

		sut.Categories.Select(c => c.Count).ShouldBe(new[] { 3, 1, 2, 0, 1 });

		// Each section resolves its OWN title key. Distinctness is the assertion that matters:
		// a copy-pasted section that reused the Characters key would look perfectly fine on
		// screen for whichever category happened to be checked first.
		sut.Categories.Select(c => c.Title).Distinct().Count().ShouldBe(5);
		sut.Categories.ShouldAllBe(c => !string.IsNullOrWhiteSpace(c.Title));
	}

	// T38 (FR-028): on arrival ONLY Characters requests. The other four are collapsed and have
	// issued no call at all - loading all five eagerly would mean up to 38 requests per film
	// against a free community-run service, most for sections the user never opens.
	[Fact]
	public async Task OnNavigatedToAsync_loads_only_the_characters_section_and_leaves_the_rest_collapsed()
	{
		var service = Substitute.For<IStarWarsService>();
		service.GetFilmAsync("1").Returns(FilmWithEveryCategory());
		service.GetRelatedResourcesAsync(Arg.Any<IReadOnlyList<string>>())
			.Returns(new RelatedResourceLoadResult(new[] { Person("Luke Skywalker", 1) }, 3, 2));
		var sut = CreateSut(service);

		await sut.OnNavigatedToAsync("1");

		var characters = sut.Categories.Single(c => c.Category == RelatedCategory.Characters);
		characters.IsExpanded.ShouldBeTrue();
		characters.State.ShouldBe(PageLoadState.Loaded);

		foreach (var other in sut.Categories.Where(c => c.Category != RelatedCategory.Characters))
		{
			other.IsExpanded.ShouldBeFalse($"{other.Category} must start collapsed");
			other.State.ShouldBe(PageLoadState.NotStarted, $"{other.Category} must not have been requested");
			other.Items.ShouldBeEmpty();
		}

		// Exactly one batch call was made, and it was for the character urls.
		await service.Received(1).GetRelatedResourcesAsync(Arg.Any<IReadOnlyList<string>>());
		await service.Received(1).GetRelatedResourcesAsync(
			Arg.Is<IReadOnlyList<string>>(urls => urls.Contains("people/1")));
	}

	// T41 (FR-029, V14, AC-009): two sections loading produce two DISTINCT progress messages,
	// each matched by a byte-identical done event.
	//
	// This is not cosmetic. ShellViewModel.OnNotifyDone does RemoveAt(IndexOf(...)) with no -1
	// guard, so if two sections shared one progress string the first done event would remove the
	// second's entry and the second would call RemoveAt(-1) - an ArgumentOutOfRangeException on
	// the UI thread. With five sections a user can expand, that is reachable, not theoretical.
	[Fact]
	public async Task Each_section_posts_progress_under_a_message_naming_its_own_category()
	{
		var aggregator = new RecordingEventAggregator();
		var service = Substitute.For<IStarWarsService>();
		service.GetFilmAsync("1").Returns(FilmWithEveryCategory());
		service.GetRelatedResourcesAsync(Arg.Any<IReadOnlyList<string>>())
			.Returns(new RelatedResourceLoadResult(new[] { Person("Luke Skywalker", 1) }, 1, 0));
		var sut = CreateSut(service, aggregator);

		await sut.OnNavigatedToAsync("1");          // film + Characters
		await SectionFor(sut, RelatedCategory.Planets).ToggleCommand.ExecuteAsync(null);

		var busies = aggregator.Posted.OfType<NotifyBusyEvent>().Select(e => e.Event).ToList();
		var dones = aggregator.Posted.OfType<NotifyDoneEvent>().Select(e => e.Event).ToList();

		// Three operations: the film, the Characters section, the Planets section.
		busies.Count.ShouldBe(3);
		busies.ShouldBe(dones, ignoreOrder: true);
		busies.Distinct().Count().ShouldBe(3, "every concurrent operation needs its own progress string");

		// And the section messages actually name their categories, rather than merely differing.
		busies.ShouldContain(SectionFor(sut, RelatedCategory.Characters).LoadingMessage);
		busies.ShouldContain(SectionFor(sut, RelatedCategory.Planets).LoadingMessage);
	}

	// T42 (FR-012): one section failing leaves the film and the OTHER FOUR sections untouched.
	[Fact]
	public async Task A_failing_section_leaves_the_film_and_the_other_sections_untouched()
	{
		var service = Substitute.For<IStarWarsService>();
		service.GetFilmAsync("1").Returns(FilmWithEveryCategory());

		// Characters succeeds; Planets fails outright.
		service.GetRelatedResourcesAsync(Arg.Is<IReadOnlyList<string>>(urls => urls.Contains("people/1")))
			.Returns(new RelatedResourceLoadResult(new[] { Person("Luke Skywalker", 1) }, 3, 2));
		service.GetRelatedResourcesAsync(Arg.Is<IReadOnlyList<string>>(urls => urls.Contains("planets/1")))
			.Returns<Task<RelatedResourceLoadResult>>(_ => throw new InvalidOperationException("every planet failed"));

		var userInteraction = Substitute.For<IUserInteractionService>();
		userInteraction.ShowRetryDialogAsync().Returns(RetryDialogResult.Cancel);
		var sut = CreateSut(service, userInteractionService: userInteraction);

		await sut.OnNavigatedToAsync("1");
		await SectionFor(sut, RelatedCategory.Planets).ToggleCommand.ExecuteAsync(null);

		SectionFor(sut, RelatedCategory.Planets).State.ShouldBe(PageLoadState.Error);

		// The film is untouched...
		sut.FilmState.ShouldBe(PageLoadState.Loaded);
		sut.Film!.Title.ShouldBe("A New Hope");

		// ...as is the section that had already succeeded...
		SectionFor(sut, RelatedCategory.Characters).State.ShouldBe(PageLoadState.Loaded);
		SectionFor(sut, RelatedCategory.Characters).Items.Count.ShouldBe(1);

		// ...and the three nobody has opened are still untouched and still openable.
		foreach (var untouched in new[] { RelatedCategory.Starships, RelatedCategory.Vehicles, RelatedCategory.Species })
		{
			SectionFor(sut, untouched).State.ShouldBe(PageLoadState.NotStarted);
			SectionFor(sut, untouched).IsExpanded.ShouldBeFalse();
		}
	}

	// T11/T12 for the FILM load specifically.
	[Fact]
	public async Task OnNavigatedToAsync_retries_the_film_load_and_succeeds_on_the_second_attempt()
	{
		var attempt = 0;
		var service = Substitute.For<IStarWarsService>();
		service.GetFilmAsync("1").Returns<Task<FilmDto>>(_ =>
		{
			attempt++;
			if (attempt == 1)
			{
				throw new InvalidOperationException("first attempt fails");
			}
			return Task.FromResult(FilmWithCharacters());
		});
		service.GetRelatedResourcesAsync(Arg.Any<IReadOnlyList<string>>())
			.Returns(new RelatedResourceLoadResult(Array.Empty<NamedResourceDto>(), 0, 0));
		var userInteraction = Substitute.For<IUserInteractionService>();
		userInteraction.ShowRetryDialogAsync().Returns(RetryDialogResult.Retry);
		var sut = CreateSut(service, userInteractionService: userInteraction);

		await sut.OnNavigatedToAsync("1");

		attempt.ShouldBe(2);
		sut.FilmState.ShouldBe(PageLoadState.Loaded);
	}

	// The on-page retry for the FILM, after cancelling into the error state.
	[Fact]
	public async Task RetryFilmCommand_reloads_the_film_after_the_user_cancelled_into_the_error_state()
	{
		var attempt = 0;
		var service = Substitute.For<IStarWarsService>();
		service.GetFilmAsync("1").Returns<Task<FilmDto>>(_ =>
		{
			attempt++;
			if (attempt == 1)
			{
				throw new InvalidOperationException("first load fails");
			}
			return Task.FromResult(FilmWithCharacters());
		});
		service.GetRelatedResourcesAsync(Arg.Any<IReadOnlyList<string>>())
			.Returns(new RelatedResourceLoadResult(Array.Empty<NamedResourceDto>(), 0, 0));
		var userInteraction = Substitute.For<IUserInteractionService>();
		userInteraction.ShowRetryDialogAsync().Returns(RetryDialogResult.Cancel);
		var sut = CreateSut(service, userInteractionService: userInteraction);

		await sut.OnNavigatedToAsync("1");
		sut.FilmState.ShouldBe(PageLoadState.Error);

		await sut.RetryFilmCommand.ExecuteAsync(null);

		sut.FilmState.ShouldBe(PageLoadState.Loaded);
	}

	// The section-local retry, which must NOT re-request the film - the film is already on
	// screen and re-fetching it would be wasted work and a visible flicker.
	[Fact]
	public async Task SectionRetryCommand_reloads_only_that_section_and_does_not_refetch_the_film()
	{
		var characterAttempt = 0;
		var service = Substitute.For<IStarWarsService>();
		service.GetFilmAsync("1").Returns(FilmWithCharacters("u/1"));
		service.GetRelatedResourcesAsync(Arg.Any<IReadOnlyList<string>>()).Returns<Task<RelatedResourceLoadResult>>(_ =>
		{
			characterAttempt++;
			if (characterAttempt == 1)
			{
				throw new InvalidOperationException("characters fail first time");
			}
			return Task.FromResult(new RelatedResourceLoadResult(new[] { Person("Luke Skywalker", 1) }, 1, 0));
		});
		var userInteraction = Substitute.For<IUserInteractionService>();
		userInteraction.ShowRetryDialogAsync().Returns(RetryDialogResult.Cancel);
		var sut = CreateSut(service, userInteractionService: userInteraction);

		await sut.OnNavigatedToAsync("1");
		sut.FilmState.ShouldBe(PageLoadState.Loaded);
		Characters(sut).State.ShouldBe(PageLoadState.Error);

		await Characters(sut).RetryCommand.ExecuteAsync(null);

		Characters(sut).State.ShouldBe(PageLoadState.Loaded);
		Characters(sut).Items.Count.ShouldBe(1);
		// The film was fetched exactly once, by the original navigation - never again.
		await service.Received(1).GetFilmAsync("1");
	}
}
