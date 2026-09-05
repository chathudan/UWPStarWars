using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DrawboardCodingExercise.Services.StarWars;
using DrawboardCodingExercise.Services.StarWars.Dtos;

namespace DrawboardCodingExercise.ViewModel.Models;

/// <summary>
/// Maps API DTOs to display models. Pure and static so it is testable with no container, no
/// substitutes, and no async - see data-model.md for the mapping rules (M1-M6) this implements.
///
/// notAvailableText and episodeLabelFormatter default to plain English fallbacks so the mapper
/// needs no dependency on ILocalizationService; real callers (the ViewModels, which do have
/// ILocalizationService via DI) pass the localized versions instead, keeping this class free of
/// any container dependency while the shipped app still shows correctly localized text.
/// </summary>
public static class FilmMapper
{
	private const string DefaultNotAvailableText = "Not available";
	private const string ReleaseDateSourceFormat = "yyyy-MM-dd";

	/// <summary>
	/// Builds the episode label from an already-computed Roman numeral.
	///
	/// This takes a FUNCTION rather than a format string on purpose. The obvious-looking
	/// alternative - fetching "Episode {0}" from ILocalizationService and formatting it here -
	/// cannot work: Translate() itself ends in string.Format, so asking it for a value that
	/// still contains {0}, without supplying the argument, throws FormatException. The caller
	/// therefore owns the substitution and hands us the finished label.
	/// </summary>
	private static string DefaultEpisodeLabel(string romanNumeral) => $"Episode {romanNumeral}";

	// M1: the film's identifier comes from `url` ONLY, and is never derived from EpisodeId.
	// A film's identifier is its release-order position, unrelated to its episode number
	// (e.g. films/1 is Episode 4) - conflating the two would silently open the wrong film.
	public static FilmListItem ToListItem(
		FilmDto dto,
		string notAvailableText = DefaultNotAvailableText,
		Func<string, string>? episodeLabelFormatter = null)
	{
		if (dto is null)
		{
			throw new ArgumentNullException(nameof(dto));
		}

		return new FilmListItem(
			id: SwapiResourcePath.ExtractId(dto.Url),
			title: NotBlankOr(dto.Title, notAvailableText),
			episodeNumber: dto.EpisodeId,
			episodeLabel: EpisodeLabel(dto.EpisodeId, episodeLabelFormatter));
	}

	public static FilmDetailsDisplay ToDetailsDisplay(
		FilmDto dto,
		string notAvailableText = DefaultNotAvailableText,
		Func<string, string>? episodeLabelFormatter = null)
	{
		if (dto is null)
		{
			throw new ArgumentNullException(nameof(dto));
		}

		return new FilmDetailsDisplay(
			id: SwapiResourcePath.ExtractId(dto.Url),
			title: NotBlankOr(dto.Title, notAvailableText),
			episodeNumber: dto.EpisodeId,
			episodeLabel: EpisodeLabel(dto.EpisodeId, episodeLabelFormatter),
			releaseDateDisplay: FormatReleaseDate(dto.ReleaseDate, notAvailableText),
			director: NotBlankOr(dto.Director, notAvailableText),
			producer: NotBlankOr(dto.Producer, notAvailableText),
			openingCrawl: NotBlankOr(dto.OpeningCrawl, notAvailableText),
			relatedUrls: BuildRelatedUrls(dto));
	}

	// M7 (V15): build the dictionary from the enum, not from the DTO's populated arrays, so
	// every RelatedCategory value is always a key. Driving it the other way round - adding a key
	// only where the source supplied an array - would leave sections looking up a missing key
	// for any film that omits a category, and would silently drop a sixth category added to the
	// enum but never mapped here. This way that omission is a compile error at the switch.
	private static IReadOnlyDictionary<RelatedCategory, IReadOnlyList<string>> BuildRelatedUrls(FilmDto dto)
	{
		var urls = new Dictionary<RelatedCategory, IReadOnlyList<string>>();

		foreach (RelatedCategory category in Enum.GetValues(typeof(RelatedCategory)))
		{
			urls[category] = OrEmpty(SourceListFor(dto, category));
		}

		return urls;
	}

	private static List<string> SourceListFor(FilmDto dto, RelatedCategory category)
	{
		switch (category)
		{
			case RelatedCategory.Characters: return dto.Characters;
			case RelatedCategory.Planets: return dto.Planets;
			case RelatedCategory.Starships: return dto.Starships;
			case RelatedCategory.Vehicles: return dto.Vehicles;
			case RelatedCategory.Species: return dto.Species;
			default:
				// Reached only by adding an enum value without mapping it here. Failing loudly at
				// the mapper beats a KeyNotFoundException surfacing later on the UI thread.
				throw new ArgumentOutOfRangeException(
					nameof(category), category, "No film reference array is mapped for this related category.");
		}
	}

	// M5: a null or absent reference array becomes an empty list, so the section shows its
	// "nothing listed" empty state rather than dereferencing null.
	private static IReadOnlyList<string> OrEmpty(List<string> source) =>
		(IReadOnlyList<string>)source ?? Array.Empty<string>();

	public static RelatedResourceListItem ToRelatedResourceListItem(NamedResourceDto dto, string notAvailableText = DefaultNotAvailableText)
	{
		if (dto is null)
		{
			throw new ArgumentNullException(nameof(dto));
		}

		return new RelatedResourceListItem(NotBlankOr(dto.Name, notAvailableText), dto.Url);
	}

	// M2: never let a blank source value reach the UI as a gap.
	private static string NotBlankOr(string value, string placeholder) =>
		string.IsNullOrWhiteSpace(value) ? placeholder : value;

	// M3: a bad or missing date degrades to a placeholder for that one field; it never throws
	// and never fails the whole film's mapping.
	private static string FormatReleaseDate(string sourceValue, string placeholder)
	{
		if (string.IsNullOrWhiteSpace(sourceValue))
		{
			return placeholder;
		}

		return DateTime.TryParseExact(
			sourceValue, ReleaseDateSourceFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
			? parsed.ToString("d MMMM yyyy", CultureInfo.CurrentCulture)
			: placeholder;
	}

	// M4: the label is display-only; sorting always happens on the underlying int (see the
	// ViewModel, not here) - the mapper never sorts (M6).
	private static string EpisodeLabel(int episodeNumber, Func<string, string>? formatter)
	{
		var numeral = ToRomanNumeral(episodeNumber);
		return formatter is null ? DefaultEpisodeLabel(numeral) : formatter(numeral);
	}

	private static readonly (int Value, string Numeral)[] RomanNumerals =
	{
		(1000, "M"), (900, "CM"), (500, "D"), (400, "CD"),
		(100, "C"), (90, "XC"), (50, "L"), (40, "XL"),
		(10, "X"), (9, "IX"), (5, "V"), (4, "IV"), (1, "I")
	};

	private static string ToRomanNumeral(int number)
	{
		if (number <= 0)
		{
			return number.ToString(CultureInfo.InvariantCulture);
		}

		var result = new System.Text.StringBuilder();
		foreach (var (value, numeral) in RomanNumerals)
		{
			while (number >= value)
			{
				result.Append(numeral);
				number -= value;
			}
		}

		return result.ToString();
	}
}
