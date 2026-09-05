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
			var films = await _apiClient.GetAsync<List<FilmDto>>("films").ConfigureAwait(false);
			return (IReadOnlyList<FilmDto>)films ?? Array.Empty<FilmDto>();
		}
		catch (Exception ex)
		{
			_logger.Error(ex, "Failed to retrieve the Star Wars film list");
			throw;
		}
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
