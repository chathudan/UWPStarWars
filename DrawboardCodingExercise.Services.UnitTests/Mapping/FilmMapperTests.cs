using System.Linq;
using DrawboardCodingExercise.Services.StarWars.Dtos;
using DrawboardCodingExercise.Services.UnitTests.TestData;
using DrawboardCodingExercise.Services.UnitTests.TestDoubles;
using DrawboardCodingExercise.ViewModel.Models;
using Shouldly;
using Xunit;

namespace DrawboardCodingExercise.Services.UnitTests.Mapping;

/// <summary>
/// T26-T29: mapping is asserted against payloads captured from the live API (SwapiPayloads),
/// deserialized through the app's real JsonSerializerSettings (AppJson) - never against a
/// hand-written camel-case fixture, which would pass while the real thing silently binds
/// episode_id to 0. See research.md R1.
/// </summary>
public class FilmMapperTests
{
	private static FilmDto ANewHope => AppJson.Deserialize<FilmDto>(SwapiPayloads.ANewHope);
	private static FilmDto ThePhantomMenace => AppJson.Deserialize<FilmDto>(SwapiPayloads.ThePhantomMenace);
	private static FilmDto FilmWithMissingFields => AppJson.Deserialize<FilmDto>(SwapiPayloads.FilmWithMissingOptionalFields);

	// T26: mapping across all fields, from a payload that actually uses SWAPI's snake_case names.
	[Fact]
	public void ToListItem_maps_title_and_episode_number()
	{
		var item = FilmMapper.ToListItem(ANewHope);

		item.Title.ShouldBe("A New Hope");
		item.EpisodeNumber.ShouldBe(4);
		item.Id.ShouldBe("1");
	}

	[Fact]
	public void ToDetailsDisplay_maps_every_required_field()
	{
		var display = FilmMapper.ToDetailsDisplay(ANewHope);

		display.Title.ShouldBe("A New Hope");
		display.EpisodeNumber.ShouldBe(4);
		display.Director.ShouldBe("George Lucas");
		display.Producer.ShouldBe("Gary Kurtz, Rick McCallum");
		display.OpeningCrawl.ShouldContain("civil war");
		display.Id.ShouldBe("1");
		display.RelatedUrls[RelatedCategory.Characters].Count.ShouldBe(3);
		display.RelatedUrls[RelatedCategory.Characters].ShouldContain("https://swapi.info/api/people/1");
	}

	[Fact]
	public void ToDetailsDisplay_formats_the_release_date_for_reading()
	{
		var display = FilmMapper.ToDetailsDisplay(ANewHope);

		// Not the raw "1977-05-25" - formatted for a human (FR-008).
		display.ReleaseDateDisplay.ShouldNotBe("1977-05-25");
		display.ReleaseDateDisplay.ShouldContain("1977");
		display.ReleaseDateDisplay.ShouldNotBeNullOrWhiteSpace();
	}

	[Fact]
	public void ToRelatedResourceListItem_maps_name_and_url()
	{
		var person = AppJson.Deserialize<NamedResourceDto>(SwapiPayloads.LukeSkywalker);

		var item = FilmMapper.ToRelatedResourceListItem(person);

		item.Name.ShouldBe("Luke Skywalker");
		item.Url.ShouldBe("https://swapi.info/api/people/1");
	}

	// T27: missing director/producer/crawl -> a placeholder, never a blank gap (FR-009, M2).
	[Fact]
	public void ToDetailsDisplay_substitutes_a_placeholder_for_missing_director()
	{
		var display = FilmMapper.ToDetailsDisplay(FilmWithMissingFields, notAvailableText: "N/A");

		display.Director.ShouldBe("N/A");
	}

	[Fact]
	public void ToDetailsDisplay_substitutes_a_placeholder_for_whitespace_producer()
	{
		var display = FilmMapper.ToDetailsDisplay(FilmWithMissingFields, notAvailableText: "N/A");

		display.Producer.ShouldBe("N/A");
	}

	[Fact]
	public void ToDetailsDisplay_substitutes_a_placeholder_for_empty_opening_crawl()
	{
		var display = FilmMapper.ToDetailsDisplay(FilmWithMissingFields, notAvailableText: "N/A");

		display.OpeningCrawl.ShouldBe("N/A");
	}

	[Fact]
	public void ToDetailsDisplay_returns_an_empty_character_list_when_the_source_array_is_null()
	{
		var display = FilmMapper.ToDetailsDisplay(FilmWithMissingFields);

		display.RelatedUrls[RelatedCategory.Characters].ShouldNotBeNull();
		display.RelatedUrls[RelatedCategory.Characters].ShouldBeEmpty();
	}

	// T28: an unparseable or missing release date degrades to a placeholder and never throws (FR-008, M3).
	[Fact]
	public void ToDetailsDisplay_substitutes_a_placeholder_for_an_unparseable_release_date()
	{
		var display = FilmMapper.ToDetailsDisplay(FilmWithMissingFields, notAvailableText: "N/A");

		display.ReleaseDateDisplay.ShouldBe("N/A");
	}

