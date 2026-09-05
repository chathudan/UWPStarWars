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

	[JsonProperty("characters")]
	public List<string> Characters { get; set; }

	[JsonProperty("url")]
	public string Url { get; set; }
}
