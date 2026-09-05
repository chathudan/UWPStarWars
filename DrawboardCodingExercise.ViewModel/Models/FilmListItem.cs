namespace DrawboardCodingExercise.ViewModel.Models;

/// <summary>
/// What the Films page shows for one film: its title and episode number in display form, plus
/// its opaque identifier used to open the correct detail view. Id is a film's stable source
/// identifier (extracted from its "url"), which has no relationship to its episode number -
/// see FilmMapper and the SWAPI contract notes for why the two must never be confused.
/// </summary>
public sealed class FilmListItem
{
	public FilmListItem(string id, string title, int episodeNumber, string episodeLabel)
	{
		Id = id;
		Title = title;
		EpisodeNumber = episodeNumber;
		EpisodeLabel = episodeLabel;
	}

	public string Id { get; }
	public string Title { get; }
	public int EpisodeNumber { get; }
	public string EpisodeLabel { get; }
}
