using System.Collections.Generic;
using System.Threading.Tasks;
using DrawboardCodingExercise.Services.StarWars.Dtos;

namespace DrawboardCodingExercise.Services.StarWars;

/// <summary>
/// The only abstraction between the app and SWAPI. ViewModels never see IAPIClient, HttpClient,
/// or a URL - see contracts/IStarWarsService.md for the full behavioural contract.
/// </summary>
public interface IStarWarsService
{
	/// <summary>
	/// Retrieves every film, unsorted (ordering is the caller's responsibility). An empty or
	/// null response yields an empty list rather than an error. Non-success status, malformed
	/// bodies, network failures and a request exceeding the configured budget all propagate as
	/// exceptions - recoverable failures the caller can offer retry/cancel for.
	/// </summary>
	Task<IReadOnlyList<FilmDto>> GetFilmsAsync();

	/// <summary>
	/// Retrieves one film by its opaque identifier. Returns null when the source responds
	/// not-found - the identifier is wrong, and no retry can fix that. Every other failure mode
	/// propagates as a recoverable exception.
	/// </summary>
	Task<FilmDto> GetFilmAsync(string filmId);

	/// <summary>
	/// Retrieves the people referenced by <paramref name="characterUrls"/>, preserving their
	/// input order regardless of completion order, with at most a small bounded number of
	/// requests in flight at once. A single failing character does not fail the whole batch;
	/// only a *total* failure (every request fails) throws.
	/// </summary>
	Task<CharacterLoadResult> GetCharactersAsync(IReadOnlyList<string> characterUrls);
}

/// <summary>
/// The outcome of a character batch load. "Some characters failed" is a first-class result
/// here, not indistinguishable from a film that genuinely has fewer characters.
/// </summary>
public sealed class CharacterLoadResult
{
	public CharacterLoadResult(IReadOnlyList<PersonDto> characters, int requestedCount, int failedCount)
	{
		Characters = characters;
		RequestedCount = requestedCount;
		FailedCount = failedCount;
	}

	/// <summary>Successfully retrieved characters, in the order they were requested.</summary>
	public IReadOnlyList<PersonDto> Characters { get; }

	public int RequestedCount { get; }

	public int FailedCount { get; }

	public bool IsPartial => FailedCount > 0 && Characters.Count > 0;
}
