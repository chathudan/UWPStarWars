using System.Collections.Generic;

namespace DrawboardCodingExercise.ViewModel.Models;

/// <summary>
/// What the FilmDetails page shows for one film: its formatted release date, its text fields
/// with placeholders applied where the source value is missing, and the raw character URLs
/// the detail ViewModel uses to drive its own, independent character load.
/// </summary>
public sealed class FilmDetailsDisplay
{
	public FilmDetailsDisplay(
		string id,
		string title,
		int episodeNumber,
		string episodeLabel,
		string releaseDateDisplay,
		string director,
		string producer,
		string openingCrawl,
		IReadOnlyList<string> characterUrls)
	{
		Id = id;
		Title = title;
		EpisodeNumber = episodeNumber;
		EpisodeLabel = episodeLabel;
		ReleaseDateDisplay = releaseDateDisplay;
		Director = director;
		Producer = producer;
		OpeningCrawl = openingCrawl;
		CharacterUrls = characterUrls;
	}

	public string Id { get; }
	public string Title { get; }
	public int EpisodeNumber { get; }
	public string EpisodeLabel { get; }
	public string ReleaseDateDisplay { get; }
	public string Director { get; }
	public string Producer { get; }
	public string OpeningCrawl { get; }
	public IReadOnlyList<string> CharacterUrls { get; }
}
