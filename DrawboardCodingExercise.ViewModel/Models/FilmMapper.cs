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
/// notAvailableText and episodeLabelFormat default to plain English fallbacks so the mapper
/// needs no dependency on ILocalizationService; real callers (the ViewModels, which do have
/// ILocalizationService via DI) pass the localized versions instead, keeping this class free of
/// any container dependency while the shipped app still shows correctly localized text.
/// </summary>
public static class FilmMapper
{
	private const string DefaultNotAvailableText = "Not available";
	private const string DefaultEpisodeLabelFormat = "Episode {0}";
	private const string ReleaseDateSourceFormat = "yyyy-MM-dd";

	// M1: the film's identifier comes from `url` ONLY, and is never derived from EpisodeId.
	// A film's identifier is its release-order position, unrelated to its episode number
	// (e.g. films/1 is Episode 4) - conflating the two would silently open the wrong film.
	public static FilmListItem ToListItem(
		FilmDto dto,
		string notAvailableText = DefaultNotAvailableText,
		string episodeLabelFormat = DefaultEpisodeLabelFormat)
	{
		if (dto is null)
		{
			throw new ArgumentNullException(nameof(dto));
		}

		return new FilmListItem(
			id: SwapiResourcePath.ExtractId(dto.Url),
			title: NotBlankOr(dto.Title, notAvailableText),
			episodeNumber: dto.EpisodeId,
			episodeLabel: EpisodeLabel(dto.EpisodeId, episodeLabelFormat));
	}

	public static FilmDetailsDisplay ToDetailsDisplay(
		FilmDto dto,
		string notAvailableText = DefaultNotAvailableText,
		string episodeLabelFormat = DefaultEpisodeLabelFormat)
	{
		if (dto is null)
		{
			throw new ArgumentNullException(nameof(dto));
		}

		return new FilmDetailsDisplay(
			id: SwapiResourcePath.ExtractId(dto.Url),
			title: NotBlankOr(dto.Title, notAvailableText),
			episodeNumber: dto.EpisodeId,
			episodeLabel: EpisodeLabel(dto.EpisodeId, episodeLabelFormat),
			releaseDateDisplay: FormatReleaseDate(dto.ReleaseDate, notAvailableText),
			director: NotBlankOr(dto.Director, notAvailableText),
			producer: NotBlankOr(dto.Producer, notAvailableText),
			openingCrawl: NotBlankOr(dto.OpeningCrawl, notAvailableText),
			characterUrls: (IReadOnlyList<string>)dto.Characters ?? Array.Empty<string>());
	}

	public static CharacterListItem ToCharacterListItem(PersonDto dto, string notAvailableText = DefaultNotAvailableText)
	{
		if (dto is null)
		{
			throw new ArgumentNullException(nameof(dto));
		}

		return new CharacterListItem(NotBlankOr(dto.Name, notAvailableText), dto.Url);
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
	private static string EpisodeLabel(int episodeNumber, string format) =>
		string.Format(CultureInfo.CurrentCulture, format, ToRomanNumeral(episodeNumber));

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
