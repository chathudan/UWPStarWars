using Newtonsoft.Json;

namespace DrawboardCodingExercise.Services.StarWars.Dtos;

/// <summary>
/// The wire shape of a SWAPI character record. Only Name and Url are modelled because only
/// the name is displayed by this feature (FR-010) - modelling the rest would be speculative.
/// </summary>
public class PersonDto
{
	[JsonProperty("name")]
	public string Name { get; set; }

	[JsonProperty("url")]
	public string Url { get; set; }
}
