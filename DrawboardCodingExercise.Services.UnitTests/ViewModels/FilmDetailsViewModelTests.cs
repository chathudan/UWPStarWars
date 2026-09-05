using System;
using System.Threading.Tasks;
using DrawboardCodingExercise.Contracts.Services;
using DrawboardCodingExercise.Services.StarWars;
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
}
