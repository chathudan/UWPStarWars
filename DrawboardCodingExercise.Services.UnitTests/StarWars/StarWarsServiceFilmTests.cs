using System;
using System.Net;
using System.Threading.Tasks;
using DrawboardCodingExercise.Services.StarWars;
using DrawboardCodingExercise.Services.UnitTests.TestData;
using DrawboardCodingExercise.Services.UnitTests.TestDoubles;
using NSubstitute;
using Serilog;
using Shouldly;
using Xunit;

namespace DrawboardCodingExercise.Services.UnitTests.StarWars;

/// <summary>
/// T34: a single film is retrieved by its opaque identifier. A 404 means the identifier is
/// wrong and returns null rather than throwing - retrying an unknown id can never succeed
/// (FR-013, V4). Every other status still propagates as a recoverable failure.
/// </summary>
public class StarWarsServiceFilmTests
{
	private static StarWarsService CreateSut(FakeApiClient apiClient) =>
		new StarWarsService(apiClient, new FakeApiSettings(), Substitute.For<ILogger>(), TimeSpan.FromSeconds(15));

	[Fact]
	public async Task GetFilmAsync_returns_the_film_for_a_known_identifier()
	{
		var apiClient = new FakeApiClient().Returns("films/1", SwapiPayloads.ANewHope);
		var sut = CreateSut(apiClient);

		var film = await sut.GetFilmAsync("1");

		film.ShouldNotBeNull();
		film.Title.ShouldBe("A New Hope");
		film.EpisodeId.ShouldBe(4);
	}

	[Fact]
	public async Task GetFilmAsync_returns_null_when_the_source_responds_not_found()
	{
		var apiClient = new FakeApiClient().Throws("films/99", new HttpStatusException(HttpStatusCode.NotFound));
		var sut = CreateSut(apiClient);

		var film = await sut.GetFilmAsync("99");

		film.ShouldBeNull();
	}

	[Fact]
	public async Task GetFilmAsync_propagates_a_non_not_found_status_as_a_recoverable_failure()
	{
		var apiClient = new FakeApiClient().Throws("films/1", new HttpStatusException(HttpStatusCode.ServiceUnavailable));
		var sut = CreateSut(apiClient);

		var exception = await Should.ThrowAsync<HttpStatusException>(() => sut.GetFilmAsync("1"));

		exception.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
	}
}