	[Fact]
	public void ToDetailsDisplay_does_not_throw_for_a_null_release_date()
	{
		var dto = FilmWithMissingFields;
		dto.ReleaseDate = null;

		Should.NotThrow(() => FilmMapper.ToDetailsDisplay(dto));
	}

	// T29: the id comes from `url` only - NEVER from episode_id. The Phantom Menace is episode 1
	// but its url is ".../films/4": deriving the id from the episode number would silently open
	// the wrong film. This is the single highest-value assertion in the whole mapping layer.
	[Fact]
	public void ToListItem_extracts_the_id_from_url_never_from_episode_number()
	{
		var item = FilmMapper.ToListItem(ThePhantomMenace);

		item.EpisodeNumber.ShouldBe(1);
		item.Id.ShouldBe("4");           // from url, NOT "1" (which would be the episode number)
		item.Id.ShouldNotBe(item.EpisodeNumber.ToString());
	}

	// Regression: the episode label is built by a CALLER-SUPPLIED function, never by handing a
	// format string through ILocalizationService.Translate. Translate ends in string.Format, so
	// asking it for "Episode {0}" without supplying the argument throws FormatException - which
	// is exactly what made the film list fail to load on every launch while all tests passed.
	[Fact]
	public void ToListItem_builds_the_episode_label_through_the_supplied_formatter()
	{
		var localization = new EchoLocalizationService();

		var item = FilmMapper.ToListItem(
			ANewHope,
			notAvailableText: "N/A",
			episodeLabelFormatter: numeral => localization.Translate("Film.EpisodeLabel.Text", numeral));

		item.EpisodeLabel.ShouldBe("Episode IV");
	}

	[Fact]
	public void ToDetailsDisplay_builds_the_episode_label_through_the_supplied_formatter()
	{
		var localization = new EchoLocalizationService();

		var display = FilmMapper.ToDetailsDisplay(
			ThePhantomMenace,
			notAvailableText: "N/A",
			episodeLabelFormatter: numeral => localization.Translate("Film.EpisodeLabel.Text", numeral));

		display.EpisodeLabel.ShouldBe("Episode I");
	}

	[Fact]
	public void ToDetailsDisplay_extracts_the_id_from_url_never_from_episode_number()
	{
		var display = FilmMapper.ToDetailsDisplay(ThePhantomMenace);

		display.EpisodeNumber.ShouldBe(1);
		display.Id.ShouldBe("4");
	}

	// T36 (FR-027, M5, M7, V15): all five reference arrays are mapped, and every RelatedCategory
	// value is present as a key even when the source array is absent or null.
	//
	// The absent-array half is the half that matters. A film payload omitting "vehicles" entirely
	// is normal, and the difference between "this key maps to an empty list" and "this key is not
	// here" is the difference between an empty section and a KeyNotFoundException on the UI
	// thread - and only the second one is invisible until a user opens that film.

	[Fact]
	public void ToDetailsDisplay_maps_all_five_related_url_collections()
	{
		var display = FilmMapper.ToDetailsDisplay(ANewHope);

		display.RelatedUrls[RelatedCategory.Characters].ShouldBe(new[]
		{
			"https://swapi.info/api/people/1",
			"https://swapi.info/api/people/2",
			"https://swapi.info/api/people/3"
		});
		display.RelatedUrls[RelatedCategory.Planets].ShouldBe(new[] { "https://swapi.info/api/planets/1" });
		display.RelatedUrls[RelatedCategory.Starships].ShouldBe(new[] { "https://swapi.info/api/starships/2" });
		display.RelatedUrls[RelatedCategory.Vehicles].ShouldBe(new[] { "https://swapi.info/api/vehicles/4" });
		display.RelatedUrls[RelatedCategory.Species].ShouldBe(new[] { "https://swapi.info/api/species/1" });
	}

	[Fact]
	public void ToDetailsDisplay_always_contains_every_category_key_even_when_the_source_array_is_absent()
	{
		// ThePhantomMenace's payload carries "characters" and nothing else - the other four
		// arrays are absent from the JSON entirely, not merely empty.
		var display = FilmMapper.ToDetailsDisplay(ThePhantomMenace);

		display.RelatedUrls.Keys.ShouldBe(
			System.Enum.GetValues(typeof(RelatedCategory)).Cast<RelatedCategory>(),
			ignoreOrder: true);

		display.RelatedUrls[RelatedCategory.Characters].Count.ShouldBe(2);
		display.RelatedUrls[RelatedCategory.Planets].ShouldBeEmpty();
		display.RelatedUrls[RelatedCategory.Starships].ShouldBeEmpty();
		display.RelatedUrls[RelatedCategory.Vehicles].ShouldBeEmpty();
		display.RelatedUrls[RelatedCategory.Species].ShouldBeEmpty();
	}

	[Fact]
	public void ToDetailsDisplay_maps_an_explicitly_null_array_to_an_empty_list()
	{
		// This payload has "characters": null - explicitly null rather than absent (M5).
		var display = FilmMapper.ToDetailsDisplay(FilmWithMissingFields);

		display.RelatedUrls[RelatedCategory.Characters].ShouldBeEmpty();
	}
}
