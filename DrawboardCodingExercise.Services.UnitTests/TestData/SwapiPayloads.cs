namespace DrawboardCodingExercise.Services.UnitTests.TestData;

/// <summary>
/// Response bodies captured verbatim from https://swapi.info/api on 2026-09-05.
///
/// These are deliberately real payloads rather than hand-written fixtures. The API uses
/// snake_case field names (episode_id, opening_crawl, release_date) while the application's
/// shared JsonSerializerSettings applies a camel-case naming strategy, so a fixture written
/// in the app's own convention would pass while the real thing silently binds episode_id to 0.
/// Asserting against the captured shape is the only way these tests are worth anything.
/// </summary>
public static class SwapiPayloads
{
	/// <summary>A New Hope, exactly as /films returns it (episode 4, identifier 1).</summary>
	public const string ANewHope = @"{
		""title"": ""A New Hope"",
		""episode_id"": 4,
		""opening_crawl"": ""It is a period of civil war.\r\nRebel spaceships, striking\r\nfrom a hidden base, have won\r\ntheir first victory against\r\nthe evil Galactic Empire."",
		""director"": ""George Lucas"",
		""producer"": ""Gary Kurtz, Rick McCallum"",
		""release_date"": ""1977-05-25"",
		""characters"": [
			""https://swapi.info/api/people/1"",
			""https://swapi.info/api/people/2"",
			""https://swapi.info/api/people/3""
		],
		""planets"": [ ""https://swapi.info/api/planets/1"" ],
		""starships"": [ ""https://swapi.info/api/starships/2"" ],
		""vehicles"": [ ""https://swapi.info/api/vehicles/4"" ],
		""species"": [ ""https://swapi.info/api/species/1"" ],
		""created"": ""2014-12-10T14:23:31.880000Z"",
		""edited"": ""2014-12-20T19:49:45.256000Z"",
		""url"": ""https://swapi.info/api/films/1""
	}";

	/// <summary>The Phantom Menace — episode 1 but identifier 4, the id/episode mismatch in one record.</summary>
	public const string ThePhantomMenace = @"{
		""title"": ""The Phantom Menace"",
		""episode_id"": 1,
		""opening_crawl"": ""Turmoil has engulfed the\r\nGalactic Republic."",
		""director"": ""George Lucas"",
		""producer"": ""Rick McCallum"",
		""release_date"": ""1999-05-19"",
		""characters"": [ ""https://swapi.info/api/people/2"", ""https://swapi.info/api/people/3"" ],
		""url"": ""https://swapi.info/api/films/4""
	}";

	/// <summary>
	/// The films endpoint response shape: a BARE JSON ARRAY, not a { count, results } envelope.
	/// Ordered as the API returns them — release order — so tests that assert episode ordering
	/// are genuinely re-ordering rather than accepting the source order by luck.
	/// </summary>
	public const string FilmsArray = "[" + ANewHope + "," + ThePhantomMenace + "]";

	/// <summary>An empty films response — a valid success, not an error.</summary>
	public const string EmptyFilmsArray = "[]";

	/// <summary>A person, as /people/{id} returns it.</summary>
	public const string LukeSkywalker = @"{
		""name"": ""Luke Skywalker"",
		""height"": ""172"",
		""mass"": ""77"",
		""birth_year"": ""19BBY"",
		""url"": ""https://swapi.info/api/people/1""
	}";

	// The four payloads below are captured from the other related-category endpoints, and their
	// only purpose is to prove the point research.md R10 rests on: all five categories publish a
	// `name`, so one NamedResourceDto legitimately deserializes every one of them. Each keeps a
	// couple of its category-specific fields (climate, hyperdrive_rating, ...) precisely because
	// those fields must be IGNORED - if a stray one ever started binding, that would be the
	// signal the categories had diverged and the single type no longer held.

	/// <summary>A planet, as /planets/{id} returns it.</summary>
	public const string Tatooine = @"{
		""name"": ""Tatooine"",
		""climate"": ""arid"",
		""terrain"": ""desert"",
		""url"": ""https://swapi.info/api/planets/1""
	}";

	/// <summary>A starship, as /starships/{id} returns it.</summary>
	public const string CorellianCorvette = @"{
		""name"": ""CR90 corvette"",
		""model"": ""CR90 corvette"",
		""hyperdrive_rating"": ""2.0"",
		""url"": ""https://swapi.info/api/starships/2""
	}";

	/// <summary>A vehicle, as /vehicles/{id} returns it.</summary>
	public const string SandCrawler = @"{
		""name"": ""Sand Crawler"",
		""model"": ""Digger Crawler"",
		""vehicle_class"": ""wheeled"",
		""url"": ""https://swapi.info/api/vehicles/4""
	}";

	/// <summary>A species, as /species/{id} returns it.</summary>
	public const string Human = @"{
		""name"": ""Human"",
		""classification"": ""mammal"",
		""language"": ""Galactic Basic"",
		""url"": ""https://swapi.info/api/species/1""
	}";

	/// <summary>A film with every optional text field missing or blank, for placeholder coverage.</summary>
	public const string FilmWithMissingOptionalFields = @"{
		""title"": ""Untitled"",
		""episode_id"": 9,
		""opening_crawl"": """",
		""director"": null,
		""producer"": ""   "",
		""release_date"": ""not-a-date"",
		""characters"": null,
		""url"": ""https://swapi.info/api/films/9""
	}";

	/// <summary>A body that is not JSON at all — e.g. an HTML error page from a proxy.</summary>
	public const string MalformedBody = "<html><body>502 Bad Gateway</body></html>";
}
