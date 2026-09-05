using Newtonsoft.Json;

namespace DrawboardCodingExercise.Services.StarWars.Dtos;

/// <summary>
/// The wire shape of any record a film relates to - a character, planet, starship, vehicle or
/// species. One type serves all five because all five publish the same displayable shape: this
/// was verified live against people/1, planets/1, starships/2, vehicles/4 and species/1, and
/// every one of them returns a `name`. See research.md R10.
///
/// Only Name and Url are modelled because only the name is displayed (FR-010). If a category
/// ever needs a field the others lack, this type splits and the retrieval path above it is
/// untouched - it never reads anything but the name.
/// </summary>
public class NamedResourceDto
{
	[JsonProperty("name")]
	public string Name { get; set; }

	[JsonProperty("url")]
	public string Url { get; set; }
}
