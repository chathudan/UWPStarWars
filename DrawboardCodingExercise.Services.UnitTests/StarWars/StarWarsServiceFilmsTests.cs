using System;
using System.Net;
using System.Threading.Tasks;
using DrawboardCodingExercise.Services.StarWars;
using DrawboardCodingExercise.Services.StarWars.Dtos;
using DrawboardCodingExercise.Services.UnitTests.TestData;
using DrawboardCodingExercise.Services.UnitTests.TestDoubles;
using Newtonsoft.Json;
using NSubstitute;
using Serilog;
using Shouldly;
using Xunit;

namespace DrawboardCodingExercise.Services.UnitTests.StarWars;

/// <summary>
/// T24, T25, T32, T35: film-list retrieval and its failure modes. FR-025 - none of this touches
/// the live service; IAPIClient is a hand-written fake so responses and failures are exact.
/// </summary>
public class StarWarsServiceFilmsTests
{
	private static StarWarsService CreateSut(FakeApiClient apiClient, out ILogger logger)
	{
		// In this Serilog version, Error/Warning/Debug/etc. are genuine ILogger interface
		// members (default interface methods), so NSubstitute records calls to them directly -
		// there is no lower-level Write(...) call to intercept instead.
		logger = Substitute.For<ILogger>();
		return new StarWarsService(apiClient, new FakeApiSettings(), logger, TimeSpan.FromSeconds(15));
	}

	// T24: a non-success status is a recoverable failure - it propagates rather than being swallowed.
	[Fact]
	public async Task GetFilmsAsync_propagates_a_non_success_status_as_a_recoverable_failure()
	{
		var apiClient = new FakeApiClient().Throws("films", new HttpStatusException(HttpStatusCode.ServiceUnavailable));
		var sut = CreateSut(apiClient, out _);

		var exception = await Should.ThrowAsync<HttpStatusException>(() => sut.GetFilmsAsync());

		exception.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
	}

	// T25: null or empty deserialized responses are a successful-but-empty result, not a crash.
	[Fact]
	public async Task GetFilmsAsync_returns_an_empty_list_when_the_response_deserializes_to_null()
	{
		var apiClient = new FakeApiClient().Returns("films", "null");
		var sut = CreateSut(apiClient, out _);

		var films = await sut.GetFilmsAsync();

		films.ShouldNotBeNull();
		films.ShouldBeEmpty();
	}

	[Fact]
	public async Task GetFilmsAsync_returns_an_empty_list_for_an_empty_array_response()
	{
		var apiClient = new FakeApiClient().Returns("films", SwapiPayloads.EmptyFilmsArray);
		var sut = CreateSut(apiClient, out _);

		var films = await sut.GetFilmsAsync();

		films.ShouldNotBeNull();
		films.ShouldBeEmpty();
	}

	// T32: a malformed (non-JSON) body is a recoverable failure, not an unhandled crash.
	[Fact]
	public async Task GetFilmsAsync_propagates_a_malformed_response_body_as_a_recoverable_failure()
	{
		var apiClient = new FakeApiClient().Returns("films", SwapiPayloads.MalformedBody);
		var sut = CreateSut(apiClient, out _);

		await Should.ThrowAsync<JsonException>(() => sut.GetFilmsAsync());
	}

	// T35: a failed retrieval writes an Error-level diagnostic. Level is asserted, not message
	// wording, so the test survives the prose being reworded - see quickstart.md.
	[Fact]
	public async Task GetFilmsAsync_logs_at_error_level_when_the_retrieval_fails()
	{
		var apiClient = new FakeApiClient().Throws("films", new HttpStatusException(HttpStatusCode.ServiceUnavailable));
		var sut = CreateSut(apiClient, out var logger);

		await Should.ThrowAsync<HttpStatusException>(() => sut.GetFilmsAsync());

		// Asserts against the exact 2-arg Error(Exception, string) overload. Serilog's ILogger
		// declares several Error overloads (plain, generic-with-values, params array); calling
		// with different argument counts silently resolves to a DIFFERENT interface member, so
		// matching a params-array overload here would never see a plain 2-arg call. Keeping
		// production calls at this exact arity (see StarWarsService) is what makes the
		// assertion unambiguous rather than overload-sensitive.
		logger.Received(1).Error(Arg.Any<Exception>(), Arg.Any<string>());
	}

	[Fact]
	public async Task GetFilmsAsync_does_not_log_at_error_level_on_success()
	{
		var apiClient = new FakeApiClient().Returns("films", SwapiPayloads.FilmsArray);
		var sut = CreateSut(apiClient, out var logger);

		await sut.GetFilmsAsync();

		logger.DidNotReceive().Error(Arg.Any<Exception>(), Arg.Any<string>());
	}
}
