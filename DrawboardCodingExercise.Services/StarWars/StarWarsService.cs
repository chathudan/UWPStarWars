using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DrawboardCodingExercise.Services.StarWars.Dtos;
using Serilog;

namespace DrawboardCodingExercise.Services.StarWars;

/// <summary>
/// API-specific logic for the Star Wars feature, sitting above IAPIClient (Constitution VI).
/// The requestBudget is injectable so tests can use a millisecond-scale budget instead of
/// waiting on the real 15-second value - see research.md R3.
/// </summary>
public class StarWarsService : IStarWarsService
{
	private readonly IAPIClient _apiClient;
	private readonly IAPISettings _apiSettings;
	private readonly ILogger _logger;
	private readonly TimeSpan _requestBudget;

	public StarWarsService(IAPIClient apiClient, IAPISettings apiSettings, ILogger logger, TimeSpan requestBudget)
	{
		_apiClient = apiClient;
		_apiSettings = apiSettings;
		_logger = logger;
		_requestBudget = requestBudget;
	}

	public async Task<IReadOnlyList<FilmDto>> GetFilmsAsync()
	{
		try
		{
			var films = await WithBudgetAsync(() => _apiClient.GetAsync<List<FilmDto>>("films")).ConfigureAwait(false);
			return (IReadOnlyList<FilmDto>)films ?? Array.Empty<FilmDto>();
		}
		catch (Exception ex)
		{
			_logger.Error(ex, "Failed to retrieve the Star Wars film list");
			throw;
		}
	}

	/// <summary>
	/// Races <paramref name="operation"/> against the configured request budget. IAPIClient
	/// exposes no CancellationToken and its underlying HttpClient is static and shared, so this
	/// only stops the *caller* waiting - the abandoned HTTP request keeps running in the
	/// background until the platform default elapses. That trade-off is deliberate: it meets
	/// the user-visible contract (FR-015) without touching starter framework code. See
	/// research.md R3.
	/// </summary>
	private async Task<T> WithBudgetAsync<T>(Func<Task<T>> operation)
	{
		var operationTask = operation();
		var delayTask = Task.Delay(_requestBudget);

		var completed = await Task.WhenAny(operationTask, delayTask).ConfigureAwait(false);
		if (completed == delayTask)
		{
			throw new TimeoutException(
				$"The request did not complete within the configured {_requestBudget.TotalSeconds:0.###}s budget.");
		}

		return await operationTask.ConfigureAwait(false);
	}

	public async Task<FilmDto> GetFilmAsync(string filmId)
	{
		try
		{
			return await WithBudgetAsync(() => _apiClient.GetAsync<FilmDto>($"films/{filmId}")).ConfigureAwait(false);
		}
		catch (HttpStatusException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
		{
			// The identifier is wrong; no retry can fix that (FR-013, V4). This is an
			// invalid-selection outcome for the caller, not a recoverable failure.
			_logger.Warning("No Star Wars film was found for identifier {FilmId}", filmId);
			return null;
		}
		catch (Exception ex)
		{
			_logger.Error(ex, "Failed to retrieve Star Wars film {FilmId}", filmId);
			throw;
		}
	}

	// FR-010: at most this many requests in flight at once, PER CALL. A tuning value, not a
	// contract - chosen to be civil to a free community-run mirror rather than to satisfy any
	// specific requirement on the exact number. See research.md R5.
	//
	// Per-call rather than shared across the whole service on purpose: five category sections
	// expanded at once could put 30 requests in flight between them, which is accepted, because a
	// single shared semaphore would serialise sections behind each other and make an expanded
	// section look frozen while an unrelated one loaded. See research.md R10.
	// TODO: move to ApplicationConfiguration
	private const int MaxConcurrentResourceRequests = 6;

	public async Task<RelatedResourceLoadResult> GetRelatedResourcesAsync(IReadOnlyList<string> resourceUrls)
	{
		if (resourceUrls is null || resourceUrls.Count == 0)
		{
			return new RelatedResourceLoadResult(Array.Empty<NamedResourceDto>(), requestedCount: 0, failedCount: 0);
		}

		using (var throttle = new SemaphoreSlim(MaxConcurrentResourceRequests))
		{
			// Task.WhenAll returns results positionally, preserving the caller's requested
			// order regardless of which request actually completes first (FR-010, V8).
			var tasks = resourceUrls.Select(url => FetchOneResourceAsync(url, throttle)).ToArray();
			var slots = await Task.WhenAll(tasks).ConfigureAwait(false);

			var resources = slots.Where(slot => slot.Resource != null).Select(slot => slot.Resource).ToList();
			var failedCount = slots.Count(slot => slot.Resource is null);

			if (resources.Count == 0 && failedCount > 0)
			{
				// Every request failed - that is a recoverable failure the caller can retry,
				// not a silently-empty section (FR-012).
				var firstFailure = slots.First(slot => slot.Failure != null).Failure;
				_logger.Error(firstFailure, "Failed to retrieve any of {RequestedCount} related Star Wars resources");
				throw firstFailure;
			}

			return new RelatedResourceLoadResult(resources, requestedCount: resourceUrls.Count, failedCount: failedCount);
		}
	}

	private async Task<(NamedResourceDto Resource, Exception Failure)> FetchOneResourceAsync(string resourceUrl, SemaphoreSlim throttle)
	{
		var relativePath = SwapiResourcePath.ToRelativePath(resourceUrl, _apiSettings.ServerAddress);
		if (relativePath is null)
		{
			var failure = new InvalidOperationException($"Resource URL '{resourceUrl}' is not under the configured API base.");
			_logger.Warning("Skipped a resource URL that was not under the configured base: {ResourceUrl}", resourceUrl);
			return (null, failure);
		}

		await throttle.WaitAsync().ConfigureAwait(false);
		try
		{
			var resource = await WithBudgetAsync(() => _apiClient.GetAsync<NamedResourceDto>(relativePath)).ConfigureAwait(false);
			return (resource, null);
		}
		catch (Exception ex)
		{
			// A single failing record never fails the whole batch (FR-012, V9) - the exception
			// is captured here in case every request ends up failing.
			_logger.Warning("Failed to retrieve a related Star Wars resource at {ResourceUrl}", resourceUrl);
			return (null, ex);
		}
		finally
		{
			throttle.Release();
		}
	}
}
