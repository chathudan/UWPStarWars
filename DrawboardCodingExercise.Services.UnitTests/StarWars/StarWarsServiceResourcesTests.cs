using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using DrawboardCodingExercise.Services.StarWars;
using DrawboardCodingExercise.Services.UnitTests.TestDoubles;
using NSubstitute;
using Serilog;
using Shouldly;
using Xunit;

namespace DrawboardCodingExercise.Services.UnitTests.StarWars;

/// <summary>
/// T18-T21: character retrieval is bounded (at most 6 concurrent), order-preserving regardless
/// of completion order, tolerant of partial failure, and treats total failure as recoverable.
/// </summary>
public class StarWarsServiceCharactersTests
{
	private const string Base = "https://swapi.info/api";

	private static StarWarsService CreateSut(FakeApiClient apiClient) =>
		new StarWarsService(apiClient, new FakeApiSettings(Base), Substitute.For<ILogger>(), TimeSpan.FromSeconds(15));

	private static string PersonJson(string name, int id) =>
		$@"{{ ""name"": ""{name}"", ""url"": ""{Base}/people/{id}"" }}";

	// T18: results come back in the order requested, not the order responses arrive.
	[Fact]
	public async Task GetRelatedResourcesAsync_preserves_request_order_regardless_of_completion_order()
	{
		var apiClient = new FakeApiClient()
			.Returns("people/1", PersonJson("Luke Skywalker", 1)).DelayFor("people/1", TimeSpan.FromMilliseconds(60))
			.Returns("people/2", PersonJson("C-3PO", 2)).DelayFor("people/2", TimeSpan.FromMilliseconds(5))
			.Returns("people/3", PersonJson("R2-D2", 3)).DelayFor("people/3", TimeSpan.FromMilliseconds(20));

		var sut = CreateSut(apiClient);
		var urls = new[] { $"{Base}/people/1", $"{Base}/people/2", $"{Base}/people/3" };

		var result = await sut.GetRelatedResourcesAsync(urls);

		result.Resources.Select(c => c.Name).ShouldBe(new[] { "Luke Skywalker", "C-3PO", "R2-D2" });
	}

	// T19: no more than 6 requests in flight at once, even with many more characters than that.
	[Fact]
	public async Task GetRelatedResourcesAsync_limits_concurrency_to_at_most_six()
	{
		var apiClient = new FakeApiClient { Delay = TimeSpan.FromMilliseconds(40) };
		var urls = Enumerable.Range(1, 18).Select(i => $"{Base}/people/{i}").ToArray();
		foreach (var i in Enumerable.Range(1, 18))
		{
			apiClient.Returns($"people/{i}", PersonJson($"Character {i}", i));
		}

		var sut = CreateSut(apiClient);

		var result = await sut.GetRelatedResourcesAsync(urls);

		result.Resources.Count.ShouldBe(18);
		apiClient.PeakConcurrency.ShouldBeLessThanOrEqualTo(6);
	}

	// T20: some characters failing keeps the successes and reports how many were lost.
	[Fact]
	public async Task GetRelatedResourcesAsync_keeps_successful_results_when_some_requests_fail()
	{
		var apiClient = new FakeApiClient()
			.Returns("people/1", PersonJson("Luke Skywalker", 1))
			.Throws("people/2", new HttpStatusException(HttpStatusCode.ServiceUnavailable))
			.Returns("people/3", PersonJson("R2-D2", 3));

		var sut = CreateSut(apiClient);
		var urls = new[] { $"{Base}/people/1", $"{Base}/people/2", $"{Base}/people/3" };

		var result = await sut.GetRelatedResourcesAsync(urls);

		result.RequestedCount.ShouldBe(3);
		result.FailedCount.ShouldBe(1);
		result.IsPartial.ShouldBeTrue();
		result.Resources.Select(c => c.Name).ShouldBe(new[] { "Luke Skywalker", "R2-D2" });
	}

	// T21: if every character request fails, that is a recoverable failure the caller can retry.
	[Fact]
	public async Task GetRelatedResourcesAsync_throws_when_every_request_fails()
	{
		var apiClient = new FakeApiClient()
			.Throws("people/1", new HttpStatusException(HttpStatusCode.ServiceUnavailable))
			.Throws("people/2", new HttpStatusException(HttpStatusCode.ServiceUnavailable));

		var sut = CreateSut(apiClient);
		var urls = new[] { $"{Base}/people/1", $"{Base}/people/2" };

		await Should.ThrowAsync<HttpStatusException>(() => sut.GetRelatedResourcesAsync(urls));
	}

	[Fact]
	public async Task GetRelatedResourcesAsync_logs_requested_count_when_every_request_fails()
	{
		var apiClient = new FakeApiClient()
			.Throws("people/1", new HttpStatusException(HttpStatusCode.ServiceUnavailable))
			.Throws("people/2", new HttpStatusException(HttpStatusCode.ServiceUnavailable));
		var logger = Substitute.For<ILogger>();
		var sut = new StarWarsService(apiClient, new FakeApiSettings(Base), logger, TimeSpan.FromSeconds(15));
		var urls = new[] { $"{Base}/people/1", $"{Base}/people/2" };

		await Should.ThrowAsync<HttpStatusException>(() => sut.GetRelatedResourcesAsync(urls));

		logger.Received(1).Error(
			Arg.Any<Exception>(),
			"Failed to retrieve any of {RequestedCount} related Star Wars resources",
			2);
	}

	[Fact]
	public async Task GetRelatedResourcesAsync_returns_an_empty_result_for_a_null_or_empty_input()
	{
		var sut = CreateSut(new FakeApiClient());

		var nullResult = await sut.GetRelatedResourcesAsync(null);
		var emptyResult = await sut.GetRelatedResourcesAsync(Array.Empty<string>());

		nullResult.RequestedCount.ShouldBe(0);
		nullResult.Resources.ShouldBeEmpty();
		emptyResult.RequestedCount.ShouldBe(0);
	}
}
