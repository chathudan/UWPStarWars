using System.Collections.Generic;

namespace DrawboardCodingExercise.ViewModel.Models;

/// <summary>
/// What the FilmDetails page shows for one film: its formatted release date, its text fields
/// with placeholders applied where the source value is missing, and the raw reference URLs for
/// each related category, which the detail ViewModel uses to drive one independent load per
/// section.
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
		IReadOnlyDictionary<RelatedCategory, IReadOnlyList<string>> relatedUrls)
	{
		Id = id;
		Title = title;
		EpisodeNumber = episodeNumber;
		EpisodeLabel = episodeLabel;
		ReleaseDateDisplay = releaseDateDisplay;
		Director = director;
		Producer = producer;
		OpeningCrawl = openingCrawl;
		RelatedUrls = relatedUrls;
	}

	public string Id { get; }
	public string Title { get; }
	public int EpisodeNumber { get; }
	public string EpisodeLabel { get; }
	public string ReleaseDateDisplay { get; }
	public string Director { get; }
	public string Producer { get; }
	public string OpeningCrawl { get; }

	/// <summary>
	/// The film's reference URLs, keyed by category. ALWAYS contains an entry for every
	/// RelatedCategory value - an empty list where the film references nothing (M7, V15). A
	/// section reading its own URLs therefore never has to ask whether its key exists, so
	/// "this film has no vehicles" and "vehicles were never mapped" cannot be confused.
	/// </summary>
	public IReadOnlyDictionary<RelatedCategory, IReadOnlyList<string>> RelatedUrls { get; }
}
