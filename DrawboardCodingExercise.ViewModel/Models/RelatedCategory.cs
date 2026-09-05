namespace DrawboardCodingExercise.ViewModel.Models;

/// <summary>
/// The five categories a film relates to, as SWAPI publishes them.
///
/// This is a DISPLAY grouping and deliberately never crosses into IStarWarsService: the film's
/// own response already supplies the URLs for each category, so the service takes URLs and
/// returns names without knowing or caring which category it is serving. See research.md R10.
///
/// An enum rather than a string, so a typo is a compile error and the five sections cannot
/// silently become four. Declaration order IS display order.
/// </summary>
public enum RelatedCategory
{
	Characters,
	Planets,
	Starships,
	Vehicles,
	Species
}
