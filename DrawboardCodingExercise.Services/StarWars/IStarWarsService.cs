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
	/// Retrieves the named records referenced by <paramref name="resourceUrls"/>, preserving
	/// their input order regardless of completion order, with at most a small bounded number of
	/// requests in flight at once. A single failing record does not fail the whole batch; only a
	/// *total* failure (every request fails) throws.
	///
	/// This is deliberately category-agnostic: it takes URLs and returns names, and nothing in it
	/// knows whether it is fetching people or starships. RelatedCategory is a display grouping and
	/// never crosses this boundary - the film's own response already supplied the paths, so a
	/// category parameter would be one the method never reads. See research.md R10.
	/// </summary>
	Task<RelatedResourceLoadResult> GetRelatedResourcesAsync(IReadOnlyList<string> resourceUrls);
}

/// <summary>
/// The outcome of one category's batch load. "Some entries failed" is a first-class result here,
/// not indistinguishable from a film that genuinely references fewer entries.
/// </summary>
public sealed class RelatedResourceLoadResult
{
	public RelatedResourceLoadResult(IReadOnlyList<NamedResourceDto> resources, int requestedCount, int failedCount)
	{
		Resources = resources;
		RequestedCount = requestedCount;
		FailedCount = failedCount;
	}

	/// <summary>Successfully retrieved records, in the order they were requested.</summary>
	public IReadOnlyList<NamedResourceDto> Resources { get; }

	public int RequestedCount { get; }

	public int FailedCount { get; }

	public bool IsPartial => FailedCount > 0 && Resources.Count > 0;
}
