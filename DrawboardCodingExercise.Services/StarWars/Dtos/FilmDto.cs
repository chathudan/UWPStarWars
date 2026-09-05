using System.Collections.Generic;
using Newtonsoft.Json;

namespace DrawboardCodingExercise.Services.StarWars.Dtos;

/// <summary>
/// The wire shape of a SWAPI film record. Every property is explicitly attributed with its
/// JSON name, including ones that happen to already match, because relying on an incidental
/// match here is exactly the fragility this class exists to remove - see research.md R1.
///
/// ReleaseDate is deliberately a string, not a DateTime: a malformed date should degrade to a
/// placeholder for that one field (FilmMapper), not fail deserialization for the whole film.
/// </summary>
public class FilmDto
{
	[JsonProperty("title")]
	public string Title { get; set; }

	[JsonProperty("episode_id")]
	public int EpisodeId { get; set; }

	[JsonProperty("opening_crawl")]
	public string OpeningCrawl { get; set; }

	[JsonProperty("director")]
	public string Director { get; set; }

	[JsonProperty("producer")]
	public string Producer { get; set; }

	[JsonProperty("release_date")]
	public string ReleaseDate { get; set; }

	// The five related-category reference arrays. They are modelled identically because they ARE
	// identical - five arrays of absolute URLs to records that all publish a `name`. Any of them
	// may be absent from the payload or explicitly null; FilmMapper (M5) normalises that away so
	// no caller ever has to check.
	[JsonProperty("characters")]
	public List<string> Characters { get; set; }

	[JsonProperty("planets")]
	public List<string> Planets { get; set; }

	[JsonProperty("starships")]
	public List<string> Starships { get; set; }

	[JsonProperty("vehicles")]
	public List<string> Vehicles { get; set; }

	[JsonProperty("species")]
	public List<string> Species { get; set; }

	[JsonProperty("url")]
	public string Url { get; set; }
}
