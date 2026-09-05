using System;
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
/// T23: a single request that exceeds its budget is abandoned and treated as a recoverable
/// failure (FR-015). The budget is injected so this test runs in milliseconds, never the real
/// 15 seconds - see research.md R3.
/// </summary>
public class StarWarsServiceBudgetTests
{
	[Fact]
	public async Task GetFilmsAsync_treats_a_request_exceeding_its_budget_as_a_recoverable_failure()
	{
		var apiClient = new FakeApiClient { Delay = TimeSpan.FromMilliseconds(200) }
			.Returns("films", SwapiPayloads.FilmsArray);
		var sut = new StarWarsService(apiClient, new FakeApiSettings(), Substitute.For<ILogger>(), TimeSpan.FromMilliseconds(20));

		await Should.ThrowAsync<TimeoutException>(() => sut.GetFilmsAsync());
	}

	[Fact]
	public async Task GetFilmsAsync_succeeds_when_the_response_arrives_within_the_budget()
	{
		var apiClient = new FakeApiClient { Delay = TimeSpan.FromMilliseconds(5) }
			.Returns("films", SwapiPayloads.FilmsArray);
		var sut = new StarWarsService(apiClient, new FakeApiSettings(), Substitute.For<ILogger>(), TimeSpan.FromSeconds(15));

		var films = await sut.GetFilmsAsync();

		films.Count.ShouldBe(2);
	}
}
