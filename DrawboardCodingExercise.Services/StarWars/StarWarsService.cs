using System;
using System.Collections.Generic;
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

	public Task<FilmDto> GetFilmAsync(string filmId)
	{
		throw new NotImplementedException();
	}

	public Task<CharacterLoadResult> GetCharactersAsync(IReadOnlyList<string> characterUrls)
	{
		throw new NotImplementedException();
	}
}
